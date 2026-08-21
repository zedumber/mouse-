namespace VirtualController.Core.Mapping;

public static class StickComposer
{
    public static (float X, float Y) Compose(IReadOnlySet<Direction> activeDirections, bool normalizeDiagonal)
    {
        float x = 0f;
        float y = 0f;

        if (activeDirections.Contains(Direction.Right)) x += 1f;
        if (activeDirections.Contains(Direction.Left)) x -= 1f;
        if (activeDirections.Contains(Direction.Up)) y += 1f;
        if (activeDirections.Contains(Direction.Down)) y -= 1f;

        if (normalizeDiagonal && x != 0f && y != 0f)
        {
            return Mouse.Vector2Math.WithMagnitude(x, y, Mouse.Vector2Math.Magnitude(x, y), 1f);
        }

        return (x, y);
    }
}
