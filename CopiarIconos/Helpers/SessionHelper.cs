using System;
using System.Runtime.InteropServices;

namespace CopiarIconos.Helpers
{
    public static class SessionHelper
    {
        [DllImport("kernel32.dll")]
        static extern int WTSGetActiveConsoleSessionId();

        [DllImport("Wtsapi32.dll")]
        static extern bool WTSQuerySessionInformation(nint hServer, int sessionId, int infoClass, out nint buffer, out uint bytesReturned);

        [DllImport("Wtsapi32.dll")]
        static extern void WTSFreeMemory(nint memory);

        public static string GetActiveSessionUser()
        {
            int sessionId = WTSGetActiveConsoleSessionId();
            nint buffer;
            uint strLen;
            if (WTSQuerySessionInformation(nint.Zero, sessionId, 5, out buffer, out strLen) && strLen > 1)
            {
                string? user = Marshal.PtrToStringAnsi(buffer);
                WTSFreeMemory(buffer);
                return user ?? string.Empty;
            }
            return string.Empty;
        }
    }
}