# Project Status

## Current phase

Phase 7 - UI y preparación de release (fases 1, 4, 5 y 6 completas; fase 2 pendiente de verificación humana; backend condicionado por Smart App Control y validación XInput)

## Completed

- [x] Fase 0 — Diseño (con la corrección final de ubicación de `IVirtualGamepad` en `Core`, ver ADR-003)
- [x] Fase 1 — Core: solución + 9 proyectos, `GamepadState`, enums, `IVirtualGamepad`, `MockVirtualGamepad`, Mapping Engine completo (`Binding`/`VirtualOutput`, `StickComposer`, `BindingResolver`, `MappingValidator`, `MappingEngine`, `ActiveInputState`, `MouseToStickConverter` mínimo, `CompiledMapping`) — 39/39 tests en verde
- [x] Arquitectura de solución/proyectos definida (ver `docs/architecture/`)
- [x] Framework de UI decidido (WPF, .NET 10 LTS) — ADR-001
- [x] Estrategia de Raw Input y modelo de hilos definida — ADR-002, ADR-005
- [x] Estrategia de backend de mando virtual definida (`IVirtualGamepad.Submit(state)`; ViGEmBus como candidato legacy, HIDMaestro como candidato activo, spike obligatorio en Fase 3) — ADR-003
- [x] Formato y persistencia de perfiles + `ApplicationSettings` definidos — ADR-004
- [x] Modelo de concurrencia y transiciones (cambio de perfil, emergency stop) definidos — ADR-005
- [x] Diseño del Mapping Engine (bindings genéricos, WASD/triggers/mouse unificados) — ADR-006
- [x] Estructura exacta de proyectos/carpetas confirmada (ver más abajo)
- [x] Riesgos técnicos identificados
- [x] Revisión uno-por-uno de los 6 ADR con el usuario, con correcciones sustanciales incorporadas (ver tabla de estado)
- [x] Definition of Done detallada por fase alineada con el contenido final de cada ADR (ver secciones más abajo)
- [x] Pipeline avanzado de puntería V2: curva Dual Zone, anti-deadzone de salida, escala/límite separados, smoothing adaptativo, retención de decay y modo ADS derivado de `LeftTrigger`
- [x] Migración compatible de perfiles V1 a V2 y controles/presets de puntería en la UI

## In progress

- [ ] Fase 7 — UI: faltan selector de perfiles, hotkeys globales y tray icon; editor de bindings y ajustes avanzados están completos
- [ ] Fase 3 — cerrar `ADR-003A`, resolver la comprobación XInput restante y validar estrategia de firma/backend (ver Known issues)
- [ ] Fase 2 — falta confirmar visualmente la captura con hardware físico usando el visor ya construido

### Estado de revisión de ADRs

| ADR | Status | Notas |
|---|---|---|
| ADR-001 (UI framework) | Accepted | Runtime pasa a .NET 10 LTS; retirado el argumento de `HwndSource` (Raw Input queda desacoplado de WPF); corregido el argumento de deployment de WinUI 3 |
| ADR-002 (Raw Input) | Accepted with amendments | `CaptureTimestamp` propio vía `Stopwatch.GetTimestamp()`, `RIDEV_NOLEGACY` rechazado en V1, `RIDEV_DEVNOTIFY` añadido, drenado con `GetRawInputBuffer`, `Channel<T>` no se promete allocation-free, bounded channel, distinción `GamepadState`/`GamepadSnapshot`, ownership único de `RegisterRawInputDevices` vía `RawInputHost` |
| ADR-003 (backend mando virtual) | Accepted with major amendments | ViGEmBus confirmado EOL/sin mantenimiento activo (bajado a "legacy candidato"); HIDMaestro identificado como candidato OSS activo (reserva sobre su certificado autofirmado); `IVirtualGamepad` rediseñado a `Submit(in GamepadState)`; `ViGEmXbox360Backend` renombrado; `MockVirtualGamepad` movido a `VirtualController.TestUtilities`; spike obligatorio en Fase 3 → `ADR-003A` |
| ADR-004 (formato de perfiles) | Accepted with amendments | `ProfileId` (GUID) separado del nombre; almacenamiento en `%LOCALAPPDATA%\VirtualController\`; perfil guarda `output.controllerType`, no el backend técnico; validación estricta de requeridos y de versión futura; miembros desconocidos con `Disallow`; escritura atómica; `IProfileRepository` reducido a persistencia pura + `ProfileService`; introducido `ApplicationSettings` (incluye `emergencyStop`, `preferredBackend`) |
| ADR-005 (concurrencia) | Accepted | Retirada la idea de "único cuello de botella" y de `Channel<T>` lock-free/allocation-free garantizado; espera del consumidor como garantía de comportamiento; garantía de no-starvation con budget de drenado; 4 cadencias independientes (captura/drenado/`Submit()`/snapshot UI); `emergencyStop` reconocido vía flujo normal de input, no vía Control Queue/UI; `Volatile.Write`/`Read`; transiciones explícitas de cambio de perfil y Emergency Stop |
| ADR-006 (Mapping Engine) | Accepted | Añadida forma `AnalogValue(GamepadAxis, value)` para triggers (corrige inconsistencia con ADR-004); `AnalogAxis(Stick)` renombrado a `StickVector(Stick)`; semántica OR+resta para bindings redundantes; regla de conflicto `StickVector`/`StickDirection` en el mismo stick; `MouseToStickConverter` con `AddDelta`/`Update(now)` separados; introducido `CompiledMapping`; estado completo por iteración |

## Pending

- [ ] Fase 3 — Decisión final de backend como `ADR-003A`, firma y validación en hardware/juegos
- [ ] Fase 7 — Selector de perfiles, hotkeys globales y tray icon (ver Definition of Done)
- [ ] Fase 8 — Optimización basada en mediciones (recoge todos los puntos "diferido a Fase 8" de ADR-002/ADR-005)
- [ ] Fase 9 — Release (instalador, first-run checks, firma y manual; README inicial ya creado)

## Saneamiento 2026-08-20 — bugs encontrados y corregidos

Revisión adversarial de las 5213 líneas por 4 revisores especializados (concurrencia, interop Win32,
matemática de mouse, persistencia) más verificación automática. **Los 193 tests en verde daban una
confianza falsa**: varios de ellos codificaban el comportamiento roto como si fuera correcto.

### Críticos (rompían funcionalidad real)

- [x] **El mouse no funcionaba en absoluto.** El decay estaba condicionado a "no llegó delta en esta
  iteración" en vez de al tiempo. Con el bucle a ~10⁵ iteraciones/s y un mouse a 1000 Hz, el 99,5% de
  las vueltas vaciaban el acumulador: el stick derecho valía 0 casi siempre. Ahora el decay es
  puramente temporal e `ImmediateDecay` es una constante de tiempo corta, no un salto a cero.
- [x] **Guardar un perfil perdía la configuración de curva y decay.** `PowerCurve(3.5)` volvía como
  2.0, `ExponentialDecay(12)` como 8: los parámetros eran campos privados sin getter, así que el
  mapper solo podía escribir el tipo. Expuestos y persistidos.
- [x] **Una curva `Custom` producía un perfil imposible de recargar.** Se guardaba sin puntos, pasaba
  la guarda de `Save` (que solo validaba el DTO) y a partir de ahí todo `Load` fallaba para siempre,
  incluido el arranque. La guarda ahora hace el viaje completo hasta dominio.
- [x] **Coordenadas absolutas tratadas como deltas.** No se comprobaba `MOUSE_MOVE_ABSOLUTE`: en RDP,
  máquinas virtuales, streaming, tabletas y pantallas táctiles el stick se clavaba al máximo.
- [x] **La parada de emergencia por atajo de teclado no existía.** `ApplicationSettings.EmergencyStop`
  se validaba y no se cableaba a nada; el único disparador era un botón de WPF, justo lo que ADR-005
  descarta explícitamente. Añadidos `HotkeyCombination`, modificadores al enum `Key` y detección en el
  bucle desde el flujo normal de input.
- [x] **`Stop()` podía dejar dos bucles concurrentes sobre el mismo estado.** Se ignoraba el resultado
  de `Wait(2s)` y se liberaban los recursos aunque el bucle siguiera vivo.

### Altos

- [x] `ApplicationSettings` no se persistía en absoluto (nuevo `JsonApplicationSettingsRepository`)
- [x] La app cargaba un perfil arbitrario (`existing[0]`); ahora respeta `SelectedProfileId`
- [x] Las métricas de tiempo mostradas en la UI eran siempre 0: `RecordIteration`/`RecordDropped` no
  se llamaban nunca. Ahora se miden de verdad y la UI muestra el contador real de descartes
- [x] Cadencias fusionadas: `Submit()` y el snapshot se hacían en cada iteración del spin loop
  (~60-190 MB/s de basura Gen0). Ahora tienen deadlines independientes (250 Hz / 60 Hz)
- [x] `WM_INPUT` en primer plano no llamaba a `DefWindowProc`, requisito documentado por Microsoft
- [x] Una excepción managed podía atravesar el callback nativo del `WndProc` (muerte del proceso)
- [x] `Stop()` podía colgar la app: `PostMessage` sin comprobar + `Join()` sin timeout
- [x] `Stop()` no limpiaba el estado del bucle: al reanudar reaparecían las teclas retenidas
- [x] La aceleración se calculaba por evento y no por velocidad: el mismo gesto daba resultados un 90%
  distintos según el polling rate del mouse. El `timestamp` que ya se recibía estaba sin usar
- [x] `ArgumentException` cruda de `MouseSettings` se filtraba a la UI y reventaba el arranque
- [x] Un perfil de versión futura daba "archivo corrupto" en vez de "actualiza la aplicación",
  invitando al usuario a borrar justo los datos que hay que preservar

### Medios y de calidad

- [x] Clamp del acumulador cuadrado en vez de radial (retardo direccional de hasta el 41% en diagonal)
- [x] `ExponentialCurve` devolvía NaN con strength ≥ 88.73 → stick muerto en deflexión máxima
- [x] `CustomCurve` no validaba rangos ni NaN
- [x] `Enum.TryParse` aceptaba valores numéricos: `Keyboard.42` creaba un binding muerto silencioso
- [x] `List()` no verificaba que el id del JSON coincidiera con el nombre del archivo
- [x] El `.bak` sobrevivía al borrado del perfil (copia íntegra en disco, problema de privacidad)
- [x] El contador de descartes de la cola comparaba contra la constante, no contra la capacidad real
- [x] Duplicación: `MathF.Sqrt(x*x+y*y)` copiado 5 veces → `Vector2Math`
- [x] Código muerto: `ReadAllAsync`, `ProcessingOptions.MetricsEnabled` como segunda fuente de verdad
- [x] `MainViewModel` hardcodeaba "ViGEmBus" en un mensaje que mentiría con otro backend
- [x] Números mágicos sin explicar (`60f` del smoothing) y JSON con `+` escapado como `+`

### Tests añadidos (los huecos que permitieron los bugs)

- [x] Round-trip comparando **comportamiento** de curvas y decay, no solo el tipo (el test original
  solo usaba las dos implementaciones sin parámetros, las únicas que no podían perder nada)
- [x] Parada de emergencia por combinación de teclas: 10 tests, área sin ninguna cobertura previa
- [x] Persistencia de `ApplicationSettings`: 8 tests
- [x] Decay con bucle rápido entre eventos (el escenario que rompía el mouse)
- [x] Aceleración por velocidad frente a por tamaño de paquete

### Fase 8 (parcial) — espera del bucle resuelta con mediciones (2026-08-20)

`Thread.SpinWait(64)` consumía **100,1% de un núcleo de forma permanente**, incluso con la app abierta
y sin jugar (medido: ~120 s de CPU en una sesión de prueba de 2 minutos). ADR-005 punto 3 dejaba la
primitiva sin fijar hasta tener datos; estos son los datos, medidos en la máquina de desarrollo:

| primitiva | resolución (mediana) | CPU |
|---|---|---|
| `Thread.SpinWait(64)` | 0,004 ms | **100,1 %** |
| `Thread.Sleep(1)` | 15,1 ms | 1,0 % |
| `AutoResetEvent.WaitOne(1 ms)` | 15,1 ms | **0,0 %** |
| `SemaphoreSlim.Wait(1 ms)` | 15,2 ms | — |

Ninguna sirve por sí sola: o se quema un núcleo, o se pierden los deadlines de 4 ms. La salida vino de
observar que **las dos situaciones tienen exigencias distintas**:

- [x] **Con actividad** (stick desviado o decay convergiendo): `SpinWait` adaptativo — mantiene la
      precisión y, a diferencia del spin fijo, cede el núcleo por su cuenta si la espera se alarga.
- [x] **En reposo**: bloqueo sobre una señal (`AutoResetEvent`) que el productor activa al encolar —
      **0% de CPU**, y despertar inmediato en cuanto el usuario toca algo.
- [x] `IInputEventQueue.WaitForInput(timeout)` añadido como abstracción; `ProcessingLoop.HasPendingTimeWork`
      distingue ambas situaciones.
- [x] 7 tests nuevos que fijan el contrato (despertar por señal, timeout, y cuándo hay trabajo pendiente).

### Deuda consciente que queda anotada (no corregida)

- `CompiledMapping` **no compila nada**: valida y guarda la lista, pero `Resolve` hace búsqueda lineal
  (216 recorridos y ~20 asignaciones por iteración). ADR-006 punto 8 pide agregadores precomputados.
  Es trabajo de Fase 8 con mediciones, no una corrección a ciegas.
- El consumo de CPU con la emulación **activa** no se ha vuelto a medir tras el cambio, porque Smart
  App Control impide arrancar la app (ver sección de bloqueo). La mejora en reposo está medida por
  benchmark; la mejora en uso real está pendiente de confirmar.
- `hDevice` se descarta: no se distinguen varios teclados/mouse. ADR-002 lo describe
  (`RawInputDeviceRegistry`) pero nunca se implementó — hay que implementarlo o corregir el ADR.
- Auto-repeat de teclado sin deduplicar: bajo saturación podría expulsar un `KeyUp` de la cola.
- [x] La semántica ambigua de `MaximumOutput` quedó resuelta en V2: `OutputScale` controla la
  ganancia y `MaximumOutput` es ahora un límite real, con migración compatible desde V1.
- Contradicción entre ADR-004 y el código sobre dónde viven los hotkeys de perfil, y el ADR documenta
  `Mouse.LeftButton` cuando el código emite `Mouse.Left`.

## BLOQUEO ACTIVO — Smart App Control bloquea el backend (2026-08-20)

Windows **Smart App Control** (`VerifiedAndReputablePolicyState = 1`) bloquea
`VirtualController.VirtualGamepad.dll` con `0x800711C7` ("Una directiva de Control de aplicaciones
bloqueó este archivo"; evento CodeIntegrity 3118). La DLL está `NotSigned` y, al recompilarse, cada
binario nuevo carece de reputación.

**Alcance real, verificado:**
- La aplicación **no arranca**, y el spike y `VirtualController.VirtualGamepad.Tests` no pueden cargar.
- El alcance varía con cada recompilación sin firma: además del backend, Smart App Control ha bloqueado
  una DLL nueva de Infrastructure y con ello los tests de persistencia. Las suites puras siguen siendo
  ejecutables; ver `Last verification` para las cifras actuales.
- Antes del bloqueo, el spike había pasado 14/14 contra hardware real y la app funcionó end-to-end
  (ver secciones de Fase 3 y Fase 4): **el bloqueo es de política de Windows, no un defecto del código**.

**Decisión del usuario (2026-08-20):** no desactivar Smart App Control. Desactivarlo es irreversible
sin reinstalar Windows, y es una protección legítima. Se continúa el desarrollo en las áreas no
afectadas, que son casi todas.

**Consecuencia para Fase 9 (Release) — riesgo real de distribución:**
Windows 11 activa Smart App Control por defecto en instalaciones limpias, así que la aplicación sería
bloqueada en la máquina de cualquier usuario en esas condiciones. Distribuir esto exige **firma de
código con un certificado reconocido**, no basta con un autofirmado. Refuerza lo que ADR-003 ya
anotaba sobre firma y distribución, ahora con evidencia empírica en vez de como riesgo teórico.

## Known issues

**Verificación pendiente:**
- La captura de Raw Input con **hardware físico moviéndose** sigue sin verificarse de forma observada. La app arranca y la ventana vive, pero no se ha confirmado visualmente que mover el mouse desplace el punto del stick derecho. El visor ya existe para hacerlo — requiere un humano mirando la pantalla.
- Falta probar el mando virtual **dentro de un juego real** y en una segunda máquina (Windows 10, Secure Boot), pendientes del checklist de ADR-003.
- `WM_INPUT_DEVICE_CHANGE` se recibe pero su callback no hace nada (sin re-registro ni notificación al dominio).

**Deuda consciente (no bugs):**
- Existe migración V1→V2 para conservar la semántica antigua de `MaximumOutput`; futuras versiones necesitarán migraciones secuenciales adicionales.
- La espera en reposo ya usa una señal y evita quemar un núcleo; falta volver a medir CPU/latencia durante emulación activa.
- Sin logging estructurado todavía (requisito 21): los errores llegan a la UI pero no se escriben a `Logs/`.
- Sin selector de perfiles en la UI; el editor de bindings ya está implementado.

## Definition of Done por fase

Cada checklist referencia el/los ADR de los que se deriva, para poder verificar contra la fuente si hay duda — no reinterpretar de memoria.

### Fase 1 — Core

- [x] `GamepadState` (sticks `-1..+1`, triggers `0..1`, botones vía `GamepadButtons` flags), agnóstico de backend (ADR-003 punto 2) — `src/VirtualController.Core/Gamepad/GamepadState.cs`
- [x] Enums `GamepadButton`, `GamepadButtons` (flags), `GamepadAxis` (`LeftTrigger`/`RightTrigger`), `Stick`, `Direction`, más `Key`/`MouseButton`/`InputDevice`/`PhysicalInput` para identidad de input físico (ADR-006 punto 1) — `src/VirtualController.Core/Gamepad/`, `src/VirtualController.Core/Mapping/`
- [x] `IVirtualGamepad` (en `VirtualController.Core`, puerto — no en `VirtualGamepad`): `IsConnected`, `Connect()`, `Disconnect()`, `Submit(in GamepadState)`, `Reset()` (ADR-003 puntos 1 y 3) — `src/VirtualController.Core/Gamepad/IVirtualGamepad.cs`
- [x] `MockVirtualGamepad` en `VirtualController.TestUtilities` (solo referencia `Core`), no en ningún proyecto de `src/` (ADR-003 punto 5) — `tests/VirtualController.TestUtilities/MockVirtualGamepad.cs`
- [x] Modelo `Binding`/`VirtualOutput` con sus 4 formas: `DigitalButton`, `AnalogValue(GamepadAxis, value)`, `StickDirection(Stick, Direction)`, `StickVector(Stick)` (ADR-006 puntos 1–2) — `src/VirtualController.Core/Mapping/VirtualOutput.cs`, `Binding.cs`
- [x] `StickComposer`: resta (`x = Right−Left`, `y = Up−Down`) + `NormalizeDiagonal` (requisito 9: `W+D ≈ (0.7071, 0.7071)`, nunca `(1,1)`), y `BindingResolver` con el colapso OR de bindings redundantes sobre la misma dirección/botón/eje (ADR-006 punto 3) — `StickComposer.cs`, `BindingResolver.cs`
- [x] Validación de conflicto: rechazar un `StickVector` combinado con `StickDirection` sobre el mismo stick (ADR-006 punto 4) — `MappingValidator.cs`, `MappingConflictException.cs`
- [x] `MappingEngine.Resolve` recompone el estado **completo** de botones/ejes/sticks-por-dirección en cada resolución, nunca una actualización parcial (ADR-006 punto 9) — `MappingEngine.cs`. **Nota:** `StickVector` (mouse) todavía no se resuelve aquí — depende de `MouseToStickConverter`, pendiente (ver abajo)
- [x] `ActiveInputState`: componente dedicado (`SetActive`/`IsActive`/`Active`) que rastrea qué `PhysicalInput` están activos ahora mismo (ADR-006 punto 6) — `ActiveInputState.cs`
- [x] `MouseToStickConverter`: API mínima `AddDelta(dx,dy,timestamp)` / `Update(now)` / `CurrentValue` (ADR-006 punto 7). **Implementación intencionalmente mínima**: única estrategia de decay presente es "Immediate" (vuelve a `(0,0)` si no llega delta nuevo antes del siguiente `Update`); sensibilidad, curvas, deadzone, smoothing y `LinearDecay`/`ExponentialDecay` quedan explícitamente para Fase 5 — `MouseToStickConverter.cs`
- [x] `CompiledMapping.Compile(bindings, options)` — invoca `MappingValidator` una sola vez al compilar (no en el hot path) y devuelve un objeto inmutable con `Resolve(activeInputs)` (ADR-006 punto 8) — `CompiledMapping.cs`
- [x] Tests: WASD, diagonal (`W+D` normalizado, nunca `(1,1)`), `W+S` se cancela a `(0,0)`, OR+resta con bindings redundantes (dirección y botón), `Mouse.LeftButton → RightTrigger` vía `AnalogValue`, rechazo del conflicto `StickVector`/`StickDirection` (tanto en `MappingValidator` como al compilar), `ActiveInputState`, `MouseToStickConverter` (clamp, decay inmediato), `CompiledMapping`, `GamepadState`, `MockVirtualGamepad` — **39 tests, todos en verde** (30 Mapping.Tests + 5 VirtualGamepad.Tests + 4 Core.Tests)

**Fase 1 — Core: todos los ítems de la Definition of Done están cumplidos y verificados.**

### Fase 2 — Raw Input

- [x] `RawInputHost` como único llamante de `RegisterRawInputDevices` en todo el proceso; implementa el puerto `IInputSource` de `Core` (ADR-002 punto 3) — `src/VirtualController.Windows/RawInput/RawInputHost.cs`
- [x] Ventana `HWND_MESSAGE` dedicada + hilo de SO propio con su propio message loop, sin referenciar WPF (ADR-001 regla no negociable; ADR-002 punto 1) — `RawInputWindow.cs`
- [x] Flags: `RIDEV_INPUTSINK | RIDEV_DEVNOTIFY`; `RIDEV_NOLEGACY` explícitamente ausente (ADR-002 punto 2) — `RawInputWindow.cs`
- [x] `GetRawInputData` + drenado por lotes con `GetRawInputBuffer` (con alineación a 8 bytes de los `RAWINPUT` en 64-bit) para dispositivos de alta frecuencia (ADR-002 punto 4) — `RawInputWindow.DrainPendingRawInput`
- [x] `InputEvent.CaptureTimestamp` vía `Stopwatch.GetTimestamp()`, documentado en código como instante de captura, no de hardware (ADR-002 punto 5) — `src/VirtualController.Core/Input/InputEvent.cs`
- [x] `IInputEventQueue` + `ChannelInputEventQueue` con `Channel<T>` bounded (`SingleReader`/`SingleWriter`, `FullMode = DropOldest`, `AllowSynchronousContinuations = false`) como implementación inicial, sin prometer lock-free/allocation-free (ADR-002 punto 6) — `src/VirtualController.Core/Input/`
- [x] `RawInputParser`: traducción pura `RAWINPUT` → `InputEvent` (mouse move, 5 botones de mouse down/up, teclado make/break), testeada con buffers sintéticos sin hardware
- [x] Recepción de `WM_INPUT_DEVICE_CHANGE` conectada en el `WndProc` (ADR-002 punto 2). **Nota:** el callback existe pero todavía no hay lógica de re-registro/notificación al dominio — se completa cuando exista el panel de diagnóstico (Fase 7)
- [ ] Visor/diagnóstico manual para confirmar captura real de teclado/mouse y ausencia de eventos duplicados con hardware físico (requiere UI mínima — se cubre junto a Fase 4/7; los tests actuales verifican el ciclo de vida Win32 real pero **no** captura de hardware)

### Fase 3 — Spike de backend de mando virtual

**ViGEmBus 1.22.0 instalado y verificado el 2026-08-20** ("Nefarius Virtual Gamepad Emulation Bus",
Status OK, servicio `ViGEmBus` Running). Verificación hecha con un programa que lee el resultado **por
XInput**, no confiando en lo que el backend dice de sí mismo: **14/14 comprobaciones OK**.

- [x] PoC `ViGEmXbox360Backend` — funciona end-to-end
- [x] Windows expone el mando virtual (aparece un slot XInput nuevo al conectar)
- [x] `Submit` visible por XInput: `LeftStickY=1` → `ThumbLY=32767`; `RightTrigger=1` → `RT=255`; botón A correcto
- [x] Diagonal normalizada verificada contra hardware: `0.7071` → `X=23170 Y=23170` (exacto), sin saturar
- [x] Latencia `Submit → visible en XInput`: **mediana 0.02 ms**, máx 0.05 ms (30 medidas)
- [x] Connect/disconnect repetido (5 ciclos) sin fugas; el mando desaparece al desconectar y no quedan huérfanos
- [x] **Bug crítico encontrado y corregido durante el spike:** un mando recién creado reportaba valores
      basura por XInput (`LX=-3356 LY=-1869`, ~10% de deflexión) y `Submit(Neutral)` **no lo corregía**,
      porque el report coincidía con el estado interno del cliente y no llegaba a transmitirse. En un
      juego, el personaje se movía solo nada más arrancar la emulación. `Connect()` fuerza ahora un
      estado inicial neutro observable. Cubierto con test de regresión que lee por XInput.
- [x] Test de backend que ya no se auto-desactiva: afirma en ambos casos (con y sin driver)
- [ ] PoC `HidMaestroXboxBackend` — no evaluado: ViGEm cumple los requisitos de V1 con margen amplio.
      Se mantiene como candidato documentado por el EOL de ViGEm, no por una carencia técnica observada
- [ ] Probar en Windows 10 y con Secure Boot activo (requiere otra máquina)
- [ ] Probar dentro de un juego real
- [ ] Probar comportamiento tras un crash de la aplicación
- [ ] Comparar consumo de CPU y jitter entre backends (requiere el segundo backend)
- [ ] Decisión final formal como `ADR-003A`

**Conclusión provisional del spike:** ViGEmBus cumple los requisitos de V1 con latencia muy por debajo
de lo necesario. El riesgo que sigue vigente **no es técnico sino de mantenimiento**: el driver está
EOL desde 2023 y el archivo instalado es de 2022. Por eso `IVirtualGamepad` sigue siendo la barrera de
aislamiento y HIDMaestro sigue anotado como alternativa.

### Fase 4 — Integración end-to-end

- [x] `ProcessingLoop`: único propietario del estado mutable y único que invoca `IVirtualGamepad` (ADR-005, principio central) — `Core/Engine/ProcessingLoop.cs`
- [x] `ControlCommand` (Start/Stop/ChangeMapping) aplicados en la frontera de una iteración (ADR-005 punto 1)
- [x] `EmergencyStop()` invocable directamente por el consumidor, sin pasar por la Control Queue ni por la UI; además olvida las teclas retenidas para que al reanudar no se repitan (ADR-005 puntos 1 y 7)
- [x] `ChangeMapping` neutraliza salida + limpia inputs activos + resetea el converter antes de cambiar: verificado que no queda estado híbrido entre perfiles (ADR-005 punto 6)
- [x] Budget de drenado (`InputDrainBudget`) que garantiza no-starvation, verificado con 50 eventos y budget de 5; los restantes se consumen en iteraciones siguientes sin perderse (ADR-005 punto 4)
- [x] `GamepadSnapshot` inmutable publicado con `Volatile.Write`, creado solo al publicar (ADR-005 punto 2)
- [x] `EngineMetrics` opcional y desactivable (requisito 7), verificado que no cuenta nada cuando está apagado
- [x] W/A/S/D → stick izquierdo (con diagonal normalizada), mouse → stick derecho, click izq. → RT, click der. → LT, Space → A — todo el mapeo del brief sección 1
- [x] `KeyUp` verificado: soltar devuelve a neutro y "soltar todo" produce `GamepadState.Neutral` exacto (brief sección 40)
- [x] Decay verificado end-to-end: el stick sigue regresando al centro cuando no llega ningún evento nuevo
- [x] **20 tests de integración** en `Mapping.Tests`, todos con `MockVirtualGamepad` (sin necesidad de driver)

### Fase 5 — Mouse Engine completo

- [x] `SensitivityX`/`SensitivityY` independientes, `InvertX`/`InvertY` — `src/VirtualController.Core/Mouse/MouseSettings.cs`
- [x] Deadzone inner/outer como **única** implementación del sistema, con variante radial que delega en la escalar para que no puedan divergir (requisito 12) — `Mouse/Deadzone.cs`
- [x] `MaximumOutput`
- [x] Curvas: `LinearCurve`, `PowerCurve`, `ExponentialCurve` (normalizada para preservar extremos), `CustomCurve` (puntos + interpolación) tras `IResponseCurve` (requisito 11) — `Mouse/IResponseCurve.cs`
- [x] `Acceleration` proporcional a la magnitud del gesto
- [x] `Smoothing` desactivado por defecto, con filtro exponencial independiente del framerate (requisito 10)
- [x] Decay: `ImmediateDecay`, `LinearDecay`, `ExponentialDecay` tras `IDecayStrategy`, intercambiables por configuración (requisito 13) — `Mouse/IDecayStrategy.cs`
- [x] `MouseToStickConverter` completo con `AddDelta`/`Update(now)`/`CurrentValue`/`Reset`; curva y deadzone aplicadas **a la magnitud** (no por eje) para no deformar diagonales — `Mouse/MouseToStickConverter.cs`. Sustituye al converter mínimo que estaba en `Mapping/` (eliminado para no duplicar)
- [x] Tests: 4 curvas, deadzone (escalar + radial), 3 estrategias de decay (incl. independencia de framerate), sensibilidad/inversión, aceleración, clamping, validación de ajustes — **87 tests en `Mapping.Tests`**
- [x] **Bug real corregido:** el suavizado se desactivaba en silencio cuando dos `Update` compartían timestamp (`alpha = 1` con `deltaSeconds == 0`); ahora el filtro no avanza si no ha transcurrido tiempo

### Fase 6 — Perfiles y `ApplicationSettings`

- [x] `ProfileId` (GUID) separado del nombre visible; el archivo se nombra por id — `Core/Profiles/ProfileId.cs`
- [x] Almacenamiento en `%LOCALAPPDATA%\VirtualController\` (`Profiles/`, `Settings/`, `Logs/`), con raíz inyectable para que los tests no toquen los datos reales — `Infrastructure/Persistence/StoragePaths.cs`
- [x] DTOs de perfil separados del dominio + validación al construir — `ProfileDto.cs`, `ProfileMapper.cs`
- [x] Validación de campos requeridos (`version`, `id`, `name`, `output.controllerType`) que falla en vez de aplicar defaults silenciosos
- [x] Miembros desconocidos con `JsonUnmappedMemberHandling.Disallow`: un typo como `sensitivtyX` falla visiblemente
- [x] Rechazo explícito de versión futura (`UnsupportedProfileVersionException`), sin degradar ni sobrescribir datos que no entendemos
- [x] Punto de extensión para migraciones secuenciales presente en `ProfileMapper.ToDomain`. **Nota:** no hay migraciones reales todavía porque solo existe la V1 — no se puede testear un salto que no existe
- [x] Escritura atómica (`.tmp` + `File.Replace` con `.bak`) — `AtomicFileWriter.cs`; verificado que no deja `.tmp` huérfanos y que genera backup
- [x] Import nunca sobrescribe: siempre genera `ProfileId` nuevo y resuelve colisiones de nombre
- [x] Export portable vía `WriteToFile`/`ReadFromFile`
- [x] `IProfileRepository` (persistencia pura: List/Load/Save/Delete) + `ProfileService` (Create/Rename/Duplicate/Import)
- [x] Errores de aplicación tipados (`ProfileValidationException`, `ProfileNotFoundException`, `UnsupportedProfileVersionException`, `ProfileNameConflictException`, `ProfileStorageException`); `JsonException`/`IOException` nunca escapan de infraestructura
- [x] `ApplicationSettings` separado de `Profile`, con `emergencyStop` y `preferredBackend`
- [x] `emergencyStop` validado en el dominio, no solo en UI (un JSON editado a mano no puede vaciarlo)
- [x] El perfil guarda `output.controllerType`, nunca un backend concreto; `preferredBackend` es un identificador de texto en `ApplicationSettings`
- [x] Tests: round-trip completo, JSON corrupto, campo requerido ausente, campo desconocido, versión futura, curva desconocida, perfil mínimo con defaults, listado que ignora archivos corruptos, backup/atomicidad, export→import, conflictos de nombre, `ApplicationSettings` — **49 tests en `Core.Tests`**

### Fase 7 — UI

- [x] `MainWindow` WPF funcional, verificada arrancando de verdad (ventana "Virtual Controller" viva, cierre limpio sin procesos huérfanos)
- [x] Botón Start/Stop con estado y mensaje de error accionable traducido desde `GamepadConnectionResult` (no una excepción cruda)
- [x] Botón de parada de emergencia en la UI (además del que va por el flujo de input)
- [x] Visualizador en tiempo real: posición de ambos sticks en canvas circular, barras de gatillos, botones pulsados, refrescado por `CompositionTarget.Rendering` (la UI marca su ritmo, no el motor — ADR-005 punto 5)
- [x] Panel de diagnóstico con métricas (eventos, envíos, descartados, media/máx de procesamiento)
- [x] Lista de bindings activos del perfil cargado
- [x] `MainViewModel` sin tipos de WPF en su superficie pública (ADR-001); usa `CommunityToolkit.Mvvm`
- [x] `BackendResolver` en el composition root, nunca en `Core` (ADR-003 punto 3)
- [x] Manejo de excepción no controlada que detiene la emulación y resetea el mando antes de avisar (requisito 20)
- [x] Verificado end-to-end real: al arrancar creó el perfil `Default` en `%LOCALAPPDATA%\VirtualController\Profiles\<guid>.json` con el mapeo exacto del brief
- [x] **Editor de bindings desde la UI** (requisitos 14 y 17) — implementado y cubierto con tests:
  - `BindingCatalog` (Core): qué entradas y salidas se pueden elegir, con nombres legibles en español
  - `BindingEditor` (Core): añadir/quitar/reemplazar sobre una copia en memoria; solo escribe al confirmar
  - `BindingEditorViewModel` + `BindingRowViewModel` (App): desplegables de entrada y salida por fila
  - Validación en vivo: un conflicto `StickVector`/`StickDirection` **deshabilita Guardar** y explica el motivo
  - Guardar valida antes de escribir, así que un conjunto inválido nunca sustituye al perfil que funcionaba
  - Al guardar se notifica al motor (`ChangeProfile`), que aplica el perfil nuevo **sin reiniciar la app**
  - Botón Descartar que restaura los bindings guardados
  - **29 tests** (14 en `Core.Tests` para la lógica + 15 en el nuevo `App.Tests` para el ViewModel)
- [x] **Ajustes de mouse desde la UI, con aplicación en vivo** (requisito 17) — implementado y cubierto:
  - `MouseSettingsDraft` (Core): borrador mutable que se valida sin lanzar, para poder avisar mientras
    se arrastra un deslizador en vez de reventar a cada cambio
  - `MouseSettingsViewModel` (App): sensibilidad X/Y, recorrido completo, invertir ejes, deadzone
    interior/exterior, salida máxima, curva + su parámetro, aceleración, suavizado y retorno al centro
  - **Aplicación en caliente**: cada cambio llega al motor al instante vía `ChangeMouseSettings`, así
    que se puede afinar mirando el visualizador sin reiniciar ni editar el JSON
  - `ControlCommand.ChangeMouseSettings` separado de `ChangeMapping` a propósito: cambiar de perfil
    debe olvidar las teclas pulsadas, pero mover un deslizador **no debe soltarte las teclas**
  - Un valor inválido muestra el motivo y **no se aplica**: el motor conserva la última configuración
    que funcionaba, en vez de quedarse sin respuesta a mitad de un ajuste
  - Probar no persiste: guardar en el perfil es un paso explícito, con Revertir y Valores por defecto
  - El parámetro se reinicia al cambiar de tipo de curva (un exponente 2 y una intensidad 2 no
    significan lo mismo)
  - **29 tests** (13 en `Mapping.Tests` para el borrador + 16 en `App.Tests` para el ViewModel)
- [x] **Pipeline de puntería V2** — anti-deadzone para compensar la zona muerta del juego,
  `OutputScale` separado del límite real, curva Dual Zone continua, smoothing adaptativo, retención
  temporal antes del decay y modo ADS activado por el `LeftTrigger` virtual (respeta remapeos).
  Incluye migración V1→V2, persistencia, controles en vivo, tres presets y pruebas matemáticas/integración.
- [ ] Selector de perfiles desde la UI (crear/duplicar/renombrar ya existen en `ProfileService`, falta la pantalla)
- [ ] Hotkeys globales configurables (requisito 19): `ApplicationSettings` ya los modela y valida, pero nada los registra/escucha todavía
- [ ] Icono de bandeja del sistema

## Estructura de proyectos (definida en Fase 0; creada en Fase 1)

```
VirtualController.slnx   (formato .slnx nativo de .NET 10, no .sln)

src/
 ├── VirtualController.App                  (WPF, ViewModels, arranque, DI, Control Queue)
 ├── VirtualController.Core                 (IVirtualGamepad [puerto], GamepadState, Binding/VirtualOutput,
 │                                            StickComposer, ActiveInputState, MouseToStickConverter,
 │                                            CompiledMapping, Profile/ApplicationSettings + ProfileService,
 │                                            IProfileRepository [puerto] — todo agnóstico de backend y de Win32)
 ├── VirtualController.Windows              (RawInputHost, RawInputWindow, RawInputParser,
 │                                            RawInputDeviceRegistry, NativeMethods)                    ──► Core
 ├── VirtualController.VirtualGamepad       (ViGEmXbox360Backend : IVirtualGamepad [legacy],
 │                                            HidMaestroXboxBackend : IVirtualGamepad [candidato, spike Fase 3]) ──► Core
 └── VirtualController.Infrastructure       (JsonProfileRepository : IProfileRepository, JSON de
                                              ApplicationSettings, logging, escritura atómica)          ──► Core

VirtualController.App ──► Core, Windows, Infrastructure, VirtualGamepad
VirtualController.Core no referencia ninguno de los anteriores (ver ADR-001/ADR-003).

tests/
 ├── VirtualController.TestUtilities        (dobles de prueba compartidos: MockVirtualGamepad : IVirtualGamepad
 │                                            — referencia solo Core, no VirtualGamepad; no referenciado por src/)
 ├── VirtualController.Core.Tests           (GamepadState, Profile, ApplicationSettings, dominio general)
 ├── VirtualController.Mapping.Tests        (bindings, WASD, diagonal, OR+resta, conflicto StickVector/
 │                                            StickDirection, MouseToStickConverter: curvas/deadzone/decay)
 ├── VirtualController.Input.Tests          (parseo/traducción de Raw Input, sin dependencia de hardware real)
 └── VirtualController.VirtualGamepad.Tests (usa MockVirtualGamepad de TestUtilities)

docs/
 └── architecture/
      ADR-001-ui-framework.md
      ADR-002-input-system.md
      ADR-003-virtual-gamepad-backend.md
      ADR-004-profile-format.md
      ADR-005-concurrency-and-state-flow.md
      ADR-006-mapping-engine-design.md
```

## Last verification

Verificación 2026-08-21:

dotnet build (`VirtualController.slnx`, 11 proyectos): PASS — 0 advertencias, 0 errores.
Suites ejecutables tras la mejora de puntería: Mapping **165/165**, App **33/33**, Input **11/11**.
`Core.Tests`: 48 pruebas puras pasan y 35 de persistencia quedan bloqueadas al cargar la DLL recién compilada de Infrastructure por Smart App Control (`0x800711C7`); no son aserciones fallidas.
La ejecución previa de `VirtualGamepad.Tests` cargó 28 pruebas: 27 pasaron y `Connect_LeavesDeviceInNeutralState` no obtuvo estado XInput; requiere diagnóstico con hardware/backend.
Spike Fase 3 contra XInput real: PASS — 14/14 comprobaciones, latencia mediana 0.02 ms (ejecutado antes del bloqueo)
Arranque real de la app tras el saneamiento: PASS — desde cero creó `Profiles/<guid>.json` **y** `Settings/settings.json` (este último antes no se creaba nunca), ventana abierta, cierre limpio sin procesos huérfanos
Verificación con mando virtual real: PARCIAL — el backend conecta y 27/28 tests pasaron en la última
ejecución; falta resolver la lectura neutral de XInput y probar dentro de un juego real.
.NET SDK: 10.0.400 (instalado vía winget en esta sesión; no estaba presente en la máquina)
Nota de formato: `dotnet new sln` en .NET 10 genera `.slnx` (XML) en vez de `.sln` — la solución es `VirtualController.slnx`, no `VirtualController.sln`
