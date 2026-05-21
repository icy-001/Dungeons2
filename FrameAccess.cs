using Vortice.Direct3D11;

namespace Dungeons2
{
    public readonly ref struct FrameAccess(MappedSubresource map, ID3D11DeviceContext ctx, ID3D11Texture2D tex, bool ownTexture) : IDisposable
    {
        public readonly MappedSubresource Map = map;

        public int Width => (int)tex.Description.Width;
        public int Height => (int)tex.Description.Height;

        public void Dispose()
        {
            ctx.Unmap(tex, 0);
            if (ownTexture)
                tex.Dispose();
        }
    }
}
