using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    public class AnnotatedMapRenderer
    {
        static Registry Registry => Registry.Instance;

        public static RenderTargetBitmap DrawMap(int mapWidth, int mapHeight, Map map)
        {
            int offX = (mapWidth - 256) / 2, offY = (mapHeight - 256) / 2;
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, mapWidth, mapHeight));

                for (int i = 0; i < 8; ++i)
                {
                    for (int j = 0; j < 8; ++j)
                    {
                        Room room = map.Rooms[i, j];

                        if (room.Doors != Directions.None)
                        {
                            int x = j * 32 + offX;
                            int y = i * 32 + offY;

                            dc.DrawImage(Registry.RoomBitmaps[(int)room.Doors], new Rect(x, y, 32, 32));

                            FormattedText tickText = new FormattedText(
                                "120",
                                CultureInfo.InvariantCulture,
                                FlowDirection.LeftToRight,
                                new Typeface("Consolas"),
                                10,
                                Brushes.Yellow,
                                1.0);

                            dc.DrawText(tickText, new Point(x + 2, y + 2));
                        }
                    }
                }
            }

            RenderTargetBitmap renderTarget = new RenderTargetBitmap(
                mapWidth, mapHeight, 96, 96, PixelFormats.Pbgra32);

            renderTarget.Render(visual);

            return renderTarget;
        }
    }
}