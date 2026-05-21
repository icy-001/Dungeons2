using System.Drawing;

namespace Dungeons2
{
    public readonly struct RawImage(uint[] pixels, int width, int height)
    {
        public readonly uint[] Pixels = pixels;
        public readonly int Width = width;
        public readonly int Height = height;

        public uint this[int x, int y] => Pixels[GetPixelIndex(x, y)];

        public static implicit operator RawImageView(RawImage img)
        {
            return new(img.Pixels, img.Width, img.Height);
        }

        public Color GetColor(int x, int y)
        {
            return Color.FromArgb((int)this[x, y]);
        }

        public int GetPixelIndex(int x, int y)
        {
            return y * Width + x;
        }
    }
}