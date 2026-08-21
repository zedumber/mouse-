namespace VirtualController.Core.Mapping;

public enum Key
{
    W,
    A,
    S,
    D,
    Space,
    Q,
    E,
    R,
    LeftShift,
    Escape,

    // Modificadores: necesarios para poder expresar combinaciones como la parada de emergencia
    // (Ctrl+Alt+Escape). Sin ellos, ApplicationSettings.EmergencyStop no era representable.
    LeftControl,
    LeftAlt,
}
