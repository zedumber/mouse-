namespace VirtualController.Core.Mapping;

public sealed class ActiveInputState
{
    private readonly HashSet<PhysicalInput> _active = new();

    public IReadOnlySet<PhysicalInput> Active => _active;

    public void SetActive(PhysicalInput input, bool active)
    {
        if (active)
        {
            _active.Add(input);
        }
        else
        {
            _active.Remove(input);
        }
    }

    public bool IsActive(PhysicalInput input) => _active.Contains(input);

    /// <summary>
    /// Olvida todo lo pulsado. Necesario al cambiar de perfil o en una parada de emergencia: si no,
    /// una tecla que quedó activa seguiría produciendo salida bajo la configuración nueva.
    /// </summary>
    public void Clear() => _active.Clear();
}
