using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using ApiInformation = Windows.Foundation.Metadata.ApiInformation;

namespace Dungeons2
{
    [GeneratedComInterface, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    partial interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, in Guid iid);
        IntPtr CreateForMonitor(IntPtr monitor, in Guid iid);
    }

    [GeneratedComInterface, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    partial interface IDirect3DDxgiInterfaceAccess
    {
        IntPtr GetInterface(in Guid iid);
    }

    public partial class GameCapture : IDisposable
    {
        public static GameCapture Instance { get; } = new();

        private ID3D11Device device = null!;
        private ID3D11DeviceContext context = null!;
        private IDirect3DDevice winrtDevice = null!;

        private GraphicsCaptureSession? session;
        private Direct3D11CaptureFramePool? framePool;
        private readonly Lock frameLock = new();
        private Direct3D11CaptureFrame? currentFrame;
        private ID3D11Texture2D stagingTexture = null!;
        private ID3D11Texture2D winterfaceTexture = null!;
        private Exception? captureFailure;
        private DateTime retryAfter;
        private int consecutiveFailures;
        private bool disposedValue;
        private IntPtr targetWindow;
        private Windows.Graphics.SizeInt32 poolSize;
        private Windows.Graphics.SizeInt32 pendingSize;
        private bool resizing;
        private const int HudWidth = 117;
        private const int HudHeight = 154;

        private GameCapture() { }

        private void CreateDeviceResources()
        {
            D3D11.D3D11CreateDevice(null, Vortice.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport, [], out device, out context).CheckError();

            using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
            Marshal.ThrowExceptionForHR(Interop.CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out IntPtr pUnknown));
            try { winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(pUnknown); }
            finally { Marshal.Release(pUnknown); }

            stagingTexture = device.CreateTexture2D(new()
            {
                Width = MapHudReader.MapWidth,
                Height = MapHudReader.MapHeight + HudHeight,
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

        public bool IsActive
        {
            get
            {
                try { return IsActiveCore; }
                catch (Exception e) when (IsCaptureError(e)) { RequestRecovery(e); return false; }
            }
        }

        internal static bool IsCaptureError(Exception e) =>
            e is SharpGen.Runtime.SharpGenException or COMException or ObjectDisposedException or ArgumentException;

        internal void RequestRecovery(Exception e)
        {
            lock (frameLock)
                captureFailure ??= e;
        }

        // Called only between capture iterations, after all FrameAccess leases have
        // been disposed. Never destroy GPU resources from a callback or readback.
        public void RecoverIfNeeded()
        {
            Exception? failure;
            lock (frameLock)
            {
                failure = captureFailure;
                captureFailure = null;
            }
            if (failure is null || disposedValue) return;
            string reason;
            try { reason = device?.DeviceRemovedReason.ToString() ?? "device unavailable"; }
            catch (Exception e) when (IsCaptureError(e)) { reason = e.Message; }
            retryAfter = DateTime.UtcNow.AddSeconds(Math.Min(1 << Math.Min(consecutiveFailures++, 4), 16));
            StopCapture();
            ReleaseDeviceResources();
            Services.LogService.Instance.LogError($"Capture stopped: {failure.Message}; device status: {reason}. Retrying after {retryAfter:O}.");
        }

        private bool IsActiveCore
        {
            get
            {
                if (disposedValue || targetWindow == IntPtr.Zero || DateTime.UtcNow < retryAfter)
                    return false;
                lock (frameLock)
                    if (captureFailure != null) return false;
                if (!Interop.User32.IsWindow(targetWindow))
                {
                    SetTarget(IntPtr.Zero);
                    return false;
                }
                if (Interop.User32.IsIconic(targetWindow))
                {
                    StopCapture();
                    return false;
                }
                if (session is null)
                    SetTarget(targetWindow);

                Direct3D11CaptureFramePool? pool;
                Windows.Graphics.SizeInt32 size;
                lock (frameLock)
                {
                    pool = framePool;
                    size = pendingSize;
                    if (pool is null || size.Width <= 0 || size.Height <= 0)
                        return currentFrame != null;
                    resizing = true;
                    currentFrame?.Dispose();
                    currentFrame = null;
                    pendingSize = default;
                }
                // Never wait for the capture worker while holding its frame lock.
                try
                {
                    pool.Recreate(winrtDevice, Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
                    lock (frameLock)
                        poolSize = size;
                }
                finally
                {
                    lock (frameLock)
                        resizing = false;
                }
                // A static window may only produce a frame while Recreate is in
                // progress. Consume that queued frame even if its event was skipped.
                OnFrameArrived(pool, EventArgs.Empty);
                return false; // Wait for a frame from the resized pool.
            }
        }

        private void StopCapture()
        {
            GraphicsCaptureSession? oldSession;
            Direct3D11CaptureFramePool? oldPool;
            lock (frameLock)
            {
                oldSession = session;
                oldPool = framePool;
                session = null;
                framePool = null;
                DisposeCaptureResource(currentFrame);
                currentFrame = null;
                pendingSize = default;
            }
            if (oldPool != null)
            {
                try { oldPool.FrameArrived -= OnFrameArrived; }
                catch (Exception e) when (IsCaptureError(e)) { }
            }
            DisposeCaptureResource(oldSession);
            DisposeCaptureResource(oldPool);
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            try { ReceiveFrame(sender); }
            catch (Exception e) when (IsCaptureError(e)) { RequestRecovery(e); }
        }

        private void ReceiveFrame(Direct3D11CaptureFramePool sender)
        {
            lock (frameLock)
            {
                if (sender != framePool || disposedValue || resizing || captureFailure != null)
                    return;
                Direct3D11CaptureFrame? frame = sender.TryGetNextFrame();
                if (frame is null)
                    return;
                try
                {
                    currentFrame?.Dispose();
                    currentFrame = null;
                    var size = frame.ContentSize;
                    if (size.Width <= 0 || size.Height <= 0 ||
                        size.Width != poolSize.Width || size.Height != poolSize.Height)
                    {
                        pendingSize = size;
                        return;
                    }
                    currentFrame = frame;
                    frame = null; // Ownership transferred to currentFrame.
                }
                finally { DisposeCaptureResource(frame); }
            }
        }

        public void SetTarget(IntPtr windowHandle)
        {
            try { SetTargetCore(windowHandle); }
            catch (Exception e) when (IsCaptureError(e)) { RequestRecovery(e); }
        }

        private void SetTargetCore(IntPtr windowHandle)
        {
            StopCapture();
            targetWindow = windowHandle;
            if (disposedValue || windowHandle == IntPtr.Zero || !Interop.User32.IsWindow(windowHandle) ||
                Interop.User32.IsIconic(windowHandle) || !GraphicsCaptureSession.IsSupported())
                return;
            if (DateTime.UtcNow < retryAfter || captureFailure != null) return;
            if (device is null) CreateDeviceResources();

            // HWND interop works on Windows 10; TryCreateFromWindowId does not.
            GraphicsCaptureItem item;
            try
            {
                var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
                var itemPointer = interop.CreateForWindow(windowHandle, new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760"));
                try
                {
                    item = GraphicsCaptureItem.FromAbi(itemPointer);
                }
                finally
                {
                    Marshal.Release(itemPointer);
                }
            }
            catch (Exception e) when (e is COMException or ArgumentException)
            {
                RequestRecovery(e);
                return;
            }

            lock (frameLock)
            {
                poolSize = item.Size;
                if (poolSize.Width <= 0 || poolSize.Height <= 0)
                    return;

                framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                    winrtDevice,
                    Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                    2,
                    item.Size
                );

                session = framePool.CreateCaptureSession(item);
                session.IsCursorCaptureEnabled = false;
                // These optional properties are unavailable on Windows 10.
                const string sessionType = "Windows.Graphics.Capture.GraphicsCaptureSession";
                if (ApiInformation.IsPropertyPresent(sessionType, nameof(GraphicsCaptureSession.IncludeSecondaryWindows)))
                    session.IncludeSecondaryWindows = false;
                if (ApiInformation.IsPropertyPresent(sessionType, nameof(GraphicsCaptureSession.IsBorderRequired)))
                    session.IsBorderRequired = false;
                if (ApiInformation.IsPropertyPresent(sessionType, nameof(GraphicsCaptureSession.MinUpdateInterval)))
                    session.MinUpdateInterval = TimeSpan.Zero;

                framePool.FrameArrived += OnFrameArrived;
            }
            session.StartCapture();
        }

        private bool FitsFrame(ID3D11Texture2D texture, int x, int y, int width, int height)
        {
            var size = currentFrame!.ContentSize;
            long availableWidth = Math.Min((long)texture.Description.Width, size.Width);
            long availableHeight = Math.Min((long)texture.Description.Height, size.Height);
            return x >= 0 && y >= 0 && (long)x + width <= availableWidth && (long)y + height <= availableHeight;
        }

        /// <summary>
        /// Used for calibration.
        /// </summary>
        /// <returns>Returns the full screen frame access.</returns>
        public FrameAccess CaptureFullScreen()
        {
            try { return CaptureFullScreenCore(); }
            catch (Exception e) when (IsCaptureError(e)) { RequestRecovery(e); return new(); }
        }

        private FrameAccess CaptureFullScreenCore()
        {
            if (!IsActive) return new();
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

                try
                {
                    context.CopyResource(localStaging, frameTexture);
                    var map = context.Map(localStaging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    consecutiveFailures = 0;
                    return new(map, context, localStaging, frameLock, true);
                }
                catch
                {
                    localStaging.Dispose();
                    throw;
                }
            }
        }

        public FrameAccess CaptureMapKeybag()
        {
            try { return CaptureMapKeybagCore(); }
            catch (Exception e) when (IsCaptureError(e)) { RequestRecovery(e); return new(); }
        }

        private FrameAccess CaptureMapKeybagCore()
        {
            if (!IsActive) return new();
            lock (frameLock)
            {
                if (currentFrame is null || stagingTexture is null)
                    return new();

                using ID3D11Texture2D1 frameTexture = new(currentFrame.Surface.As<IDirect3DDxgiInterfaceAccess>().GetInterface(typeof(ID3D11Texture2D1).GUID));

                if (!FitsFrame(frameTexture, Settings.Instance.MapX, Settings.Instance.MapY, MapHudReader.MapWidth, MapHudReader.MapHeight) ||
                    !FitsFrame(frameTexture, Settings.Instance.HudX, Settings.Instance.HudY, HudWidth, HudHeight))
                    return new();

                context.CopySubresourceRegion(stagingTexture, 0, 0, 0, 0, frameTexture, 0,
                    new(offset: new(Settings.Instance.MapX, Settings.Instance.MapY, 0),
                        extent: new(MapHudReader.MapWidth, MapHudReader.MapHeight, 1)));
                context.CopySubresourceRegion(stagingTexture, 0, 0, MapHudReader.MapHeight, 0, frameTexture, 0,
                    new(offset: new(Settings.Instance.HudX, Settings.Instance.HudY, 0),
                        extent: new(HudWidth, HudHeight, 1)));

                var map = context.Map(stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                consecutiveFailures = 0;
                return new(map, context, stagingTexture, frameLock, false);
            }
        }

        public FrameAccess CaptureWinterface()
        {
            try { return CaptureWinterfaceCore(); }
            catch (Exception e) when (IsCaptureError(e)) { RequestRecovery(e); return new(); }
        }

        private FrameAccess CaptureWinterfaceCore()
        {
            if (!IsActive) return new();
            lock (frameLock)
            {
                if (currentFrame is null)
                    return new();

                using ID3D11Texture2D1 frameTexture = new(currentFrame.Surface.As<IDirect3DDxgiInterfaceAccess>().GetInterface(typeof(ID3D11Texture2D1).GUID));

                int winterfaceX = Settings.Instance.WinterfaceX;
                int winterfaceY = Settings.Instance.WinterfaceY;
                if (!FitsFrame(frameTexture, winterfaceX, winterfaceY, WinterfaceReader.WinterfaceWidth, WinterfaceReader.WinterfaceHeight))
                    return new();

                context.CopySubresourceRegion(winterfaceTexture, 0, 0, 0, 0, frameTexture, 0,
                    new(offset: new(winterfaceX, winterfaceY, 0),
                        extent: new(WinterfaceReader.WinterfaceWidth, WinterfaceReader.WinterfaceHeight, 1)));

                var map = context.Map(winterfaceTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                return new(map, context, winterfaceTexture, frameLock, false);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue) return;

            disposedValue = true;
            StopCapture();

            ReleaseDeviceResources();
        }

        private static void DisposeCaptureResource(IDisposable? resource)
        {
            try { resource?.Dispose(); }
            catch (Exception e) when (IsCaptureError(e)) { }
        }

        private void ReleaseDeviceResources()
        {
            lock (frameLock)
            {
                DisposeCaptureResource(stagingTexture);
                DisposeCaptureResource(winterfaceTexture);
                DisposeCaptureResource(winrtDevice as IDisposable);
                DisposeCaptureResource(context);
                DisposeCaptureResource(device);
                stagingTexture = null!;
                winterfaceTexture = null!;
                winrtDevice = null!;
                context = null!;
                device = null!;
            }

        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
