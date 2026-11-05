using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OpenAK820.Hid;

/// <summary>Everything the protocol layer needs from one HID top-level collection.</summary>
public interface IHidPort : IDisposable
{
    /// <summary>Send a feature report. Byte 0 is the report ID.</summary>
    void SetFeature(byte[] report);
    /// <summary>Fetch a feature report into <paramref name="report"/>. Byte 0 is the report ID.</summary>
    void GetFeature(byte[] report);
    /// <summary>Send an output report. Byte 0 is the report ID.</summary>
    void Write(byte[] report);
    /// <summary>Wait for an input report. Returns the byte count, or 0 on timeout.</summary>
    int Read(byte[] buffer, int timeoutMs);
}

public sealed record HidInterfaceInfo(
    string Path, ushort VendorId, ushort ProductId, ushort Release,
    ushort UsagePage, ushort Usage,
    int InputLength, int OutputLength, int FeatureLength,
    string Manufacturer, string Product);

public static class HidEnumerator
{
    public static List<HidInterfaceInfo> List(ushort vendorId)
    {
        var result = new List<HidInterfaceInfo>();
        Native.HidD_GetHidGuid(out var guid);
        var set = Native.SetupDiGetClassDevsW(ref guid, IntPtr.Zero, IntPtr.Zero, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
        if (set == new IntPtr(-1)) return result;
        try
        {
            var iface = new Native.SP_DEVICE_INTERFACE_DATA { cbSize = Marshal.SizeOf<Native.SP_DEVICE_INTERFACE_DATA>() };
            var detail = new byte[2048];
            for (uint i = 0; Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, i, ref iface); i++)
            {
                BitConverter.TryWriteBytes(detail, IntPtr.Size == 8 ? 8 : 6);
                if (!Native.SetupDiGetDeviceInterfaceDetailW(set, ref iface, detail, detail.Length, out _, IntPtr.Zero)) continue;
                var path = System.Text.Encoding.Unicode.GetString(detail, 4, detail.Length - 4);
                path = path[..path.IndexOf('\0')];
                if (!path.Contains($"vid_{vendorId:x4}", StringComparison.OrdinalIgnoreCase)) continue;
                var info = Describe(path);
                if (info != null) result.Add(info);
            }
        }
        finally { Native.SetupDiDestroyDeviceInfoList(set); }
        return result;
    }

    private static HidInterfaceInfo? Describe(string path)
    {
        // Access 0 lets us read attributes and capabilities without claiming the device.
        using var h = Native.CreateFileW(path, 0, Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
        if (h.IsInvalid) return null;
        var attrs = new Native.HIDD_ATTRIBUTES { Size = Marshal.SizeOf<Native.HIDD_ATTRIBUTES>() };
        if (!Native.HidD_GetAttributes(h, ref attrs)) return null;
        if (!Native.HidD_GetPreparsedData(h, out var pp)) return null;
        Native.HIDP_CAPS caps;
        try { if (Native.HidP_GetCaps(pp, out caps) != Native.HIDP_STATUS_SUCCESS) return null; }
        finally { Native.HidD_FreePreparsedData(pp); }
        return new HidInterfaceInfo(path, attrs.VendorID, attrs.ProductID, attrs.VersionNumber,
            caps.UsagePage, caps.Usage, caps.InputReportByteLength, caps.OutputReportByteLength, caps.FeatureReportByteLength,
            GetString(h, Native.HidD_GetManufacturerString), GetString(h, Native.HidD_GetProductString));
    }

    private delegate bool StringGetter(SafeFileHandle h, char[] buffer, int bytes);

    private static string GetString(SafeFileHandle h, StringGetter getter)
    {
        var buffer = new char[128];
        if (!getter(h, buffer, buffer.Length * 2)) return "";
        var s = new string(buffer);
        var end = s.IndexOf('\0');
        return end >= 0 ? s[..end] : s;
    }
}

/// <summary>A real HID collection opened for overlapped I/O, so reads can time out.</summary>
public sealed unsafe class WinHidPort : IHidPort
{
    private readonly SafeFileHandle _handle;
    private readonly EventWaitHandle _event = new ManualResetEvent(false);
    private readonly NativeOverlapped* _overlapped;

    public WinHidPort(string path)
    {
        _handle = Native.CreateFileW(path, Native.GENERIC_READ | Native.GENERIC_WRITE,
            Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
        if (_handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not open HID interface");
        _overlapped = (NativeOverlapped*)NativeMemory.AllocZeroed((nuint)sizeof(NativeOverlapped));
        _overlapped->EventHandle = _event.SafeWaitHandle.DangerousGetHandle();
    }

    public void SetFeature(byte[] report)
    {
        if (!Native.HidD_SetFeature(_handle, report, report.Length))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "HidD_SetFeature failed");
    }

    public void GetFeature(byte[] report)
    {
        if (!Native.HidD_GetFeature(_handle, report, report.Length))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "HidD_GetFeature failed");
    }

    public void Write(byte[] report)
    {
        fixed (byte* p = report)
        {
            if (Transfer(write: true, p, report.Length, 2000) != report.Length)
                throw new IOException("HID write did not complete");
        }
    }

    public int Read(byte[] buffer, int timeoutMs)
    {
        fixed (byte* p = buffer) return Transfer(write: false, p, buffer.Length, timeoutMs);
    }

    private int Transfer(bool write, byte* buffer, int length, int timeoutMs)
    {
        _event.Reset();
        _overlapped->OffsetLow = 0;
        _overlapped->OffsetHigh = 0;
        var started = write
            ? Native.WriteFile(_handle, buffer, length, out _, _overlapped)
            : Native.ReadFile(_handle, buffer, length, out _, _overlapped);
        if (!started)
        {
            var err = Marshal.GetLastWin32Error();
            if (err != Native.ERROR_IO_PENDING) throw new Win32Exception(err, write ? "HID write failed" : "HID read failed");
        }
        var completed = _event.WaitOne(timeoutMs);
        if (!completed)
        {
            Native.CancelIoEx(_handle, _overlapped);
            // Wait for the cancelled I/O to retire: the kernel still owns the pinned buffer until it does.
            Native.GetOverlappedResult(_handle, _overlapped, out _, true);
            return 0;
        }
        if (!Native.GetOverlappedResult(_handle, _overlapped, out var count, false))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "HID transfer failed");
        return count;
    }

    public void Dispose()
    {
        _handle.Dispose();
        NativeMemory.Free(_overlapped);
        _event.Dispose();
    }
}

internal static unsafe partial class Native
{
    public const uint DIGCF_PRESENT = 0x2, DIGCF_DEVICEINTERFACE = 0x10;
    public const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000;
    public const uint FILE_SHARE_READ = 1, FILE_SHARE_WRITE = 2, OPEN_EXISTING = 3, FILE_FLAG_OVERLAPPED = 0x40000000;
    public const int ERROR_IO_PENDING = 997;
    public const int HIDP_STATUS_SUCCESS = 0x00110000;

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVICE_INTERFACE_DATA { public int cbSize; public Guid InterfaceClassGuid; public int Flags; public IntPtr Reserved; }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDD_ATTRIBUTES { public int Size; public ushort VendorID, ProductID, VersionNumber; }

    [StructLayout(LayoutKind.Sequential)]
    public struct HIDP_CAPS
    {
        public ushort Usage, UsagePage, InputReportByteLength, OutputReportByteLength, FeatureReportByteLength;
        public fixed ushort Reserved[17];
        public ushort NumberLinkCollectionNodes, NumberInputButtonCaps, NumberInputValueCaps, NumberInputDataIndices,
            NumberOutputButtonCaps, NumberOutputValueCaps, NumberOutputDataIndices,
            NumberFeatureButtonCaps, NumberFeatureValueCaps, NumberFeatureDataIndices;
    }

    [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetAttributes(SafeFileHandle h, ref HIDD_ATTRIBUTES a);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
    [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr data);
    [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data, out HIDP_CAPS caps);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_SetFeature(SafeFileHandle h, byte[] report, int length);
    [DllImport("hid.dll", SetLastError = true)] public static extern bool HidD_GetFeature(SafeFileHandle h, byte[] report, int length);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] public static extern bool HidD_GetProductString(SafeFileHandle h, [Out] char[] buffer, int bytes);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)] public static extern bool HidD_GetManufacturerString(SafeFileHandle h, [Out] char[] buffer, int bytes);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevsW(ref Guid guid, IntPtr enumerator, IntPtr parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr devInfo, ref Guid guid, uint index, ref SP_DEVICE_INTERFACE_DATA data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool SetupDiGetDeviceInterfaceDetailW(IntPtr set, ref SP_DEVICE_INTERFACE_DATA data, byte[] detail, int size, out int required, IntPtr devInfo);
    [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadFile(SafeFileHandle h, byte* buffer, int length, out int read, NativeOverlapped* overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool WriteFile(SafeFileHandle h, byte* buffer, int length, out int written, NativeOverlapped* overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetOverlappedResult(SafeFileHandle h, NativeOverlapped* overlapped, out int transferred, bool wait);
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CancelIoEx(SafeFileHandle h, NativeOverlapped* overlapped);
}
