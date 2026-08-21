# ADR-005: Modelo de concurrencia y flujo de estado

**Status:** Accepted
**Last reviewed:** 2026-08-20

## Context

El brief (requisitos 7 y 25) exige baja latencia, cero bloqueos del hilo de input, y que se explique con precisión qué partes del sistema necesitan sincronización y por qué — sin introducir hilos "porque sí".

## Decision

**Principio central** (todo lo demás en este ADR se deriva de esto):

> El consumidor de procesamiento es el único propietario del estado mutable del controller (`GamepadState`, `MouseToStickConverter` y cualquier otro estado con memoria) y el único componente autorizado para modificar el estado lógico o enviar output al mando virtual (`IVirtualGamepad`).

```text
                                UI: botón Start/Stop, selector de perfil
                                              │
                                              ▼
                                        Control Queue
                                              │
                                              ▼
[Hilo de captura — OS thread]        [Consumidor de procesamiento]         [Hilo de UI]
Ventana message-only          ──►    único owner de:                       WPF Dispatcher
Parseo WM_INPUT → InputEvent          - GamepadState (mutable)             Volatile.Read(snapshot)
(incluye la combinación de            - MouseToStickConverter              a ~60 Hz
 emergencyStop — sin trato            - Mapping Engine
 especial en la captura)              - IVirtualGamepad.Submit(state)
Encola en Channel<T> (SPSC)  ──────►  reconoce emergencyStop como
                                       parte normal del drenado
                                       de input, no vía Control Queue
                                              │
                                     Volatile.Write(snapshot) ──► GamepadSnapshot inmutable
```

### 1. Puntos de cruce de hilos

- **Hilo de captura → consumidor de procesamiento**: `Channel<T>` de un solo productor/un solo consumidor. Es el principal punto explícito de encolamiento en el camino captura→procesamiento, y por tanto uno de los puntos que deberá medirse específicamente en Fase 8 — **no** el único candidato posible a cuello de botella de latencia: el Mapping Engine, `MouseToStickConverter`, la cadencia de salida y sobre todo `IVirtualGamepad.Submit()` + el backend/driver concreto (ADR-003 deja explícitamente pendiente de medición la latencia y el jitter del backend) también pueden introducirla.
- **Consumidor → hilo de UI**: el consumidor mantiene un `GamepadState` mutable de uso interno y, al publicar, construye un `GamepadSnapshot` inmutable expuesto mediante `Volatile.Write(ref _latestSnapshot, snapshot)`; la UI lee con `Volatile.Read(ref _latestSnapshot)`. Se elige `Volatile.Write`/`Volatile.Read` de forma explícita (no "`Volatile.Write` o `Interlocked.Exchange`" como alternativas intercambiables): es un único escritor, N lectores, objeto inmutable, y solo importa el snapshot más reciente ("último gana") — no se necesita el valor anterior que devolvería `Interlocked.Exchange`, así que no hay razón para pagar por esa semántica.
- **Control (UI) → consumidor**: comandos explícitos originados en la UI (start/stop, cambio de perfil) viajan por una `Control Queue` y se aplican al principio de una iteración del bucle de procesamiento.
- **`emergencyStop` no pasa por la Control Queue.** Es una combinación de teclas detectada por Raw Input exactamente igual que cualquier otra tecla — llega al consumidor de procesamiento por el mismo canal de input que WASD o cualquier binding, y el propio consumidor la reconoce como una acción de control durante el drenado normal de input (ver punto 7). Esto es deliberado: si el hotkey de emergencia dependiera de la Control Queue (y por tanto, indirectamente, de que la UI lo despache), un WPF congelado podría retrasar la única acción de seguridad que el requisito 19/20 del brief exige que nunca falle.

### 2. `Channel<T>` no promete ausencia de locks ni de asignaciones

Corrección de consistencia con ADR-002 (que ya lo dice correctamente): se retira la afirmación de que la elección de `Channel<T>` con `SingleReader`/`SingleWriter` garantiza "una estructura sin locks y con asignaciones mínimas". Esas propiedades permiten a la implementación optimizar internamente, pero no son una garantía contractual de lock-free ni de allocation-free. `Channel<T>` es la implementación **inicial** detrás de `IInputEventQueue`; su comportamiento real se mide en Fase 8, y si el profiling lo justifica se sustituye por una cola SPSC manual sin tocar el resto del sistema.

### 3. La espera del consumidor es una garantía de comportamiento, no una elección de primitiva

Se retira "espera con timeout corto sobre el canal" — contradecía la abstracción que ADR-002 deja deliberadamente abierta (`WaitUntilInputOrNextDeadline()`). En su lugar, una garantía de comportamiento:

```text
El consumidor debe despertar cuando:
1. hay input nuevo pendiente;
2. hay un comando de control pendiente en la Control Queue;
3. se alcanza el siguiente deadline temporal (decay, publicación de salida o de snapshot).
```

La primitiva concreta (espera acotada sobre la cola, `PeriodicTimer`, waitable timer de Win32, u otra) se decide con datos en Fase 8 — nunca `Thread.Sleep` de duración fija, eso sí es una decisión ya cerrada.

### 4. Ningún flujo continuo de input puede provocar starvation

Adición nueva, y probablemente la más importante de esta revisión: `DrainAvailableInput()` **no puede significar "hasta vaciar la cola" sin límite**. Con un mouse de alta frecuencia (varios miles de Hz), drenar sin límite podría mantener al consumidor ocupado el tiempo suficiente para retrasar indefinidamente el decay, los comandos de control, la publicación al backend y el snapshot de UI — exactamente lo que este ADR existe para evitar. Se introduce un *budget* de drenado (por número de eventos, por tiempo transcurrido, o ambos — el valor exacto se decide con profiling en Fase 8), sujeto a esta garantía no negociable:

> Un flujo continuo de input no puede impedir indefinidamente la ejecución del trabajo dirigido por tiempo ni de los comandos de control.

### 5. Cuatro cadencias independientes, no una sola

El diagrama no debe leerse como "un evento de input implica inmediatamente un `Submit()`". Hay cuatro frecuencias distintas, y confundirlas es un error de diseño real a evitar desde ahora:

```text
frecuencia de captura            — la que imponga el dispositivo físico (p. ej. mouse a 8000 Hz)
frecuencia de drenado/procesamiento — acotada por el budget del punto 4
frecuencia de IVirtualGamepad.Submit() — independiente de las dos anteriores
frecuencia de publicación de GamepadSnapshot hacia la UI — ~60 Hz, ya decidida
```

La política definitiva de cuándo llamar `Submit()` (por cambio de estado, por iteración, limitada a una frecuencia fija, o un híbrido) pertenece al consumidor de procesamiento y se determina mediante benchmark en Fase 8, considerando latencia, jitter, CPU, y las características del backend que resulte de `ADR-003A`. Esto deja la arquitectura preparada para cualquiera de esas políticas sin tener que volver a tocar el modelo de concurrencia.

### 6. Transición: cambio de perfil

```text
ProfileChange:
    neutralizar el estado virtual actual
        (evita mezclar botones/ejes que quedaron activos con el perfil anterior)
    resetear cualquier converter con estado (MouseToStickConverter, decay)
    sustituir el perfil activo
    reconstruir el estado lógico a partir de las teclas/botones físicos
        actualmente activos, cuando corresponda
```

Nunca debe existir un frame que mezcle configuración del perfil A y del perfil B. Ejecutado íntegramente por el consumidor de procesamiento, nunca a mitad de un ciclo de mapping (ver punto 1 de la versión original, mantenido).

### 7. Transición: Emergency Stop

```text
EmergencyStop:
    reset GamepadState
    reset MouseToStickConverter (y cualquier otro estado con memoria)
    IVirtualGamepad.Reset()
    detener el procesamiento de mappings hasta una reanudación explícita
```

Reconocido directamente por el consumidor de procesamiento a partir del flujo normal de `InputEvent` (ver punto 1) — no depende de la Control Queue ni, por tanto, del hilo de UI ni de que WPF responda.

## Alternatives

- **Un único hilo (UI) haciendo todo**: descartado directamente — cualquier operación de layout/render de WPF introduciría latencia y jitter directamente en el camino input→gamepad, violando el requisito de baja latencia.
- **Pool de hilos / `Task.Run` por evento**: descartado. Introduciría *scheduling overhead* variable y no habría garantía de orden de procesamiento de eventos de input, que sí importa (KeyDown antes que KeyUp del mismo frame, deltas de mouse en orden).
- **Locks (`lock`/`Monitor`) en el camino caliente**: descartados salvo que el profiling de la Fase 8 demuestre que son necesarios; el diseño actual no los requiere en ningún punto del *hot path*.
- **`emergencyStop` enrutado a través de la Control Queue/UI**: descartado explícitamente — dependería de que el hilo de UI esté vivo y respondiendo, justo lo contrario de lo que un mecanismo de emergencia necesita garantizar.

## Consequences

- Solo tres flujos de larga vida en la aplicación (captura, procesamiento, UI); uno de ellos, el de procesamiento, no está garantizado como hilo de sistema operativo físico en V1 (ver ADR-002). Ninguno usa `Thread.Sleep` de duración fija en su bucle principal.
- Cuatro cadencias (captura, drenado, `Submit()`, snapshot de UI) documentadas como independientes entre sí; ninguna requiere un nuevo punto de sincronización más allá de los ya descritos en el punto 1.
- Cualquier adición futura de un nuevo cruce de hilos debe justificarse explícitamente y documentarse aquí (o en un ADR nuevo), no añadirse de forma ad-hoc.

### Registro de decisiones

```text
YA DECIDIDO (arquitectura):
  Raw Input no bloquea el hilo de UI
  Un único "processing owner" para todo el estado mutable del controller
  GamepadState mutable solo existe dentro del consumidor de procesamiento
  La UI solo ve GamepadSnapshot inmutable, publicado con Volatile.Write/Read
  Solo el consumidor de procesamiento invoca IVirtualGamepad (ADR-003)
  emergencyStop se reconoce en el flujo normal de input, no vía Control Queue/UI
  Ningún input continuo puede provocar starvation de comandos ni de trabajo por tiempo
  Captura, drenado, Submit() y snapshot de UI son cadencias independientes
  Cambio de perfil y EmergencyStop son transiciones explícitas sin frame híbrido

DIFERIDO A FASE 8 (implementación/parámetros, no arquitectura):
  ¿Processing como Thread de SO físicamente dedicado, o consumidor lógico?
  Channel<T> vs. cola SPSC manual
  Tamaño exacto del budget de drenado de input
  Primitiva concreta de espera (cola con timeout, PeriodicTimer, waitable timer...)
  Cadencia/política exacta de IVirtualGamepad.Submit()
  Frecuencia óptima de comunicación con el backend elegido en ADR-003A
```

## Review history

- 2026-08-20 (1) — Sincronización mecánica tras revisión de ADR-002: "hilo de procesamiento" → "consumidor de procesamiento" (sin exigir `Thread` físico en V1); distinción `GamepadState` (mutable, interno)/`GamepadSnapshot` (inmutable, publicado).
- 2026-08-20 (2) — Sincronización mecánica tras revisión de ADR-003: `IVirtualGamepad.Update()` → `IVirtualGamepad.Submit(state)`.
- 2026-08-20 (3) — Revisión completa con el usuario; el ADR sale de `Draft`. Cambios: (a) retirada la afirmación de que el canal captura→procesamiento es el "único candidato" a cuello de botella; (b) retirada la garantía de "sin locks/asignaciones mínimas" de `Channel<T>`, alineado con ADR-002; (c) la espera del consumidor pasa de una primitiva concreta ("timeout corto sobre el canal") a una garantía de comportamiento, implementación diferida a Fase 8; (d) añadida la garantía de no-starvation con budget de drenado de input; (e) documentadas cuatro cadencias independientes (captura, drenado, `Submit()`, snapshot de UI); (f) separada la Control Queue (comandos de UI) del reconocimiento de `emergencyStop` (vía flujo normal de input, sin pasar por UI); (g) elegido `Volatile.Write`/`Volatile.Read` en vez de dejar `Interlocked.Exchange` como alternativa abierta; (h) añadidas las transiciones explícitas de cambio de perfil y de Emergency Stop; (i) destacado el principio central de "un único propietario del estado mutable" en `Decision`. Fuentes citadas por el usuario: `ChannelOptions.SingleWriter`, `Interlocked.Exchange`, `Volatile.Write` (Microsoft Learn).
