namespace VirtualController.Core.Mapping;

public enum Key
{
    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,
    Space,
    Escape,
    LeftShift,

    // Modificadores: necesarios para poder expresar combinaciones como la parada de emergencia
    // (Ctrl+Alt+Escape). Sin ellos, ApplicationSettings.EmergencyStop no era representable.
    LeftControl,
    LeftAlt,
}
