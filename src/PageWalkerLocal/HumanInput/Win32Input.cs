using System.Runtime.InteropServices;
using PageWalkerLocal.Core;
using PageWalkerLocal.Windowing;

namespace PageWalkerLocal.HumanInput;

public static class Win32Input
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventWheel = 0x0800;
    private const uint MouseEventAbsolute = 0x8000;
    private const uint MouseEventVirtualDesk = 0x4000;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const int VkF12 = 0x7B;
    private const int WmHotkey = 0x0312;
    private const int WmQuit = 0x0012;

    public static ScreenPoint GetCursorPosition()
    {
        return GetCursorPos(out var point) ? new ScreenPoint(point.X, point.Y) : new ScreenPoint(0, 0);
    }

    public static void MoveMouseAbsolute(ScreenPoint point)
    {
        var virtualX = GetSystemMetrics(SmXVirtualScreen);
        var virtualY = GetSystemMetrics(SmYVirtualScreen);
        var virtualWidth = Math.Max(1, GetSystemMetrics(SmCxVirtualScreen));
        var virtualHeight = Math.Max(1, GetSystemMetrics(SmCyVirtualScreen));
        var normalizedX = (int)Math.Round((point.X - virtualX) * 65535.0 / Math.Max(1, virtualWidth - 1));
        var normalizedY = (int)Math.Round((point.Y - virtualY) * 65535.0 / Math.Max(1, virtualHeight - 1));
        SendMouse(normalizedX, normalizedY, 0, MouseEventMove | MouseEventAbsolute | MouseEventVirtualDesk);
    }

    public static void LeftClick()
    {
        SendMouse(0, 0, 0, MouseEventLeftDown);
        SendMouse(0, 0, 0, MouseEventLeftUp);
    }

    public static void ScrollWheel(int delta)
    {
        SendMouse(0, 0, unchecked((uint)delta), MouseEventWheel);
    }

    public static void KeyPress(ConsoleKey key)
    {
        var vk = (ushort)key;
        SendKey(vk, keyUp: false);
        SendKey(vk, keyUp: true);
    }

    public static void CtrlKeyPress(ConsoleKey key)
    {
        const ushort vkControl = 0x11;
        var vk = (ushort)key;
        SendKey(vkControl, keyUp: false);
        SendKey(vk, keyUp: false);
        SendKey(vk, keyUp: true);
        SendKey(vkControl, keyUp: true);
    }

    public static void TypeText(string text)
    {
        foreach (var ch in text)
        {
            SendUnicode(ch, keyUp: false);
            SendUnicode(ch, keyUp: true);
            Thread.Sleep(20);
        }
    }

    public static IDisposable StartEmergencyHotkeyListener(Action onStop, AppLogger logger)
    {
        var listener = new HotkeyListener(onStop, logger);
        listener.Start();
        return listener;
    }

    private static void SendMouse(int dx, int dy, uint data, uint flags)
    {
        var input = new NativeInput
        {
            Type = InputMouse,
            Mouse = new MouseInput
            {
                Dx = dx,
                Dy = dy,
                MouseData = data,
                Flags = flags
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<NativeInput>());
    }

    private static void SendKey(ushort vk, bool keyUp)
    {
        var input = new NativeInput
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput
            {
                VirtualKey = vk,
                Flags = keyUp ? KeyEventKeyUp : 0
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<NativeInput>());
    }

    private static void SendUnicode(char ch, bool keyUp)
    {
        var input = new NativeInput
        {
            Type = InputKeyboard,
            Keyboard = new KeyboardInput
            {
                ScanCode = ch,
                Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0)
            }
        };
        _ = SendInput(1, new[] { input }, Marshal.SizeOf<NativeInput>());
    }

    private sealed class HotkeyListener : IDisposable
    {
        private readonly Action _onStop;
        private readonly AppLogger _logger;
        private readonly ManualResetEventSlim _started = new();
        private Thread? _thread;
        private uint _threadId;

        public HotkeyListener(Action onStop, AppLogger logger)
        {
            _onStop = onStop;
            _logger = logger;
        }

        public void Start()
        {
            _thread = new Thread(Run) { IsBackground = true, Name = "PageWalkerLocalEmergencyHotkey" };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            _started.Wait(TimeSpan.FromSeconds(2));
        }

        public void Dispose()
        {
            if (_threadId != 0)
            {
                _ = PostThreadMessage(_threadId, WmQuit, UIntPtr.Zero, IntPtr.Zero);
            }
        }

        private void Run()
        {
            _threadId = GetCurrentThreadId();
            var registered = RegisterHotKey(IntPtr.Zero, 1209, ModControl | ModAlt, VkF12);
            if (!registered)
            {
                _logger.Warning("Could not register emergency stop hotkey Ctrl+Alt+F12.");
                _started.Set();
                return;
            }

            _logger.Debug("Emergency stop hotkey registered: Ctrl+Alt+F12.");
            _started.Set();
            try
            {
                while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
                {
                    if (message.Message == WmHotkey)
                    {
                        _onStop();
                    }
                }
            }
            finally
            {
                _ = UnregisterHotKey(IntPtr.Zero, 1209);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, NativeInput[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint idThread, int msg, UIntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Hwnd;
        public int Message;
        public UIntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public NativePoint Point;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInput
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public MouseInput Mouse;

        [FieldOffset(8)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
