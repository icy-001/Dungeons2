using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.UI;
using WinRT;

namespace Dungeons2
{
    [GeneratedComInterface, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    partial interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface(in Guid iid);
    }

    public class GameCapture
    {
        private readonly ID3D11Device device;
        private readonly ID3D11DeviceContext context;
        private readonly IDirect3DDevice winrtDevice;

        private GraphicsCaptureSession? session;
        private Direct3D11CaptureFramePool? framePool;
        private readonly Lock frameLock = new();
        private Direct3D11CaptureFrame? currentFrame;

        private readonly ID3D11Texture2D stagingTexture;
        private readonly ID3D11Texture2D winterfaceTexture;

        public GameCapture()
        {
            _ = D3D11.D3D11CreateDevice(null, Vortice.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport, [], out device, out context);

            using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
            _ = Interop.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pUnknown);
            winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(pUnknown);
            _ = Marshal.Release(pUnknown);

            stagingTexture = device.CreateTexture2D(new()
            {
                Width = MapKeybagReader.MapWidth,
                Height = MapKeybagReader.MapHeight + 102,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read
            });

            winterfaceTexture = device.CreateTexture2D(new()
            {
                Width = WinterfaceReader.WinterfaceWidth,
                Height = WinterfaceReader.WinterfaceHeight,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Staging,
                BindFlags = BindFlags.None,
                CPUAccessFlags = CpuAccessFlags.Read
            });
        }

        public bool IsActive => currentFrame != null;

        public void SetTarget(WindowId windowId)
        {
            var item = GraphicsCaptureItem.TryCreateFromWindowId(windowId);
            if (item == null) return;

            lock (frameLock)
            {
                session?.Dispose();
                framePool?.Dispose();
                currentFrame?.Dispose();
                currentFrame = null;

                framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    winrtDevice,
                    Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    item.Size
                );

                session = framePool.CreateCaptureSession(item);
                session.IsCursorCaptureEnabled = false;
                session.IsBorderRequired = false;

                framePool.FrameArrived += (sender, args) =>
                {
                    var frame = sender.TryGetNextFrame();
                    if (frame == null) return;

                    lock (frameLock)
                    {
                        currentFrame?.Dispose();
                        currentFrame = frame;
                    }
                };

                session.StartCapture();
            }
        }

        public FrameAccess CaptureFullScreen()
        {
            lock (frameLock)
            {
                if (currentFrame == null)
                    return new();

                using ID3D11Texture2D1 frameTexture = new(currentFrame.Surface.As<IDirect3DDxgiInterfaceAccess>().GetInterface(typeof(ID3D11Texture2D1).GUID));
                var localStaging = device.CreateTexture2D(new()
                {
                    Width = frameTexture.Description.Width,
                    Height = frameTexture.Description.Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = frameTexture.Description.Format,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CPUAccessFlags = CpuAccessFlags.Read
                });

                context.CopyResource(localStaging, frameTexture);

                var map = context.Map(localStaging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                return new(map, context, localStaging, true);
            }
        }

        public FrameAccess CaptureMapKeybag()
        {
            lock (frameLock)
            {
                if (currentFrame == null)
                    return new();

                using ID3D11Texture2D1 frameTexture = new(currentFrame.Surface.As<IDirect3DDxgiInterfaceAccess>().GetInterface(typeof(ID3D11Texture2D1).GUID));

                context.CopySubresourceRegion(stagingTexture, 0, 0, 0, 0, frameTexture, 0, new(Settings.Default.MapX, Settings.Default.MapY, 0, Settings.Default.MapX + MapKeybagReader.MapWidth, Settings.Default.MapY + MapKeybagReader.MapHeight, 1));
                context.CopySubresourceRegion(stagingTexture, 0, 0, MapKeybagReader.MapHeight, 0, frameTexture, 0, new(Settings.Default.KeybagX, Settings.Default.KeybagY, 0, Settings.Default.KeybagX + 112, Settings.Default.KeybagY + 102, 1));

                var map = context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                return new(map, context, stagingTexture, false);
            }
        }

        public FrameAccess CaptureWinterface()
        {
            lock (frameLock)
            {
                if (currentFrame == null)
                    return new();

                using ID3D11Texture2D1 frameTexture = new(currentFrame.Surface.As<IDirect3DDxgiInterfaceAccess>().GetInterface(typeof(ID3D11Texture2D1).GUID));

                context.CopySubresourceRegion(winterfaceTexture, 0, 0, 0, 0, frameTexture, 0, new(Settings.Default.WinterfaceX, Settings.Default.WinterfaceY, 0, Settings.Default.WinterfaceX + WinterfaceReader.WinterfaceWidth, Settings.Default.WinterfaceY + WinterfaceReader.WinterfaceHeight, 1));

                var map = context.Map(winterfaceTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                return new(map, context, winterfaceTexture, false);
            }
        }
    }
}