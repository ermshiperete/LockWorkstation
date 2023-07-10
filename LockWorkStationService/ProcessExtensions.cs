using System;
using System.Runtime.InteropServices;

// see https://github.com/murrayju/CreateProcessAsUser

namespace LockWorkStationService
{
    public static class ProcessExtensions
    {
        #region Win32 Constants

        private const int CreateUnicodeEnvironment = 0x00000400;
        private const int CreateNoWindow = 0x08000000;

        private const int CreateNewConsole = 0x00000010;

        private const uint InvalidSessionId = 0xFFFFFFFF;
        private static readonly IntPtr WtsCurrentServerHandle = IntPtr.Zero;

        #endregion

        #region DllImports

        [DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUser", SetLastError = true, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool CreateProcessAsUser(
            IntPtr hToken,
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandle,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref Startupinfo lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        [DllImport("advapi32.dll", EntryPoint = "DuplicateTokenEx")]
        private static extern bool DuplicateTokenEx(
            IntPtr existingTokenHandle,
            uint dwDesiredAccess,
            IntPtr lpThreadAttributes,
            int tokenType,
            int impersonationLevel,
            ref IntPtr duplicateTokenHandle);

        [DllImport("userenv.dll", SetLastError = true)]
        private static extern bool CreateEnvironmentBlock(ref IntPtr lpEnvironment, IntPtr hToken, bool bInherit);

        [DllImport("userenv.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyEnvironmentBlock(IntPtr lpEnvironment);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hSnapshot);

        [DllImport("kernel32.dll")]
        private static extern uint WTSGetActiveConsoleSessionId();

        [DllImport("Wtsapi32.dll")]
        private static extern uint WTSQueryUserToken(uint sessionId, ref IntPtr phToken);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern int WTSEnumerateSessions(
            IntPtr hServer,
            int reserved,
            int version,
            ref IntPtr ppSessionInfo,
            ref int pCount);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);
        
        #endregion

        #region Win32 Structs

        private enum Sw
        {
            SwHide = 0,
            SwShownormal = 1,
            SwNormal = 1,
            SwShowminimized = 2,
            SwShowmaximized = 3,
            SwMaximize = 3,
            SwShownoactivate = 4,
            SwShow = 5,
            SwMinimize = 6,
            SwShowminnoactive = 7,
            SwShowna = 8,
            SwRestore = 9,
            SwShowdefault = 10,
            SwMax = 10
        }

        private enum WtsConnectstateClass
        {
            WtsActive,
            WtsConnected,
            WtsConnectQuery,
            WtsShadow,
            WtsDisconnected,
            WtsIdle,
            WtsListen,
            WtsReset,
            WtsDown,
            WtsInit
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        private enum SecurityImpersonationLevel
        {
            SecurityAnonymous = 0,
            SecurityIdentification = 1,
            SecurityImpersonation = 2,
            SecurityDelegation = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Startupinfo
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        private enum TokenType
        {
            TokenPrimary = 1,
            TokenImpersonation = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WtsSessionInfo
        {
            public readonly uint SessionID;

            [MarshalAs(UnmanagedType.LPStr)]
            public readonly string pWinStationName;

            public readonly WtsConnectstateClass State;
        }

        #endregion

        private static Logger _logger;

        // Gets the user token from the currently active session
        private static bool GetSessionUserToken(ref IntPtr phUserToken)
        {
            var bResult = false;
            var hImpersonationToken = IntPtr.Zero;
            var activeSessionId = InvalidSessionId;
            var pSessionInfo = IntPtr.Zero;
            var sessionCount = 0;

            // Get a handle to the user access token for the current active session.
            if (WTSEnumerateSessions(WtsCurrentServerHandle, 0, 1, ref pSessionInfo, ref sessionCount) != 0)
            {
                var arrayElementSize = Marshal.SizeOf(typeof(WtsSessionInfo));
                var current = pSessionInfo;

                for (var i = 0; i < sessionCount; i++)
                {
                    var si = (WtsSessionInfo)Marshal.PtrToStructure((IntPtr)current, typeof(WtsSessionInfo));
                    current += arrayElementSize;

                    _logger?.Write($"Session {si.SessionID}: {si.pWinStationName}, state {si.State}");
                    if (si.State == WtsConnectstateClass.WtsActive)
                    {
                        activeSessionId = si.SessionID;
                    }
                }

                WTSFreeMemory(pSessionInfo);
            }

            // If enumerating did not work, fall back to the old method
            if (activeSessionId == InvalidSessionId)
            {
                activeSessionId = WTSGetActiveConsoleSessionId();
            }

            if (WTSQueryUserToken(activeSessionId, ref hImpersonationToken) != 0)
            {
                // Convert the impersonation token to a primary token
                bResult = DuplicateTokenEx(hImpersonationToken, 0, IntPtr.Zero,
                    (int)SecurityImpersonationLevel.SecurityImpersonation, (int)TokenType.TokenPrimary,
                    ref phUserToken);

                CloseHandle(hImpersonationToken);
            }
            else
            {
                throw new ApplicationException($"Got error from WTSQueryUserToken: {Marshal.GetLastWin32Error()}");
            }

            return bResult;
        }

        public static bool StartProcessAsCurrentUser(string appPath, string cmdLine = null,
            string workDir = null, bool visible = true, Logger logger = null)
        {
            _logger = logger;
            var hUserToken = IntPtr.Zero;
            var startInfo = new Startupinfo();
            var procInfo = new ProcessInformation();
            var pEnv = IntPtr.Zero;

            startInfo.cb = Marshal.SizeOf(typeof(Startupinfo));

            try
            {
                if (!GetSessionUserToken(ref hUserToken))
                {
                    throw new ApplicationException("StartProcessAsCurrentUser: GetSessionUserToken failed.");
                }

                var dwCreationFlags = CreateUnicodeEnvironment | (uint)(visible ? CreateNewConsole : CreateNoWindow);
                startInfo.wShowWindow = (short)(visible ? Sw.SwShow : Sw.SwHide);
                startInfo.lpDesktop = "winsta0\\default";

                if (!CreateEnvironmentBlock(ref pEnv, hUserToken, false))
                {
                    throw new ApplicationException("StartProcessAsCurrentUser: CreateEnvironmentBlock failed.");
                }

                if (!CreateProcessAsUser(hUserToken,
                    appPath, // Application Name
                    cmdLine, // Command Line
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    dwCreationFlags,
                    pEnv,
                    workDir, // Working directory
                    ref startInfo,
                    out procInfo))
                {
                    throw new ApplicationException($"StartProcessAsCurrentUser: CreateProcessAsUser failed.  Error Code - {Marshal.GetLastWin32Error()}");
                }
            }
            finally
            {
                CloseHandle(hUserToken);
                if (pEnv != IntPtr.Zero)
                {
                    DestroyEnvironmentBlock(pEnv);
                }
                CloseHandle(procInfo.hThread);
                CloseHandle(procInfo.hProcess);
            }

            return true;
        }

    }
}
