using System;
using System.Runtime.InteropServices;

namespace CopiarIconos
{
    public static class SessionHelper
    {
        [DllImport("kernel32.dll")]
        static extern int WTSGetActiveConsoleSessionId();

        [DllImport("Wtsapi32.dll")]
        static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, int infoClass, out IntPtr buffer, out uint bytesReturned);

        [DllImport("Wtsapi32.dll")]
        static extern void WTSFreeMemory(IntPtr memory);

        public static string GetActiveSessionUser()
        {
            int sessionId = WTSGetActiveConsoleSessionId();
            IntPtr buffer;
            uint strLen;
            if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, 5, out buffer, out strLen) && strLen > 1)
            {
                string? user = Marshal.PtrToStringAnsi(buffer);
                WTSFreeMemory(buffer);
                return user ?? string.Empty;
            }
            return string.Empty;
        }
    }
}