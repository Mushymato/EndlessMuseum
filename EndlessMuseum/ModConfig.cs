using Microsoft.Xna.Framework;

namespace EndlessMuseum;

public sealed class ModConfig
{
    public Point BaseOrigin { get; set; } = new(25, 0);
    public Point DoorPosition { get; set; } = new(39, 16);
    public int MinRoomWidth { get; set; } = 0;
    public bool? EnableGlass { get; set; } = null;
    internal bool EnableGlassValue { get; set; } = false;
}
