using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dungeons2
{
    public static class ImageWriter
    {
        public static void SaveToPng(BitmapSource image, string filePath)
        {
            // Encode the frame into a PNG file stream
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(image));

            using FileStream stream = new(filePath, FileMode.Create);
            encoder.Save(stream);
        }

        public static void SaveToPng(RawImageView image, string filePath)
        {
            // A uint is exactly 4 bytes. Stride is the width of a single row in bytes.
            int stride = image.Width * 4;

            // Create an unmanaged-to-managed WPF BitmapSource directly from the array
            var bitmap = BitmapSource.Create(
                image.Width,
                image.Height,
                96,     // standard DPI X
                96,     // standard DPI Y
                PixelFormats.Bgra32, // Maps exactly to standard 32-bit uint layout
                null,
                image.Pixels.ToArray(),
                stride);
            SaveToPng(bitmap, filePath);
        }

        public static void SaveCroppedToPng(RawImageView image, string filePath, int x, int y, int cropWidth, int cropHeight)
        {
            int stride = image.Width * 4;

            var fullBitmap = BitmapSource.Create(
                image.Width,
                image.Height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                image.Pixels.ToArray(),
                stride);

            var cropRect = new Int32Rect(x, y, cropWidth, cropHeight);
            var croppedBitmap = new CroppedBitmap(fullBitmap, cropRect);

            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));

            using FileStream stream = new(filePath, FileMode.Create);
            encoder.Save(stream);
        }
    }
}