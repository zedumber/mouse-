# ADR-006: Diseño del Mapping Engine (bindings genéricos vs. casos especiales)

**Status:** Accepted
**Last reviewed:** 2026-08-20

## Context

El brief pide un Mapping Engine genérico (`PhysicalInput → Binding → VirtualOutput`, requisito 14) pero también describe WASD como si necesitara lógica de composición vectorial especial (requisito 9) y el mouse como un conversor aparte con estado propio (requisito 10). Sin una decisión explícita, es fácil terminar con dos sistemas de mapping distintos — justo la duplicación de lógica que el brief pide evitar (requisito 5).

La idea central de la versión anterior de este ADR (un único modelo de `Binding`, WASD no es un caso especial) se mantiene. Pero al revisarla contra ADR-004 y ADR-005 aparecieron una inconsistencia funcional real (los triggers no podían expresarse con el modelo original) y varias reglas de comportamiento que estaban implícitas y debían quedar explícitas para que el sistema sea determinista.

## Decision

### 1. `VirtualOutput` se amplía: faltaba una forma para triggers/valores analógicos escalares

Inconsistencia real detectada: ADR-004 ya usa como ejemplo `Mouse.LeftButton → Gamepad.RightTrigger`, y ADR-003/`GamepadState` definen los triggers como valores normalizados `0.0..1.0`, distintos de los botones digitales y de los sticks. La versión anterior de `VirtualOutput` (`DigitalButton` / `StickDirection` / `AnalogAxis(Stick)`) **no tenía ninguna forma capaz de representar ese binding**. Se añade:

```csharp
VirtualOutput
├── DigitalButton(GamepadButton)
├── AnalogValue(GamepadAxis axis, double value)   // triggers y futuros escalares
├── StickDirection(Stick, Direction)
└── StickVector(Stick)                             // ver punto 2
```

`Mouse.LeftButton → AnalogValue(RightTrigger, 1.0)` al pulsar, `AnalogValue(RightTrigger, 0.0)` al soltar. Este mismo mecanismo permite, sin inventar nada nuevo, algo como `Key.Q → AnalogValue(LeftTrigger, 0.5)` si en el futuro se quisiera un trigger parcial desde una tecla.

### 2. `AnalogAxis(Stick)` renombrado a `StickVector(Stick)`

Corrección de nombre, no solo estética: el mouse no produce un único "eje" — produce `dx`/`dy`, es decir, un **vector bidimensional** que termina en `Stick.X`/`Stick.Y`. `AnalogAxis(Stick.Right)` sugería (incorrectamente) un escalar. `StickVector(Stick.Right)` describe con precisión lo que `MouseToStickConverter` produce.

### 3. Regla explícita para múltiples bindings apuntando a la misma salida

No estaba definido qué pasa si, por ejemplo, `W` y `↑` apuntan ambos a `Stick.Left.Up`. Sumar un vector por cada binding activo (`(0,+1) + (0,+1) = (0,+2)`) sería un bug real, no un detalle menor.

- **Salidas digitales**: semántica **OR**. `Space → A` y `Enter → A` simultáneos dan `A = Space || Enter`, nunca "doble intensidad" (los botones no tienen intensidad).
- **`StickDirection`**: primero se colapsan las direcciones lógicas activas con OR, y solo entonces se compone el vector:

```text
Up    = W || ↑        Left  = A || ←
Down  = S || ↓         Right = D || →

x = Right − Left
y = Up − Down
```

Así, `W + ↑` sigue dando `(0, +1)`, y `W + S` da correctamente `(0, 0)` — determinista, sin depender de cuántos bindings coincidan en la misma dirección.

### 4. Regla de conflicto: un stick no puede tener dos productores stateful simultáneos

Caso no cubierto: ¿qué pasa si WASD y el movimiento del mouse apuntan al mismo stick? Para V1, en vez de inventar una política de mezcla sin un caso de uso real que la justifique:

> Un stick puede recibir múltiples `StickDirection` (se combinan por OR + resta, punto 3). Un stick admite **como máximo un** `StickVector` (productor con estado, ligado a `MouseToStickConverter`). Un perfil que asigne un `StickVector` y uno o más `StickDirection` al **mismo** stick se **rechaza en la validación del perfil** (ADR-004), no en tiempo de ejecución.

```text
VÁLIDO                          INVÁLIDO (V1)
W/A/S/D → Left.*                W/A/S/D → Right.*
Mouse   → Right.StickVector     Mouse   → Right.StickVector
```

Si en el futuro aparece un caso de uso real para mezclarlos, se añade una política explícita (`Combine`/`Override`/`MaxMagnitude`...) como su propia decisión, no por omisión.

### 5. "Cada frame" no es un concepto válido tras ADR-005

ADR-005 no tiene un "frame" — tiene un consumidor de procesamiento dirigido por input y por deadlines, con cadencias independientes. Se sustituye "cada frame del bucle de procesamiento" por:

> en cada iteración del consumidor de procesamiento en la que deba recomputarse el estado de salida.

Esto mantiene el Mapping Engine independiente de la frecuencia concreta del loop (que ADR-005 deja para Fase 8).

### 6. Teclado/botones y mouse producen tipos de señal distintos — y se tratan como tales

El teclado (y los botones del mouse) generan **cambios de estado** (`KeyDown`/`KeyUp` → activo/inactivo); el mouse entrega **deltas relativos** (`dx`/`dy`). Se formaliza como dos caminos hacia el mismo `GamepadState`:

```text
InputEvent (Key/Button)  →  ActiveInputState  →─┐
                                                  ├──► Mapping Engine → GamepadState
MouseDelta                →  MouseToStickConverter ─┘
```

`ActiveInputState` es el conjunto de controles digitales actualmente activos (consultado por `DigitalButton`/`StickDirection`); `MouseToStickConverter` es el único componente que consume deltas relativos y mantiene estado propio (acumulación, decay, smoothing).

### 7. El decay no puede depender de que llegue un nuevo `MouseDelta`

Cuando el mouse deja de moverse, `MouseToStickConverter` todavía tiene trabajo pendiente (el decay hacia el centro). ADR-002 ya separa explícitamente trabajo dirigido por input de trabajo dirigido por tiempo — `MouseToStickConverter` debe reflejar esa misma separación en su propia API, no solo el bucle exterior:

```csharp
mouseConverter.AddDelta(dx, dy, timestamp);   // dirigido por input, solo cuando llega un MouseDelta
mouseConverter.Update(now);                    // dirigido por tiempo, cada iteración con recomputación
var stick = mouseConverter.CurrentValue;       // lectura del estado actual
```

Con cero eventos de mouse en una iteración, `Update(now)` sigue ejecutándose y el stick sigue regresando correctamente hacia `(0,0)`.

### 8. El perfil se compila a una representación de ejecución; no se interpreta JSON/strings en el hot path

No se recorren estructuras pensadas para persistencia (`"Keyboard.W"`, `"Stick.Left.Up"` como strings) en cada iteración. El perfil validado (ADR-004) se compila una vez a una representación inmutable de ejecución:

```text
Profile (validado) → MappingCompiler → CompiledMapping (inmutable)
```

`CompiledMapping` contiene ya resueltas las estructuras que el hot path necesita: agregadores por salida, `StickComposer` por stick, el binding del mouse hacia su `StickVector` destino, etc. Un cambio de perfil compila la nueva configuración y la activa en la frontera de una iteración del consumidor de procesamiento — coherente con la transición de cambio de perfil ya definida en ADR-005 (punto 6 de ese ADR). Esto no se plantea como optimización prematura, sino como una separación limpia entre "configuración legible por humanos" y "representación de ejecución".

### 9. El Mapping Engine debe producir el estado **completo** de lo que posee, no solo escribir lo activo

`GamepadState` es mutable y persiste entre iteraciones (ADR-005). Si el motor solo escribiera las salidas actualmente activas, una tecla que deja de estar pulsada nunca haría `A = false` — quedaría "pegada". Regla:

> En cada recomputación, el Mapping Engine escribe el estado **completo** de todos los outputs que posee (botones, sticks, triggers) — nunca solo una actualización parcial de lo que cambió — a menos que en el futuro se implemente explícitamente tracking incremental como una decisión propia.

Para V1, recomputar el estado completo es mucho más difícil de romper que un modelo de "solo escribir diffs".

### 10. Consecuencia suavizada: no toda extensión futura es gratis

La afirmación anterior ("añadir un nuevo control físico solo requiere un nuevo caso de `PhysicalInput`, no un nuevo motor") era demasiado absoluta — p. ej. la rueda del mouse es relativa/pulsátil, no pressed/released como una tecla, y puede necesitar semántica propia. Se reformula:

> Añadir un nuevo control físico no requiere un Mapping Engine paralelo; puede requerir ampliar `PhysicalInput` y, cuando su semántica lo exija, añadir un resolver/converter especializado detrás del mismo modelo de `Binding` — no necesariamente cero código nuevo, pero sí sin duplicar el motor de resolución.

### Arquitectura resultante

```text
InputEvent (Key/Button) ──► ActiveInputState ──┐
                                                 │
MouseDelta ──► MouseToStickConverter ───────────┤
                                                 ▼
                                        CompiledMapping
                                                 │
                    ┌────────────────────────────┼───────────────────────┐
                    ▼                            ▼                       ▼
             DigitalResolver               StickComposer         (delega en) MouseToStickConverter
        (DigitalButton, AnalogValue)   (StickDirection, OR+resta)      (StickVector)
                    │                            │                       │
                    └────────────────────────────┼───────────────────────┘
                                                 ▼
                                          GamepadState (completo)
                                                 │
                                                 ▼
                                    IVirtualGamepad.Submit(state)
```

El modelo conceptual sigue siendo uno solo: `PhysicalInput → Binding → VirtualOutput`. Lo especializado no es el Mapping Engine, sino los *resolvers* de determinados tipos de salida (`DigitalResolver`, `StickComposer`, `MouseToStickConverter`) que operan detrás del mismo modelo de bindings.

## Alternatives

- **WASD como caso hardcodeado**: descartado — contradice bindings "completamente configurables" y duplicaría la lógica de composición si se quisiera aplicar el mismo patrón a otro conjunto de teclas.
- **Tratar el mouse como un binding stateless más**: descartado — el conversor de mouse necesita estado persistente entre iteraciones (acumulación, decay), a diferencia de un botón digital.
- **Sumar vectores por binding sin colapsar direcciones lógicas primero** (punto 3): descartado — produce magnitudes incorrectas con bindings redundantes (`W` + `↑` ambos a `Left.Up`).
- **Permitir mezclar `StickDirection` y `StickVector` en el mismo stick sin una política definida**: descartado para V1 — se prefiere rechazar explícitamente en validación a inventar una regla de blending sin caso de uso real (punto 4).

## Consequences

- Un solo `Binding`/`VirtualOutput` resuelve WASD, triggers, botones digitales y el vector del mouse — sin un segundo motor paralelo.
- `NormalizeDiagonal` sigue siendo una opción del `StickComposer`, reutilizable para cualquier stick.
- La validación de perfiles (ADR-004) gana una regla nueva: rechazar un `StickVector` y uno o más `StickDirection` sobre el mismo stick.
- `MouseToStickConverter` expone `AddDelta`/`Update(now)`/`CurrentValue` como API propia, reflejando la separación input-dirigido/tiempo-dirigido ya establecida en ADR-002.
- Un cambio de perfil implica compilar un nuevo `CompiledMapping` y activarlo en la frontera de una iteración (ver ADR-005, transición de cambio de perfil).
- El Mapping Engine recomputa el estado completo de sus outputs en cada iteración relevante; no hay actualizaciones parciales en V1.

## Review history

- 2026-08-20 — Revisado con el usuario. Cambios: (1) añadida la forma `AnalogValue(GamepadAxis, value)` a `VirtualOutput` — corrige una inconsistencia real con el ejemplo de binding de triggers ya usado en ADR-004; (2) `AnalogAxis(Stick)` renombrado a `StickVector(Stick)` por precisión (el mouse produce un vector, no un eje); (3) definida semántica OR + resta para múltiples `StickDirection`/`DigitalButton` sobre la misma salida, evitando magnitudes incorrectas; (4) definida regla de conflicto (rechazo en validación) para un `StickVector` y `StickDirection` sobre el mismo stick; (5) "cada frame" sustituido por "cada iteración del consumidor de procesamiento", coherente con ADR-005; (6) formalizados dos caminos de señal (`ActiveInputState` para digitales, `MouseToStickConverter` para deltas); (7) `MouseToStickConverter` expone `AddDelta`/`Update(now)` separados, para que el decay no dependa de que llegue un nuevo `MouseDelta`; (8) introducido `CompiledMapping` como representación de ejecución compilada desde el perfil validado; (9) exigido que el Mapping Engine recompute el estado completo de sus outputs, evitando salidas "pegadas"; (10) suavizada la afirmación sobre el coste de añadir nuevos controles físicos.
