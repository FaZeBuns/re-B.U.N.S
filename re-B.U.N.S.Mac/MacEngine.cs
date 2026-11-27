using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ReBuns;

public static class MacNative
{
    const string AppServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";
    const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    public const uint kCGHIDEventTap = 0;
    public const uint kCGHeadInsertEventTap = 0;
    public const uint kCGEventTapOptionDefault = 0;

    public const int kCGEventLeftMouseDown = 1;
    public const int kCGEventLeftMouseUp = 2;
    public const int kCGEventRightMouseDown = 3;
    public const int kCGEventRightMouseUp = 4;
    public const int kCGEventMouseMoved = 5;
    public const int kCGEventLeftMouseDragged = 6;
    public const int kCGEventRightMouseDragged = 7;
    public const int kCGEventKeyDown = 10;
    public const int kCGEventKeyUp = 11;
    public const int kCGEventFlagsChanged = 12;
    public const int kCGEventScrollWheel = 22;
    public const int kCGEventOtherMouseDown = 25;
    public const int kCGEventOtherMouseUp = 26;
    public const int kCGEventOtherMouseDragged = 27;
    public const int kCGEventTapDisabledByTimeout = -2;

    public const int kCGMouseEventDeltaX = 4;
    public const int kCGMouseEventDeltaY = 5;
    public const int kCGMouseEventButtonNumber = 3;
    public const int kCGKeyboardEventKeycode = 9;
    public const int kCGScrollWheelEventDeltaAxis1 = 11;

    [StructLayout(LayoutKind.Sequential)]
    public struct CGPoint { public double X, Y; }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr CGEventTapCallBack(IntPtr proxy, int type, IntPtr cgEvent, IntPtr userInfo);

    [DllImport(AppServices)]
    public static extern bool AXIsProcessTrusted();

    [DllImport(AppServices)]
    public static extern IntPtr CGEventTapCreate(uint tap, uint place, uint options, ulong eventsOfInterest, CGEventTapCallBack callback, IntPtr userInfo);

    [DllImport(AppServices)]
    public static extern void CGEventTapEnable(IntPtr tap, bool enable);

    [DllImport(AppServices)]
    public static extern long CGEventGetIntegerValueField(IntPtr cgEvent, int field);

    [DllImport(AppServices)]
    public static extern ulong CGEventGetFlags(IntPtr cgEvent);

    [DllImport(AppServices)]
    public static extern int CGAssociateMouseAndMouseCursorPosition(bool connected);

    [DllImport(AppServices)]
    public static extern int CGWarpMouseCursorPosition(CGPoint newCursorPosition);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFMachPortCreateRunLoopSource(IntPtr allocator, IntPtr tap, long order);

    [DllImport(CoreFoundation)]
    public static extern IntPtr CFRunLoopGetCurrent();

    [DllImport(CoreFoundation)]
    public static extern void CFRunLoopAddSource(IntPtr rl, IntPtr source, IntPtr mode);

    [DllImport(CoreFoundation)]
    public static extern void CFRunLoopRun();

    [DllImport(CoreFoundation)]
    public static extern void CFRunLoopStop(IntPtr rl);

    /// <summary>Converts a macOS virtual keycode (Carbon / IOKit) to our cross-platform InputNames code.</summary>
    public static int MacKeyCodeToVk(int macKey) => macKey switch
    {
        0x00 => 'A', 0x01 => 'S', 0x02 => 'D', 0x03 => 'F', 0x04 => 'H', 0x05 => 'G',
        0x06 => 'Z', 0x07 => 'X', 0x08 => 'C', 0x09 => 'V', 0x0B => 'B', 0x0C => 'Q',
        0x0D => 'W', 0x0E => 'E', 0x0F => 'R', 0x10 => 'Y', 0x11 => 'T',
        0x12 => '1', 0x13 => '2', 0x14 => '3', 0x15 => '4', 0x17 => '5', 0x16 => '6',
        0x1A => '7', 0x1C => '8', 0x19 => '9', 0x1D => '0',
        0x1F => 'O', 0x20 => 'U', 0x22 => 'I', 0x23 => 'P', 0x24 => 13 /* Return */,
        0x25 => 'L', 0x26 => 'J', 0x28 => 'K', 0x2B => 0xBC /* Oemcomma */,
        0x2D => 'N', 0x2E => 'M', 0x2F => 0xBE /* OemPeriod */,
        0x30 => 0x09 /* Tab */, 0x31 => 0x20 /* Space */, 0x33 => 0x08 /* Backspace */,
        0x35 => 0x1B /* Escape */,
        0x37 or 0x36 => 0x5B /* Command -> LWin (Pause/Play) */,
        0x38 or 0x3C => 0xA0 /* LShiftKey */,
        0x39 => 0x14 /* Caps Lock */,
        0x3A or 0x3D => 0xA4 /* Option -> LMenu/Alt */,
        0x3B or 0x3E => 0xA2 /* Control -> LControlKey */,
        0x7B => 0x25 /* Left */, 0x7C => 0x27 /* Right */, 0x7D => 0x28 /* Down */, 0x7E => 0x26 /* Up */,
        0x75 => 0x2E /* Forward Delete */,
        _ => 0
    };
}

public static class InputNames
{
    public const int WheelUp = 0x1001, WheelDown = 0x1002, Max = 0x1100;

    static readonly Dictionary<string, int> NameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mouse1"] = 1, ["Mouse2"] = 2, ["Mouse3"] = 4, ["Mouse4"] = 5, ["Mouse5"] = 6,
        ["WheelUp"] = WheelUp, ["WheelDown"] = WheelDown,
        ["Back"] = 0x08, ["Tab"] = 0x09, ["Return"] = 13, ["Escape"] = 0x1B, ["Space"] = 0x20,
        ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28, ["Delete"] = 0x2E,
        ["D0"] = '0', ["D1"] = '1', ["D2"] = '2', ["D3"] = '3', ["D4"] = '4',
        ["D5"] = '5', ["D6"] = '6', ["D7"] = '7', ["D8"] = '8', ["D9"] = '9',
        ["LWin"] = 0x5B, ["RWin"] = 0x5C,
        ["LShiftKey"] = 0xA0, ["RShiftKey"] = 0xA1,
        ["LControlKey"] = 0xA2, ["RControlKey"] = 0xA3,
        ["LMenu"] = 0xA4, ["RMenu"] = 0xA5,
        ["Oemcomma"] = 0xBC, ["OemPeriod"] = 0xBE, ["Capital"] = 0x14
    };

    static readonly Dictionary<int, string> CodeToName;

    static InputNames()
    {
        for (char c = 'A'; c <= 'Z'; c++) NameToCode[c.ToString()] = c;
        CodeToName = new();
        foreach (var kv in NameToCode)
            if (!CodeToName.ContainsKey(kv.Value)) CodeToName[kv.Value] = kv.Key;
    }

    public static int ToCode(string name) =>
        !string.IsNullOrEmpty(name) && NameToCode.TryGetValue(name, out int c) ? c : 0;

    public static string ToName(int code) =>
        CodeToName.TryGetValue(code, out var n) ? n : $"Key{code}";

    public static string Pretty(string name) => name switch
    {
        "Mouse1" => "L-Click", "Mouse2" => "R-Click", "Mouse3" => "M-Click",
        "Mouse4" => "Mouse 4", "Mouse5" => "Mouse 5", "WheelUp" => "Wheel ↑", "WheelDown" => "Wheel ↓",
        "LShiftKey" => "L-Shift", "RShiftKey" => "R-Shift", "LControlKey" => "Control", "RControlKey" => "R-Control",
        "LMenu" => "Option", "RMenu" => "R-Option", "Return" => "Return", "Back" => "Delete ⌫", "Oemcomma" => ",",
        "OemPeriod" => ".", "Capital" => "Caps", "Escape" => "Esc", "LWin" => "⌘ Cmd", "RWin" => "R-⌘",
        _ => name.StartsWith("D") && name.Length == 2 && char.IsDigit(name[1]) ? name[1..] : name
    };

    public static string ShortTarget(PadTarget t) => t switch
    {
        PadTarget.LStickUp => "LS↑", PadTarget.LStickDown => "LS↓", PadTarget.LStickLeft => "LS←", PadTarget.LStickRight => "LS→",
        PadTarget.RStickUp => "RS↑", PadTarget.RStickDown => "RS↓", PadTarget.RStickLeft => "RS←", PadTarget.RStickRight => "RS→",
        PadTarget.DUp => "D↑", PadTarget.DDown => "D↓", PadTarget.DLeft => "D←", PadTarget.DRight => "D→",
        _ => t.ToString()
    };
}

public struct PadSnapshot
{
    public uint ButtonsMask;
    public byte LT, RT;
    public short LX, LY, RX, RY;

    public readonly bool Has(PadTarget t) => (ButtonsMask & (1u << (int)t)) != 0;

    public readonly string FormatButtons()
    {
        if (ButtonsMask == 0) return "—";
        var list = new List<string>(8);
        foreach (var t in Enum.GetValues<PadTarget>())
            if (t != PadTarget.None && Has(t)) list.Add(t.ToString());
        return string.Join(" + ", list);
    }

    public readonly string ToW3CGamepadJson()
    {
        // Standard W3C Gamepad mapping (17 buttons, 4 axes -1..+1)
        uint mask = ButtonsMask;
        int B(PadTarget t) => (mask & (1u << (int)t)) != 0 ? 1 : 0;
        double lt = Math.Round(LT / 255.0, 3), rt = Math.Round(RT / 255.0, 3);
        double lx = Math.Round(LX / 32767.0, 4), ly = Math.Round(-LY / 32767.0, 4);
        double rx = Math.Round(RX / 32767.0, 4), ry = Math.Round(-RY / 32767.0, 4);
        return $"{{\"axes\":[{lx},{ly},{rx},{ry}],\"buttons\":[{B(PadTarget.A)},{B(PadTarget.B)},{B(PadTarget.X)},{B(PadTarget.Y)},{B(PadTarget.LB)},{B(PadTarget.RB)},{lt},{rt},{B(PadTarget.Back)},{B(PadTarget.Start)},{B(PadTarget.LS)},{B(PadTarget.RS)},{B(PadTarget.DUp)},{B(PadTarget.DDown)},{B(PadTarget.DLeft)},{B(PadTarget.DRight)},{B(PadTarget.Guide)}]}}";
    }
}

public sealed class MacEngine : IDisposable
{
    public bool Running { get; private set; }
    public bool Locked { get; private set; }
    public bool Active => Running && Locked;
    public string Error { get; private set; }
    public event Action StateChanged;
    public volatile Action<string> Capture;

    Profile _profile;
    readonly object _lock = new();

    record struct CBind(int Code, Binding B);
    CBind[] _binds = Array.Empty<CBind>();
    bool[] _mapped = new bool[InputNames.Max];

    readonly bool[] _down = new bool[InputNames.Max];
    readonly bool[] _rawDown = new bool[InputNames.Max];
    readonly int[] _pressCount = new int[InputNames.Max];
    readonly int[] _tapCount = new int[InputNames.Max];
    readonly int[] _doubleTapCount = new int[InputNames.Max];
    readonly long[] _downTick = new long[InputNames.Max];
    readonly bool[] _isDoubleTapHold = new bool[InputNames.Max];
    long _wheelUpUntil, _wheelDownUntil;
    int _accX, _accY;

    Thread _tapThread, _tickThread, _httpThread;
    IntPtr _eventTap, _runLoop;
    MacNative.CGEventTapCallBack _tapCallback;
    HttpListener _http;
    volatile bool _stop;

    public PadSnapshot Snapshot;

    public bool IsRawDown(int code) => code > 0 && code < InputNames.Max && (_rawDown[code] || _down[code]);

    public static bool IsAccessibilityTrusted()
    {
        if (!OperatingSystem.IsMacOS()) return true;
        try { return MacNative.AXIsProcessTrusted(); } catch { return false; }
    }

    public MacEngine()
    {
        EnsureBrowserExtensionInstalled();
        StartLocalGamepadServer();
        if (OperatingSystem.IsMacOS())
        {
            _tapCallback = EventTapCallback;
            _tapThread = new Thread(TapLoop) { IsBackground = true, Name = "re-B.U.N.S macOS CGEventTap", Priority = ThreadPriority.Highest };
            _tapThread.Start();
        }
    }

    public void SetProfile(Profile p)
    {
        lock (_lock)
        {
            _profile = p;
            var list = new List<CBind>();
            var mapped = new bool[InputNames.Max];
            foreach (var b in p.Bindings)
            {
                int c = InputNames.ToCode(b.Input);
                if (c <= 0 || b.Target == PadTarget.None) continue;
                list.Add(new CBind(c, b));
                mapped[c] = true;
            }
            _binds = list.ToArray();
            _mapped = mapped;
            _toggleState = new bool[_binds.Length];
            _lastPress = new int[_binds.Length];
            _lastTap = new int[_binds.Length];
            _lastDoubleTap = new int[_binds.Length];
            _pulseUntil = new long[_binds.Length];
        }
    }

    public bool Start()
    {
        if (Running) return true;
        if (OperatingSystem.IsMacOS() && !IsAccessibilityTrusted())
        {
            Error = "macOS Accessibility permission is required to capture keyboard & mouse events.\nOpen System Settings → Privacy & Security → Accessibility and enable re-B.U.N.S.";
            StateChanged?.Invoke();
            return false;
        }
        Error = null;
        _stop = false;
        Running = true;
        SetLocked(true);
        _tickThread = new Thread(TickLoop) { IsBackground = true, Name = "re-B.U.N.S macOS Output", Priority = ThreadPriority.AboveNormal };
        _tickThread.Start();
        StateChanged?.Invoke();
        return true;
    }

    public void Stop()
    {
        if (!Running) return;
        _stop = true;
        _tickThread?.Join(500);
        SetLocked(false);
        Running = false;
        StateChanged?.Invoke();
    }

    void SetLocked(bool v)
    {
        if (v && !Running) v = false;
        if (Locked == v) return;
        Interlocked.Exchange(ref _accX, 0);
        Interlocked.Exchange(ref _accY, 0);
        Array.Clear(_down);
        Locked = v;
        if (OperatingSystem.IsMacOS())
        {
            try { MacNative.CGAssociateMouseAndMouseCursorPosition(!v); } catch { }
        }
        StateChanged?.Invoke();
    }

    public void SimulateInputForTest(int code, bool down, int mouseDx = 0, int mouseDy = 0)
    {
        if (down) RecordKeyDown(code); else RecordKeyUp(code);
        Interlocked.Add(ref _accX, mouseDx);
        Interlocked.Add(ref _accY, mouseDy);
    }

    void RecordKeyDown(int code)
    {
        long now = Environment.TickCount64;
        if (!_down[code])
        {
            bool dbl = (now - _downTick[code]) <= 280;
            _downTick[code] = now;
            _down[code] = true;
            _pressCount[code]++;
            _isDoubleTapHold[code] = dbl;
            if (dbl) _doubleTapCount[code]++;
        }
    }

    void RecordKeyUp(int code)
    {
        long now = Environment.TickCount64;
        if (_down[code] && (now - _downTick[code] <= 220))
            _tapCount[code]++;
        _down[code] = false;
        _isDoubleTapHold[code] = false;
    }

    void TapLoop()
    {
        try
        {
            ulong mask = (1UL << MacNative.kCGEventKeyDown) | (1UL << MacNative.kCGEventKeyUp) |
                         (1UL << MacNative.kCGEventFlagsChanged) | (1UL << MacNative.kCGEventMouseMoved) |
                         (1UL << MacNative.kCGEventLeftMouseDown) | (1UL << MacNative.kCGEventLeftMouseUp) |
                         (1UL << MacNative.kCGEventRightMouseDown) | (1UL << MacNative.kCGEventRightMouseUp) |
                         (1UL << MacNative.kCGEventOtherMouseDown) | (1UL << MacNative.kCGEventOtherMouseUp) |
                         (1UL << MacNative.kCGEventLeftMouseDragged) | (1UL << MacNative.kCGEventRightMouseDragged) |
                         (1UL << MacNative.kCGEventScrollWheel);

            _eventTap = MacNative.CGEventTapCreate(
                MacNative.kCGHIDEventTap,
                MacNative.kCGHeadInsertEventTap,
                MacNative.kCGEventTapOptionDefault,
                mask,
                _tapCallback,
                IntPtr.Zero);

            if (_eventTap == IntPtr.Zero) return;
            var src = MacNative.CFMachPortCreateRunLoopSource(IntPtr.Zero, _eventTap, 0);
            _runLoop = MacNative.CFRunLoopGetCurrent();
            // kCFRunLoopCommonModes
            MacNative.CGEventTapEnable(_eventTap, true);
            MacNative.CFRunLoopRun();
        }
        catch { }
    }

    bool _cmdHeld;
    IntPtr EventTapCallback(IntPtr proxy, int type, IntPtr cgEvent, IntPtr userInfo)
    {
        if (type == MacNative.kCGEventTapDisabledByTimeout)
        {
            if (_eventTap != IntPtr.Zero) MacNative.CGEventTapEnable(_eventTap, true);
            return cgEvent;
        }

        if (type is MacNative.kCGEventKeyDown or MacNative.kCGEventKeyUp or MacNative.kCGEventFlagsChanged)
        {
            int macKey = (int)MacNative.CGEventGetIntegerValueField(cgEvent, MacNative.kCGKeyboardEventKeycode);
            int code = MacNative.MacKeyCodeToVk(macKey);
            if (code == 0) return cgEvent;

            bool isDown = type == MacNative.kCGEventKeyDown;
            if (type == MacNative.kCGEventFlagsChanged)
            {
                ulong flags = MacNative.CGEventGetFlags(cgEvent);
                isDown = code switch
                {
                    0x5B => (flags & 0x100000) != 0, // Command
                    0xA0 => (flags & 0x020000) != 0, // Shift
                    0xA2 => (flags & 0x040000) != 0, // Control
                    0xA4 => (flags & 0x080000) != 0, // Option
                    _ => !_rawDown[code]
                };
            }
            _rawDown[code] = isDown;

            if (isDown && Capture is { } cap)
            {
                Capture = null;
                cap(InputNames.ToName(code));
                return IntPtr.Zero;
            }

            // Control + Option + Delete (or Backspace) stops the virtual pad completely
            if (Running && isDown && _rawDown[0xA2] && _rawDown[0xA4] && (code == 0x08 || code == 0x2E))
            {
                ThreadPool.QueueUserWorkItem(_ => Stop());
                return cgEvent;
            }

            // Command key (⌘) = Pause / Play AND pass through to macOS
            if (Running && code == 0x5B)
            {
                var mode = _profile?.UnlockMode ?? UnlockMode.Toggle;
                if (isDown && !_cmdHeld)
                {
                    _cmdHeld = true;
                    if (mode == UnlockMode.Toggle) SetLocked(!Locked); else SetLocked(false);
                }
                else if (!isDown)
                {
                    _cmdHeld = false;
                    if (mode == UnlockMode.Hold) SetLocked(true);
                }
                return cgEvent;
            }

            if (Active && _mapped[code])
            {
                if (isDown) RecordKeyDown(code); else RecordKeyUp(code);
                return IntPtr.Zero;
            }
            return cgEvent;
        }

        if (type is MacNative.kCGEventMouseMoved or MacNative.kCGEventLeftMouseDragged or MacNative.kCGEventRightMouseDragged or MacNative.kCGEventOtherMouseDragged)
        {
            if (Active)
            {
                int dx = (int)MacNative.CGEventGetIntegerValueField(cgEvent, MacNative.kCGMouseEventDeltaX);
                int dy = (int)MacNative.CGEventGetIntegerValueField(cgEvent, MacNative.kCGMouseEventDeltaY);
                Interlocked.Add(ref _accX, dx);
                Interlocked.Add(ref _accY, dy);
            }
            return cgEvent;
        }

        int mCode = 0; bool? mDown = null;
        switch (type)
        {
            case MacNative.kCGEventLeftMouseDown: mCode = 1; mDown = true; break;
            case MacNative.kCGEventLeftMouseUp: mCode = 1; mDown = false; break;
            case MacNative.kCGEventRightMouseDown: mCode = 2; mDown = true; break;
            case MacNative.kCGEventRightMouseUp: mCode = 2; mDown = false; break;
            case MacNative.kCGEventOtherMouseDown:
                long bNum = MacNative.CGEventGetIntegerValueField(cgEvent, MacNative.kCGMouseEventButtonNumber);
                mCode = bNum == 2 ? 4 : (bNum == 3 ? 5 : 6); mDown = true; break;
            case MacNative.kCGEventOtherMouseUp:
                long bNumUp = MacNative.CGEventGetIntegerValueField(cgEvent, MacNative.kCGMouseEventButtonNumber);
                mCode = bNumUp == 2 ? 4 : (bNumUp == 3 ? 5 : 6); mDown = false; break;
            case MacNative.kCGEventScrollWheel:
                long dyWheel = MacNative.CGEventGetIntegerValueField(cgEvent, MacNative.kCGScrollWheelEventDeltaAxis1);
                if (dyWheel != 0) { mCode = dyWheel > 0 ? InputNames.WheelUp : InputNames.WheelDown; mDown = true; }
                break;
        }

        if (mCode is > 0 and < 0x100 && mDown.HasValue) _rawDown[mCode] = mDown.Value;
        if (mDown == true && mCode != 0 && Capture is { } mCap)
        {
            Capture = null;
            mCap(InputNames.ToName(mCode));
            return IntPtr.Zero;
        }
        if (Active && mCode != 0 && _mapped[mCode])
        {
            long now = Environment.TickCount64;
            if (mCode == InputNames.WheelUp) { _wheelUpUntil = now + 70; _pressCount[mCode]++; }
            else if (mCode == InputNames.WheelDown) { _wheelDownUntil = now + 70; _pressCount[mCode]++; }
            else if (mDown == true) RecordKeyDown(mCode);
            else if (mDown == false) RecordKeyUp(mCode);
            return IntPtr.Zero;
        }
        return cgEvent;
    }

    bool[] _toggleState = Array.Empty<bool>();
    int[] _lastPress = Array.Empty<int>();
    int[] _lastTap = Array.Empty<int>();
    int[] _lastDoubleTap = Array.Empty<int>();
    long[] _pulseUntil = Array.Empty<long>();

    public string CurrentScenario { get; set; } = "On Foot";
    long _lastMoveTick;

    public void StepTick(double dtMs)
    {
        long tick = Environment.TickCount64;
        Profile p; CBind[] binds;
        lock (_lock) { p = _profile; binds = _binds; }
        if (p == null) return;

        uint btnMask = 0;
        double lt = 0, rt = 0, lx = 0, ly = 0, rx = 0, ry = 0;

        bool active = Active;
        _down[InputNames.WheelUp] = _rawDown[InputNames.WheelUp] = tick < _wheelUpUntil;
        _down[InputNames.WheelDown] = _rawDown[InputNames.WheelDown] = tick < _wheelDownUntil;

        if (active)
        {
            for (int i = 0; i < binds.Length; i++)
            {
                var (code, b) = binds[i];
                if (!string.IsNullOrEmpty(b.Scenario) &&
                    !b.Scenario.Equals("All", StringComparison.OrdinalIgnoreCase) &&
                    !b.Scenario.Equals(CurrentScenario, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool newEdge = false, heldSignal = false;
                switch (b.Activator)
                {
                    case KeyActivator.Regular:
                        heldSignal = _down[code];
                        if (_pressCount[code] != _lastPress[i]) { _lastPress[i] = _pressCount[code]; newEdge = true; }
                        break;
                    case KeyActivator.SingleTap:
                        if (_tapCount[code] != _lastTap[i]) { _lastTap[i] = _tapCount[code]; newEdge = true; }
                        break;
                    case KeyActivator.LongHold:
                        heldSignal = _down[code] && (tick - _downTick[code]) >= 260;
                        if (heldSignal && _pressCount[code] != _lastPress[i]) { _lastPress[i] = _pressCount[code]; newEdge = true; }
                        break;
                    case KeyActivator.DoubleTap:
                        heldSignal = _down[code] && _isDoubleTapHold[code];
                        if (_doubleTapCount[code] != _lastDoubleTap[i]) { _lastDoubleTap[i] = _doubleTapCount[code]; newEdge = true; }
                        break;
                }

                bool on = false;
                switch (b.Mode)
                {
                    case BindMode.Hold:
                        if (b.Activator == KeyActivator.SingleTap && newEdge) _pulseUntil[i] = tick + 90;
                        else if (b.Activator == KeyActivator.DoubleTap && newEdge) _pulseUntil[i] = tick + 140;
                        on = heldSignal || tick < _pulseUntil[i];
                        break;
                    case BindMode.TapPulse:
                        if (newEdge) _pulseUntil[i] = tick + 90;
                        on = tick < _pulseUntil[i];
                        break;
                    case BindMode.HoldPulse:
                        if (newEdge) _pulseUntil[i] = tick + 650;
                        on = tick < _pulseUntil[i];
                        break;
                    case BindMode.Toggle:
                        if (newEdge) _toggleState[i] = !_toggleState[i];
                        on = _toggleState[i];
                        break;
                    case BindMode.Turbo:
                        on = heldSignal && (tick / Math.Max(10, b.TurboMs)) % 2 == 0;
                        break;
                }
                if (!on) continue;
                double a = Math.Clamp(b.Amount, 0, 100) / 100.0;
                switch (b.Target)
                {
                    case PadTarget.LT: lt = Math.Max(lt, a); break;
                    case PadTarget.RT: rt = Math.Max(rt, a); break;
                    case PadTarget.LStickUp: ly += a; break;
                    case PadTarget.LStickDown: ly -= a; break;
                    case PadTarget.LStickLeft: lx -= a; break;
                    case PadTarget.LStickRight: lx += a; break;
                    case PadTarget.RStickUp: ry += a; break;
                    case PadTarget.RStickDown: ry -= a; break;
                    case PadTarget.RStickLeft: rx -= a; break;
                    case PadTarget.RStickRight: rx += a; break;
                    default: btnMask |= 1u << (int)b.Target; break;
                }
            }
            Circle(ref lx, ref ly);
            Circle(ref rx, ref ry);
        }

        int dx = Interlocked.Exchange(ref _accX, 0), dy = Interlocked.Exchange(ref _accY, 0);
        var ms = p.Mouse;
        if (dx != 0 || dy != 0) _lastMoveTick = tick;
        if (active && ms.Stick != MouseStick.None)
        {
            double tau = Math.Clamp(ms.SmoothingMs, 2.0, 40.0);
            double decay = Math.Exp(-dtMs / tau);
            _vx = _vx * decay + (dx / tau);
            _vy = _vy * decay + (dy / tau);

            if (dx == 0 && dy == 0 && (tick - _lastMoveTick) > Math.Max(14, tau * 1.4))
            {
                _vx *= 0.45;
                _vy *= 0.45;
                if (Math.Abs(_vx) < 0.015) _vx = 0;
                if (Math.Abs(_vy) < 0.015) _vy = 0;
            }

            double ix = _vx * ms.SensitivityX / 60.0, iy = -_vy * ms.SensitivityY / 60.0;
            if (ms.InvertY) iy = -iy;
            double mag = Math.Sqrt(ix * ix + iy * iy);
            if (mag > 0.004)
            {
                double c = Math.Min(1, Math.Pow(Math.Min(mag, 1), Math.Max(0.1, ms.Curve)));
                double ad = Math.Clamp(ms.AntiDeadzone, 0, 95) / 100.0;
                double outMag = Math.Min(1, ad + (1 - ad) * c);
                double ox = ix / mag * outMag, oy = iy / mag * outMag;
                if (ms.Stick == MouseStick.Right) { rx = Math.Clamp(rx + ox, -1, 1); ry = Math.Clamp(ry + oy, -1, 1); }
                else { lx = Math.Clamp(lx + ox, -1, 1); ly = Math.Clamp(ly + oy, -1, 1); }
            }
        }
        else { _vx = _vy = 0; }

        Snapshot = new PadSnapshot
        {
            ButtonsMask = btnMask,
            LT = (byte)Math.Round(Math.Clamp(lt, 0, 1) * 255),
            RT = (byte)Math.Round(Math.Clamp(rt, 0, 1) * 255),
            LX = (short)Math.Clamp(Math.Round(lx * 32767), -32768, 32767),
            LY = (short)Math.Clamp(Math.Round(ly * 32767), -32768, 32767),
            RX = (short)Math.Clamp(Math.Round(rx * 32767), -32768, 32767),
            RY = (short)Math.Clamp(Math.Round(ry * 32767), -32768, 32767)
        };
    }
    double _vx, _vy;

    void TickLoop()
    {
        var sw = Stopwatch.StartNew();
        double last = 0;
        while (!_stop)
        {
            Thread.Sleep(1);
            double now = sw.Elapsed.TotalMilliseconds;
            double dt = Math.Max(0.25, now - last);
            last = now;
            StepTick(dt);
        }
        Snapshot = default;
    }

    static void Circle(ref double x, ref double y)
    {
        double m = Math.Sqrt(x * x + y * y);
        if (m > 1) { x /= m; y /= m; }
    }

    // ================= Local Virtual Xbox 360 W3C Gamepad Bridge for macOS Cloud Gaming / Browsers =================
    public static string BrowserExtensionDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "re-B.U.N.S", "VirtualPadExtension");

    public static void EnsureBrowserExtensionInstalled()
    {
        try
        {
            string dir = BrowserExtensionDir;
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), """
            {
              "manifest_version": 3,
              "name": "re-B.U.N.S Virtual Xbox 360 Controller Bridge",
              "version": "1.6.4",
              "description": "Injects the re-B.U.N.S virtual Xbox 360 controller into Xbox Cloud Gaming, GeForce NOW, and web apps on macOS.",
              "content_scripts": [
                {
                  "matches": ["<all_urls>"],
                  "js": ["inject.js"],
                  "run_at": "document_start",
                  "world": "MAIN"
                }
              ]
            }
            """);
            File.WriteAllText(Path.Combine(dir, "inject.js"), """
            (() => {
              let state = { axes: [0, 0, 0, 0], buttons: new Array(17).fill(0) };
              let connected = false;
              async function poll() {
                try {
                  const r = await fetch("http://127.0.0.1:27654/pad");
                  state = await r.json();
                  if (!connected) {
                    connected = true;
                    window.dispatchEvent(new Event("gamepadconnected"));
                  }
                } catch {}
                setTimeout(poll, 4);
              }
              poll();
              navigator.getGamepads = function() {
                const pad = {
                  id: "Xbox 360 Controller (XInput STANDARD GAMEPAD)",
                  index: 0,
                  connected: true,
                  timestamp: performance.now(),
                  mapping: "standard",
                  axes: state.axes,
                  buttons: state.buttons.map(v => ({ pressed: v > 0.1, touched: v > 0, value: v }))
                };
                return [pad, null, null, null];
              };
            })();
            """);
        }
        catch { }
    }

    public static string GetEmulatorHtml()
    {
        try
        {
            using var s = typeof(MacEngine).Assembly.GetManifestResourceStream("emulator.html");
            if (s != null)
            {
                using var reader = new StreamReader(s, Encoding.UTF8);
                return reader.ReadToEnd();
            }
        }
        catch { }
        return "<!DOCTYPE html><html><body><h1>re-B.U.N.S Virtual Xbox 360 Controller Emulator</h1></body></html>";
    }

    void StartLocalGamepadServer()
    {
        _httpThread = new Thread(() =>
        {
            try
            {
                _http = new HttpListener();
                _http.Prefixes.Add("http://127.0.0.1:27654/");
                _http.Start();
                while (_http.IsListening)
                {
                    var ctx = _http.GetContext();
                    ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
                    string path = ctx.Request.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";
                    if (path == "/pad")
                    {
                        byte[] body = Encoding.UTF8.GetBytes(Snapshot.ToW3CGamepadJson());
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.ContentLength64 = body.Length;
                        ctx.Response.OutputStream.Write(body, 0, body.Length);
                        ctx.Response.OutputStream.Close();
                    }
                    else
                    {
                        byte[] html = Encoding.UTF8.GetBytes(GetEmulatorHtml());
                        ctx.Response.ContentType = "text/html; charset=utf-8";
                        ctx.Response.ContentLength64 = html.Length;
                        ctx.Response.OutputStream.Write(html, 0, html.Length);
                        ctx.Response.OutputStream.Close();
                    }
                }
            }
            catch { }
        }) { IsBackground = true, Name = "re-B.U.N.S Local Gamepad HTTP Bridge" };
        _httpThread.Start();
    }

    public static void LaunchCloudGamingBrowser()
    {
        EnsureBrowserExtensionInstalled();
        string ext = BrowserExtensionDir;
        string[] macBrowsers =
        {
            "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
            "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser",
            "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"
        };
        foreach (var b in macBrowsers)
        {
            if (File.Exists(b))
            {
                Process.Start(new ProcessStartInfo(b, $"--load-extension=\"{ext}\" \"https://www.xbox.com/play\"") { UseShellExecute = false });
                return;
            }
        }
        Process.Start(new ProcessStartInfo("open", "https://www.xbox.com/play") { UseShellExecute = true });
    }

    public void Dispose()
    {
        Stop();
        try { _http?.Stop(); } catch { }
        if (OperatingSystem.IsMacOS() && _runLoop != IntPtr.Zero)
        {
            try { MacNative.CFRunLoopStop(_runLoop); } catch { }
        }
    }
}
