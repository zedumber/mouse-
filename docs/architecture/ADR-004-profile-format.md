# ADR-004: Formato y persistencia de perfiles

**Status:** Accepted with amendments
**Last reviewed:** 2026-08-20

## Context

Cada perfil (Default, Shooter, Racing, Custom...) debe guardar bindings, sensibilidad de mouse, curvas, deadzone, smoothing, decay, inversión de ejes, tipo de mando deseado y opciones adicionales (requisito 15). Debe poder crearse, cargarse, guardarse, renombrarse, duplicarse, borrarse, exportarse e importarse (requisito 16), con validación y soporte de versionado.

La dirección general de la versión anterior de este ADR (JSON, `System.Text.Json`, DTO → validación → dominio, migraciones explícitas) se mantiene. Los cambios importantes están en **cómo identificamos, dónde guardamos, qué validamos estrictamente, y qué NO debería vivir dentro de un perfil**.

## Decision

### 1. JSON + `System.Text.Json` — mantenido

Se mantiene JSON, un archivo por perfil, `System.Text.Json` (sin `Newtonsoft.Json`): `System.Text.Json` ya permite controlar explícitamente el manejo de propiedades desconocidas vía `JsonUnmappedMemberHandling`, y exigir miembros/parámetros de constructor requeridos durante la deserialización — cubre lo que necesitamos sin dependencia adicional.

### 2. `ProfileId` estable, separado del nombre visible

Corrección importante respecto a la versión anterior: usar el nombre visible del perfil como identidad física del archivo (`Profiles/Shooter.json`) acopla dos cosas que deben ser independientes. Problemas concretos: nombres reservados de Windows (`CON`, `AUX`...), caracteres inválidos, colisiones por mayúsculas/minúsculas, renombrar implicando renombrar archivos, y riesgo de path traversal si la validación de nombre falla en algún punto.

```json
{
  "version": 1,
  "id": "43bdf9bc-6963-4d48-b748-1d4f75a955fa",
  "name": "Shooter"
}
```

Almacenamiento interno por `ProfileId` (GUID), no por nombre:

```text
Profiles/
├── 43bdf9bc-6963-4d48-b748-1d4f75a955fa.json
└── ba690c39-b488-49c9-885d-3553425e10ba.json
```

Renombrar un perfil solo cambia el campo `name`; el archivo y el `ProfileId` permanecen estables. Al **exportar**, sí se genera un nombre de archivo amigable (p. ej. `Shooter.json`) porque en ese momento es un artefacto que el usuario maneja voluntariamente, no la identidad interna del sistema.

### 3. Ubicación de almacenamiento: `%LOCALAPPDATA%`, no relativo al ejecutable

Corrección: `./Profiles/` relativo al ejecutable falla si la app está instalada en `C:\Program Files\...` (sin permisos de escritura). Se usa `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`:

```text
%LOCALAPPDATA%
└── VirtualController
    ├── Profiles
    │   ├── <guid>.json
    │   └── <guid>.json
    ├── Settings
    │   └── settings.json
    └── Logs
```

Un futuro **modo portable** (guardar junto al ejecutable) queda anotado como opción posible, pero no es el comportamiento por defecto de V1.

### 4. El perfil describe el **tipo de mando deseado**, nunca el backend técnico

Cambio arquitectónico más importante de esta revisión, con impacto directo en ADR-003: `"gamepadBackend": "Xbox360"` mezclaba dos conceptos distintos — el tipo de controlador que el usuario quiere emular, y la tecnología concreta que lo produce hoy (ViGEm, HIDMaestro, un backend futuro). Si el perfil guardara el nombre del backend, quedaría acoplado a una decisión de infraestructura que ADR-003 ya trata como reemplazable.

```json
"output": { "controllerType": "Xbox360" }
```

El perfil dice *"quiero comportamiento de Xbox 360"*; la aplicación resuelve **qué backend concreto** lo produce mediante `ApplicationSettings.PreferredBackend` (ver punto 15) más un resolver (`IVirtualGamepadFactory` o equivalente, detallado en ADR-003) que decide entre `ViGEmXbox360Backend` / `HidMaestroXboxBackend` / un backend futuro. Consecuencia directa: si dentro de dos años se retira ViGEm por completo, los perfiles de usuario existentes **no necesitan migrarse** — siguen diciendo `Xbox360` y el nuevo backend simplemente lo implementa.

### 5. Validación estricta: los campos requeridos nunca reciben un default silencioso

Corrección respecto a la versión anterior ("una propiedad requerida ausente cae a su valor por defecto documentado"): eso es aceptable para opciones de comportamiento, pero no para campos cuya ausencia normalmente significa archivo corrupto o perfil inválido.

- **Requeridos** (`version`, `id`, `name`, `output.controllerType`): si faltan, el perfil **no se carga** — se lanza un error de validación explícito, nunca se asume un valor por defecto.
- **Opcionales con default documentado** (`normalizeDiagonal`, `smoothing`, `acceleration`, `invertX`/`invertY`, etc.): sí pueden faltar y caer a su valor por defecto, porque su ausencia es un caso de uso legítimo (perfil mínimo), no un indicio de corrupción.

### 6. Manejo explícito de versión futura: nunca "mejor esfuerzo"

Se definen tres casos, sin ambigüedad:

```text
version == actual   → validar y cargar
version <  actual   → migrar secuencialmente (ver punto 16)
version >  actual   → RECHAZAR explícitamente
```

Cargar un perfil de una versión futura como si fuera la actual (ignorando campos que no reconocemos) y luego **guardarlo** destruiría silenciosamente datos de esa versión futura. Un perfil con `version` mayor que la soportada produce un error explícito de tipo "versión no soportada, requiere una versión más reciente de la aplicación" — nunca se intenta adivinar el formato.

### 7. Miembros desconocidos: estrictos para la versión soportada, no silenciosos

Para la versión actual que sabemos interpretar, se configura `JsonUnmappedMemberHandling.Disallow` en el DTO en lugar de dejar el comportamiento por defecto de ignorar desconocidos. Razón: un typo como `"sensitivtyX"` en vez de `"sensitivityX"` no debe cargar en silencio con `sensitivityX` en su valor por defecto sin que el usuario se entere — debe fallar con un mensaje claro (`"Unknown property 'sensitivtyX'"`). Esto es distinto del caso del punto 6: una versión conocida con un campo desconocido es un error de validación; una versión futura es un error de "versión no soportada" — son dos rutas de error diferentes, no la misma.

### 8. Escritura atómica obligatoria

Corrección: nunca escribir directamente sobre el archivo destino (`File.WriteAllText(profilePath, json)`), porque un cierre de Windows, un proceso matado, un fallo de disco o un crash a mitad de escritura puede dejar un JSON truncado y destruir el perfil anterior. Flujo:

```text
serializar → validar el resultado serializado → escribir a <id>.tmp → flush
    → reemplazo atómico del destino (File.Replace, que permite conservar backup)
```

`Save()` debe ser crash-safe en la medida razonable. Se evalúa además mantener `<id>.bak` junto a `<id>.json` para poder recuperar automáticamente el último perfil válido si el reemplazo atómico llegara a fallar.

### 9. Import nunca escribe directamente en `Profiles/`

```text
usuario selecciona .json → leer → parsear → comprobar versión → migrar si aplica
    → validar → normalizar → generar un ProfileId NUEVO → guardado atómico
```

Nunca copiar el JSON externo tal cual dentro de la carpeta de perfiles — evita path traversal, IDs duplicados, sobrescritura de perfiles existentes, y perfiles con un formato que no hemos validado.

### 10. Export es portable; Import siempre genera un `ProfileId` nuevo

Al exportar, el `id` original puede conservarse como metadato informativo, pero **importar siempre genera un `ProfileId` local nuevo**, salvo que en el futuro se implemente explícitamente un modo "actualizar perfil existente" (no en V1) — así se evita cualquier colisión de identidad entre el perfil exportado y perfiles locales.

### 11. `IProfileRepository`/`ProfileService` devuelven errores de aplicación tipados

La UI nunca debe depender de `IOException`/`JsonException`/`UnauthorizedAccessException` que se filtran desde `Infrastructure`. Se traducen a errores de aplicación con significado propio: `ProfileNotFound`, `InvalidProfile`, `UnsupportedProfileVersion`, `ProfileNameConflict`, `StorageUnavailable`, `ImportFailed` — como excepciones tipadas propias o un resultado tipado; no se exige un framework `Result<T>` genérico para esto.

### 12. Separar persistencia pura (`IProfileRepository`) de casos de uso (`ProfileService`)

Corrección: el `IProfileRepository` original mezclaba operaciones de persistencia con casos de uso. `Duplicate` es realmente "cargar A, nuevo id, nuevo nombre, guardar B"; `Rename` es "cargar, cambiar una propiedad de dominio, guardar"; `Export`/`Import` implican más que almacenamiento (ver puntos 9–10).

```csharp
public interface IProfileRepository
{
    IReadOnlyList<ProfileMetadata> List();
    Profile Load(ProfileId id);
    void Save(Profile profile);
    void Delete(ProfileId id);
}
```

Por encima, `ProfileService` (en `Core`) implementa `Create`/`Rename`/`Duplicate`/`Import`/`Export` usando `IProfileRepository` como única dependencia de persistencia:

```text
UI → ProfileService → IProfileRepository → JsonProfileRepository
```

### 13. Modelo de perfil actualizado (boceto)

```json
{
  "version": 1,
  "id": "43bdf9bc-6963-4d48-b748-1d4f75a955fa",
  "name": "Shooter",
  "output": { "controllerType": "Xbox360" },
  "bindings": [
    { "input": "Keyboard.W", "output": "Stick.Left.Up" },
    { "input": "Keyboard.A", "output": "Stick.Left.Left" },
    { "input": "Mouse.LeftButton", "output": "Gamepad.RightTrigger" },
    { "input": "Keyboard.Space", "output": "Gamepad.A" }
  ],
  "wasd": { "normalizeDiagonal": true },
  "mouse": {
    "sensitivityX": 1.0,
    "sensitivityY": 1.0,
    "invertX": false,
    "invertY": false,
    "deadzone": { "inner": 0.05, "outer": 0.98 },
    "maximumOutput": 1.0,
    "responseCurve": { "type": "Linear" },
    "acceleration": { "enabled": false, "strength": 0.0 },
    "smoothing": { "enabled": false, "strength": 0.0 },
    "decay": { "strategy": "Exponential", "speed": 8.0 }
  },
  "hotkeys": {
    "startStop": "Ctrl+Alt+S",
    "toggleMouseCapture": "Ctrl+Alt+M"
  }
}
```

Nota: `deadzone`, `acceleration`, `smoothing` y `decay` se agrupan en sub-objetos en vez de campos planos (`deadzoneInner`, `decaySpeed`...) para poder evolucionar cada uno sin llenar `mouse` de propiedades sueltas. `emergencyStop` **ya no aparece aquí** — ver punto 14/15.

### 14. La parada de emergencia se protege en dominio, no solo en UI

De acuerdo con la protección ya prevista, pero se refuerza: si la validación de "no se puede borrar el emergency stop" solo vive en la UI de WPF, un usuario que edite el JSON a mano y deje `"emergencyStop": ""` la elude por completo. La construcción del objeto de dominio debe rechazar un emergency stop inválido o vacío con un error de validación propio, no solo impedir la acción desde los controles de la UI.

### 15. `ApplicationSettings` separado de `Profile`

Distinción nueva e importante: no todo lo que hoy vive "en el perfil" debería depender de qué perfil está activo. Si el emergency stop cambiara entre perfiles, el usuario no podría confiar en una combinación fija para "parar todo". Se introduce `ApplicationSettings` (persistido igual que un perfil — JSON, `%LOCALAPPDATA%\VirtualController\Settings\settings.json` — pero como configuración única global, no como una colección):

```text
ApplicationSettings                  Profile
├── selectedProfileId                ├── output.controllerType
├── emergencyStop                    ├── bindings
├── startMinimized                   ├── mouse (sensibilidad, curvas, deadzone...)
├── minimizeToTray                   ├── wasd.normalizeDiagonal
├── diagnosticsEnabled                └── hotkeys propios del perfil (startStop, toggleMouseCapture)
└── preferredBackend
```

`preferredBackend` en particular vive aquí, no en el perfil (ver punto 4): el perfil dice qué tipo de mando quiere, `ApplicationSettings` dice con qué backend técnico intentarlo primero.

### 16. Migraciones: siempre secuenciales y sobre la representación persistida, nunca sobre el dominio

Se mantiene la cadena secuencial (V1→V2→V3→...), nunca un salto directo V1→última versión. Las migraciones operan sobre JSON/DTO, no sobre el objeto de dominio — el dominio representa el estado *actual*, no formatos históricos:

```csharp
public interface IProfileMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    JsonNode Migrate(JsonNode source);
}
```

Cada migración necesita sus propios tests (round-trip, campos añadidos/renombrados, valores por defecto para campos nuevos).

## Alternatives

- **SQLite**: sobredimensionado para configuración de usuario de este tamaño; JSON es legible, versionable y fácil de exportar/compartir a mano.
- **Registro de Windows**: descartado, dificulta exportar/versionar perfiles y compartirlos entre máquinas.
- **YAML/TOML**: no aportan ventaja real sobre JSON aquí y añadirían una dependencia NuGet sin necesidad clara.
- **Nombre de perfil como identidad de archivo**: descartado — ver punto 2.
- **Carpeta relativa al ejecutable**: descartada — ver punto 3.
- **`gamepadBackend` en el perfil**: descartado — ver punto 4.

## Consequences

- `IProfileRepository` queda reducido a persistencia pura (`List`/`Load`/`Save`/`Delete`); los casos de uso (`Create`/`Rename`/`Duplicate`/`Import`/`Export`) viven en `ProfileService`, construido sobre el repositorio.
- Se introduce `ApplicationSettings` como modelo y persistencia nuevos (mismo patrón JSON que perfiles, pero singular), viviendo en `Core`/`Infrastructure` junto al resto de "Configuration" ya previsto en la arquitectura original — no requiere un proyecto nuevo.
- La resolución del backend concreto (ADR-003) ahora tiene una fuente de datos explícita: `ApplicationSettings.PreferredBackend` + `Profile.Output.ControllerType`, combinados por un resolver — nunca un nombre de backend embebido en el perfil.
- Toda escritura de perfil o de `ApplicationSettings` pasa por escritura atómica (`.tmp` + `File.Replace`), nunca sobrescritura directa.
- El emergency stop se valida también en la construcción del dominio, no solo en la UI, y vive en `ApplicationSettings`, no en `Profile`.
- Import genera siempre un `ProfileId` nuevo; nunca copia JSON externo directamente a la carpeta de perfiles.

## Review history

- 2026-08-20 — Revisado con el usuario. Cambios: (1) `ProfileId` estable separado del nombre visible; (2) almacenamiento movido de una carpeta relativa al ejecutable a `%LOCALAPPDATA%\VirtualController\`; (3) el perfil pasa a describir `output.controllerType` en vez de un backend técnico concreto (impacto directo en ADR-003, ver su nota de consecuencia); (4) validación estricta: campos requeridos rechazan el perfil si faltan, en vez de recibir un default silencioso; (5) manejo explícito de versión futura (rechazo, nunca "mejor esfuerzo"); (6) miembros desconocidos con `JsonUnmappedMemberHandling.Disallow` para la versión soportada; (7) escritura atómica obligatoria (`.tmp` + `File.Replace`, backup opcional); (8) Import nunca escribe directo a `Profiles/`, siempre genera un `ProfileId` nuevo; (9) errores de aplicación tipados en vez de excepciones de infraestructura filtrándose a la UI; (10) `IProfileRepository` reducido a persistencia pura, casos de uso movidos a `ProfileService`; (11) introducido `ApplicationSettings` separado de `Profile`, incluyendo mover `emergencyStop` y `preferredBackend` fuera del perfil; (12) reforzada la validación del emergency stop a nivel de dominio, no solo de UI. Fuentes citadas por el usuario: documentación de `System.Text.Json` sobre `JsonUnmappedMemberHandling` y miembros requeridos en deserialización, `Environment.GetFolderPath`, y `File.Replace`.
