using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    public static unsafe class SpriteLoader
    {
        public static RawImage LoadSprite(string resourcePath)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath)!;
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.StreamSource = stream ?? throw new FileNotFoundException();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            FormatConvertedBitmap convertedBitmap = new(bitmap, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int stride = convertedBitmap.PixelWidth * 4;
            uint[] fullBuffer = new uint[convertedBitmap.PixelWidth * convertedBitmap.PixelHeight];
            convertedBitmap.CopyPixels(fullBuffer, stride, 0);
            return new(fullBuffer, convertedBitmap.PixelWidth, convertedBitmap.PixelHeight);
        }

        public static uint[][] LoadSpriteSheet(string resourcePath, int rows, int cols, int spriteWidth, int spriteHeight)
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcePath)!;
            BitmapImage bitmap = new();
            bitmap.BeginInit();
            bitmap.StreamSource = stream ?? throw new FileNotFoundException();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            int stride = bitmap.PixelWidth * 4;
            byte[] fullBuffer = new byte[bitmap.PixelHeight * stride];
            bitmap.CopyPixels(fullBuffer, stride, 0);

            int totalSprites = rows * cols;
            uint[][] templates = new uint[totalSprites][];

            fixed (byte* pFull = fullBuffer)
            {
                uint* pPixels = (uint*)pFull;
                int atlasWidth = bitmap.PixelWidth;

                for (int i = 0; i < totalSprites; i++)
                {
                    int gridX = i % cols;
                    int gridY = i / cols;

                    int startX = gridX * spriteWidth;
                    int startY = gridY * spriteHeight;

                    templates[i] = new uint[spriteWidth * spriteHeight];
                    int pixelIndex = 0;

                    for (int y = 0; y < spriteHeight; y++)
                    {
                        int atlasY = startY + y;
                        for (int x = 0; x < spriteWidth; x++)
                        {
                            int atlasX = startX + x;
                            templates[i][pixelIndex] = pPixels[atlasY * atlasWidth + atlasX];
                            pixelIndex++;
                        }
                    }
                }
            }

            return templates;
        }
    }
}
