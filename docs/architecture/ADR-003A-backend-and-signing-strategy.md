# ADR-003A: Transición de backend y estrategia de firma

**Status:** Accepted (migración condicionada a spike local)
**Date:** 2026-08-21

## Contexto comprobado

- El repositorio oficial de [ViGEmBus](https://github.com/nefarius/ViGEmBus) está archivado desde
  2023. La versión instalada sigue funcionando y este proyecto la verifica de extremo a extremo por
  XInput, pero no es una base mantenida para una distribución nueva.
- [HIDMaestro](https://github.com/hifihedgehog/HIDMaestro) es MIT, mantiene releases activas y ofrece
  mandos Xbox visibles por XInput/DirectInput/GameInput mediante UMDF2. Su SDK se distribuye como DLL,
  no como paquete NuGet estable, e instala localmente un certificado autofirmado de confianza.
- Smart App Control ya bloqueó DLL propias recién compiladas con `0x800711C7`. Microsoft indica que
  para que Smart App Control confíe en una aplicación pública su código debe firmarse con un
  certificado RSA de un proveedor incluido en el programa de raíces de confianza:
  [firma para Smart App Control](https://learn.microsoft.com/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control).
- Si en el futuro se distribuyera un driver kernel propio, Windows exige el flujo de Hardware Dev
  Center y certificado EV para las presentaciones de firma:
  [requisitos oficiales de firma de drivers](https://learn.microsoft.com/windows-hardware/drivers/dashboard/code-signing-reqs).

## Decisión

1. **Pruebas personales inmediatas:** conservar `ViGEmXbox360Backend` como backend legacy. Ya está
   instalado, entrega estado correcto por XInput y permite probar el comportamiento dentro del juego
   sin introducir otra variable.
2. **Backend objetivo:** implementar `HidMaestroXboxBackend` y convertirlo en predeterminado solo
   después de superar el gate descrito abajo. ViGEm queda como fallback explícito, no como dependencia
   recomendada para instalaciones nuevas.
3. **Instalación de HIDMaestro:** nunca instalar certificados silenciosamente. El first-run debe
   explicar que se añadirá un certificado local, requerir consentimiento y elevación, verificar la
   versión/hash del SDK fijado y ofrecer desinstalación limpia.
4. **Firma de la aplicación:** las releases públicas firmarán EXE y DLL con SHA-256, sello de tiempo y
   certificado RSA de una CA de confianza. Certificados y secretos solo vivirán en el almacén seguro
   de CI; nunca en Git. Un certificado autofirmado sirve para desarrollo controlado, pero no resuelve
   Smart App Control en equipos de terceros.
5. **No crear un driver kernel propio en V1.** Si esa decisión cambia, se abre un ADR separado y se
   asume EV + Partner Center + HLK/WHCP según el canal de distribución.

## Gate para activar HIDMaestro

- Adaptador implementado sin filtrar tipos del SDK fuera de `VirtualController.VirtualGamepad`.
- Estado neutral, sticks, gatillos y botones observados por XInput.
- 30 mediciones `Submit -> XInput`, jitter y CPU comparados con ViGEm.
- Diez ciclos connect/disconnect y terminación forzada sin dispositivos huérfanos.
- Prueba en Windows 10 y Windows 11 con Secure Boot; al menos un juego XInput y uno GameInput/SDL.
- Instalación/desinstalación y certificado revisados manualmente.

Hasta completar ese gate, cambiar `preferredBackend` a `hidmaestro` no se ofrecerá en la UI. Esta
restricción evita convertir una alternativa prometedora pero aún no integrada en una falsa garantía.

## Consecuencias

- La prueba de juego actual no queda bloqueada por la migración: usa ViGEm, que es el camino ya
  medido en esta máquina.
- La publicación para terceros sí queda bloqueada hasta disponer de firma de confianza.
- `IVirtualGamepad` y `BackendResolver` permiten hacer la migración sin tocar Core, perfiles ni UI.
