namespace VirtualController.Core.Gamepad;

/// <summary>
/// Vista inmutable del estado, creada solo al publicar hacia la UI (~60 Hz) — no en cada iteración del
/// hot path (ADR-002, punto 9 / ADR-005, punto 2). Al ser inmutable, la UI siempre ve un estado
/// completo y coherente, nunca uno a medio escribir.
/// </summary>
public sealed record GamepadSnapshot(
    GamepadState State,
    bool IsEmulating,
    int LastIterationInputEvents,
    long DroppedInputEvents)
{
    public static readonly GamepadSnapshot Neutral = new(GamepadState.Neutral, false, 0, 0);
}
