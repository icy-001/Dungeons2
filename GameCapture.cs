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

    public partial class GameCapture : IDisposable
    {
        public static GameCapture Instance { get; } = new();

        private readonly ID3D11Device device;
        private readonly ID3D11DeviceContext context;
        private readonly IDirect3DDevice winrtDevice;

        private GraphicsCaptureSession? session;
        private Direct3D11CaptureFramePool? framePool;
        private readonly Lock frameLock = new();
        private Direct3D11CaptureFrame? currentFrame;
        private readonly ID3D11Texture2D stagingTexture;
        private readonly ID3D11Texture2D winterfaceTexture;
        private bool disposedValue;

        private GameCapture()
        {
            _ = D3D11.D3D11CreateDevice(null, Vortice.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport, [], out device, out context);

            using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
            _ = Interop.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pUnknown);
            winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(pUnknown);
            _ = Marshal.Release(pUnknown);

            stagingTexture = device.CreateTexture2D(new()
            {
                Width = MapHudReader.MapWidth,
                Height = MapHudReader.MapHeight + 102,
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
            if (item is null) return;

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
                session.IncludeSecondaryWindows = false;
                session.IsBorderRequired = false;
                session.MinUpdateInterval = TimeSpan.Zero;

                framePool.FrameArrived += (sender, args) =>
                {
                    var frame = sender.TryGetNextFrame();
                    if (frame is null) return;

                    lock (frameLock)
                    {
                        currentFrame?.Dispose();
                        currentFrame = frame;
                    }
                };

                session.StartCapture();
            }
        }

        /// <summary>
        /// Used for calibration.
        /// </summary>
        /// <returns>Returns the full screen frame access.</returns>
        public FrameAccess CaptureFullScreen()
        {
            lock (frameLock)
            {
                if (currentFrame is null)
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
                return new(map, context, localStaging, frameLock, true);
            }
        }

        public FrameAccess CaptureMapKeybag()
        {
            lock (frameLock)
            {
                if (currentFrame is null || stagingTexture is null)
                    return new();

                using ID3D11Texture2D1 frameTexture = new(currentFrame.Surface.As<IDirect3DDxgiInterfaceAccess>().GetInterface(typeof(ID3D11Texture2D1).GUID));

                context.CopySubresourceRegion(stagingTexture, 0, 0, 0, 0, frameTexture, 0,
                    new(offset: new(Settings.Instance.MapX, Settings.Instance.MapY, 0),
                        extent: new(MapHudReader.MapWidth, MapHudReader.MapHeight, 1)));
                context.CopySubresourceRegion(stagingTexture, 0, 0, MapHudReader.MapHeight, 0, frameTexture, 0,
                    new(offset: new(Settings.Instance.HudX, Settings.Instance.HudY, 0),
                        extent: new(117, 154, 1)));

                var map = context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                return new(map, context, stagingTexture, frameLock, false);
            }
        }

        public FrameAccess CaptureWinterface()
        {
            lock (frameLock)
            {
                if (currentFrame is null)
                    return new();

                using ID3D11Texture2D1 frameTexture = new(currentFrame.Surface.As<IDirect3DDxgiInterfaceAccess>().GetInterface(typeof(ID3D11Texture2D1).GUID));

                context.CopySubresourceRegion(winterfaceTexture, 0, 0, 0, 0, frameTexture, 0,
                    new(offset: new(Settings.Instance.WinterfaceX, Settings.Instance.WinterfaceY, 0),
                        extent: new(WinterfaceReader.WinterfaceWidth, WinterfaceReader.WinterfaceHeight, 1)));

                var map = context.Map(winterfaceTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                return new(map, context, winterfaceTexture, frameLock, false);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue) return;

            lock (frameLock)
            {
                session?.Dispose();
                framePool?.Dispose();
                currentFrame?.Dispose();
                stagingTexture?.Dispose();
                winterfaceTexture?.Dispose();

                if (winrtDevice is IDisposable disposableWinrtDevice)
                    disposableWinrtDevice.Dispose();

                context?.Dispose();
                device?.Dispose();
            }

            disposedValue = true;
        }

        ~GameCapture()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}