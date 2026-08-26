using System.Runtime.InteropServices;

namespace DropCaptureList.Windows.Services;

internal static class RunningComObject
{
    private const int MkEUnavailable = unchecked((int)0x800401E3);

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string lpszProgID, out Guid pclsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    public static object Get(string progId)
    {
        var hr = CLSIDFromProgID(progId, out var clsid);
        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        hr = GetActiveObject(ref clsid, IntPtr.Zero, out var instance);
        if (hr == MkEUnavailable)
        {
            throw new InvalidOperationException("Excel is not running. Open a workbook, highlight cells, then capture.");
        }

        if (hr < 0)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        return instance;
    }
}
