using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    public class AnnotatedMapRenderer
    {
        static Registry Registry => Registry.Instance;

        public static void DrawMap(int mapWidth, int mapHeight, Map map, RenderTargetBitmap target)
        {
            int offX = (mapWidth - 256) / 2, offY = (mapHeight - 256) / 2;
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, mapWidth, mapHeight));

                for (int i = 0; i < 8; ++i)
                    for (int j = 0; j < 8; ++j)
                    {
                        Room room = map.Rooms[i, j];

                        if (room.Doors != Directions.None)
                        {
                            int x = j * 32 + offX;
                            int y = i * 32 + offY;

                            dc.DrawImage(Registry.RoomBitmaps[(int)room.Doors], new Rect(x, y, 32, 32));
                            if (room.IsBase)
                                dc.DrawImage(Registry.BaseOverlayBitmap, new Rect(x, y, 32, 32));
                            else if (room.IsBoss)
                                dc.DrawImage(Registry.BossOverlayBitmap, new Rect(x, y, 32, 32));
                        }
                    }
            }

            target.Render(visual);
        }
    }
}
