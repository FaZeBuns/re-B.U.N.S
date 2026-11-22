<p align="center">
  <img src="assets/Logo.png" alt="re-B.U.N.S Logo" width="210" />
</p>

<h1 align="center">re-B.U.N.S</h1>
<p align="center">
  <strong>Native Windows Keyboard &amp; Mouse → Xbox Controller Remapper for GTA 6</strong>
</p>

<p align="center">
  <a href="https://github.com/FaZeBuns/re-B.U.N.S/releases/latest">
    <img src="assets/download-button.svg" alt="Download Latest Release" width="420" />
  </a>
</p>

---

**re-B.U.N.S** is a lightweight, native Windows (C# / .NET 8 WPF, zero Electron) keyboard and mouse to virtual Xbox 360 controller remapper designed specifically for **GTA 6**.

## ✨ Features

- **All-In-One Standalone Executable (`re-BUNS.exe`)**:
  - Single-file native Windows executable with built-in **ViGEmBus Virtual Controller Driver** auto-detection and 1-click setup.
- **GTA 6 Control Scheme UI & 4 Built-In Presets**:
  1. **`1 / 4  ·  ★ re-B.U.N.S`** — Custom GTA 6 keyboard & mouse preset (`WASD` Move, `Mouse` Look, `L-Shift` Run, `Space` Jump, `Q/Tab` Holster/Loadout/Cover, `E` Action/Cover, `R` Reload/Melee, `F/Wheel` Vehicle/Interact, `L-Ctrl/C` Crouch/Look Behind, `Arrows + B/N/Z/T` D-Pad).
  2. **`2 / 4  ·  Default`** — Matches GTA 6's in-game `1/3 Default` control scheme.
  3. **`3 / 4  ·  Classic`** — Matches GTA 6's in-game `2/3 Classic` control scheme.
  4. **`4 / 4  ·  Left-Handed`** — Matches GTA 6's in-game `3/3 Left-Handed` control scheme.
- **Tap vs. Hold (`◎`) & Double-Tap Support**:
  - Supports **1:1 Tap & Hold**, **Single Tap**, **Long Hold (`◎` 650ms auto-hold)**, and **Double-Tap** activators for multi-function GTA 6 controller buttons (Cellphone / Waink App, Holster / Loadout, Radio Controls / Radio Menu, Crouch / Rest, etc.).
- **1000 Hz Zero-Allocation Input & Output Engine**:
  - Low-level Windows keyboard & mouse hooks (`WH_KEYBOARD_LL` / `WH_MOUSE_LL`) driving a virtual Xbox controller with configurable X/Y Sensitivity, Anti-Deadzone %, Response Curve, Smoothing (ms), and Y-Axis Invert.
- **Automatic GitHub Updates**:
  - Automatically checks GitHub Releases on launch and offers a 1-click **"Install Update & Restart"** popup when a new version is published.

## ⌨️ Global Hotkeys

| Hotkey | Action |
| :--- | :--- |
| **`Windows Key`** | **Pause / Play Virtual Pad Controls** (unlocks/locks mouse & keyboard without opening the Windows Start Menu or stealing game focus) |
| **`Ctrl + Alt + Delete`** | **Stop Virtual Pad Completely** (disconnects the virtual controller immediately) |

## 🚀 Getting Started

1. Click the **Download Latest Release** button above and download **`re-BUNS.exe`**.
2. Run **`re-BUNS.exe`** (if the ViGEmBus driver is not yet installed on your PC, `re-B.U.N.S` will prompt to install it automatically).
3. Select your preferred GTA 6 Control Scheme preset and click **`▶ START VIRTUAL PAD`**.

## 🛠️ Building from Source

Requires the [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows 10/11:

```powershell
dotnet build re-B.U.N.S/re-B.U.N.S.csproj -c Release
```

## 📄 License

Licensed under the [MIT License](LICENSE).
