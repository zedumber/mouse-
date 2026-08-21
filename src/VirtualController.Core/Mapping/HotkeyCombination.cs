namespace VirtualController.Core.Mapping;

/// <summary>
/// Combinación de teclas que se considera pulsada cuando TODAS sus teclas están activas a la vez.
///
/// Existe porque la parada de emergencia debe reconocerse desde el flujo normal de input, sin pasar
/// por la UI (ADR-005, puntos 1 y 7): antes de esto, `ApplicationSettings.EmergencyStop` se validaba
/// pero no era representable como algo que el motor pudiera detectar.
/// </summary>
public sealed class HotkeyCombination
{
    private readonly Key[] _keys;

    public HotkeyCombination(IReadOnlyCollection<Key> keys)
    {
        if (keys.Count == 0)
        {
            throw new ArgumentException("Una combinación necesita al menos una tecla.", nameof(keys));
        }

        _keys = keys.Distinct().ToArray();
    }

    public IReadOnlyList<Key> Keys => _keys;

    public bool IsFullyPressed(ActiveInputState activeInputs)
    {
        foreach (var key in _keys)
        {
            if (!activeInputs.IsActive(PhysicalInput.FromKey(key)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parsea el formato textual usado en configuración ("Ctrl+Alt+Escape"). Devuelve false en vez de
    /// lanzar, porque el llamante necesita poder caer a un valor por defecto seguro: quedarse sin
    /// parada de emergencia por un texto mal escrito sería peor que ignorar el texto.
    /// </summary>
    public static bool TryParse(string? text, out HotkeyCombination combination)
    {
        combination = null!;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var keys = new List<Key>();

        foreach (var token in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryParseToken(token, out var key))
            {
                return false;
            }

            keys.Add(key);
        }

        if (keys.Count == 0)
        {
            return false;
        }

        combination = new HotkeyCombination(keys);
        return true;
    }

    private static bool TryParseToken(string token, out Key key)
    {
        switch (token.ToLowerInvariant())
        {
            case "ctrl" or "control" or "leftctrl" or "leftcontrol":
                key = Key.LeftControl;
                return true;
            case "alt" or "leftalt":
                key = Key.LeftAlt;
                return true;
            case "shift" or "leftshift":
                key = Key.LeftShift;
                return true;
            case "esc" or "escape":
                key = Key.Escape;
                return true;
            default:
                return Enum.TryParse(token, ignoreCase: true, out key);
        }
    }

    public override string ToString() => string.Join("+", _keys);
}
