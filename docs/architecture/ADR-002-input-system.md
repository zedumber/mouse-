# ADR-002: Captura de input (Raw Input) y modelo de hilos

**Status:** Accepted with amendments
**Last reviewed:** 2026-08-20

## Context

Necesitamos capturar teclado y mouse (incluyendo deltas relativos de mouse, no posición de cursor) con la menor latencia posible, sin bloquear la UI, y conociendo timestamp/device/tipo/valor de cada evento (requisito 6 y 7 del brief).

Dos familias de API de Windows son candidatas:

- **Raw Input** (`RegisterRawInputDevices` + `WM_INPUT`): entrega deltas de mouse crudos (no acelerados por el puntero de Windows), reporta el `HANDLE` del dispositivo físico origen (permite distinguir múltiples teclados/mouses).
- **Hooks globales** (`WH_KEYBOARD_LL` / `WH_MOUSE_LL`): sus callbacks se entregan en el hilo que instaló el hook, a través del message loop de ese hilo, están sujetos al timeout de `LowLevelHooksTimeout`, y no dan acceso a movimiento relativo crudo por dispositivo de forma fiable (dependen de posición de cursor ya procesada). Microsoft recomienda Raw Input para la mayoría de escenarios que necesitan monitorizar teclado/mouse con este nivel de control.

## Decision

**Raw Input**, con los siguientes detalles de diseño.

### 1. Ventana dedicada, hilo dedicado

Una ventana **message-only** (`HWND_MESSAGE`) propia, corriendo en su **propio hilo** con su propio bucle de mensajes Win32 (`GetMessage`/`DispatchMessage`), separada de la ventana principal de WPF (ver ADR-001, regla no negociable: `VirtualController.Windows` nunca referencia WPF). Microsoft documenta exactamente este patrón (ventana `HWND_MESSAGE` + registro con `RIDEV_INPUTSINK`) como forma recomendada de recibir Raw Input sin necesitar una ventana visible.

- Alternativa descartada: enganchar Raw Input a la ventana principal de WPF vía `HwndSource.AddHook`. Acoplaría la recepción de input al hilo de UI: si WPF hace layout/render pesado, el bucle de mensajes de esa ventana se retrasa y con él la captura de input.

### 2. Flags de registro

```text
Mouse:    RIDEV_INPUTSINK | RIDEV_DEVNOTIFY
Keyboard: RIDEV_INPUTSINK | RIDEV_DEVNOTIFY

RIDEV_NOLEGACY: explícitamente NO en V1.
```

- `RIDEV_INPUTSINK`: recibir input aunque nuestra ventana no tenga foco — necesario porque el usuario normalmente tendrá el foco en el juego, no en nuestra app.
- `RIDEV_DEVNOTIFY`: nos permite detectar conexión/desconexión de dispositivos en caliente vía `WM_INPUT_DEVICE_CHANGE` (útil para el panel de diagnóstico y para no quedarnos "sordos" si el usuario desconecta un teclado/mouse a mitad de sesión).
- `RIDEV_NOLEGACY` queda **rechazado para V1**: suprimiría mensajes legacy (`WM_MOUSEMOVE`, `WM_KEYDOWN`, clicks) para todo el proceso, lo que podría interferir con la propia UI WPF cuando necesite recibir esos mensajes normalmente (p. ej. mientras el usuario edita bindings con el mouse). No hay ninguna necesidad funcional identificada hoy que justifique ese riesgo. Si en el futuro aparece una razón concreta, se reabre esta decisión explícitamente, no se activa "por si acaso".

### 3. Ownership único de `RegisterRawInputDevices`

Windows solo permite que **una única ventana por clase de dispositivo, dentro del proceso,** esté registrada como destino de Raw Input — la última llamada a `RegisterRawInputDevices` gana y desplaza a cualquier registro previo. Por eso:

```text
RawInputHost es el único componente autorizado a llamar
RegisterRawInputDevices en todo el proceso.
```

Ninguna otra clase (UI, un futuro plugin, un servicio de diagnóstico) puede volver a registrar teclado/mouse. Estructura dentro de `VirtualController.Windows`:

```text
VirtualController.Windows
└── RawInput
    ├── RawInputHost            (dueño único del registro; arranca/detiene el hilo+ventana)
    ├── RawInputWindow          (HWND_MESSAGE + message loop)
    ├── RawInputParser          (RAWINPUT → InputEvent, función pura)
    ├── RawInputDeviceRegistry  (disambiguación de HANDLE → InputDeviceId estable)
    └── NativeMethods           (P/Invoke)
```

### 4. Lectura del mensaje: `GetRawInputData` + drenado con `GetRawInputBuffer`

Con dispositivos de alta frecuencia (mouse de 1000 Hz o más), pueden acumularse varios eventos entre iteraciones del message loop. La documentación de Microsoft sobre Raw Input recomienda, para estos casos, drenar los eventos pendientes con `GetRawInputBuffer` en vez de asumir que cada `WM_INPUT` corresponde a un único evento sin cola. Flujo:

```text
WM_INPUT
   ↓
GetRawInputData(evento actual)
   ↓
Process(evento actual)
   ↓
GetRawInputBuffer() → drenar eventos adicionales ya encolados por el sistema
```

Diseñamos el parser para drenar en lote desde el principio, aunque **no afirmamos todavía** que tasas de 2000/4000/8000 Hz se comporten perfectamente — eso es objeto de benchmark en Fase 8, no una promesa de este ADR.

### 5. Timestamp: no existe timestamp de hardware fiable — se define uno propio

`RAWINPUTHEADER` (`dwType`, `dwSize`, `hDevice`, `wParam`) y `RAWMOUSE`/`RAWKEYBOARD` **no incluyen un timestamp de alta resolución del dispositivo**. Por tanto `InputEvent.CaptureTimestamp` se define explícitamente como:

> el instante monotónico (`Stopwatch.GetTimestamp()`, contador de alta resolución de .NET) en el que `VirtualController` procesa el paquete Raw Input correspondiente — **no** un timestamp generado por el hardware.

```csharp
public readonly record struct InputEvent(
    InputDeviceId Device,
    InputEventType Type,
    int Value1,
    int Value2,
    long CaptureTimestamp);
```

Esta distinción importa porque cualquier métrica posterior (capture→mapping, capture→salida virtual, duración de procesamiento — requisito 7/26) mide desde este instante, no desde "cuándo se movió físicamente el mouse", y así debe documentarse en el código, no solo aquí.

### 6. Cruce de hilos: `Channel<T>` como implementación inicial, no como promesa de rendimiento final

`System.Threading.Channels.Channel<T>` con `SingleReader = true, SingleWriter = true` se usa como implementación **inicial** detrás de `IInputEventQueue`, porque es la primitiva SPSC mantenida por .NET y evita escribir un ring buffer a mano sin necesidad demostrada todavía. Correction: **no se promete que sea allocation-free** — la documentación de `ChannelOptions` no da esa garantía contractual, solo permite a la implementación optimizar internamente cuando se declaran esas propiedades. Su comportamiento real (allocations, latencia) se mide en Fase 8; si el profiling muestra que es el cuello de botella, se sustituye por una cola SPSC manual — decisión ya aislada detrás de `IInputEventQueue`, así que el resto del sistema no cambia.

**Bounded, no unbounded.** Capacidad inicial a definir empíricamente (punto de partida: 4096), con `FullMode` explícito en vez de dejarlo ilimitado — un consumidor momentáneamente lento no debe acumular cientos de miles de eventos de un mouse a 8000 Hz (procesar input viejo es peor que no procesarlo). Distinción importante que **no se resuelve del todo en V1** pero se documenta como restricción de diseño futura:

- Eventos discretos (`KeyDown`/`KeyUp`/`ButtonDown`/`ButtonUp`) **no deben perderse nunca** — perder un `KeyUp` deja una tecla "pegada" en el mando virtual.
- Deltas de mouse **sí pueden coalescerse** bajo backpressure (sumar `dx`/`dy` de varios eventos pendientes en uno) sin cambiar el resultado observable, ya que el stick solo ve la suma acumulada.

Esto probablemente justifique, más adelante, una cola especializada en vez de un único `Channel<T>` genérico para todo tipo de evento — **no se implementa todavía**; se deja anotado aquí para no descubrirlo tarde.

### 7. "Hilo dedicado de procesamiento" — precisión de lo que estamos prometiendo

Distinción importante que la versión anterior de este ADR difuminaba: si el consumidor del canal usa `await channel.Reader.WaitToReadAsync()`, su continuación puede ejecutarse en cualquier hilo del `ThreadPool` — eso es un **consumidor lógico único**, no necesariamente un **hilo de sistema operativo dedicado**. Son garantías distintas.

Decisión para Fase 1–4:

```text
Raw Input          = hilo de SO dedicado (obligatorio: es quien posee el HWND/message loop)
Processing         = consumidor lógico único, long-running (no se exige un Thread físico dedicado todavía)
UI                 = WPF Dispatcher
```

En Fase 8 se mide si el procesamiento necesita convertirse en un `Thread` físico dedicado (con espera manual sobre una cola, sin `async`/`ThreadPool`) para eliminar jitter de scheduling. No se decide hoy sin datos.

### 8. Bucle de procesamiento: separar trabajo dirigido por input de trabajo dirigido por tiempo

Se descarta explícitamente combinar `WaitToReadAsync()` con `Task.Delay(...)` vía `Task.WhenAny` dentro del bucle caliente — introduce asignaciones, temporizadores y presión de scheduling innecesarios en el camino más sensible a jitter de toda la aplicación. En su lugar, el bucle separa conceptualmente:

- **Trabajo dirigido por input**: teclado, mouse, botones — se procesa en cuanto llega.
- **Trabajo dirigido por tiempo**: decay del stick derecho, publicación periódica de estado, diagnóstico — debe progresar aunque no llegue input nuevo.

```text
while (running)
{
    DrainAvailableInput();
    ApplyMappings();

    var now = Stopwatch.GetTimestamp();
    UpdateDecay(now);

    if (OutputDeadlineReached(now))
        PublishGamepadSnapshot();

    WaitUntilInputOrNextDeadline();
}
```

`WaitUntilInputOrNextDeadline()` queda deliberadamente detrás de una abstracción — no se decide todavía si su implementación final es `PeriodicTimer`, un `WaitableTimer` de Win32, o espera acotada sobre la cola; se decide con datos de Fase 8, nunca `Thread.Sleep` de duración fija.

### 9. `GamepadState` interno vs. `GamepadSnapshot` publicado

Se separan dos tipos con propósitos distintos (afinamiento de ADR-005, no solo de este ADR):

- Un `GamepadState` **mutable**, propiedad exclusiva del hilo/consumidor de procesamiento — se actualiza en el sitio, sin crear objetos en cada frame del hot path.
- Un `GamepadSnapshot` **inmutable** (`sealed record`), creado solo en el momento de publicar hacia la UI (~60 Hz vía `Interlocked.Exchange`/`Volatile.Write`), que es lo único que el hilo de UI llega a ver. A 60 Hz, crear un pequeño snapshot es insignificante frente a hacerlo miles de veces por segundo en el hot path.

## Alternatives

- **Hooks globales (`WH_MOUSE_LL`/`WH_KEYBOARD_LL`)**: descartados como mecanismo principal — entrega ligada al message loop del hilo instalador, sujeta a `LowLevelHooksTimeout`, sin deltas crudos fiables por dispositivo. Podrían mantenerse como *fallback* documentado si Raw Input no estuviera disponible en algún escenario, pero no se implementa en V1 sin necesidad concreta.
- **Interception driver / filtros a nivel de driver**: fuera de alcance en V1 (no se escribe ni se depende de un driver kernel propio de captura, requisito 3/28 del brief). Podría revisarse en el futuro si Raw Input demostrara ser insuficiente para algún caso, pero no hay evidencia de eso hoy.

## Consequences

- Toda esta lógica vive en `VirtualController.Windows` detrás de una interfaz `IInputSource` (definida en `Core`), de forma que el Mapping Engine y el resto del dominio no conocen P/Invoke ni structs de Win32.
- Se requiere P/Invoke manual (no existe una librería .NET moderna y bien mantenida que envuelva Raw Input de forma completa y confiable para este caso); se aísla todo el interop en un único proyecto, bajo `RawInputHost` como único punto de registro.
- Sincronización explícita necesaria — y únicamente esta: (a) canal SPSC hilo-captura → consumidor de procesamiento, (b) publicación atómica de `GamepadSnapshot` procesamiento → hilo-UI, (c) canal de comandos hilo-UI → procesamiento (start/stop, cambio de perfil), aplicados solo al inicio de cada iteración del bucle. Ningún otro punto del sistema necesita locks.

### Registro de decisiones

```text
[ACCEPTED]            Raw Input sobre hooks globales
[ACCEPTED]             Hilo de SO dedicado para captura, con HWND_MESSAGE propio
[ACCEPTED]             RIDEV_INPUTSINK
[ACCEPTED]             RIDEV_DEVNOTIFY
[REJECTED PARA V1]     RIDEV_NOLEGACY
[ACCEPTED]             RawInputHost como único llamante de RegisterRawInputDevices
[ACCEPTED]             Stopwatch.GetTimestamp() como CaptureTimestamp (no es timestamp de hardware)
[ACCEPTED]             GetRawInputData + drenado con GetRawInputBuffer para dispositivos de alta frecuencia
[ACCEPTED COMO IMPL. INICIAL]  Channel<T> (SingleReader/SingleWriter) detrás de IInputEventQueue, bounded
[NO GARANTIZADO]       Que Channel<T> sea allocation-free — se mide, no se asume
[DIFERIDO A BENCHMARK] Cola SPSC manual si el profiling lo justifica
[DIFERIDO A BENCHMARK] Convertir "processing" en hilo de SO físicamente dedicado
[DIFERIDO, ANOTADO]    Estrategia de coalescing de deltas de mouse bajo backpressure vs. no-pérdida de eventos discretos
```

## Review history

- 2026-08-20 — Revisado con el usuario. Cambios: (1) matizada la explicación de por qué se descartan los hooks (timeout/hilo instalador, no una afirmación categórica sobre `CallNextHookEx`); (2) definido explícitamente `CaptureTimestamp` como instante de captura por la app, no timestamp de hardware; (3) `RIDEV_NOLEGACY` rechazado para V1, añadido `RIDEV_DEVNOTIFY`; (4) añadida estrategia de drenado con `GetRawInputBuffer` para mouses de alta frecuencia; (5) retirada la afirmación de que `Channel<T>` es allocation-free; (6) decidido bounded channel y anotada la distinción pérdida-aceptable (mouse) vs. pérdida-inaceptable (botones/teclas); (7) separado "hilo de SO dedicado" (captura) de "consumidor lógico único" (procesamiento), sin exigir thread físico para procesamiento todavía; (8) descartado `WaitToReadAsync + Task.Delay` en el hot path, sustituido por bucle con trabajo dirigido por input vs. por tiempo; (9) separados `GamepadState` mutable interno de `GamepadSnapshot` inmutable publicado; (10) añadida regla de ownership único de `RegisterRawInputDevices` vía `RawInputHost`. Fuentes: "Using Raw Input" y `RAWINPUTDEVICE`/`RegisterRawInputDevices`/`RAWINPUTHEADER` (Win32 apps, Microsoft Learn), `LowLevelMouseProc` (Microsoft Learn), `Stopwatch.GetTimestamp`, `ChannelOptions.SingleWriter`, `BoundedChannelOptions` (Microsoft Learn, .NET API docs).
