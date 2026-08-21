# Virtual Controller Mouse

Aplicación de Windows que convierte teclado y mouse en la entrada de un mando Xbox 360 virtual.
Está pensada para ofrecer una conversión mouse → stick configurable, predecible y con baja latencia.

> Estado: desarrollo activo. La arquitectura central, Raw Input, perfiles, editor de bindings,
> ajustes de mouse y visualizador están implementados. La distribución pública todavía requiere
> resolver firma de código, instalación del driver y validación en más equipos.

## Funciones

- Captura global de teclado y mouse mediante Windows Raw Input.
- Emulación de mando Xbox 360 a través de un backend desacoplado.
- Editor visual de bindings con validación de conflictos.
- Selector y gestión de perfiles por juego.
- Hotkeys globales configurables e icono de bandeja.
- Perfiles JSON versionados y escritura atómica.
- Visualizador de sticks, gatillos, botones y métricas de latencia/saturación/jitter en tiempo real.
- Asistente de calibración por juego y logging JSON persistente con retención de 14 días.
- Parada de emergencia desde el flujo de entrada.
- Conversión avanzada de mouse a stick:
  - sensibilidad independiente X/Y;
  - curvas Linear, Power, Exponential, Custom y Dual Zone;
  - deadzone radial y compensación de la deadzone del juego;
  - escala y límite de salida independientes;
  - aceleración basada en velocidad real;
  - smoothing fijo o adaptativo;
  - retorno al centro configurable con retención breve;
  - modo ADS activado por el gatillo izquierdo virtual;
  - presets Equilibrado, Precisión y Respuesta rápida.

La aplicación no detecta enemigos ni mueve la mira de forma automática. Produce una señal de stick
que puede interactuar con las opciones de mando y asistencia nativas de cada juego.

## Requisitos de desarrollo

- Windows 10 u 11 de 64 bits.
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Un backend de mando virtual compatible. Actualmente existe una implementación para ViGEmBus;
  ViGEm está descontinuado y se mantiene como backend legacy mientras se evalúa su sustitución.

## Compilar y probar

```powershell
dotnet restore .\VirtualController.slnx
dotnet build .\VirtualController.slnx --no-restore
dotnet test .\VirtualController.slnx --no-build --no-restore
```

Smart App Control puede bloquear DLL locales sin firma con el error `0x800711C7`. No se recomienda
desactivar esa protección: la solución para una release es firmar los binarios con un certificado de
confianza.

## Ejecutar

```powershell
dotnet run --project .\src\VirtualController.App\VirtualController.App.csproj
```

Los perfiles y ajustes se guardan en:

```text
%LOCALAPPDATA%\VirtualController\
```

## Ajuste inicial de puntería

1. Crea un perfil para el juego.
2. Empieza con el preset **Equilibrado**.
3. Ajusta `Anti-deadzone juego` hasta que un movimiento pequeño produzca respuesta sin deriva.
4. Ajusta `Recorrido completo` y sensibilidad X/Y.
5. Configura ADS y comprueba que el gatillo izquierdo virtual corresponde a apuntar.
6. Reduce smoothing si notas latencia; aumenta su respuesta adaptativa si los giros se sienten lentos.

También puedes usar el **Asistente de calibración** integrado: aplica Equilibrado, guía los ajustes
de anti-deadzone/normal/ADS y reinicia la muestra de diagnóstico antes de guardar el perfil.

Cada juego aplica sus propias deadzones, curvas y velocidad máxima. Por eso se recomienda un perfil
independiente por juego en lugar de un ajuste universal.

## Estructura

```text
src/
  VirtualController.Core/             Dominio, mapping y motor de mouse
  VirtualController.Infrastructure/   Persistencia JSON
  VirtualController.Windows/          Raw Input e interoperabilidad Win32
  VirtualController.VirtualGamepad/   Backend de mando virtual
  VirtualController.App/              Aplicación WPF
tests/                                 Pruebas unitarias y de integración
docs/architecture/                     Decisiones de arquitectura (ADR)
```

Consulta [PROJECT_STATUS.md](PROJECT_STATUS.md) para el estado detallado, riesgos y verificaciones.
La decisión de migración y firma está en
[ADR-003A](docs/architecture/ADR-003A-backend-and-signing-strategy.md).

## Uso responsable

Respeta las condiciones de servicio del juego y de su plataforma. Este proyecto se limita a remapear
entrada local a un mando virtual; no incluye lectura de pantalla, selección de objetivos ni automatización
de disparos.
