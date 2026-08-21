# ADR-001: Framework de interfaz de usuario

**Status:** Accepted
**Last reviewed:** 2026-08-20

## Context

La aplicación necesita una UI de escritorio solo-Windows con:

- Un panel en tiempo real que se actualiza a alta frecuencia (visualizador de sticks/triggers/botones).
- Un editor de bindings y ajustes de mouse (curvas, deadzone, smoothing, decay).
- Icono de bandeja del sistema (system tray) para operar con la ventana minimizada.
- La UI **nunca** debe bloquear ni acoplarse al hilo de captura/procesamiento de input de baja latencia.
- El proyecto debe mantenerse durante años, por lo que pesan la madurez, la comunidad y el coste de mantenimiento a largo plazo tanto como el rendimiento inicial.

Las tres opciones evaluadas fueron WPF, WinUI 3 y Avalonia sobre .NET, tal como pedía el brief.

Runtime objetivo: a fecha de esta revisión (2026-08-20), .NET 8 entra en EOL el 2026-11-10; .NET 10 es la LTS activa (soporte hasta 2028-11-14). Para un proyecto que arranca ahora y debe mantenerse años, el runtime objetivo es **.NET 10 LTS**, no .NET 8 — esto es independiente de la elección de framework de UI y aplica a toda la solución, no solo a `App`.

## Decision

**WPF sobre .NET 10 LTS (Windows Desktop SDK), Windows x64.**

Razones concretas para este proyecto (no genéricas):

1. **Raw Input es independiente de la elección de UI — no es un argumento a favor de WPF.** Corrección respecto a una versión anterior de este ADR: aunque WPF permite interceptar mensajes Win32 en su propio HWND vía `HwndSource.AddHook`, **no vamos a usar esa vía**. Raw Input se registra sobre una ventana *message-only* (`HWND_MESSAGE`) dedicada, en su propio hilo, completamente fuera del árbol de ventanas de WPF (ver ADR-002/ADR-005). Esto es deliberado: si se ató la captura de input al HWND/Dispatcher de WPF, cualquier congelamiento, modal, o trabajo de render de la UI retrasaría la captura de input — justo lo que la baja latencia exige evitar. Por tanto, la elección de UI **no** se apoya en facilidad de interop con Raw Input; esa capa vive enteramente en `VirtualController.Windows` y sería la misma sin importar qué framework de UI se use.
2. **Bandeja del sistema.** WPF interopera de forma directa con `System.Windows.Forms.NotifyIcon` o `Hardcodet.NotifyIcon` sin fricción. WinUI 3 sigue sin tray icon nativo de primera clase (requiere interop/paquetes adicionales). Avalonia lo soporta, de nuevo vía su propia capa de abstracción.
3. **Empaquetado/distribución.** Windows App SDK (WinUI 3) ya soporta hoy despliegue *unpackaged* y *self-contained*, incluso *single-file* bajo configuraciones determinadas — el argumento de "WinUI 3 exige MSIX" ya no es correcto y se retira de este ADR (ver sección Alternatives). Lo que sí se mantiene: WPF tiene una cadena de distribución más simple para esta utilidad concreta y no introduce el Windows App SDK como dependencia adicional del proceso de build/runtime sin obtener a cambio una ventaja funcional relevante para este proyecto.
4. **Madurez y superficie de riesgo.** WPF es la opción más estable y con más años de producción en aplicaciones Windows de escritorio "serias" (herramientas, utilidades de sistema). Menor superficie de sorpresas para un producto que debe mantenerse años.
5. **Data binding para UI en tiempo real.** El panel de sticks/triggers no necesita refrescarse al ritmo del motor de input (cientos de Hz); se publica un snapshot inmutable de `GamepadState` a ~60 Hz vía `CompositionTarget.Rendering`/`DispatcherTimer`. WPF maneja esto sin problema; no es un caso donde WinUI3/Avalonia aporten una ventaja de rendimiento decisiva.

## Alternatives

- **WinUI 3**: UI moderna, buen soporte Fluent. El Windows App SDK ya soporta despliegue *unpackaged*/self-contained/single-file en configuraciones determinadas, así que el empaquetado **ya no es un bloqueo técnico** (corrección respecto a la versión anterior de este ADR). Se descarta igualmente porque: (a) no tiene tray icon nativo de primera clase, (b) introduce el Windows App SDK como dependencia de runtime adicional sin que aporte una ventaja funcional relevante para este proyecto concreto, y (c) ecosistema todavía menos consolidado que WPF para herramientas de escritorio clásicas de larga vida.
- **Avalonia**: excelente si se necesitara multiplataforma, con buen rendimiento de renderizado. Pero aquí el valor de "multiplataforma" es cero — el resto de la aplicación (Raw Input, backend XInput/ViGEm) es intrínsecamente Win32 y nunca se planea portar a otro SO — y añade una capa de abstracción de windowing sin beneficio a cambio.
- **Electron**: descartado explícitamente por el usuario.

## Consequences

- Los `ViewModel` deben mantenerse libres de tipos específicos de WPF en su superficie pública siempre que sea razonable (se usará `CommunityToolkit.Mvvm` para `ObservableObject`/`RelayCommand`, que no es específico de WPF), de forma que una futura migración de UI no obligue a tocar dominio ni ViewModels.
- `VirtualController.App` es el único proyecto con dependencia a WPF. `Core`, `Windows`, `VirtualGamepad` e `Infrastructure` no referencian WPF.
- Se acepta la dependencia a `CommunityToolkit.Mvvm` (paquete oficial de Microsoft/.NET Foundation, activamente mantenido, reduce boilerplate de `INotifyPropertyChanged` mediante generadores de código) solo dentro de `VirtualController.App`.
- **Regla no negociable de dependencias** (refuerza ADR-002/ADR-005):
  - `VirtualController.Windows` **nunca** referencia WPF. Su HWND de captura es una ventana `HWND_MESSAGE` propia, sin relación con la ventana principal de la app.
  - `VirtualController.Core` **nunca** referencia Win32, WPF, ni el SDK del backend de mando virtual (p. ej. `Nefarius.ViGEm.Client`). Si WPF desapareciera mañana, el motor (`Core` + `Windows` + `VirtualGamepad`) seguiría compilando y funcionando sin cambios.

## Review history

- 2026-08-20 — Revisado con el usuario. Cambios: (1) runtime objetivo pasado de .NET 8 a .NET 10 LTS por vencimiento de soporte de .NET 8 el 2026-11-10; (2) retirado el argumento de `HwndSource`/Raw Input como razón para elegir WPF — Raw Input queda arquitectónicamente desacoplado de la UI desde el día 1 (ver ADR-002/ADR-005); (3) corregido el argumento de deployment de WinUI 3, que ya soporta despliegue unpackaged/self-contained. Fuentes: .NET support policy (Microsoft), Raw Input overview (Win32 apps, Microsoft Learn), `HwndSource.AddHook` (Microsoft Learn), Windows App SDK deployment overview (Microsoft Learn).
