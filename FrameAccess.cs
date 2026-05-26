using Vortice.Direct3D11;

namespace Dungeons2
{
    public readonly ref struct FrameAccess(MappedSubresource map, ID3D11DeviceContext ctx, ID3D11Texture2D? tex, bool ownTexture) : IDisposable
    {
        public readonly MappedSubresource Map = map;

        public int Width => tex is null ? 0 : (int)tex.Description.Width;
        public int Height => tex is null ? 0 : (int)tex.Description.Height;
        
        public void Dispose()
        {
            if (tex is null)
                return;

            ctx.Unmap(tex, 0);
            if (ownTexture)
                tex.Dispose();
        }
    }
}
