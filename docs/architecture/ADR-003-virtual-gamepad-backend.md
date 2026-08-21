# ADR-003: Backend de mando virtual (Xbox-compatible output)

**Status:** Accepted with major amendments
**Last reviewed:** 2026-08-20

## Context

Necesitamos exponer al sistema operativo un dispositivo compatible con XInput (esquema Xbox 360/Xbox One) sin escribir un driver kernel propio en V1 (requisito 3/28 del brief). El dominio no debe depender directamente de ninguna librería concreta de mando virtual.

La versión anterior de este ADR elegía **ViGEmBus** como backend principal, con una nota de verificación pendiente. Se revisó con fuentes en vivo el 2026-08-20 y el panorama cambió de forma sustancial respecto al supuesto original:

- **ViGEmBus está oficialmente retirado (EOL/archivado)** según la propia declaración de fin de vida de Nefarius. El último instalador del driver es la versión 1.22.0, publicada en noviembre de 2023, y esa release eliminó el actualizador automático — el driver en sí no ha recibido una versión nueva desde entonces.
- El cliente C# `Nefarius.ViGEm.Client` está en NuGet en la versión **1.21.256**, con última publicación el **5 de febrero de 2023**. Es `.NET Standard 2.0`, por lo que NuGet lo resuelve como "compatible" con runtimes modernos, pero eso no equivale a que esté probado o mantenido para .NET 10.
- El sucesor moderno de Nefarius, **VirtualPad**, existe, pero su propia documentación lo describe como un framework **comercial para business partners** — no es una opción open source viable como dependencia de este proyecto salvo que en el futuro se decida licenciarlo explícitamente.
- Ha aparecido una alternativa open source (MIT) activa en 2026: **HIDMaestro**. Crea gamepads virtuales en **user mode vía UMDF2** (sin driver kernel, sin certificado EV, sin reinicios), y declara soporte probado para Xbox 360 virtual sobre XInput, DirectInput, SDL3 y WGI/GameInput, con desarrollo activo (release 1.6.0 el 2026-08-08). Es significativamente más joven que ViGEmBus, con mucho menos historial de producción, y su distribución actual instala un **certificado autofirmado en `Root`/`TrustedPublisher`** — algo que no se quiere aceptar en silencio para un producto de distribución pública sin revisar antes el modelo de firma adecuado.

Dado esto, la decisión cambia de categoría: de "elegimos ViGEmBus" a **"ViGEmBus es un backend legacy candidato para V1; la elección definitiva se decide con un spike técnico en Fase 3"**.

## Decision

### 1. `IVirtualGamepad` se rediseña: de setters + `Update()` a `Submit(state)`

La interfaz original (tomada casi literal del brief, presentado allí como "una abstracción similar a", no como contrato cerrado) obligaba al backend a mantener su propia copia mutable de estado en paralelo a `GamepadState` del motor de procesamiento. Se sustituye por:

```csharp
public interface IVirtualGamepad : IDisposable
{
    bool IsConnected { get; }

    void Connect();
    void Disconnect();

    void Submit(in GamepadState state);

    void Reset();
}
```

Esta interfaz se define en **`VirtualController.Core`** (no en `VirtualController.VirtualGamepad`): es un puerto del dominio, no un detalle de infraestructura — ver punto 3 para la regla de dependencias completa.

Razón: el consumidor de procesamiento (ver ADR-002/ADR-005) ya es el único propietario de `GamepadState`. Publicar un snapshot completo con `Submit` en vez de una secuencia `SetLeftStick`/`SetRightStick`/`SetLeftTrigger`/`SetRightTrigger`/`SetButton`×N/`Update` elimina estado duplicado, elimina la posibilidad de una actualización parcial a medias, reduce superficie de mocks, y encaja de forma natural con backends que trabajan por reporte/snapshot completo (p. ej. ViGEmBus traduce internamente a un `XUSB_REPORT`; un backend distinto podría traducir a otro formato de reporte sin que el contrato cambie).

### 2. `GamepadState`/`GamepadButton` deben ser completamente agnósticos de backend

Ningún vocabulario específico de un backend concreto (`XUSB_REPORT`, "HID usage", nombres específicos de ViGEm o de cualquier otra librería) puede aparecer en `VirtualController.Core`. `GamepadState` expone únicamente valores normalizados (sticks `-1.0..+1.0`, triggers `0.0..1.0`, botones vía un enum de dominio). Cada backend concreto es responsable de convertir esos valores normalizados a su propio formato de wire (p. ej. `Int16` con rango `-32768..32767` para `XUSB_REPORT`).

### 3. Ubicación de `IVirtualGamepad` (puerto) vs. implementaciones concretas (adaptadores)

Corrección de esta revisión: `IVirtualGamepad` es un puerto del dominio y se define en **`VirtualController.Core`**, exactamente igual que `IProfileRepository` (ADR-004) — no en `VirtualController.VirtualGamepad`. Las implementaciones concretas (los adaptadores) sí viven en `VirtualController.VirtualGamepad`:

```text
VirtualController.Core
      IVirtualGamepad                    ← puerto, definido aquí

VirtualController.VirtualGamepad
      ├── ViGEmXbox360Backend   : IVirtualGamepad   — legacy / fallback, candidato probado en V1
      ├── HidMaestroXboxBackend : IVirtualGamepad   — candidato activo, pendiente de validar con spike
      └── FutureBackend         : IVirtualGamepad
```

Razón: el consumidor de procesamiento — que orquesta Mapping Engine + `MouseToStickConverter` + `IVirtualGamepad` — es lógicamente parte del dominio (`Core`) y depende de la abstracción, nunca de una implementación concreta. Si `IVirtualGamepad` viviera en `VirtualController.VirtualGamepad`, `Core` tendría que referenciar ese proyecto solo para obtener la interfaz, invirtiendo la dirección de dependencia que el resto de la arquitectura ya respeta (principio de inversión de dependencias: el puerto pertenece al consumidor, no al adaptador).

**Regla de dependencias no negociable para todo el grafo de proyectos** (refuerza y completa la regla de ADR-001):

```text
VirtualController.Windows        ──► Core
VirtualController.Infrastructure ──► Core
VirtualController.VirtualGamepad ──► Core
VirtualController.TestUtilities  ──► Core
VirtualController.App            ──► Core, Windows, Infrastructure, VirtualGamepad

VirtualController.Core no referencia ninguno de los anteriores.
```

- Se renombra `XboxVirtualGamepadBackend` → **`ViGEmXbox360Backend`**: ViGEmBus emula específicamente un mando **Xbox 360** (y DualShock 4), no un "Xbox virtual" genérico — el nombre debe reflejarlo con precisión.
- El título de este ADR pasa de "Backend de mando virtual (XInput)" a **"Backend de mando virtual (Xbox-compatible output)"**: XInput es la API mediante la cual las aplicaciones consumen el dispositivo; lo que construimos es el dispositivo virtual que Windows expone a ese stack, no la API en sí.

### 4. Ownership exclusivo de un solo hilo lógico

`ViGEmClient` documenta explícitamente que no es thread-safe; se adopta como regla general para **cualquier** backend, no solo ViGEm:

```text
IVirtualGamepad
       │
       │ solo puede ser invocado por
       ↓
  Processing Loop (consumidor de procesamiento, ver ADR-002/ADR-005)
```

Ni la UI, ni el hilo de captura de Raw Input, ni un futuro panel de diagnóstico llaman a `IVirtualGamepad` directamente. Esto evita necesitar locks dentro de ningún backend.

### 5. `MockVirtualGamepad` no se distribuye en el ejecutable de producción

Se crea un proyecto nuevo, pequeño y deliberado: `VirtualController.TestUtilities` (bajo `tests/`), referenciado únicamente por proyectos de test, que aloja dobles de prueba compartidos empezando por `MockVirtualGamepad` (implementación en memoria de `IVirtualGamepad`, requisito 23 del brief) y pudiendo alojar en el futuro dobles equivalentes para `IProfileRepository`/`IInputSource` si hiciera falta, en vez de duplicar mocks entre proyectos de test. Ningún proyecto de `src/` referencia `VirtualController.TestUtilities`. `VirtualController.TestUtilities` solo necesita referenciar `VirtualController.Core` — no necesita referenciar `VirtualController.VirtualGamepad` en absoluto, precisamente porque `IVirtualGamepad` es un puerto definido en `Core` (ver punto 3).

### 6. Detección de disponibilidad del backend: capacidad desde Fase 3, UX desde Fase 9

La *experiencia visual* de guiar al usuario a instalar un backend faltante sí puede esperar a la Fase 9 (Release), pero la **capacidad de detectar y reportar el problema** no puede diferirse hasta entonces — se necesita desde que exista un backend real (Fase 3), aunque solo sea para logging/diagnóstico interno. `Connect()` debe poder distinguir, mediante un resultado tipado (no una única excepción genérica): backend no instalado, versión incompatible, error de conexión, error al crear el controller, y desconexión en tiempo de ejecución. Fase 9 añade encima la UX ("se necesita instalar X, aquí tienes el instalador") sobre una detección que ya existe desde Fase 3.

### 7. Corrección de una afirmación imprecisa (dependencia transitiva)

La versión anterior de este ADR afirmaba que `App` "no tiene ninguna referencia, directa ni transitiva" al cliente de ViGEm. Eso es impreciso: si `App` referencia el proyecto `VirtualController.VirtualGamepad` y este referencia `Nefarius.ViGEm.Client`, existe una dependencia transitiva real a nivel de grafo de proyectos/NuGet, aunque `App` nunca use sus tipos. La garantía correcta, y la que realmente importa, es:

> Ningún proyecto fuera de `VirtualController.VirtualGamepad` usa tipos de `Nefarius.ViGEm.Client` (ni de ningún otro SDK de backend) en su propia API pública o implementación. La API pública de `VirtualController.VirtualGamepad` (`IVirtualGamepad`, `GamepadState`) nunca expone tipos de esos SDKs.

### Spike obligatorio en Fase 3 (antes de congelar el backend definitivo de V1)

- [ ] Implementar `IVirtualGamepad`, `GamepadState` normalizado y `MockVirtualGamepad` (ya cubierto por Fase 1).
- [ ] PoC `ViGEmXbox360Backend`.
- [ ] PoC `HidMaestroXboxBackend`.
- [ ] Crear un Xbox 360 virtual con ambos y verificarlo en `joy.cpl` y vía XInput.
- [ ] Medir tiempo `Submit → visible en XInput`.
- [ ] Probar connect/disconnect repetido sin fugas ni estados colgados.
- [ ] Probar en Windows 10 y en la build de Windows 11 vigente al momento del spike.
- [ ] Revisar comportamiento con Secure Boot activo.
- [ ] Revisar instalación/desinstalación limpia de cada backend.
- [ ] Revisar el modelo de firma/certificados de cada backend (para HIDMaestro: resolver explícitamente el tema del certificado autofirmado en `Root`/`TrustedPublisher` antes de considerarlo apto para distribución pública; ver requisitos de firma de drivers de Microsoft).
- [ ] Probar comportamiento tras un crash de la aplicación (¿el dispositivo virtual queda huérfano/conectado?).
- [ ] Comparar consumo de CPU y jitter entre ambos backends.
- [ ] Elegir backend definitivo de V1 y documentar la decisión final como `ADR-003A` (no reescribir este ADR, dejar rastro de la evolución).

## Alternatives

- **vJoy**: orientado a joystick genérico (DirectInput), no a XInput/Xbox nativamente — requeriría una capa de traducción adicional. Se descarta como opción primaria.
- **Nefarius VirtualPad**: sucesor moderno y mantenido, pero comercial/solo para business partners — no es una dependencia OSS viable para este proyecto ahora mismo; se anota como opción a reconsiderar solo si en el futuro se decide licenciar software de terceros.
- **HIDMaestro**: candidato real, ver Decision — no se declara ganador todavío por falta de historial de producción y por el tema de certificados pendiente de resolver.
- **Interception driver**: resuelve *captura* de input, no *emulación* de un dispositivo de salida — problema distinto (territorio de ADR-002, no de este ADR).
- **Escribir un driver kernel propio (KMDF/UMDF)**: fuera de alcance en V1 (coste, firma EV, certificación) — explícitamente excluido por el brief.

## Consequences

- `Submit(in GamepadState)` es el único punto de entrada desde el consumidor de procesamiento hacia cualquier backend; ADR-005 se sincroniza para dejar de mencionar `Update()` (ver nota de sincronización en ese ADR).
- `MockVirtualGamepad` vive en `VirtualController.TestUtilities` (bajo `tests/`), no en ningún proyecto de `src/`.
- V1 arranca con `ViGEmXbox360Backend` como backend por defecto mientras corre el spike de Fase 3 — es el más probado en producción históricamente, aunque ya no reciba mantenimiento activo — pero el objetivo explícito es que `HidMaestroXboxBackend` pueda convertirse en el backend por defecto sin tocar `Core` ni `App` más allá de la resolución de DI, precisamente porque `IVirtualGamepad` es la barrera de aislamiento.
- Migrar de backend implica: crear una nueva clase que implemente `IVirtualGamepad` dentro de `VirtualController.VirtualGamepad`, y cambiar la resolución de DI en `VirtualController.App`. Ninguna otra capa cambia.
- Precisión añadida por ADR-004: el perfil nunca contiene el nombre de un backend concreto, solo `Output.ControllerType` (p. ej. `Xbox360`). La resolución de qué backend concreto se instancia combina `ApplicationSettings.PreferredBackend` con ese `ControllerType` a través de un resolver en `VirtualController.App` — así un perfil de V1 sigue siendo válido aunque el backend por defecto cambie de `ViGEmXbox360Backend` a `HidMaestroXboxBackend` u otro, sin necesitar migración de perfiles. Ese resolver (`BackendResolver` o equivalente) vive en el composition root (`VirtualController.App`), nunca en `Core`: `Core` conoce `ControllerType`/`PreferredBackend` únicamente como datos de configuración (strings/enums), nunca como una referencia a una clase concreta de backend — no debe existir en `Core` ningún `if`/`switch` que instancie `ViGEmXbox360Backend` o `HidMaestroXboxBackend` por nombre.
- Se acepta, para cualquier backend basado en un driver de terceros, una dependencia de instalación adicional para el usuario final, documentada en el README y validada desde Fase 3 (detección) y con UX completa en Fase 9 (first-run).

## Review history

- 2026-08-20 — Revisado con el usuario con fuentes en vivo. Cambios: (1) categoría de la decisión rebajada de "backend elegido" a "backend legacy candidato para V1", con spike obligatorio en Fase 3 antes de congelar; (2) confirmado que ViGEmBus/`Nefarius.ViGEm.Client` están EOL/sin mantenimiento activo desde 2023; (3) identificado y evaluado VirtualPad (comercial, descartado como dependencia OSS) e HIDMaestro (candidato OSS activo en 2026, con reserva pendiente sobre su modelo de certificados); (4) `IVirtualGamepad` rediseñado de setters+`Update()` a `Submit(in GamepadState)` + `Reset()` + `IsConnected`; (5) `GamepadState` reafirmado como completamente agnóstico de backend; (6) renombrado `XboxVirtualGamepadBackend` → `ViGEmXbox360Backend`, y el propio ADR de "(XInput)" a "(Xbox-compatible output)"; (7) añadida regla de ownership exclusivo de un solo hilo lógico para `IVirtualGamepad`; (8) `MockVirtualGamepad` movido fuera de producción a un proyecto nuevo `VirtualController.TestUtilities`; (9) detección de disponibilidad de backend adelantada conceptualmente a Fase 3, dejando solo la UX de first-run para Fase 9; (10) corregida la afirmación imprecisa sobre "sin dependencia transitiva". Retirada la antigua nota de "no se pudo verificar en vivo": esta revisión sí se hizo con fuentes en vivo. Fuentes citadas por el usuario: declaración de fin de vida de ViGEmBus (docs.nefarius.at), página de NuGet de `Nefarius.ViGEm.Client`, documentación de Nefarius VirtualPad, repositorio y releases de HIDMaestro en GitHub, documentación de `ViGEmClient` (nota de thread-safety), y requisitos de firma de drivers de Microsoft Learn.
- 2026-08-20 (2) — Corrección arquitectónica: `IVirtualGamepad` estaba descrito de forma ambigua (la Definition of Done de Fase 1 en `PROJECT_STATUS.md` lo trataba como parte de `Core`, pero el punto 3 de este ADR y la estructura de proyectos lo mostraban dentro de `VirtualController.VirtualGamepad`). Se fija definitivamente: `IVirtualGamepad` es un puerto definido en `Core`; solo las implementaciones concretas viven en `VirtualController.VirtualGamepad`. Añadida la regla de dependencias no negociable para todo el grafo de proyectos, y precisado que la resolución de backend concreto (`BackendResolver`) vive en `VirtualController.App`, nunca en `Core`.
