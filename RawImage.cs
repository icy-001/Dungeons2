using System.Drawing;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    public readonly struct RawImage(uint[] pixels, int width, int height)
    {
        public readonly uint[] Pixels = pixels;
        public readonly int Width = width;
        public readonly int Height = height;

        public uint this[int x, int y] => Pixels[GetPixelIndex(x, y)];

        public static implicit operator RawImageView(RawImage img) => new(img.Pixels, img.Width, img.Height);

        public Color GetColor(int x, int y) => Color.FromArgb((int)this[x, y]);

        public int GetPixelIndex(int x, int y) => y * Width + x;

        public BitmapSource ToBitmapSource() => BitmapSource.Create(Width, Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, Pixels, Width * sizeof(uint));
    }
}