using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ClickMapper.Native
{
    /// <summary>
    /// Simuliert Mausaktionen mittels der Windows SendInput API.
    /// </summary>
    public static class MouseSimulator
    {
        #region Win32 API Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        // Input Types
        private const uint INPUT_MOUSE = 0;

        // Mouse Event Flags
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

        #endregion

        #region Win32 API Imports

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        #endregion

        #region Public Methods

        /// <summary>
        /// Führt einen Linksklick an der angegebenen Bildschirmposition aus.
        /// </summary>
        /// <param name="x">X-Koordinate (Bildschirmpixel)</param>
        /// <param name="y">Y-Koordinate (Bildschirmpixel)</param>
        /// <param name="returnToOriginal">Optional: Maus zurück zur ursprünglichen Position bewegen</param>
        public static void Click(int x, int y, bool returnToOriginal = false)
        {
            POINT originalPos = new POINT();
            if (returnToOriginal)
            {
                GetCursorPos(out originalPos);
            }

            // Maus zur Zielposition bewegen
            SetCursorPos(x, y);

            // Kurze Pause für Stabilität
            Thread.Sleep(10);

            // Linksklick simulieren
            INPUT[] inputs = new INPUT[2];

            // Mouse Down
            inputs[0] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_LEFTDOWN,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            // Mouse Up
            inputs[1] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_LEFTUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));

            // Optional: Maus zurückbewegen
            if (returnToOriginal)
            {
                Thread.Sleep(10);
                SetCursorPos(originalPos.X, originalPos.Y);
            }
        }

        /// <summary>
        /// Führt einen Rechtsklick an der angegebenen Bildschirmposition aus.
        /// </summary>
        public static void RightClick(int x, int y)
        {
            SetCursorPos(x, y);
            Thread.Sleep(10);

            INPUT[] inputs = new INPUT[2];

            inputs[0] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_RIGHTDOWN,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            inputs[1] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = MOUSEEVENTF_RIGHTUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            };

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Bewegt die Maus zu einer absoluten Bildschirmposition.
        /// </summary>
        public static void MoveTo(int x, int y)
        {
            SetCursorPos(x, y);
        }

        /// <summary>
        /// Gibt die aktuelle Mausposition zurück.
        /// </summary>
        public static (int X, int Y) GetPosition()
        {
            POINT point;
            GetCursorPos(out point);
            return (point.X, point.Y);
        }

        #endregion
    }
}
