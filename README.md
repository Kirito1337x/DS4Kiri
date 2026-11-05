# DS4Kiri

<img width="900" height="1000" alt="image" src="https://github.com/user-attachments/assets/c0fa54dc-982b-447f-9e5d-191f69106f36" />



DS4Kiri is a Windows desktop app for connecting PlayStation controllers and mapping their input to a virtual Xbox 360 controller. It also includes controller visualization, profiles, trigger and stick settings, LED color controls, themes, and optional driver setup.

## Requirements

- Windows 10 or 11, 64-bit
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build from source
- ViGEmBus for Xbox controller emulation
- HidHide is optional

The app can install the supported driver packages from the official Nefarius GitHub releases. Driver installation requires administrator approval. ViGEmBus is archived upstream; check its current support status before installing it.

## Build and run

```powershell
dotnet restore .\DS4Kiri.csproj
dotnet run --project .\DS4Kiri.csproj
```

## Create a portable release

Run this from PowerShell:

```powershell
.\Publish-Portable.ps1
```

The self-contained Windows x64 release is written to `portable\`. Copy the whole folder when distributing it; it contains the executable, themes, and branding assets.

## Universal controller mode

Universal mode reads standard HID gamepad and joystick reports and maps their common axes, buttons, and D-pad controls to the virtual Xbox controller. Compatibility depends on the controller's HID report descriptor; proprietary or XInput-only devices may not be recognized. Generic controllers may not expose battery or vibration data through this mode.

## Project layout

- `Config/` — app and controller profile settings
- `Engine/` — controller polling and virtual controller output
- `Hid/` — Sony and generic HID input/report handling
- `Mapping/` — input mapping and controller visualization
- `Native/` — Windows input interop
- `Services/` — driver, startup, tray, and profile services
- `Themes/` — bundled background artwork
- `Ui/` — WinForms user interface
- `Assets/` — app icon and image

## License

This project is distributed under the MIT License. See [LICENSE](./LICENSE).

DS4Kiri is an independent community project and is not affiliated with or endorsed by Sony Interactive Entertainment or Nefarius Software Solutions.
