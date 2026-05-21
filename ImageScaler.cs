namespace Dungeons2
{
    public static unsafe class ImageScaler
    {
        public static uint[] BilinearScale(uint[] src, int srcW, int srcH, int dstW, int dstH, float horizontalOffset = 0.0f)
        {
            uint[] dst = new uint[dstW * dstH];
            float xRatio = (float)srcW / dstW;
            float yRatio = (float)srcH / dstH;

            fixed (uint* pSrc = src)
            fixed (uint* pDst = dst)
            {
                for (int i = 0; i < dstH; i++)
                {
                    float srcY = (i + 0.5f) * yRatio - 0.5f;
                    int y = (int)MathF.Floor(srcY);
                    float yDiff = srcY - y;

                    y = Math.Max(0, y);
                    int y1 = Math.Min(y + 1, srcH - 1);

                    for (int j = 0; j < dstW; j++)
                    {
                        // Apply the horizontal sub-pixel or full-pixel offset directly to the sampling grid
                        float srcX = (j + 0.5f) * xRatio - 0.5f - horizontalOffset;
                        int x = (int)MathF.Floor(srcX);
                        float xDiff = srcX - x;

                        x = Math.Max(0, x);
                        int x1 = Math.Min(x + 1, srcW - 1);

                        uint a = pSrc[y * srcW + x];
                        uint b = pSrc[y * srcW + x1];
                        uint c = pSrc[y1 * srcW + x];
                        uint d = pSrc[y1 * srcW + x1];

                        float w1 = (1f - xDiff) * (1f - yDiff);
                        float w2 = xDiff * (1f - yDiff);
                        float w3 = (1f - xDiff) * yDiff;
                        float w4 = xDiff * yDiff;

                        uint blue = (uint)((a & 0xFF) * w1 + (b & 0xFF) * w2 + (c & 0xFF) * w3 + (d & 0xFF) * w4);
                        uint green = (uint)(((a >> 8) & 0xFF) * w1 + ((b >> 8) & 0xFF) * w2 + ((c >> 8) & 0xFF) * w3 + ((d >> 8) & 0xFF) * w4);
                        uint red = (uint)(((a >> 16) & 0xFF) * w1 + ((b >> 16) & 0xFF) * w2 + ((c >> 16) & 0xFF) * w3 + ((d >> 16) & 0xFF) * w4);
                        uint alpha = (uint)(((a >> 24) & 0xFF) * w1 + ((b >> 24) & 0xFF) * w2 + ((c >> 24) & 0xFF) * w3 + ((d >> 24) & 0xFF) * w4);

                        pDst[i * dstW + j] = (alpha << 24) | (red << 16) | (green << 8) | blue;
                    }
                }
            }
            return dst;
        }
    }
}