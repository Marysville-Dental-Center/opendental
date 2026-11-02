# Branding

The Open PMS - Dental logo is a white molar on a teal rounded square (teal `#0F766E`). It is original artwork made for this project and is
licensed with it (GNU GPL, version 2 or later).

| File | What it is |
|---|---|
| `logo.svg` | The vector master (a 100x100 design) |
| `app.ico` | Program and window icon: 16, 20, 24, 32, 40, 48, 64, 128 (32-bit) and 256 (PNG) pixels |
| `logo-256.png` | The logo as a picture, for documents |
| `splash.png` | The 500x300 start-up splash, with the attribution and the GPL notice |
| `installer-small.bmp`, `installer-small-2x.bmp` | The installer's small wizard image (55 and 110 pixels) |
| `installer-wizard.bmp`, `installer-wizard-2x.bmp` | The installer's welcome-page image (164x314 and 328x628) |

Every file except `logo.svg` is drawn by `tools/Make-BrandAssets.ps1` (WPF, no other tool needed), and the same input always gives the same
files:

```
powershell -NoProfile -STA -ExecutionPolicy Bypass -File tools\Make-BrandAssets.ps1
```

If you change the logo, change it in both `logo.svg` and the script, then run the script and rebuild.
