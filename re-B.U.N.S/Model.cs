using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReBuns;

public enum PadTarget
{
    None,
    A, B, X, Y, LB, RB, Back, Start, Guide, LS, RS,
    DUp, DDown, DLeft, DRight,
    LT, RT,
    LStickUp, LStickDown, LStickLeft, LStickRight,
    RStickUp, RStickDown, RStickLeft, RStickRight,
}

/// <summary>How the physical key / mouse button triggers the binding.</summary>
public enum KeyActivator
{
    Regular,    // 1:1 while pressed (tapping key = tap controller button, holding key = hold controller button)
    SingleTap,  // Triggers on quick tap (< 220 ms)
    LongHold,   // Triggers when held (>= 260 ms)
    DoubleTap   // Triggers when double-tapped within 280 ms
}

/// <summary>What the virtual controller button does when triggered.</summary>
public enum BindMode
{
    Hold,       // Held while key is active (1:1)
    TapPulse,   // Quick 90 ms controller tap
    HoldPulse,  // 650 ms controller hold (triggers GTA 6 HOLD actions like Loadout / Waink App / Rest)
    Toggle,     // Latches on/off
    Turbo       // Rapid repeat while held
}

public enum MouseStick { Right, Left, None }

public enum UnlockMode { Toggle, Hold }

/// <summary>The 4 Built-in GTA 6 Presets: #1 ReBuns + the 3 in-game Control Schemes (Default, Classic, Left-Handed).</summary>
public enum GtaLayoutId
{
    Custom = 0,
    ReBuns = 1,      // 1 / 4: ★ ReBuns (exact 1:1 custom keybind preset)
    Default = 2,     // 2 / 4: GTA 6 Default
    Classic = 3,     // 3 / 4: GTA 6 Classic
    LeftHanded = 4   // 4 / 4: GTA 6 Left-Handed
}

public enum SchematicHoldView
{
    TapAndHold = 0,
    TapOnly = 1,
    HoldOnly = 2
}

public enum ControllerType
{
    Xbox = 0,
    PlayStation = 1
}

public static class ControllerNames
{
    public static string ButtonName(PadTarget target, ControllerType type) => type switch
    {
        ControllerType.PlayStation => target switch
        {
            PadTarget.A => "✕ Cross",
            PadTarget.B => "○ Circle",
            PadTarget.X => "□ Square",
            PadTarget.Y => "△ Triangle",
            PadTarget.LB => "L1",
            PadTarget.RB => "R1",
            PadTarget.LT => "L2",
            PadTarget.RT => "R2",
            PadTarget.LS => "L3",
            PadTarget.RS => "R3",
            PadTarget.Back => "Share / Touchpad",
            PadTarget.Start => "Options",
            PadTarget.Guide => "PS Button",
            PadTarget.DUp => "D-Pad ↑",
            PadTarget.DDown => "D-Pad ↓",
            PadTarget.DLeft => "D-Pad ←",
            PadTarget.DRight => "D-Pad →",
            PadTarget.LStickUp => "Left Stick ↑",
            PadTarget.LStickDown => "Left Stick ↓",
            PadTarget.LStickLeft => "Left Stick ←",
            PadTarget.LStickRight => "Left Stick →",
            PadTarget.RStickUp => "Right Stick ↑",
            PadTarget.RStickDown => "Right Stick ↓",
            PadTarget.RStickLeft => "Right Stick ←",
            PadTarget.RStickRight => "Right Stick →",
            _ => target.ToString()
        },
        _ => target switch
        {
            PadTarget.A => "A",
            PadTarget.B => "B",
            PadTarget.X => "X",
            PadTarget.Y => "Y",
            PadTarget.LB => "LB",
            PadTarget.RB => "RB",
            PadTarget.LT => "LT",
            PadTarget.RT => "RT",
            PadTarget.LS => "L3",
            PadTarget.RS => "R3",
            PadTarget.Back => "Back / View",
            PadTarget.Start => "Start / Menu",
            PadTarget.Guide => "Xbox Guide",
            PadTarget.DUp => "D-Pad ↑",
            PadTarget.DDown => "D-Pad ↓",
            PadTarget.DLeft => "D-Pad ←",
            PadTarget.DRight => "D-Pad →",
            PadTarget.LStickUp => "Left Stick ↑",
            PadTarget.LStickDown => "Left Stick ↓",
            PadTarget.LStickLeft => "Left Stick ←",
            PadTarget.LStickRight => "Left Stick →",
            PadTarget.RStickUp => "Right Stick ↑",
            PadTarget.RStickDown => "Right Stick ↓",
            PadTarget.RStickLeft => "Right Stick ←",
            PadTarget.RStickRight => "Right Stick →",
            _ => target.ToString()
        }
    };

    public static string Glyph(PadTarget target, ControllerType type) => type switch
    {
        ControllerType.PlayStation => target switch
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
            PadTarget.Back => "SHARE",
            PadTarget.Start => "OPT",
            PadTarget.Guide => "PS",
            PadTarget.DUp => "✚↑",
            PadTarget.DDown => "✚↓",
            PadTarget.DLeft => "✚←",
            PadTarget.DRight => "✚→",
            PadTarget.LStickUp or PadTarget.LStickDown or PadTarget.LStickLeft or PadTarget.LStickRight => "L",
            PadTarget.RStickUp or PadTarget.RStickDown or PadTarget.RStickLeft or PadTarget.RStickRight => "R",
            _ => target.ToString()
        },
        _ => target switch
        {
            PadTarget.A => "A",
            PadTarget.B => "B",
            PadTarget.X => "X",
            PadTarget.Y => "Y",
            PadTarget.LB => "LB",
            PadTarget.RB => "RB",
            PadTarget.LT => "LT",
            PadTarget.RT => "RT",
            PadTarget.LS => "L3",
            PadTarget.RS => "R3",
            PadTarget.Back => "⧉",
            PadTarget.Start => "≡",
            PadTarget.Guide => "❖",
            PadTarget.DUp => "✚↑",
            PadTarget.DDown => "✚↓",
            PadTarget.DLeft => "✚←",
            PadTarget.DRight => "✚→",
            PadTarget.LStickUp or PadTarget.LStickDown or PadTarget.LStickLeft or PadTarget.LStickRight => "L",
            PadTarget.RStickUp or PadTarget.RStickDown or PadTarget.RStickLeft or PadTarget.RStickRight => "R",
            _ => target.ToString()
        }
    };

    public static string FormatLabel(string label, ControllerType type)
    {
        if (string.IsNullOrEmpty(label) || type != ControllerType.PlayStation) return label;
        return label
            .Replace("LB", "L1")
            .Replace("RB", "R1")
            .Replace("LT", "L2")
            .Replace("RT", "R2")
            .Replace("/ B)", "/ ○)")
            .Replace("/ A)", "/ ✕)")
            .Replace("/ X)", "/ □)")
            .Replace("/ Y)", "/ △)");
    }
}

public record CalloutSpec(
    PadTarget Target,
    string ButtonGlyph,
    string TapLabel,
    string HoldLabel,   // null if same as TapLabel
    bool IsLeft);

public class GtaLayoutInfo
{
    public GtaLayoutId Id { get; init; }
    public int Index { get; init; } // 1..4
    public string SchemeName { get; init; } = "";
    public string ProfileName { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Summary { get; init; } = "";
    public CalloutSpec[] Callouts { get; init; } = Array.Empty<CalloutSpec>();
}

public class Binding
{
    public string Input { get; set; } = "";
    public PadTarget Target { get; set; }
    public KeyActivator Activator { get; set; } = KeyActivator.Regular;
    public BindMode Mode { get; set; } = BindMode.Hold;
    public int Amount { get; set; } = 100;
    public int TurboMs { get; set; } = 60;
    public string Note { get; set; } = "";
}

public class MouseSettings
{
    public MouseStick Stick { get; set; } = MouseStick.Right;
    public double SensitivityX { get; set; } = 14.0;
    public double SensitivityY { get; set; } = 14.0;
    public double AntiDeadzone { get; set; } = 32.5;
    public double Curve { get; set; } = 1.0;
    public double SmoothingMs { get; set; } = 12.0;
    public bool InvertY { get; set; }
}

public class Profile
{
    public string Name { get; set; } = "New Profile";
    public GtaLayoutId LayoutId { get; set; } = GtaLayoutId.Custom;
    public ControllerType ControllerType { get; set; } = ControllerType.Xbox;
    public List<string> Processes { get; set; } = new();
    public bool OnlyWhenProcessFocused { get; set; }
    public bool BlockUnmappedKeys { get; set; }
    public UnlockMode UnlockMode { get; set; } = UnlockMode.Toggle;
    public MouseSettings Mouse { get; set; } = new();
    public List<Binding> Bindings { get; set; } = new();

    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>The 4 built-in presets: #1 ReBuns + #2 Default + #3 Classic + #4 Left-Handed.</summary>
    public static readonly GtaLayoutInfo[] GtaLayouts =
    {
        new()
        {
            Id = GtaLayoutId.ReBuns,
            Index = 1,
            SchemeName = "★ re-B.U.N.S",
            ProfileName = "GTA 6 — 1/4 ★ re-B.U.N.S",
            Subtitle = "1 / 4  ·  re-B.U.N.S Custom Preset (Q/Tab=LB, E/M3=RB, Ctrl/C=R3, X/Back=B, R=X, F/Wheel=Y, Shift=L3, Space=A)",
            Summary = "re-B.U.N.S custom GTA 6 keybinds: Q & Tab → LB, E & Middle-Click → RB, L-Ctrl & C → R3, X & Backspace → B, R → X, F & Wheel → Y, L-Shift → L3, Space & Enter → A, B/N/Z/T + Arrows → D-Pad, V/M → Back, Esc/, → Start.",
            Callouts = new CalloutSpec[]
            {
                // Left side
                new(PadTarget.Back,       "⧉",  "MAP (M / V)",          null,               true),
                new(PadTarget.LB,         "LB", "HOLSTER (Q / Tab)",    "LOADOUT",          true),
                new(PadTarget.LT,         "LT", "AIM / INTERACT",       null,               true),
                new(PadTarget.DUp,        "✚↑", "CELLPHONE (Up / B)",   "WAINK APP",        true),
                new(PadTarget.DLeft,      "✚←", "RADIO (Left / N)",     "RADIO MENU",       true),
                new(PadTarget.DDown,      "✚↓", "MINIMAP (Down / Z)",   "QUICK NAVIGATION", true),
                new(PadTarget.DRight,     "✚→", "DETONATE (Right / T)", "PARTNER MENU",     true),
                new(PadTarget.LS,         "L3", "RUN (L-Shift)",        null,               true),
                new(PadTarget.LStickUp,   "L",  "MOVE (W A S D)",       null,               true),
                // Right side
                new(PadTarget.Start,      "≡",  "PAUSE (Esc / ,)",      null,               false),
                new(PadTarget.RB,         "RB", "ACTION / COVER (E)",   null,               false),
                new(PadTarget.RT,         "RT", "SHOOT (L-Click)",      null,               false),
                new(PadTarget.Y,          "Y",  "VEHICLE (F / Wheel)",  null,               false),
                new(PadTarget.X,          "X",  "RELOAD / MELEE (R)",   null,               false),
                new(PadTarget.B,          "B",  "CROUCH (X / Back)",    "REST",             false),
                new(PadTarget.A,          "A",  "JUMP (Space / Enter)", null,               false),
                new(PadTarget.RS,         "R3", "CROUCH / LOOK (Ctrl/C)", null,             false),
                new(PadTarget.RStickUp,   "R",  "LOOK (Mouse)",         null,               false),
            }
        },
        new()
        {
            Id = GtaLayoutId.Default,
            Index = 2,
            SchemeName = "Default",
            ProfileName = "GTA 6 — 2/4 Default",
            Subtitle = "2 / 4  ·  GTA 6 In-Game 'Default' Scheme (L3 Run · A Jump/Climb · B Crouch/Rest · RB Cover · LB Holster/Loadout)",
            Summary = "GTA 6 In-Game Control Scheme (Default). L3 is Run (L-Shift), A is Jump/Climb (Space), B is Crouch (Tap) / Rest (Hold) (L-Ctrl), RB is Cover (Q/E), and LB is Holster (Tap) / Loadout (Hold) (Tab).",
            Callouts = new CalloutSpec[]
            {
                // Left side (Screenshots 1 & 2)
                new(PadTarget.Back,       "⧉",  "MAP",                  null,               true),
                new(PadTarget.LB,         "LB", "HOLSTER",              "LOADOUT",          true),
                new(PadTarget.LT,         "LT", "AIM / INTERACT",       null,               true),
                new(PadTarget.DUp,        "✚↑", "CELLPHONE",            "WAINK APP",        true),
                new(PadTarget.DLeft,      "✚←", "RADIO CONTROLS",       "RADIO MENU",       true),
                new(PadTarget.DDown,      "✚↓", "OBJECTIVE / MINIMAP",  "QUICK NAVIGATION", true),
                new(PadTarget.DRight,     "✚→", "DETONATE EXPLOSIVES",  "PARTNER MENU",     true),
                new(PadTarget.LS,         "L3", "RUN",                  null,               true),
                new(PadTarget.LStickUp,   "L",  "MOVE",                 null,               true),
                // Right side (Screenshots 1 & 2)
                new(PadTarget.Start,      "≡",  "PAUSE",                null,               false),
                new(PadTarget.RB,         "RB", "COVER",                null,               false),
                new(PadTarget.RT,         "RT", "SHOOT",                null,               false),
                new(PadTarget.Y,          "Y",  "INTERACT",             null,               false),
                new(PadTarget.X,          "X",  "MELEE",                null,               false),
                new(PadTarget.B,          "B",  "CROUCH",               "REST",             false),
                new(PadTarget.A,          "A",  "JUMP / CLIMB",         null,               false),
                new(PadTarget.RS,         "R3", "LOOK BEHIND",          null,               false),
                new(PadTarget.RStickUp,   "R",  "LOOK",                 null,               false),
            }
        },
        new()
        {
            Id = GtaLayoutId.Classic,
            Index = 3,
            SchemeName = "Classic",
            ProfileName = "GTA 6 — 3/4 Classic",
            Subtitle = "3 / 4  ·  GTA 6 In-Game 'Classic' Scheme (A Run · X Jump/Climb · B Melee · L3 Crouch/Rest · RB Cover)",
            Summary = "GTA 6 In-Game Control Scheme (Classic). A is Run (L-Shift), X is Jump/Climb (Space), B is Melee (R/X), L3 is Crouch (Tap) / Rest (Hold) (L-Ctrl), RB is Cover, and LB is Holster/Loadout.",
            Callouts = new CalloutSpec[]
            {
                // Left side (Screenshots 3 & 4)
                new(PadTarget.Back,       "⧉",  "MAP",                  null,               true),
                new(PadTarget.LB,         "LB", "HOLSTER",              "LOADOUT",          true),
                new(PadTarget.LT,         "LT", "AIM / INTERACT",       null,               true),
                new(PadTarget.DUp,        "✚↑", "CELLPHONE",            "WAINK APP",        true),
                new(PadTarget.DLeft,      "✚←", "RADIO CONTROLS",       "RADIO MENU",       true),
                new(PadTarget.DDown,      "✚↓", "OBJECTIVE / MINIMAP",  "QUICK NAVIGATION", true),
                new(PadTarget.DRight,     "✚→", "DETONATE EXPLOSIVES",  "PARTNER MENU",     true),
                new(PadTarget.LS,         "L3", "CROUCH",               "REST",             true),
                new(PadTarget.LStickUp,   "L",  "MOVE",                 null,               true),
                // Right side (Screenshots 3 & 4)
                new(PadTarget.Start,      "≡",  "PAUSE",                null,               false),
                new(PadTarget.RB,         "RB", "COVER",                null,               false),
                new(PadTarget.RT,         "RT", "SHOOT",                null,               false),
                new(PadTarget.Y,          "Y",  "INTERACT",             null,               false),
                new(PadTarget.X,          "X",  "JUMP / CLIMB",         null,               false),
                new(PadTarget.B,          "B",  "MELEE",                null,               false),
                new(PadTarget.A,          "A",  "RUN",                  null,               false),
                new(PadTarget.RS,         "R3", "LOOK BEHIND",          null,               false),
                new(PadTarget.RStickUp,   "R",  "LOOK",                 null,               false),
            }
        },
        new()
        {
            Id = GtaLayoutId.LeftHanded,
            Index = 4,
            SchemeName = "Left-Handed",
            ProfileName = "GTA 6 — 4/4 Left-Handed",
            Subtitle = "4 / 4  ·  GTA 6 In-Game 'Left-Handed' Scheme (LT Shoot · RT Aim · LB Cover · RB Holster/Loadout · R3 Run · R-Stick Move)",
            Summary = "GTA 6 In-Game Control Scheme (Left-Handed). Swaps triggers, bumpers, and sticks: LT is Shoot, RT is Aim, LB is Cover, RB is Holster/Loadout, R3 is Run, R-Stick is Move, and L-Stick is Look.",
            Callouts = new CalloutSpec[]
            {
                // Left side (Screenshots 5 & 6)
                new(PadTarget.Back,       "⧉",  "MAP",                  null,               true),
                new(PadTarget.LB,         "LB", "COVER",                null,               true),
                new(PadTarget.LT,         "LT", "SHOOT",                null,               true),
                new(PadTarget.DUp,        "✚↑", "CELLPHONE",            "WAINK APP",        true),
                new(PadTarget.DLeft,      "✚←", "RADIO CONTROLS",       "RADIO MENU",       true),
                new(PadTarget.DDown,      "✚↓", "OBJECTIVE / MINIMAP",  "QUICK NAVIGATION", true),
                new(PadTarget.DRight,     "✚→", "DETONATE EXPLOSIVES",  "PARTNER MENU",     true),
                new(PadTarget.LS,         "L3", "LOOK BEHIND",          null,               true),
                new(PadTarget.LStickUp,   "L",  "LOOK",                 null,               true),
                // Right side (Screenshots 5 & 6)
                new(PadTarget.Start,      "≡",  "PAUSE",                null,               false),
                new(PadTarget.RB,         "RB", "HOLSTER",              "LOADOUT",          false),
                new(PadTarget.RT,         "RT", "AIM / INTERACT",       null,               false),
                new(PadTarget.Y,          "Y",  "INTERACT",             null,               false),
                new(PadTarget.X,          "X",  "MELEE",                null,               false),
                new(PadTarget.B,          "B",  "CROUCH",               "REST",             false),
                new(PadTarget.A,          "A",  "JUMP / CLIMB",         null,               false),
                new(PadTarget.RS,         "R3", "RUN",                  null,               false),
                new(PadTarget.RStickUp,   "R",  "MOVE",                 null,               false),
            }
        }
    };

    public static string Folder
    {
        get
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "re-B.U.N.S", "Profiles");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public string FilePath => Path.Combine(Folder, string.Join("_", Name.Split(Path.GetInvalidFileNameChars())) + ".json");

    public void Save() => File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));

    public static Profile Load(string path) => JsonSerializer.Deserialize<Profile>(File.ReadAllText(path), Json);

    public Profile Clone() => JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(this, Json), Json);

    public static List<Profile> LoadAll()
    {
        var list = new List<Profile>();
        string[] obsoleteNames =
        {
            "GTA 6 (Keyboard + Mouse)",
            "GTA 6 — Layout 1: Standard FPS (Your Setup)",
            "GTA 6 — Layout 2: GTA Standard (Default)",
            "GTA 6 — Layout 3: Standard FPS 2",
            "GTA 6 — Layout 4: Alternate",
            "GTA 6 — 1/3 Default",
            "GTA 6 — 2/3 Classic",
            "GTA 6 — 3/3 Left-Handed",
            "GTA 6 — 1/4 ★ Your reWASD Config",
            "GTA 6 — 1/4 ★ ReBuns"
        };

        foreach (var f in Directory.GetFiles(Folder, "*.json"))
        {
            try
            {
                var p = Load(f);
                if (p == null) continue;
                if (obsoleteNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
                {
                    try { File.Delete(f); } catch { }
                    continue;
                }
                list.Add(p);
            }
            catch { }
        }

        // Ensure all 4 built-in presets exist (#1 Your reWASD Config, #2 Default, #3 Classic, #4 Left-Handed)
        foreach (var info in GtaLayouts)
        {
            if (!list.Any(p => p.LayoutId == info.Id && string.Equals(p.Name, info.ProfileName, StringComparison.OrdinalIgnoreCase)))
            {
                var preset = CreateGta6Preset(info.Id);
                preset.Save();
                list.Add(preset);
            }
        }

        return list
            .OrderBy(p => p.LayoutId == GtaLayoutId.Custom ? 99 : (int)p.LayoutId)
            .ThenBy(p => p.Name)
            .ToList();
    }

    public static Profile Gta6Default() => CreateGta6Preset(GtaLayoutId.ReBuns);

    public static Profile CreateGta6Preset(GtaLayoutId id)
    {
        var info = GtaLayouts.FirstOrDefault(x => x.Id == id) ?? GtaLayouts[0];
        var p = new Profile
        {
            Name = info.ProfileName,
            LayoutId = info.Id,
            Processes = { "Greenlight.exe", "XboxPcApp.exe", "GameBar.exe", "chrome.exe", "msedge.exe", "brave.exe", "GTA6.exe", "PlayGTA6.exe" },
        };
        void B(string k, PadTarget t, string n = "", KeyActivator act = KeyActivator.Regular, BindMode mode = BindMode.Hold) =>
            p.Bindings.Add(new Binding { Input = k, Target = t, Note = n, Activator = act, Mode = mode });

        if (id == GtaLayoutId.ReBuns)
        {
            // EXACT 1:1 match of C:\Users\Public\Documents\reWASD\Profiles\gta\Controller\Config 1.rewasd
            p.Mouse.Stick = MouseStick.Right;
            p.Mouse.SensitivityX = 14.0;
            p.Mouse.SensitivityY = 14.0;
            p.Mouse.AntiDeadzone = 32.5; // 10648 / 32767
            p.Mouse.SmoothingMs = 12.0;

            B("Escape",      PadTarget.Start,       "Pause Menu (Start)");
            B("Back",        PadTarget.B,           "Cancel / Back (B)");
            B("Tab",         PadTarget.LB,          "Weapon Wheel / Holster (LB)");
            B("Q",           PadTarget.LB,          "Take Cover (LB)");
            B("W",           PadTarget.LStickUp,    "Move Forward (LS Up)");
            B("E",           PadTarget.RB,          "Action / Horn (RB)");
            B("R",           PadTarget.X,           "Reload / Melee (X)");
            B("T",           PadTarget.DRight,      "Detonate / Partner Menu (D-Right)");
            B("Return",      PadTarget.A,           "Accept (A)");
            B("LControlKey", PadTarget.RS,          "Crouch / Look Behind (R3)");
            B("A",           PadTarget.LStickLeft,  "Move Left (LS Left)");
            B("S",           PadTarget.LStickDown,  "Move Back (LS Down)");
            B("D",           PadTarget.LStickRight, "Move Right (LS Right)");
            B("F",           PadTarget.Y,           "Enter Vehicle / Interact (Y)");
            B("LShiftKey",   PadTarget.LS,          "Run / Sprint (L3)");
            B("Z",           PadTarget.DDown,       "Objective / Quick Nav (D-Down)");
            B("X",           PadTarget.B,           "Crouch / Rest / Cancel (B)");
            B("C",           PadTarget.RS,          "Look Behind (R3)");
            B("V",           PadTarget.Back,        "Camera / Map (Back)");
            B("B",           PadTarget.DUp,         "Cellphone / Waink App (D-Up)");
            B("N",           PadTarget.DLeft,       "Radio Controls / Menu (D-Left)");
            B("M",           PadTarget.Back,        "Map (Back)");
            B("Oemcomma",    PadTarget.Start,       "Pause Menu (Start)");
            B("Space",       PadTarget.A,           "Jump / Climb (A)");
            B("Up",          PadTarget.DUp,         "Cellphone / Waink App (D-Up)");
            B("Left",        PadTarget.DLeft,       "Radio Controls / Menu (D-Left)");
            B("Right",       PadTarget.DRight,      "Detonate / Partner Menu (D-Right)");
            B("Down",        PadTarget.DDown,       "Objective / Quick Nav (D-Down)");
            B("Mouse1",      PadTarget.RT,          "Shoot (RT)");
            B("Mouse2",      PadTarget.LT,          "Aim / Interact (LT)");
            B("Mouse3",      PadTarget.RB,          "Action / Cover (RB)");
            B("WheelUp",     PadTarget.Y,           "Weapon Next (Y)");
            B("WheelDown",   PadTarget.Y,           "Weapon Prev (Y)");
            return p;
        }

        // Shared menus & D-Pad for the 3 GTA 6 in-game Control Schemes
        B("Escape", PadTarget.Start, "PAUSE");
        B("Oemcomma", PadTarget.Start, "PAUSE");
        B("M", PadTarget.Back, "MAP");
        B("V", PadTarget.Back, "MAP");
        B("Return", PadTarget.A, "Accept / Select");
        B("Back", PadTarget.B, "Back / Cancel");

        B("Up", PadTarget.DUp, "Tap: CELLPHONE · Hold: WAINK APP");
        B("B", PadTarget.DUp, "Tap: CELLPHONE · Hold: WAINK APP");
        B("Left", PadTarget.DLeft, "Tap: RADIO CONTROLS · Hold: RADIO MENU");
        B("N", PadTarget.DLeft, "Tap: RADIO CONTROLS · Hold: RADIO MENU");
        B("Down", PadTarget.DDown, "Tap: OBJECTIVE / MINIMAP · Hold: QUICK NAVIGATION");
        B("Z", PadTarget.DDown, "Tap: OBJECTIVE / MINIMAP · Hold: QUICK NAVIGATION");
        B("Right", PadTarget.DRight, "Tap: DETONATE EXPLOSIVES · Hold: PARTNER MENU");
        B("T", PadTarget.DRight, "Tap: DETONATE EXPLOSIVES · Hold: PARTNER MENU");

        switch (id)
        {
            case GtaLayoutId.Default:
            default:
                p.Mouse.Stick = MouseStick.Right;
                B("W", PadTarget.LStickUp, "MOVE Forward");
                B("A", PadTarget.LStickLeft, "MOVE Left");
                B("S", PadTarget.LStickDown, "MOVE Back");
                B("D", PadTarget.LStickRight, "MOVE Right");
                B("LShiftKey", PadTarget.LS, "RUN (L3)");
                B("Space", PadTarget.A, "JUMP / CLIMB (A)");
                B("LControlKey", PadTarget.B, "Tap: CROUCH · Hold: REST (B)");
                B("X", PadTarget.B, "Tap: CROUCH · Hold: REST (B)");
                B("R", PadTarget.X, "MELEE / Reload (X)");
                B("F", PadTarget.Y, "INTERACT / Vehicle (Y)");
                B("WheelUp", PadTarget.Y, "INTERACT / Cycle (Y)");
                B("WheelDown", PadTarget.Y, "INTERACT / Cycle (Y)");
                B("Tab", PadTarget.LB, "Tap: HOLSTER · Hold: LOADOUT (LB)");
                B("Q", PadTarget.RB, "COVER (RB)");
                B("E", PadTarget.RB, "COVER / Action (RB)");
                B("Mouse3", PadTarget.RB, "COVER (RB)");
                B("Mouse1", PadTarget.RT, "SHOOT (RT)");
                B("Mouse2", PadTarget.LT, "AIM / INTERACT (LT)");
                B("C", PadTarget.RS, "LOOK BEHIND (R3)");
                break;

            case GtaLayoutId.Classic:
                p.Mouse.Stick = MouseStick.Right;
                B("W", PadTarget.LStickUp, "MOVE Forward");
                B("A", PadTarget.LStickLeft, "MOVE Left");
                B("S", PadTarget.LStickDown, "MOVE Back");
                B("D", PadTarget.LStickRight, "MOVE Right");
                B("LShiftKey", PadTarget.A, "RUN (A)");
                B("Space", PadTarget.X, "JUMP / CLIMB (X)");
                B("LControlKey", PadTarget.LS, "Tap: CROUCH · Hold: REST (L3)");
                B("R", PadTarget.B, "MELEE / Reload (B)");
                B("X", PadTarget.B, "MELEE (B)");
                B("F", PadTarget.Y, "INTERACT / Vehicle (Y)");
                B("WheelUp", PadTarget.Y, "INTERACT / Cycle (Y)");
                B("WheelDown", PadTarget.Y, "INTERACT / Cycle (Y)");
                B("Tab", PadTarget.LB, "Tap: HOLSTER · Hold: LOADOUT (LB)");
                B("Q", PadTarget.RB, "COVER (RB)");
                B("E", PadTarget.RB, "COVER / Action (RB)");
                B("Mouse3", PadTarget.RB, "COVER (RB)");
                B("Mouse1", PadTarget.RT, "SHOOT (RT)");
                B("Mouse2", PadTarget.LT, "AIM / INTERACT (LT)");
                B("C", PadTarget.RS, "LOOK BEHIND (R3)");
                break;

            case GtaLayoutId.LeftHanded:
                p.Mouse.Stick = MouseStick.Left;
                B("W", PadTarget.RStickUp, "MOVE Forward (R-Stick)");
                B("A", PadTarget.RStickLeft, "MOVE Left (R-Stick)");
                B("S", PadTarget.RStickDown, "MOVE Back (R-Stick)");
                B("D", PadTarget.RStickRight, "MOVE Right (R-Stick)");
                B("LShiftKey", PadTarget.RS, "RUN (R3)");
                B("Space", PadTarget.A, "JUMP / CLIMB (A)");
                B("LControlKey", PadTarget.B, "Tap: CROUCH · Hold: REST (B)");
                B("X", PadTarget.B, "Tap: CROUCH · Hold: REST (B)");
                B("R", PadTarget.X, "MELEE / Reload (X)");
                B("F", PadTarget.Y, "INTERACT / Vehicle (Y)");
                B("WheelUp", PadTarget.Y, "INTERACT / Cycle (Y)");
                B("WheelDown", PadTarget.Y, "INTERACT / Cycle (Y)");
                B("Q", PadTarget.LB, "COVER (LB)");
                B("E", PadTarget.LB, "COVER (LB)");
                B("Tab", PadTarget.RB, "Tap: HOLSTER · Hold: LOADOUT (RB)");
                B("Mouse3", PadTarget.RB, "Tap: HOLSTER · Hold: LOADOUT (RB)");
                B("Mouse1", PadTarget.LT, "SHOOT (LT)");
                B("Mouse2", PadTarget.RT, "AIM / INTERACT (RT)");
                B("C", PadTarget.LS, "LOOK BEHIND (L3)");
                break;
        }
        return p;
    }
}
