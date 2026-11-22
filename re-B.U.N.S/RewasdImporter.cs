using System.IO;
using System.Text.Json;

namespace ReBuns;

/// <summary>Imports reWASD .rewasd (schema 3) keyboard/mouse → gamepad configs.</summary>
public static class RewasdImporter
{
    static readonly Dictionary<int, PadTarget> GamepadIds = new()
    {
        [1] = PadTarget.A, [2] = PadTarget.B, [3] = PadTarget.X, [4] = PadTarget.Y,
        [5] = PadTarget.LB, [6] = PadTarget.RB, [7] = PadTarget.Back, [8] = PadTarget.Start,
        [9] = PadTarget.LS, [10] = PadTarget.RS, [11] = PadTarget.Guide,
        [33] = PadTarget.DUp, [34] = PadTarget.DDown, [35] = PadTarget.DLeft, [36] = PadTarget.DRight,
        [40] = PadTarget.LStickUp, [41] = PadTarget.LStickDown, [42] = PadTarget.LStickLeft, [43] = PadTarget.LStickRight,
        [47] = PadTarget.RStickUp, [48] = PadTarget.RStickDown, [49] = PadTarget.RStickLeft, [50] = PadTarget.RStickRight,
        [51] = PadTarget.LT, [55] = PadTarget.RT,
    };

    static string MouseName(string desc) => desc switch
    {
        "MOUSE_BTN_1" => "Mouse1", "MOUSE_BTN_2" => "Mouse2", "MOUSE_BTN_3" => "Mouse3",
        "MOUSE_BTN_4" => "Mouse4", "MOUSE_BTN_5" => "Mouse5",
        "Mouse scroll up" => "WheelUp", "Mouse scroll down" => "WheelDown",
        _ => null
    };

    static string KeyFromDik(int dik)
    {
        uint scan = dik >= 128 ? (uint)(0xE000 | (dik - 128)) : (uint)dik;
        uint vk = Native.MapVirtualKey(scan, 3 /* MAPVK_VSC_TO_VK_EX */);
        if (vk == 0) return null;
        return InputNames.ToName((int)vk);
    }

    public static Profile Import(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;
        var p = new Profile { Name = Path.GetFileNameWithoutExtension(path) };
        if (root.TryGetProperty("config", out var cfg))
        {
            if (cfg.TryGetProperty("appName", out var an)) p.Name = an.GetString() + " - " + p.Name;
            if (cfg.TryGetProperty("processNames", out var pn))
                foreach (var x in pn.EnumerateArray()) p.Processes.Add(x.GetString());
        }

        // mouse stick settings
        try
        {
            foreach (var hw in root.GetProperty("devices").GetProperty("hardware").EnumerateArray())
                if (hw.TryGetProperty("default", out var d) && d.TryGetProperty("stick", out var st))
                {
                    if (st.TryGetProperty("smoothing", out var sm)) p.Mouse.SmoothingMs = 4 + sm.GetDouble() * 8;
                    if (st.TryGetProperty("scaleFactorX", out var sf)) p.Mouse.SensitivityX = p.Mouse.SensitivityY = Math.Round(sf.GetDouble() / 340.0, 1);
                }
            foreach (var g in root.GetProperty("devices").GetProperty("virtual").GetProperty("gamepads").EnumerateArray())
                foreach (var s in g.GetProperty("default").GetProperty("sticks").EnumerateArray())
                    if (s.GetProperty("name").GetString() == "right" && s.TryGetProperty("initialDeflection", out var idf))
                        p.Mouse.AntiDeadzone = Math.Round(idf.GetDouble() / 32767.0 * 100, 1);
        }
        catch { }

        // masks: id -> input name
        var masks = new Dictionary<int, string>();
        if (root.TryGetProperty("masks", out var ms))
            foreach (var m in ms.EnumerateArray())
            {
                var set = m.GetProperty("set");
                if (set.GetArrayLength() != 1) continue;
                var e = set[0];
                string desc = e.TryGetProperty("description", out var de) ? de.GetString() : "";
                string name = desc.StartsWith("DIK_") ? KeyFromDik(e.GetProperty("buttonId").GetInt32()) : MouseName(desc);
                if (name != null) masks[m.GetProperty("id").GetInt32()] = name;
            }

        if (root.TryGetProperty("mappings", out var mp))
            foreach (var m in mp.EnumerateArray())
            {
                if (!m.TryGetProperty("condition", out var c) || !c.TryGetProperty("mask", out var cm)) continue;
                if (!m.TryGetProperty("macros", out var macros)) continue;
                if (!masks.TryGetValue(cm.GetProperty("id").GetInt32(), out var input)) continue;
                string note = m.TryGetProperty("description", out var dd) ? dd.GetString() : "";
                var mode = BindMode.Hold;
                if (cm.TryGetProperty("activator", out var act) && act.TryGetProperty("mode", out var am))
                    mode = am.GetString() switch { "toggle" => BindMode.Toggle, "turbo" => BindMode.Turbo, _ => BindMode.Hold };
                foreach (var mac in macros.EnumerateArray())
                {
                    if (!mac.TryGetProperty("gamepad", out var gp)) continue;
                    if (!GamepadIds.TryGetValue(gp.GetProperty("buttonId").GetInt32(), out var t)) continue;
                    int amt = 100;
                    if (gp.TryGetProperty("value", out var v)) amt = (int)Math.Round(Math.Abs(v.GetDouble()) / 32767.0 * 100);
                    p.Bindings.Add(new Binding { Input = input, Target = t, Mode = mode, Amount = Math.Clamp(amt, 1, 100), Note = note });
                }
            }
        return p;
    }
}
