using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace AntonsBackupManager.App;

internal sealed class DeviceArrivalMonitor
{
    private const int WmDeviceChange = 0x0219;
    private const int DbtDeviceArrival = 0x8000;
    private HwndSource? source;

    public event EventHandler<VolumeArrival>? RemovableDeviceArrived;

    public DeviceArrivalMonitor(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            source = PresentationSource.FromVisual(window) as HwndSource;
            source?.AddHook(WindowMessage);
        };
        window.Closed += (_, _) => source?.RemoveHook(WindowMessage);
    }

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmDeviceChange && wParam.ToInt64() == DbtDeviceArrival && lParam != IntPtr.Zero &&
            Marshal.ReadInt32(lParam, 0) >= 18 && Marshal.ReadInt32(lParam, 4) == 2)
            RemovableDeviceArrived?.Invoke(this, new VolumeArrival(RootsFromMask(unchecked((uint)Marshal.ReadInt32(lParam, 12)))));
        return IntPtr.Zero;
    }

    internal static IReadOnlyList<string> RootsFromMask(uint mask) => Enumerable.Range(0, 26)
        .Where(index => (mask & (1U << index)) != 0).Select(index => $"{(char)('A' + index)}:\\").ToArray();
}

internal sealed class VolumeArrival(IReadOnlyList<string> roots) : EventArgs
{
    public IReadOnlyList<string> Roots { get; } = roots;
}
