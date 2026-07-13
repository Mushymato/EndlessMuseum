using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace EndlessMuseum;

public sealed class ModConfig
{
    public Point BaseOrigin { get; set; } = new(25, 0);
    public Point DoorPosition { get; set; } = new(41, 13);
    public int MinRoomWidth { get; set; } = 0;
    public int MinSlotCount { get; set; } = 0;
    public bool? EnableGlass { get; set; } = null;
    internal bool EnableGlassValue
    {
        get => EnableGlass ?? field;
        set => field = value;
    } = false;

    /// <summary>Restore default config values</summary>
    private void Reset()
    {
        BaseOrigin = new(25, 0);
        DoorPosition = new(39, 16);
        MinRoomWidth = 0;
        EnableGlass = false;
    }

    internal void Register(IModHelper helper, IManifest mod)
    {
        Integration.IGenericModConfigMenuApi? GMCM = helper.ModRegistry.GetApi<Integration.IGenericModConfigMenuApi>(
            "spacechase0.GenericModConfigMenu"
        );
        if (GMCM == null)
        {
            helper.WriteConfig(this);
            return;
        }
        GMCM.Register(
            mod: mod,
            reset: () =>
            {
                Reset();
                helper.WriteConfig(this);
            },
            save: () =>
            {
                helper.WriteConfig(this);
                helper.GameContent.InvalidateCache(ModEntry.MAP_ARCHAEOLOGY_HOUSE);
            },
            titleScreenOnly: false
        );
        GMCM.AddTextOption(
            mod,
            () => EnableGlass.HasValue ? EnableGlass.Value.ToString() : "null",
            (value) =>
            {
                switch (value)
                {
                    case "null":
                        EnableGlass = null;
                        break;
                    case "true":
                        EnableGlass = true;
                        break;
                    case "false":
                        EnableGlass = false;
                        break;
                }
            },
            I18n.Config_EnableGlass_Name,
            I18n.Config_EnableGlass_Desc,
            ["null", "true", "false"],
            (value) => I18n.GetByKey($"config.EnableGlass.value.{value}")
        );
        GMCM.AddTextOption(
            mod,
            () => $"{DoorPosition.X} {DoorPosition.Y}",
            (value) =>
            {
                if (
                    ArgUtility.TryGetPoint(
                        ArgUtility.SplitBySpace(value),
                        0,
                        out Point pnt,
                        out string error,
                        "gmcm value DoorPosition"
                    )
                )
                    DoorPosition = pnt;
            },
            I18n.Config_DoorPosition_Name,
            I18n.Config_DoorPosition_Desc
        );
        GMCM.AddTextOption(
            mod,
            () => $"{BaseOrigin.X} {BaseOrigin.Y}",
            (value) =>
            {
                if (
                    ArgUtility.TryGetPoint(
                        ArgUtility.SplitBySpace(value),
                        0,
                        out Point pnt,
                        out string error,
                        "gmcm value BaseOrigin"
                    )
                )
                    BaseOrigin = pnt;
            },
            I18n.Config_BaseOrigin_Name,
            I18n.Config_BaseOrigin_Desc
        );
        GMCM.AddNumberOption(
            mod,
            () => MinRoomWidth,
            (value) => MinRoomWidth = value,
            I18n.Config_MinRoomWidth_Name,
            I18n.Config_MinRoomWidth_Desc,
            0,
            200,
            interval: 4
        );
        GMCM.AddNumberOption(
            mod,
            () => MinSlotCount,
            (value) => MinSlotCount = value,
            I18n.Config_MinSlotCount_Name,
            I18n.Config_MinSlotCount_Desc,
            0,
            200,
            interval: 4
        );
    }
}
