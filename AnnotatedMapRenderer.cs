using System.Globalization;
using System.Windows;
using System.Windows.Media;


namespace Dungeons2
{
    public class AnnotatedMapRenderer
    {
        static Registry Registry => Registry.Instance;

        public static void DrawMap(DrawingContext dc, int offX, int offY, Map map)
        {
            byte[,] unopenedGrid = map.ComputeUnopenedGrid();
            bool[,] criticalGrid = map.ComputeCriticalGrid();

            for (int i = 0; i < 8; ++i)
                for (int j = 0; j < 8; ++j)
                {
                    int x = j * 32 + offX;
                    int y = i * 32 + offY;

                    var room = map[i, j];
                    byte unopenedDir = unopenedGrid[i, j];

                    if (room.Opened)
                    {
                        dc.DrawImage(criticalGrid[i, j] ? Registry.CritRoomBitmaps[(int)room.Doors] : Registry.RoomBitmaps[(int)room.Doors], new(x, y, 32, 32));
                        if (room.IsBase)
                            dc.DrawImage(Registry.BaseOverlayBitmap, new(x, y, 32, 32));
                        else if (room.IsBoss)
                            dc.DrawImage(Registry.BossOverlayBitmap, new(x, y, 32, 32));
                    }
                    else if (unopenedDir != 0)
                    {
                        dc.DrawImage(room.Lock == -1 && room.SkillDoor == -1 ? Registry.MysteryBitmaps[unopenedDir - 1] : Registry.UnopenedBitmaps[unopenedDir - 1], new(x, y, 32, 32));
                    }
                    if (room.Lock != -1)
                    {
                        var bmp = Registry.GetMapKey(room.Lock).ToBitmapSource();
                        dc.DrawImage(bmp, new(x + MapHudReader.MapKeyOffsetX, y + MapHudReader.MapKeyOffsetY, bmp.Width, bmp.Height));
                    }
                    else if (room.SkillDoor != -1 && !room.Opened)
                    {
                        var bmp = Registry.GetSkillIcon(room.SkillDoor).ToBitmapSource();
                        dc.DrawImage(bmp, new(x + MapHudReader.SkillOffsetX, y + MapHudReader.SkillOffsetY, bmp.Width, bmp.Height));
                    }
                    if (room.Key != -1)
                    {
                        var bmp = Registry.GetKeybagKey(room.Key).ToBitmapSource();
                        dc.DrawImage(bmp, new(x + 14, y + 1, bmp.Width, bmp.Height));
                    }
                }
            for (int i = 0; i < 5; ++i)
                if (map.PlayerLocations[i] is Coord p)
                {
                    int x = p.Col * 32 + offX;
                    int y = p.Row * 32 + offY;
                    var bmp = Registry.PlayerDotBitmaps[i];
                    dc.DrawImage(bmp, new(x + Registry.PlayerDotOffsets[i].X, y + Registry.PlayerDotOffsets[i].Y, bmp.Width, bmp.Height));
                }
        }

        public static void DrawRoomTicks(DrawingContext dc, int offX, int offY, IEnumerable<RoomSplit> roomTicks)
        {
            foreach (var (r, c, ticks) in roomTicks)
            {
                if (r < 0 || c < 0 || ticks <= 0)
                    continue;

                int x = c * 32 + offX;
                int y = r * 32 + offY;

                var brush = Registry.GetTicksBrush(ticks);
                var text = new FormattedText(
                    ticks.ToString(),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Cascadia Code"),
                    10,
                    brush,
                    1.0);

                dc.DrawText(text, new Point(x + 4, y + 18));
            }
        }
    }
}