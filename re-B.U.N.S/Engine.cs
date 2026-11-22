using System.Diagnostics;
using System.Windows.Forms;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client.Targets.DualShock4;

namespace ReBuns;

public static class InputNames
{
    public const int WheelUp = 0x1001, WheelDown = 0x1002, Max = 0x1100;

    public static int ToCode(string name)
    {
        switch (name)
        {
            case "Mouse1": return 1;
            case "Mouse2": return 2;
            case "Mouse3": return 4;
            case "Mouse4": return 5;
            case "Mouse5": return 6;
            case "WheelUp": return WheelUp;
            case "WheelDown": return WheelDown;
        }
        return Enum.TryParse<Keys>(name, true, out var k) ? (int)k & 0xFF : 0;
    }

    public static string ToName(int code) => code switch
    {
        1 => "Mouse1", 2 => "Mouse2", 4 => "Mouse3", 5 => "Mouse4", 6 => "Mouse5",
        WheelUp => "WheelUp", WheelDown => "WheelDown",
        13 => "Return",
        _ => ((Keys)code).ToString()
    };

    public static string Pretty(string name) => name switch
    {
        "Mouse1" => "L-Click", "Mouse2" => "R-Click", "Mouse3" => "M-Click",
        "Mouse4" => "Mouse 4", "Mouse5" => "Mouse 5", "WheelUp" => "Wheel ↑", "WheelDown" => "Wheel ↓",
        "LShiftKey" => "L-Shift", "RShiftKey" => "R-Shift", "LControlKey" => "L-Ctrl", "RControlKey" => "R-Ctrl",
        "LMenu" => "L-Alt", "RMenu" => "R-Alt", "Return" => "Enter", "Back" => "Backspace", "Oemcomma" => ",",
        "OemPeriod" => ".", "OemMinus" => "-", "Oemplus" => "=", "OemQuestion" => "/", "OemSemicolon" => ";",
        "OemQuotes" => "'", "OemOpenBrackets" => "[", "OemCloseBrackets" => "]", "OemPipe" => "\\", "Oemtilde" => "`",
        "Capital" => "Caps", "Escape" => "Esc", "LWin" => "Win", "RWin" => "R-Win",
        _ => name.StartsWith("D") && name.Length == 2 && char.IsDigit(name[1]) ? name[1..] : name
    };

    public static string ShortTarget(PadTarget t, ControllerType type = ControllerType.Xbox)
    {
        if (type == ControllerType.PlayStation)
        {
            return t switch
            {
                PadTarget.A => "✕",
                PadTarget.B => "○",
                PadTarget.X => "□",
                PadTarget.Y => "△",
                PadTarget.LB => "L1",
                PadTarget.RB => "R1",
                PadTarget.LT => "L2",
                PadTarget.RT => "R2",
                PadTarget.LS => "L3",
                PadTarget.RS => "R3",
                PadTarget.Back => "Share",
                PadTarget.Start => "Opt",
                PadTarget.Guide => "PS",
                PadTarget.LStickUp => "L3↑", PadTarget.LStickDown => "L3↓", PadTarget.LStickLeft => "L3←", PadTarget.LStickRight => "L3→",
                PadTarget.RStickUp => "R3↑", PadTarget.RStickDown => "R3↓", PadTarget.RStickLeft => "R3←", PadTarget.RStickRight => "R3→",
                PadTarget.DUp => "D↑", PadTarget.DDown => "D↓", PadTarget.DLeft => "D←", PadTarget.DRight => "D→",
                _ => t.ToString()
            };
        }
        return t switch
        {
            PadTarget.LStickUp => "LS↑", PadTarget.LStickDown => "LS↓", PadTarget.LStickLeft => "LS←", PadTarget.LStickRight => "LS→",
            PadTarget.RStickUp => "RS↑", PadTarget.RStickDown => "RS↓", PadTarget.RStickLeft => "RS←", PadTarget.RStickRight => "RS→",
            PadTarget.DUp => "D↑", PadTarget.DDown => "D↓", PadTarget.DLeft => "D←", PadTarget.DRight => "D→",
            _ => t.ToString()
        };
    }
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
}

public sealed class Engine : IDisposable
{
    public bool Running { get; private set; }
    public bool Locked { get; private set; }
    public bool ProcessOk { get; private set; } = true;
    public bool Active => Running && Locked && ProcessOk;
    public string Error { get; private set; }
    public string ForegroundProcess { get; private set; } = "";
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
    readonly bool[] _swallowedDown = new bool[InputNames.Max];
    long _wheelUpUntil, _wheelDownUntil;
    int _accX, _accY;
    Native.POINT _anchor;

    Thread _hookThread, _tickThread;
    uint _hookThreadId;
    IntPtr _kbHook, _msHook;
    readonly Native.HookProc _kbProc, _msProc;
    volatile bool _stop;

    ViGEmClient _client;
    IXbox360Controller _xboxPad;
    IDualShock4Controller _ds4Pad;
    public ControllerType ControllerType { get; set; } = ControllerType.Xbox;

    public PadSnapshot Snapshot;

    public bool IsRawDown(int code) => code > 0 && code < InputNames.Max && (_rawDown[code] || _down[code]);

    public Engine()
    {
        _kbProc = KbHook;
        _msProc = MsHook;
        _hookThread = new Thread(HookLoop) { IsBackground = true, Name = "re-B.U.N.S hooks", Priority = ThreadPriority.Highest };
        _hookThread.Start();
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
            for (int i = 0; i < _binds.Length; i++)
            {
                int c = _binds[i].Code;
                _lastPress[i] = _pressCount[c];
                _lastTap[i] = _tapCount[c];
                _lastDoubleTap[i] = _doubleTapCount[c];
            }
        }
    }

    public bool Start()
    {
        if (Running) return true;
        try
        {
            _client ??= new ViGEmClient();
            if (ControllerType == ControllerType.PlayStation)
            {
                _ds4Pad = _client.CreateDualShock4Controller();
                _ds4Pad.AutoSubmitReport = false;
                _ds4Pad.Connect();
            }
            else
            {
                _xboxPad = _client.CreateXbox360Controller();
                _xboxPad.AutoSubmitReport = false;
                _xboxPad.Connect();
            }
            Error = null;
        }
        catch (Exception ex)
        {
            Error = "ViGEmBus driver not available: " + ex.Message;
            StateChanged?.Invoke();
            return false;
        }
        _stop = false;
        Running = true;
        SetLocked(true);
        _tickThread = new Thread(TickLoop) { IsBackground = true, Name = "re-B.U.N.S output", Priority = ThreadPriority.AboveNormal };
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
        try { _xboxPad?.Disconnect(); } catch { }
        try { _ds4Pad?.Disconnect(); } catch { }
        _xboxPad = null;
        _ds4Pad = null;
        StateChanged?.Invoke();
    }

    public void ToggleLock() => SetLocked(!Locked);

    void SetLocked(bool v)
    {
        if (v && !Running) v = false;
        if (Locked == v) return;
        Native.GetCursorPos(out _anchor);
        Interlocked.Exchange(ref _accX, 0);
        Interlocked.Exchange(ref _accY, 0);
        Array.Clear(_down);
        Array.Clear(_isDoubleTapHold);
        Locked = v;
        StateChanged?.Invoke();
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
        if (_down[code])
        {
            if (now - _downTick[code] <= 220)
                _tapCount[code]++;
        }
        _down[code] = false;
        _isDoubleTapHold[code] = false;
    }

    void HookLoop()
    {
        _hookThreadId = Native.GetCurrentThreadId();
        var mod = Native.GetModuleHandle(null);
        _kbHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _kbProc, mod, 0);
        _msHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _msProc, mod, 0);
        while (Native.GetMessage(out _, IntPtr.Zero, 0, 0) > 0) { }
        Native.UnhookWindowsHookEx(_kbHook);
        Native.UnhookWindowsHookEx(_msHook);
    }

    unsafe IntPtr KbHook(int n, IntPtr w, IntPtr l)
    {
        if (n < 0) return Native.CallNextHookEx(_kbHook, n, w, l);
        var k = *(Native.KBDLLHOOKSTRUCT*)l;
        if ((k.flags & Native.LLKHF_INJECTED) != 0) return Native.CallNextHookEx(_kbHook, n, w, l);
        int msg = (int)w;
        bool isDown = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
        int code = (int)k.vkCode & 0xFF;
        _rawDown[code] = isDown;

        if (isDown && Capture is { } cap)
        {
            Capture = null;
            _swallowedDown[code] = true;
            cap(InputNames.ToName(code));
            return (IntPtr)1;
        }

        // Ctrl + Alt + Delete (or Ctrl + Alt + Backspace) stops the virtual pad completely
        bool ctrlDown = _rawDown[0xA2] || _rawDown[0xA3] || _rawDown[0x11];
        bool altDown = _rawDown[0xA4] || _rawDown[0xA5] || _rawDown[0x12];
        if (Running && isDown && ctrlDown && altDown && (code == 0x2E || code == 0x08))
        {
            ThreadPool.QueueUserWorkItem(_ => Stop());
            return Native.CallNextHookEx(_kbHook, n, w, l);
        }

        // Windows key = Pause / Play virtual pad controls (swallowed while running so it doesn't open Start Menu or steal focus from play.xbox.com!)
        if (Running && ProcessOk && (code == 0x5B || code == 0x5C))
        {
            var mode = _profile?.UnlockMode ?? UnlockMode.Toggle;
            if (isDown && !_winHeld)
            {
                _winHeld = true;
                if (mode == UnlockMode.Toggle) SetLocked(!Locked); else SetLocked(false);
            }
            else if (!isDown)
            {
                _winHeld = false;
                if (mode == UnlockMode.Hold) SetLocked(true);
            }
            return (IntPtr)1;
        }

        if (!isDown && _swallowedDown[code])
        {
            _swallowedDown[code] = false;
            RecordKeyUp(code);
            return (IntPtr)1;
        }

        if (Active)
        {
            bool mapped = _mapped[code];
            if (isDown)
            {
                if (mapped) RecordKeyDown(code);
                if (mapped || (_profile?.BlockUnmappedKeys ?? false))
                {
                    _swallowedDown[code] = true;
                    return (IntPtr)1;
                }
            }
        }
        return Native.CallNextHookEx(_kbHook, n, w, l);
    }
    bool _winHeld;

    unsafe IntPtr MsHook(int n, IntPtr w, IntPtr l)
    {
        if (n < 0) return Native.CallNextHookEx(_msHook, n, w, l);
        var m = *(Native.MSLLHOOKSTRUCT*)l;
        if ((m.flags & Native.LLMHF_INJECTED) != 0) return Native.CallNextHookEx(_msHook, n, w, l);
        int msg = (int)w;
        int code = 0; bool? down = null;
        switch (msg)
        {
            case Native.WM_LBUTTONDOWN: code = 1; down = true; break;
            case Native.WM_LBUTTONUP: code = 1; down = false; break;
            case Native.WM_RBUTTONDOWN: code = 2; down = true; break;
            case Native.WM_RBUTTONUP: code = 2; down = false; break;
            case Native.WM_MBUTTONDOWN: code = 4; down = true; break;
            case Native.WM_MBUTTONUP: code = 4; down = false; break;
            case Native.WM_XBUTTONDOWN: code = (m.mouseData >> 16) == 1 ? 5 : 6; down = true; break;
            case Native.WM_XBUTTONUP: code = (m.mouseData >> 16) == 1 ? 5 : 6; down = false; break;
            case Native.WM_MOUSEWHEEL: code = (short)(m.mouseData >> 16) > 0 ? InputNames.WheelUp : InputNames.WheelDown; down = true; break;
        }
        if (code is > 0 and < 0x100 && down.HasValue)
            _rawDown[code] = down.Value;

        if (down == true && code != 0 && Capture is { } cap)
        {
            Capture = null;
            if (code < 0x100) _swallowedDown[code] = true;
            cap(InputNames.ToName(code));
            return (IntPtr)1;
        }

        if (down == false && _swallowedDown[code])
        {
            _swallowedDown[code] = false;
            RecordKeyUp(code);
            return (IntPtr)1;
        }

        if (!Active) return Native.CallNextHookEx(_msHook, n, w, l);

        if (msg == Native.WM_MOUSEMOVE)
        {
            Interlocked.Add(ref _accX, m.pt.X - _anchor.X);
            Interlocked.Add(ref _accY, m.pt.Y - _anchor.Y);
            return (IntPtr)1;
        }
        if (code != 0 && down == true && _mapped[code])
        {
            long now = Environment.TickCount64;
            if (code == InputNames.WheelUp) { _wheelUpUntil = now + 70; _pressCount[code]++; }
            else if (code == InputNames.WheelDown) { _wheelDownUntil = now + 70; _pressCount[code]++; }
            else { RecordKeyDown(code); _swallowedDown[code] = true; }
            return (IntPtr)1;
        }
        return Native.CallNextHookEx(_msHook, n, w, l);
    }

    bool[] _toggleState = Array.Empty<bool>();
    int[] _lastPress = Array.Empty<int>();
    int[] _lastTap = Array.Empty<int>();
    int[] _lastDoubleTap = Array.Empty<int>();
    long[] _pulseUntil = Array.Empty<long>();

    void TickLoop()
    {
        Native.timeBeginPeriod(1);
        var sw = Stopwatch.StartNew();
        double last = 0, vx = 0, vy = 0;
        long nextProcCheck = 0;
        try
        {
            while (!_stop)
            {
                Thread.Sleep(1);
                double now = sw.Elapsed.TotalMilliseconds;
                double dt = Math.Max(0.25, now - last);
                last = now;
                long tick = Environment.TickCount64;

                if (tick >= nextProcCheck) { nextProcCheck = tick + 250; CheckProcess(); }

                Profile p; CBind[] binds;
                lock (_lock) { p = _profile; binds = _binds; }
                if (p == null) continue;

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
                        bool newEdge = false;
                        bool heldSignal = false;

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
                double alpha = dt / (Math.Max(0, ms.SmoothingMs) + dt);
                vx += alpha * (dx / dt - vx);
                vy += alpha * (dy / dt - vy);
                if (active && ms.Stick != MouseStick.None)
                {
                    double ix = vx * ms.SensitivityX / 100.0, iy = -vy * ms.SensitivityY / 100.0;
                    if (ms.InvertY) iy = -iy;
                    double mag = Math.Sqrt(ix * ix + iy * iy);
                    if (mag > 0.004)
                    {
                        double c = Math.Min(1, Math.Pow(Math.Min(mag, 1), Math.Max(0.1, ms.Curve)));
                        double ad = Math.Clamp(ms.AntiDeadzone, 0, 95) / 100.0;
                        double outMag = Math.Min(1, ad + (1 - ad) * c);
                        double ox = ix / mag * outMag, oy = iy / mag * outMag;
                        if (ms.Stick == MouseStick.Right)
                        {
                            rx = Math.Clamp(rx + ox, -1, 1);
                            ry = Math.Clamp(ry + oy, -1, 1);
                        }
                        else
                        {
                            lx = Math.Clamp(lx + ox, -1, 1);
                            ly = Math.Clamp(ly + oy, -1, 1);
                        }
                    }
                }
                else { vx = vy = 0; }

                Submit(btnMask, lt, rt, lx, ly, rx, ry);
            }
        }
        finally
        {
            Submit(0, 0, 0, 0, 0, 0, 0);
            Native.timeEndPeriod(1);
        }
    }

    static void Circle(ref double x, ref double y)
    {
        double m = Math.Sqrt(x * x + y * y);
        if (m > 1) { x /= m; y /= m; }
    }

    static short Ax(double v) => (short)Math.Clamp(Math.Round(v * 32767), -32768, 32767);

    PadSnapshot _lastSent;
    bool _hasSent;
    void Submit(uint btnMask, double lt, double rt, double lx, double ly, double rx, double ry)
    {
        var s = new PadSnapshot
        {
            ButtonsMask = btnMask,
            LT = (byte)Math.Round(Math.Clamp(lt, 0, 1) * 255), RT = (byte)Math.Round(Math.Clamp(rt, 0, 1) * 255),
            LX = Ax(lx), LY = Ax(ly), RX = Ax(rx), RY = Ax(ry)
        };
        Snapshot = s;
        if (_xboxPad == null && _ds4Pad == null) return;
        if (_hasSent && s.ButtonsMask == _lastSent.ButtonsMask && s.LT == _lastSent.LT && s.RT == _lastSent.RT &&
            s.LX == _lastSent.LX && s.LY == _lastSent.LY && s.RX == _lastSent.RX && s.RY == _lastSent.RY) return;
        _lastSent = s;
        _hasSent = true;
        try
        {
            if (_ds4Pad != null)
            {
                void DsBtn(DualShock4Button b, PadTarget t) => _ds4Pad.SetButtonState(b, s.Has(t));
                DsBtn(DualShock4Button.Cross, PadTarget.A);
                DsBtn(DualShock4Button.Circle, PadTarget.B);
                DsBtn(DualShock4Button.Square, PadTarget.X);
                DsBtn(DualShock4Button.Triangle, PadTarget.Y);
                DsBtn(DualShock4Button.ShoulderLeft, PadTarget.LB);
                DsBtn(DualShock4Button.ShoulderRight, PadTarget.RB);
                DsBtn(DualShock4Button.TriggerLeft, PadTarget.LT);
                DsBtn(DualShock4Button.TriggerRight, PadTarget.RT);
                DsBtn(DualShock4Button.ThumbLeft, PadTarget.LS);
                DsBtn(DualShock4Button.ThumbRight, PadTarget.RS);
                DsBtn(DualShock4Button.Share, PadTarget.Back);
                DsBtn(DualShock4Button.Options, PadTarget.Start);

                byte spec = 0;
                if (s.Has(PadTarget.Guide)) spec |= 1; // PS Button
                if (s.Has(PadTarget.Back)) spec |= 2;  // Touchpad click
                _ds4Pad.SetSpecialButtonsFull(spec);

                bool dUp = s.Has(PadTarget.DUp), dDown = s.Has(PadTarget.DDown);
                bool dLeft = s.Has(PadTarget.DLeft), dRight = s.Has(PadTarget.DRight);
                DualShock4DPadDirection dir = DualShock4DPadDirection.None;
                if (dUp && dRight) dir = DualShock4DPadDirection.Northeast;
                else if (dUp && dLeft) dir = DualShock4DPadDirection.Northwest;
                else if (dDown && dRight) dir = DualShock4DPadDirection.Southeast;
                else if (dDown && dLeft) dir = DualShock4DPadDirection.Southwest;
                else if (dUp) dir = DualShock4DPadDirection.North;
                else if (dDown) dir = DualShock4DPadDirection.South;
                else if (dLeft) dir = DualShock4DPadDirection.West;
                else if (dRight) dir = DualShock4DPadDirection.East;
                _ds4Pad.SetDPadDirection(dir);

                _ds4Pad.SetSliderValue(DualShock4Slider.LeftTrigger, s.LT);
                _ds4Pad.SetSliderValue(DualShock4Slider.RightTrigger, s.RT);

                byte dsLx = (byte)Math.Clamp((s.LX + 32768) / 257, 0, 255);
                byte dsLy = (byte)Math.Clamp((-s.LY + 32768) / 257, 0, 255);
                byte dsRx = (byte)Math.Clamp((s.RX + 32768) / 257, 0, 255);
                byte dsRy = (byte)Math.Clamp((-s.RY + 32768) / 257, 0, 255);
                _ds4Pad.SetAxisValue(DualShock4Axis.LeftThumbX, dsLx);
                _ds4Pad.SetAxisValue(DualShock4Axis.LeftThumbY, dsLy);
                _ds4Pad.SetAxisValue(DualShock4Axis.RightThumbX, dsRx);
                _ds4Pad.SetAxisValue(DualShock4Axis.RightThumbY, dsRy);
                _ds4Pad.SubmitReport();
            }
            else if (_xboxPad != null)
            {
                void Btn(Xbox360Button b, PadTarget t) => _xboxPad.SetButtonState(b, s.Has(t));
                Btn(Xbox360Button.A, PadTarget.A); Btn(Xbox360Button.B, PadTarget.B);
                Btn(Xbox360Button.X, PadTarget.X); Btn(Xbox360Button.Y, PadTarget.Y);
                Btn(Xbox360Button.LeftShoulder, PadTarget.LB); Btn(Xbox360Button.RightShoulder, PadTarget.RB);
                Btn(Xbox360Button.Back, PadTarget.Back); Btn(Xbox360Button.Start, PadTarget.Start);
                Btn(Xbox360Button.Guide, PadTarget.Guide);
                Btn(Xbox360Button.LeftThumb, PadTarget.LS); Btn(Xbox360Button.RightThumb, PadTarget.RS);
                Btn(Xbox360Button.Up, PadTarget.DUp); Btn(Xbox360Button.Down, PadTarget.DDown);
                Btn(Xbox360Button.Left, PadTarget.DLeft); Btn(Xbox360Button.Right, PadTarget.DRight);
                _xboxPad.SetSliderValue(Xbox360Slider.LeftTrigger, s.LT);
                _xboxPad.SetSliderValue(Xbox360Slider.RightTrigger, s.RT);
                _xboxPad.SetAxisValue(Xbox360Axis.LeftThumbX, s.LX);
                _xboxPad.SetAxisValue(Xbox360Axis.LeftThumbY, s.LY);
                _xboxPad.SetAxisValue(Xbox360Axis.RightThumbX, s.RX);
                _xboxPad.SetAxisValue(Xbox360Axis.RightThumbY, s.RY);
                _xboxPad.SubmitReport();
            }
        }
        catch { }
    }

    void CheckProcess()
    {
        var p = _profile;
        string name = "";
        IntPtr fg = Native.GetForegroundWindow();
        // When user presses Ctrl+Alt+Delete, Windows switches to the Secure Desktop (Winlogon) and GetForegroundWindow() becomes IntPtr.Zero
        if (Running && fg == IntPtr.Zero)
        {
            ThreadPool.QueueUserWorkItem(_ => Stop());
            return;
        }
        try
        {
            Native.GetWindowThreadProcessId(fg, out var pid);
            using var proc = Process.GetProcessById((int)pid);
            name = proc.ProcessName + ".exe";
            if (Running && name.Equals("Taskmgr.exe", StringComparison.OrdinalIgnoreCase) && (_rawDown[0xA2] || _rawDown[0xA4]))
            {
                ThreadPool.QueueUserWorkItem(_ => Stop());
                return;
            }
        }
        catch { }
        ForegroundProcess = name;
        bool ok = p == null || !p.OnlyWhenProcessFocused || p.Processes.Count == 0 ||
                  p.Processes.Any(x => string.Equals(x.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (ok != ProcessOk)
        {
            ProcessOk = ok;
            if (!ok) Array.Clear(_down);
            StateChanged?.Invoke();
        }
    }

    public void Dispose()
    {
        Stop();
        if (_hookThreadId != 0) Native.PostThreadMessage(_hookThreadId, Native.WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _client?.Dispose();
    }
}
