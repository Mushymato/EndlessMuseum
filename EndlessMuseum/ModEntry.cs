using System.Diagnostics;
using Force.DeepCloner;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.GameData.Objects;
using StardewValley.Locations;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace EndlessMuseum;

public sealed class ModEntry : Mod
{
#if DEBUG
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Debug;
#else
    private const LogLevel DEFAULT_LOG_LEVEL = LogLevel.Trace;
#endif

    public const string ModId = "mushymato.EndlessMuseum";
    private static IMonitor mon = null!;
    internal static IModHelper help = null!;
    internal static ModConfig config = null!;

    public const string MAP_ARCHAEOLOGY_HOUSE = "Maps/ArchaeologyHouse";
    private readonly MapNineSlice nineSlice = new();

    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        mon = Monitor;
        help = helper;
        config = help.ReadConfig<ModConfig>();
        config.EnableGlassValue = help.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCP");

        help.Events.GameLoop.GameLaunched += OnGameLaunched;
        help.Events.Content.AssetRequested += OnAssetRequested;
        help.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        config.Register(Helper, ModManifest);
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(MAP_ARCHAEOLOGY_HOUSE))
        {
            e.Edit(Edit_ArchaeologyHouse, AssetEditPriority.Late);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(MapNineSlice.MAP_SECTION))
        {
            e.LoadFromModFile<Map>("assets/section.tmx", AssetLoadPriority.Low);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(MapNineSlice.MAP_SECTION_GLASS))
        {
            e.LoadFromModFile<Map>("assets/section_glass.tmx", AssetLoadPriority.Low);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(MapNineSlice.MAP_PROPS))
        {
            e.LoadFromModFile<Map>("assets/props.tmx", AssetLoadPriority.Low);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo($"Maps/{ModId}/tiles_glass"))
        {
            e.LoadFromModFile<Texture2D>("assets/tiles_glass.png", AssetLoadPriority.Low);
        }
        // #if DEBUG
        //         else if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
        //         {
        //             e.Edit(
        //                 (asset) =>
        //                 {
        //                     IDictionary<string, ObjectData> dat = asset.AsDictionary<string, ObjectData>().Data;
        //                     ObjectData trilobite = dat["589"];
        //                     for (int i = 0; i < 100; i++)
        //                     {
        //                         ObjectData cloned = trilobite.ShallowClone();
        //                         cloned.Name = $"{ModId}_trilobite_{i}";
        //                         dat[cloned.Name] = cloned;
        //                     }
        //                 },
        //                 AssetEditPriority.Late
        //             );
        //         }
        // #endif
    }

    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(MapNineSlice.MAP_SECTION)))
        {
            help.GameContent.InvalidateCache(MAP_ARCHAEOLOGY_HOUSE);
        }
    }

    private static void OnUpdateTicked_RepositionDonatedArtifactsIfNeeded(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady)
            return;
        RepositionDonatedArtifactsIfNeeded();
    }

    private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        RepositionDonatedArtifactsIfNeeded();
    }

    private void Edit_ArchaeologyHouse(IAssetData asset)
    {
        // count number of artifacts
        IAssetDataForMap data = asset.AsMap();

        Map target = data.Data;
        // search for valid tiles
        Layer? bldLayer = target.GetLayer("Buildings");
        TileSheet? untitledTilesheet = target.GetTileSheet("untitled tile sheet");
        if (bldLayer == null || untitledTilesheet == null)
        {
            Log(
                $"'{MAP_ARCHAEOLOGY_HOUSE}' is missing 'Buildings' layer or 'untitled tile sheet' tilesheet",
                LogLevel.Error
            );
            return;
        }
        HashSet<Vector2> slots = FindMuseumSlots(bldLayer, untitledTilesheet);
        int requiredSlots = Math.Max(LibraryMuseum.totalArtifacts - slots.Count, config.MinSlotCount);
        if (requiredSlots <= 0)
        {
            Log(
                $"'{MAP_ARCHAEOLOGY_HOUSE}' has {slots.Count} existing donateable slots for {LibraryMuseum.totalArtifacts} total artifacts, enough to donate everything.",
                LogLevel.Debug
            );
            return;
        }

        // need to do the patches
        int allowedWidth = Math.Max((target.DisplayWidth / Game1.tileSize) - config.BaseOrigin.X, config.MinRoomWidth);
        int maxColInRow = nineSlice.GetMaxColInRow(allowedWidth);
        int maxSlotsInRow = maxColInRow * 4 + 4;
        int requiredRows = (int)MathF.Ceiling((float)requiredSlots / maxSlotsInRow);
        int requiredCols = (int)MathF.Ceiling(MathF.Ceiling((float)requiredSlots / requiredRows - 4) / 4f);

        Log(
            $"'{MAP_ARCHAEOLOGY_HOUSE}' has {slots.Count} existing donateable slots for {LibraryMuseum.totalArtifacts} total artifacts and needs {requiredSlots} more, will add {requiredRows * (requiredCols * 4 + 4)} ({requiredRows}x{requiredCols}).",
            LogLevel.Debug
        );

        Point origin = new(config.BaseOrigin.X, config.BaseOrigin.Y + target.DisplayHeight / Game1.tileSize);
        nineSlice.Patch(
            data,
            origin,
            config.EnableGlassValue,
            config.MinRoomWidth,
            requiredRows,
            requiredCols,
            out int wallLength
        );

        nineSlice.PatchDecor(data, origin, config.DoorPosition, wallLength);

        if (Context.IsMainPlayer)
            help.Events.GameLoop.UpdateTicked += OnUpdateTicked_RepositionDonatedArtifactsIfNeeded;
    }

    private static HashSet<Vector2> FindMuseumSlots(Layer bldLayer, TileSheet untitledTilesheet)
    {
        HashSet<Vector2> slots = [];
        for (int x = 0; x < bldLayer.LayerWidth; x++)
        {
            for (int y = 0; y < bldLayer.LayerHeight; y++)
            {
                Tile tile = bldLayer.Tiles[x, y];
                if (
                    tile?.TileSheet == untitledTilesheet
                    && ((uint)(tile.TileIndex - 1072) <= 2u || (uint)(tile.TileIndex - 1237) <= 1u)
                )
                    slots.Add(new(x, y));
            }
        }
        return slots;
    }

    private static void RepositionDonatedArtifactsIfNeeded()
    {
        help.Events.GameLoop.UpdateTicked -= OnUpdateTicked_RepositionDonatedArtifactsIfNeeded;
        LibraryMuseum libraryMuseum = Game1.RequireLocation<LibraryMuseum>("ArchaeologyHouse");
        Layer? bldLayer = libraryMuseum.Map.RequireLayer("Buildings");
        TileSheet? untitledTilesheet = libraryMuseum.Map.RequireTileSheet("untitled tile sheet");
        HashSet<Vector2> slots = FindMuseumSlots(bldLayer, untitledTilesheet);
        IEnumerator<Vector2> freeSlots = slots.Except(Game1.netWorldState.Value.MuseumPieces.Keys).GetEnumerator();
        List<(Vector2, Vector2, string)> modify = [];
        foreach ((Vector2 pos, string value) in Game1.netWorldState.Value.MuseumPieces.Pairs)
        {
            if (!slots.Contains(pos))
            {
                if (!freeSlots.MoveNext())
                    break;
                modify.Add((pos, freeSlots.Current, value));
            }
        }
        foreach ((Vector2 pos, Vector2 moved, string value) in modify)
        {
            Log($"Reposition donated artifact: '{value}' {pos} -> {moved}");
            Game1.netWorldState.Value.MuseumPieces[moved] = value;
            Game1.netWorldState.Value.MuseumPieces.Remove(pos);
        }
    }

    /// <summary>SMAPI static monitor Log wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void Log(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon.Log(msg, level);
    }

    /// <summary>SMAPI static monitor LogOnce wrapper</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    internal static void LogOnce(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon.LogOnce(msg, level);
    }

    /// <summary>SMAPI static monitor Log wrapper, debug only</summary>
    /// <param name="msg"></param>
    /// <param name="level"></param>
    [Conditional("DEBUG")]
    internal static void LogDebug(string msg, LogLevel level = DEFAULT_LOG_LEVEL)
    {
        mon.Log(msg, level);
    }
}
