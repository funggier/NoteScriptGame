# NoteScriptGame display design — Native Pixel UI

## Target desktop displays

Primary reference: 23–27-inch desktop monitors at 1920×1080 (Full HD) or 2560×1440 (QHD). Physical monitor size does not determine pixel size on its own; scaling is user-adjustable.

The old IMGUI layout used a 1600×900 coordinate system and applied a full-screen matrix scale. On a 2560×1440 display, this enlarged UI by 1.6×, making controls oversized and text softer than native glyph rendering.

## Implemented

- Native runtime Unity UI Toolkit; no IMGUI GUI.matrix and no upscaled RenderTexture.
- PanelSettings uses ConstantPixelSize; graphics and text rasterize for the current display output.
- Layout uses responsive flex/scroll containers with a maximum content width, rather than multiplying fixed coordinates.
- Rhythm playfield alone fits a precise 16:9 rectangle: width = min(availableWidth, availableHeight * 16/9), height = width * 9/16. Extra space remains around it without stretching the playfield or distorting note positions.
- Fullscreen mode uses the active display resolution. Windowed mode fits a 16:9 client area within the screen.
- In-game SETTINGS > UI SCALE offers 85%, 100%, 110% and 125%, persisted in PlayerPrefs. The first-run default is 100% for 1080p windows and 110% for 1440p+ windows.
- Standalone default is native-resolution borderless fullscreen; window size can change without distorting the playfield.
- Six-lane note presentation and mouse targets align with the visible timing judgement line.

This describes layout and native rendering. It does not imply measured optical sharpness on every GPU/monitor.

## Unity files and ownership

- Assets/Scripts/Game/NoteScriptGameApp.cs: gameplay, song library, import, state and scoring.
- Assets/Scripts/Game/NoteScriptGameNativeUI.cs: UI Toolkit views, input mapping, resolution settings, responsive layout.
- Assets/Resources/NoteScriptGamePanel.asset: runtime PanelSettings and theme.
- Assets/Scenes/NoteScriptGame.unity: NoteScriptGame Application has a UIDocument referencing PanelSettings.
- Assets/Editor/HighDpiSetup.cs: deterministic scene/panel setup and desktop defaults.
- Assets/Editor/NativeUiQualification.cs: verifies setup, parser/chart regressions and Windows build.

## Testing and limitations

Automated Unity Editor checks: native panel exists with correct scale mode, UIDocument references it, no IMGUI matrix in game source, playfield fitting logic exists, 21 Markdown songs parse, chart regressions pass and Windows executable compiles.

Standalone headless checks UI initialization and managed exceptions, not actual drawn pixels, monitor-specific font antialiasing, physical key input or sound output. Visual review on a physical 1080p or 1440p display remains a separate acceptance test.

## Next UI work

- Add visual themes, accessibility settings and localization.
- Handle very small window heights (less than 720 pixels) more gracefully.
- Add slide notes, manually authored charts, calibration and richer VFX.
- Build editable Piano Roll and track mixer over the same Song model.
