using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Extensions;
using xTile;
using xTile.Layers;
using xTile.Tiles;

namespace EndlessMuseum;

internal sealed class MapNineSlice
{
    public const string MAP_SECTION = $"Maps/{ModEntry.ModId}_section";
    public const string MAP_PROPS = $"Maps/{ModEntry.ModId}_props";
    public const int MIN_ROW_TO_BREAK = 6;

    private readonly Rectangle SECTION_TL = new(0, 0, 3, 6);
    private readonly Rectangle SECTION_TC = new(3, 0, 2, 6);
    private readonly Rectangle SECTION_TR = new(5, 0, 3, 6);
    private readonly Rectangle SECTION_ML = new(0, 6, 3, 5);
    private readonly Rectangle SECTION_MC = new(3, 6, 2, 5);
    private readonly Rectangle SECTION_MR = new(5, 6, 3, 5);
    private readonly Rectangle SECTION_BL = new(0, 11, 3, 2);
    private readonly Rectangle SECTION_BC = new(3, 11, 2, 2);
    private readonly Rectangle SECTION_BR = new(5, 11, 3, 2);

    private readonly Rectangle SECTION_BREAK_T = new(8, 0, 4, 6);
    private readonly Rectangle SECTION_BREAK_M = new(8, 6, 4, 5);
    private readonly Rectangle SECTION_BREAK_B = new(8, 11, 4, 2);

    private readonly Rectangle PROP_DOOR = new(0, 0, 1, 2);
    private readonly Rectangle PROP_LATCH = new(0, 2, 1, 1);
    private readonly Rectangle PROP_LADDER = new(1, 0, 1, 3);

    public int GetMaxColInRow(int minWidth)
    {
        int maxColInRow = (int)MathF.Floor(((float)minWidth - SECTION_TL.Width - SECTION_TR.Width) / SECTION_TC.Width);

        if (maxColInRow < MIN_ROW_TO_BREAK)
            return maxColInRow;
        maxColInRow -= 2;
        return maxColInRow;
    }

    public void Patch(IAssetDataForMap data, Point origin, int minWidth, int rows, int cols, out int wallLength)
    {
        wallLength = 0;
        Map source = Game1.game1.xTileContent.Load<Map>(MAP_SECTION);
        data.ExtendMap(
            origin.X + minWidth,
            origin.Y + SECTION_TL.Height - 1 + rows * (SECTION_ML.Height - 1) + SECTION_BL.Height
        );
        // left col
        PatchSection(data, source, SECTION_TL, origin, 0, 0);
        for (int j = 0; j < rows; j++)
        {
            PatchSection(data, source, SECTION_ML, origin, 0, SECTION_TL.Height - 1 + j * (SECTION_ML.Height - 1));
        }
        PatchSection(data, source, SECTION_BL, origin, 0, SECTION_TL.Height - 1 + rows * (SECTION_ML.Height - 1));
        // mid cols
        int xBreak = 0;
        if (cols < MIN_ROW_TO_BREAK)
        {
            // one table case
            PatchCols(data, source, origin, rows, 0, cols, 0);
        }
        else
        {
            // three table case
            int oneThird = cols / 3;
            int remainder = cols % 3;
            xBreak = (SECTION_BREAK_T.Width - SECTION_MC.Width) * 2;
            switch (remainder)
            {
                case 0:
                    PatchCols(data, source, origin, rows, 0, oneThird - 1, 0);
                    PatchGap(data, source, origin, rows, oneThird - 1, 0);
                    PatchCols(data, source, origin, rows, oneThird - 1, oneThird, SECTION_BREAK_T.Width);
                    PatchGap(data, source, origin, rows, oneThird * 2 - 1, SECTION_BREAK_T.Width);
                    PatchCols(data, source, origin, rows, oneThird * 2 - 1, oneThird - 1, SECTION_BREAK_T.Width * 2);
                    break;
                case 1:
                    PatchCols(data, source, origin, rows, 0, oneThird, 0);
                    PatchGap(data, source, origin, rows, oneThird, 0);
                    PatchCols(data, source, origin, rows, oneThird, oneThird - 1, SECTION_BREAK_T.Width);
                    PatchGap(data, source, origin, rows, oneThird * 2 - 1, SECTION_BREAK_T.Width);
                    PatchCols(data, source, origin, rows, oneThird * 2 - 1, oneThird, SECTION_BREAK_T.Width * 2);
                    break;
                case 2:
                    PatchCols(data, source, origin, rows, 0, oneThird, 0);
                    PatchGap(data, source, origin, rows, oneThird, 0);
                    PatchCols(data, source, origin, rows, oneThird, oneThird, SECTION_BREAK_T.Width);
                    PatchGap(data, source, origin, rows, oneThird * 2, SECTION_BREAK_T.Width);
                    PatchCols(data, source, origin, rows, oneThird * 2, oneThird, SECTION_BREAK_T.Width * 2);
                    break;
            }
        }

        // right col
        int x2 = SECTION_TL.Width + cols * SECTION_MC.Width + xBreak;
        wallLength = x2 - SECTION_TL.Width;
        PatchSection(data, source, SECTION_TR, origin, x2, 0);
        for (int j = 0; j < rows; j++)
        {
            PatchSection(data, source, SECTION_MR, origin, x2, SECTION_TR.Height - 1 + j * (SECTION_MR.Height - 1));
        }
        PatchSection(data, source, SECTION_BR, origin, x2, SECTION_TR.Height - 1 + rows * (SECTION_MR.Height - 1));
    }

    private void PatchCols(IAssetDataForMap data, Map source, Point origin, int rows, int start, int count, int xOffset)
    {
        for (int i = start; i < start + count; i++)
        {
            int x = SECTION_TL.Width + i * SECTION_MC.Width + xOffset;
            PatchSection(data, source, SECTION_TC, origin, x, 0);
            for (int j = 0; j < rows; j++)
            {
                PatchSection(data, source, SECTION_MC, origin, x, SECTION_TC.Height - 1 + j * (SECTION_MC.Height - 1));
            }
            PatchSection(data, source, SECTION_BC, origin, x, SECTION_TC.Height - 1 + rows * (SECTION_MC.Height - 1));
        }
    }

    private void PatchGap(IAssetDataForMap data, Map source, Point origin, int rows, int start, int xOffset)
    {
        int x = SECTION_TL.Width + start * SECTION_MC.Width + xOffset;
        PatchSection(data, source, SECTION_BREAK_T, origin, x, 0);
        for (int j = 0; j < rows; j++)
        {
            PatchSection(
                data,
                source,
                SECTION_BREAK_M,
                origin,
                x,
                SECTION_BREAK_T.Height - 1 + j * (SECTION_BREAK_M.Height - 1)
            );
        }
        PatchSection(
            data,
            source,
            SECTION_BREAK_B,
            origin,
            x,
            SECTION_BREAK_T.Height - 1 + rows * (SECTION_BREAK_M.Height - 1)
        );
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

    internal void PatchDecor(IAssetDataForMap data, Point origin, Point doorPos, int wallLength)
    {
        // add doors and ladders
        Map props = Game1.game1.xTileContent.Load<Map>(MAP_PROPS);
        Point ladderPos = new(origin.X + 2, origin.Y + 3);
        Layer bldLayer = data.Data.RequireLayer("Buildings");
        if (bldLayer.Tiles[doorPos.X, doorPos.Y] == null)
        {
            data.PatchMap(
                props,
                PROP_LATCH,
                new(doorPos.X, doorPos.Y - PROP_LATCH.Height + 1, PROP_LATCH.Width, PROP_LATCH.Height),
                PatchMapMode.Overlay
            );
        }
        else
        {
            data.PatchMap(
                props,
                PROP_DOOR,
                new(doorPos.X, doorPos.Y - PROP_DOOR.Height + 1, PROP_DOOR.Width, PROP_DOOR.Height),
                PatchMapMode.Overlay
            );
        }
        data.PatchMap(
            props,
            PROP_LADDER,
            new(ladderPos.X, ladderPos.Y - PROP_LADDER.Height + 1, PROP_LADDER.Width, PROP_LADDER.Height),
            PatchMapMode.Overlay
        );
        // set warps
        TileSheet? untitledTilesheet = data.Data.RequireTileSheet("untitled tile sheet");
        (
            bldLayer.Tiles[doorPos.X, doorPos.Y] ??= new StaticTile(bldLayer, untitledTilesheet, BlendMode.Alpha, 48)
        ).Properties["Action"] = $"Warp {ladderPos.X} {ladderPos.Y + 1} ArchaeologyHouse";
        (
            bldLayer.Tiles[ladderPos.X, ladderPos.Y] ??= new StaticTile(
                bldLayer,
                untitledTilesheet,
                BlendMode.Alpha,
                48
            )
        ).Properties["Action"] = $"Warp {doorPos.X} {doorPos.Y + 1} ArchaeologyHouse";
        // add random decor
        int x = origin.X + SECTION_TL.Width;
        int y = origin.Y + 1;
        int wallEnd = x + wallLength - 2;
        int propCount = props.DisplayWidth / (2 * Game1.tileSize) - 1;
        while (x <= wallEnd)
        {
            if (Random.Shared.Next(3) == 0)
            {
                Rectangle decorSource = new(2 + 2 * Random.Shared.Next(propCount), 0, 2, 4);
                data.PatchMap(props, decorSource, new(x, y, 2, 4), PatchMapMode.Overlay);
                x += 2;
            }
            else
            {
                ++x;
            }
        }
    }
}
