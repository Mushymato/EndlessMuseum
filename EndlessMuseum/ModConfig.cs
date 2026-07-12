using Microsoft.Xna.Framework;

namespace EndlessMuseum;

public sealed class ModConfig
{
    public Point BaseOrigin { get; set; } = new(25, 0);
    public bool? EnableGlass { get; set; } = null;
    internal bool EnableGlassValue { get; set; } = false;
}
