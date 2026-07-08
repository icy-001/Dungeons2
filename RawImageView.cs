using System.Drawing;
using System.Runtime.InteropServices;

namespace Dungeons2
{
    public readonly ref struct RawImageView(Span<uint> pixels, int width, int height)
    {
        public readonly Span<uint> Pixels = pixels;
        public readonly int Width = width;
        public readonly int Height = height;
        public uint this[int x, int y] => Pixels[GetPixelIndex(x, y)];

        public Color GetColor(int x, int y) => Color.FromArgb((int)this[x, y]);

        public int GetPixelIndex(int x, int y) => y * Width + x;

        public double MatchDistance(RawImageView template, int start_x, int start_y, double threshold = double.MaxValue)
        {
            static int sq(int x) => x * x;

            double totalThreshold = threshold * template.Width * template.Height;

            if (start_x < 0 || start_y < 0 || start_x + template.Width > Width || start_y + template.Height > Height)
            {
                return threshold;
            }

            int total = 0;
            int count = 0;

            for (int ty = 0; ty < template.Height; ty++)
            {
                int imgOffset = GetPixelIndex(start_x, start_y + ty);
                int tmplOffset = template.GetPixelIndex(0, ty);

                var imgRow = Pixels.Slice(imgOffset, template.Width);
                var tmplRow = template.Pixels.Slice(tmplOffset, template.Width);

                var imgBytes = MemoryMarshal.Cast<uint, byte>(imgRow);
                var tmplBytes = MemoryMarshal.Cast<uint, byte>(tmplRow);
                for (int i = 0; i < tmplBytes.Length; i += 4)
                {
                    if (tmplBytes[i + 3] == 255)
                    {
                        ++count;
                        total += sq(imgBytes[i] - tmplBytes[i])
                                  + sq(imgBytes[i + 1] - tmplBytes[i + 1])
                                  + sq(imgBytes[i + 2] - tmplBytes[i + 2]);
                        if (total >= totalThreshold)
                            return threshold;
                    }
                }
            }

            return (double)total / count;
        }

        public bool IsExactMatch(RawImageView template, int start_x, int start_y)
        {
            if (start_x < 0 || start_y < 0 || start_x + template.Width > Width || start_y + template.Height > Height)
                return false;

            for (int ty = 0; ty < template.Height; ty++)
            {
                int imgOffset = GetPixelIndex(start_x, start_y + ty);
                int tmplOffset = template.GetPixelIndex(0, ty);

                var imgRow = Pixels.Slice(imgOffset, template.Width);
                var tmplRow = template.Pixels.Slice(tmplOffset, template.Width);

                for (int i = 0; i < tmplRow.Length; i++)
                {
                    uint tmplPixel = tmplRow[i];
                    if ((tmplPixel >> 24) == 255 && imgRow[i] != tmplPixel)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns true if the template matches the image at the corresponding offset; otherwise false.
        /// Transparent pixels in the template (alpha < 255) are required to be a different color in order for it to match. 
        /// </summary>
        public bool IsMatchAlphaColor(RawImageView template, int start_x, int start_y, Color color)
        {
            if (start_x < 0 || start_y < 0 || start_x + template.Width > Width || start_y + template.Height > Height)
                return false;

            for (int y = 0; y < template.Height; y++)
                for (int x = 0; x < template.Width; x++)
                    if ((template.GetColor(x, y).A == 255 && GetColor(x + start_x, y + start_y) != color) ||
                        (template.GetColor(x, y).A != 255 && GetColor(x + start_x, y + start_y) == color))
                        return false;
            return true;
        }

        public static RawImageView FromFrame(FrameAccess frame) => new(frame.Map.AsSpan<uint>((int)frame.Map.DepthPitch / 4), (int)(frame.Map.RowPitch / 4), frame.Height);
    }
}
