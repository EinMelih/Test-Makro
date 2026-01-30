using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ClickMapper.Native
{
    /// <summary>
    /// Event-Argumente für Tastatur-Events.
    /// </summary>
    public class KeyboardHookEventArgs : EventArgs
    {
        public Key Key { get; set; }
        public bool Handled { get; set; }
    }

    /// <summary>
    /// Low-Level Keyboard Hook für globale Tastenabfrage.
    /// Verwendet WH_KEYBOARD_LL (Hook ID 13).
    /// </summary>
    public class KeyboardHook : IDisposable
    {
        #region Win32 API

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion

        #region Fields

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _hookProc;
        private bool _isRunning = false;
        private bool _disposed = false;

        #endregion

        #region Events

        /// <summary>
        /// Wird bei jedem Tastendruck ausgelöst.
        /// </summary>
        public event EventHandler<KeyboardHookEventArgs> KeyPressed;

        #endregion

        #region Constructor

        public KeyboardHook()
        {
            // Delegate als Feld speichern um GC zu verhindern
            _hookProc = HookCallback;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Startet den Keyboard Hook.
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _hookId = SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    _hookProc,
                    GetModuleHandle(curModule.ModuleName),
                    0);
            }

            if (_hookId == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    "Failed to install keyboard hook. Error code: " + errorCode);
            }

            _isRunning = true;
        }

        /// <summary>
        /// Stoppt den Keyboard Hook.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }

            _isRunning = false;
        }

        /// <summary>
        /// Gibt an, ob der Hook aktiv ist.
        /// </summary>
        public bool IsRunning
        {
            get { return _isRunning; }
        }

        #endregion

        #region Private Methods

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                KBDLLHOOKSTRUCT hookStruct = 
                    (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                Key key = KeyInterop.KeyFromVirtualKey((int)hookStruct.vkCode);

                KeyboardHookEventArgs args = new KeyboardHookEventArgs
                {
                    Key = key,
                    Handled = false
                };

                KeyPressed?.Invoke(this, args);

                if (args.Handled)
                {
                    // Event wurde behandelt, nicht an andere Programme weiterleiten
                    return (IntPtr)1;
                }
            }

            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                Stop();
            }

            _disposed = true;
        }

        ~KeyboardHook()
        {
            Dispose(false);
        }

        #endregion
    }
}
