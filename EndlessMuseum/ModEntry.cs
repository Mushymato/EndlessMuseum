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

    private const string MAP_ARCHAEOLOGY_HOUSE = "Maps/ArchaeologyHouse";
    private const string MAP_SECTION = $"Maps/{ModId}_section";
    private const string TILESHEET_GLASS = $"Maps/{ModId}/tiles_glass";

    private static readonly Rectangle SECTION_TL = new(0, 0, 3, 5);
    private static readonly Rectangle SECTION_TC = new(3, 0, 2, 5);
    private static readonly Rectangle SECTION_TR = new(5, 0, 3, 5);
    private static readonly Rectangle SECTION_ML = new(0, 5, 3, 5);
    private static readonly Rectangle SECTION_MC = new(3, 5, 2, 5);
    private static readonly Rectangle SECTION_MR = new(5, 5, 3, 5);
    private static readonly Rectangle SECTION_BL = new(0, 10, 3, 2);
    private static readonly Rectangle SECTION_BC = new(3, 10, 2, 2);
    private static readonly Rectangle SECTION_BR = new(5, 10, 3, 2);

    public override void Entry(IModHelper helper)
    {
        I18n.Init(helper.Translation);
        mon = Monitor;
        help = helper;
        config = help.ReadConfig<ModConfig>();
        config.EnableGlassValue =
            config.EnableGlass ?? help.ModRegistry.IsLoaded("FlashShifter.StardewValleyExpandedCP");

        help.Events.Content.AssetRequested += OnAssetRequested;
        help.Events.Content.AssetsInvalidated += OnAssetsInvalidated;
        help.Events.GameLoop.SaveLoaded += OnSaveLoaded;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(MAP_SECTION))
        {
            e.LoadFromModFile<Map>(
                config.EnableGlassValue ? "assets/section_glass.tmx" : "assets/section.tmx",
                AssetLoadPriority.Low
            );
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(MAP_ARCHAEOLOGY_HOUSE))
        {
            e.Edit(Edit_ArchaeologyHouse, AssetEditPriority.Late);
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(TILESHEET_GLASS))
        {
            e.LoadFromModFile<Texture2D>("assets/tiles_glass.png", AssetLoadPriority.Low);
        }
#if DEBUG
        else if (e.NameWithoutLocale.IsEquivalentTo("Data/Objects"))
        {
            e.Edit(
                (asset) =>
                {
                    IDictionary<string, ObjectData> dat = asset.AsDictionary<string, ObjectData>().Data;
                    ObjectData trilobite = dat["589"];
                    for (int i = 0; i < 100; i++)
                    {
                        ObjectData cloned = trilobite.ShallowClone();
                        cloned.Name = $"{ModId}_trilobite_{i}";
                        dat[cloned.Name] = cloned;
                    }
                },
                AssetEditPriority.Late
            );
        }
#endif
    }

    private void OnAssetsInvalidated(object? sender, AssetsInvalidatedEventArgs e)
    {
        if (e.NamesWithoutLocale.Any(name => name.IsEquivalentTo(MAP_SECTION)))
        {
            help.Events.GameLoop.UpdateTicked += OnUpdateTicked_InvalidateMapsArchaeologyHouse;
        }
    }

    private void OnUpdateTicked_InvalidateMapsArchaeologyHouse(object? sender, UpdateTickedEventArgs e)
    {
        help.GameContent.InvalidateCache(MAP_ARCHAEOLOGY_HOUSE);
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
        if (slots.Count >= LibraryMuseum.totalArtifacts)
        {
            Log(
                $"'{MAP_ARCHAEOLOGY_HOUSE}' has {slots.Count} existing donateable slots for {LibraryMuseum.totalArtifacts} total artifacts, enough to donate everything.",
                LogLevel.Info
            );
            return;
        }

        // need to do the patches
        int requiredSlots = LibraryMuseum.totalArtifacts - slots.Count;
        int maxColInRow = (int)
            MathF.Floor(
                (
                    (float)(target.DisplayWidth / Game1.tileSize - config.BaseOrigin.X)
                    - SECTION_TL.Width
                    - SECTION_TR.Width
                ) / SECTION_TC.Width
            );
        int maxSlotsInRow = maxColInRow * 4 + 4;
        int requiredRows = (int)MathF.Ceiling((float)requiredSlots / maxSlotsInRow);
        int requiredCols = (int)MathF.Ceiling(MathF.Ceiling((float)requiredSlots / requiredRows - 4) / 4f);

        Log(
            $"'{MAP_ARCHAEOLOGY_HOUSE}' has {slots.Count} existing donateable slots for {LibraryMuseum.totalArtifacts} total artifacts and needs {requiredSlots} more, will add {requiredRows * (requiredCols * 4 + 4)} ({requiredRows}x{requiredCols}).",
            LogLevel.Info
        );

        Point origin = new(config.BaseOrigin.X, config.BaseOrigin.Y + target.DisplayHeight / Game1.tileSize);
        PatchNineSlice(data, origin, requiredRows, requiredCols);

        // add door
        Layer? bld2Layer = target.GetLayer("Buildings2");
        if (bld2Layer == null)
        {
            bld2Layer = new Layer("Buildings2", bldLayer.Map, bldLayer.LayerSize, bldLayer.TileSize);
            bldLayer.Map.AddLayer(bld2Layer);
        }
        // to door
        bld2Layer.Tiles[39, 15] = new StaticTile(bld2Layer, untitledTilesheet, BlendMode.Alpha, 1389);
        bld2Layer.Tiles[39, 16] = new StaticTile(bld2Layer, untitledTilesheet, BlendMode.Alpha, 1421);
        bldLayer.Tiles[39, 16] ??= new StaticTile(bldLayer, untitledTilesheet, BlendMode.Alpha, 106);
        bldLayer.Tiles[39, 16].Properties["Action"] = $"Warp {origin.X + 1} {origin.Y + 4} ArchaeologyHouse";

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

    private static void PatchNineSlice(IAssetDataForMap data, Point origin, int rows, int cols)
    {
        Map source = Game1.game1.xTileContent.Load<Map>(MAP_SECTION);
        data.ExtendMap(0, origin.Y + SECTION_TL.Height - 1 + rows * (SECTION_ML.Height - 1) + SECTION_BL.Height);
        // left col
        PatchSection(data, source, SECTION_TL, origin, 0, 0);
        for (int j = 0; j < rows; j++)
        {
            PatchSection(data, source, SECTION_ML, origin, 0, SECTION_TL.Height - 1 + j * (SECTION_ML.Height - 1));
        }
        PatchSection(data, source, SECTION_BL, origin, 0, SECTION_TL.Height - 1 + rows * (SECTION_ML.Height - 1));
        // mid cols
        for (int i = 0; i < cols; i++)
        {
            int x = SECTION_TL.Width + i * SECTION_MC.Width;
            PatchSection(data, source, SECTION_TC, origin, x, 0);
            for (int j = 0; j < rows; j++)
            {
                PatchSection(data, source, SECTION_MC, origin, x, SECTION_TC.Height - 1 + j * (SECTION_MC.Height - 1));
            }
            PatchSection(data, source, SECTION_BC, origin, x, SECTION_TC.Height - 1 + rows * (SECTION_MC.Height - 1));
        }
        // right col
        int x2 = SECTION_TL.Width + cols * SECTION_MC.Width;
        PatchSection(data, source, SECTION_TR, origin, x2, 0);
        for (int j = 0; j < rows; j++)
        {
            PatchSection(data, source, SECTION_MR, origin, x2, SECTION_TR.Height - 1 + j * (SECTION_MR.Height - 1));
        }
        PatchSection(data, source, SECTION_BR, origin, x2, SECTION_TR.Height - 1 + rows * (SECTION_MR.Height - 1));
    }

    private static void PatchSection(IAssetDataForMap data, Map source, Rectangle section, Point origin, int x, int y)
    {
        data.PatchMap(
            source,
            section,
            new(origin.X + x, origin.Y + y, section.Width, section.Height),
            PatchMapMode.Overlay
        );
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
