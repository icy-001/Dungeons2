namespace Dungeons2.Services
{
    public static class AutoCalibrationService
    {
        public static bool TryAutoDetect(out int mapX, out int mapY, out int hudX, out int hudY)
        {
            mapX = 0;
            mapY = 0;
            hudX = 0;
            hudY = 0;

            if (!GameCapture.Instance.IsActive)
                return false;

            using var frame = GameCapture.Instance.CaptureFullScreen();
            if (frame.Width == 0 || frame.Height == 0)
                return false;

            var fullImage = RawImageView.FromFrame(frame);
            var registry = Registry.Instance;

            if (!TryFindMarker(fullImage, registry.MapMarker, out int mapMarkerX, out int mapMarkerY))
                return false;
            if (!TryFindMarker(fullImage, registry.HudMarker, out int hudMarkerX, out int hudMarkerY))
                return false;

            int[] mapXCandidates = [mapMarkerX - 265, mapMarkerX - 137];
            foreach (int candidateMapX in mapXCandidates)
            {
                if (candidateMapX < 0 || candidateMapX + MapHudReader.MapWidth > fullImage.Width)
                    continue;

                if (!TryCreateCompositeImage(fullImage, candidateMapX, mapMarkerY, hudMarkerX, hudMarkerY, out var composite))
                    continue;

                MapHudReader reader = new(composite);
                if (reader.HasMap() && reader.HasHud())
                {
                    mapX = candidateMapX;
                    mapY = mapMarkerY;
                    hudX = hudMarkerX;
                    hudY = hudMarkerY;
                    return true;
                }
            }

            return false;
        }

        public static bool TryAutoDetectWinterface(out int winterfaceX, out int winterfaceY)
        {
            winterfaceX = 0;
            winterfaceY = 0;

            if (!GameCapture.Instance.IsActive)
                return false;

            using var frame = GameCapture.Instance.CaptureFullScreen();
            if (frame.Width == 0 || frame.Height == 0)
                return false;

            var fullImage = RawImageView.FromFrame(frame);
            if (!TryFindMarker(fullImage, Registry.Instance.WinterfaceMarker, out int markerX, out int markerY))
                return false;

            if (markerX + WinterfaceReader.WinterfaceWidth > frame.Width ||
                markerY + WinterfaceReader.WinterfaceHeight > frame.Height)
                return false;

            winterfaceX = markerX;
            winterfaceY = markerY;
            return true;
        }

        private static bool TryFindMarker(RawImageView image, RawImageView marker, out int x, out int y)
        {
            x = -1;
            y = -1;

            int anchorX = 0;
            int anchorY = 0;
            bool anchorFound = false;
            for (int markerY = 0; markerY < marker.Height && !anchorFound; markerY++)
                for (int markerX = 0; markerX < marker.Width; markerX++)
                    if ((marker[markerX, markerY] >> 24) == 255)
                    {
                        anchorX = markerX;
                        anchorY = markerY;
                        anchorFound = true;
                        break;
                    }

            if (!anchorFound)
                return false;

            uint anchorPixel = marker[anchorX, anchorY];

            for (int iy = 0; iy <= image.Height - marker.Height; iy++)
            {
                for (int ix = 0; ix <= image.Width - marker.Width; ix++)
                {
                    if (image[ix + anchorX, iy + anchorY] == anchorPixel && image.IsExactMatch(marker, ix, iy))
                    {
                        x = ix;
                        y = iy;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryCreateCompositeImage(RawImageView fullImage, int mapX, int mapY, int hudX, int hudY, out RawImageView composite)
        {
            int width = MapHudReader.MapWidth;
            int height = MapHudReader.MapHeight + 154;
            uint[] pixels = new uint[width * height];
            Array.Fill(pixels, 0u);

            if (mapX < 0 || mapY < 0 || mapX + MapHudReader.MapWidth > fullImage.Width || mapY + MapHudReader.MapHeight > fullImage.Height)
            {
                composite = default;
                return false;
            }

            if (hudX < 0 || hudY < 0 || hudX + 117 > fullImage.Width || hudY + 154 > fullImage.Height)
            {
                composite = default;
                return false;
            }

            for (int y = 0; y < MapHudReader.MapHeight; y++)
            {
                for (int x = 0; x < MapHudReader.MapWidth; x++)
                    pixels[y * width + x] = fullImage[x + mapX, y + mapY];
            }

            for (int y = 0; y < 154; y++)
            {
                for (int x = 0; x < 117; x++)
                    pixels[(MapHudReader.MapHeight + y) * width + x] = fullImage[x + hudX, y + hudY];
            }

            composite = new RawImageView(pixels, width, height);
            return true;
        }
    }
}
