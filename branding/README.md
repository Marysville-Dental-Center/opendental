# Branding

The Open PMS - Dental logo is a white molar on a teal rounded square (teal `#0F766E`). It is original artwork made for this project and is
licensed with it (GNU GPL, version 2 or later).

| File | What it is | Where it is used |
|---|---|---|
| `logo.svg` | The vector master (a 100x100 design) | |
| `app.ico` | Program and window icon: 16, 20, 24, 32, 40, 48, 64, 128 (32-bit) and 256 (PNG) pixels | The icon of `OpenPmsDental.exe`, `ServiceManager.exe` and `UpdateFileCopier.exe` (`ApplicationIcon`) and of setup; embedded once in `OpenPmsDental.exe`, where `FormODBase` gives it to every window (`OpenPmsDental/Misc/BrandAssets.cs`), and once in `WpfControlsOD.dll` for the WPF windows (`FormFrame`) |
| `window.ico` | The compact window icon: 16 and 32 pixels (32-bit) | The forms that do not derive from `FormODBase` (the splash, side programs, test windows) embed a copy as `$this.Icon` in their `.resx` |
| `logo-256.png` | The logo as a picture, for documents | The About box (embedded once in `OpenPmsDental.exe`) and `README.md` |
| `splash.png` | The 500x300 start-up splash, with the attribution and the GPL notice | A copy is the background of `FormSplash` (`FormSplash.resx`); an office's own `Splash.jpg` in the program folder takes its place |
| `installer-small.bmp`, `installer-small-2x.bmp` | The installer's small wizard image (55 and 110 pixels) | `installer/OpenPmsDental.iss` |
| `installer-wizard.bmp`, `installer-wizard-2x.bmp` | The installer's welcome-page image (164x314 and 328x628) | `installer/OpenPmsDental.iss` |

Every file except `logo.svg` is drawn by `tools/Make-BrandAssets.ps1` (WPF, no other tool needed), and the same input always gives the same
files:

```
powershell -NoProfile -STA -ExecutionPolicy Bypass -File tools\Make-BrandAssets.ps1
```

If you change the logo, change it in both `logo.svg` and the script, then run the script and rebuild. `splash.png` and `window.ico` also have
copies in `.resx` files (above): put the new bytes there too. `tests/AttributionTests/BrandingTests.cs` fails while a copy differs from its
file here, or while any `.resx` still holds one of Open Dental's icons.
