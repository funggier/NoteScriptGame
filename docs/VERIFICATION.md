# NoteScriptGame v0.1 verification

## Unity Editor version

6000.6.7f1 under `N:\Unity\6000.6.7f1\Editor\Unity.exe`

## Commands

Run Scene Setup (only if rebuilding the scene):
```powershell
& 'N:\Unity\6000.6.7f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'T:\Space\Projects\Games\NoteScriptGame' -executeMethod NoteScriptGame.Editor.ProjectSetup.CreateBootstrapScene -logFile 'T:\Space\Projects\Games\NoteScriptGame-setup.log'
```

Smoke test:
```powershell
& 'N:\Unity\6000.6.7f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'T:\Space\Projects\Games\NoteScriptGame' -executeMethod NoteScriptGame.Editor.ProjectSetup.RunSmokeChecks -logFile 'T:\Space\Projects\Games\NoteScriptGame-smoke.log'
```

Windows build:
```powershell
& 'N:\Unity\6000.6.7f1\Editor\Unity.exe' -batchmode -nographics -quit -projectPath 'T:\Space\Projects\Games\NoteScriptGame' -executeMethod NoteScriptGame.Editor.ProjectSetup.BuildWindows -logFile 'T:\Space\Projects\Games\NoteScriptGame-build.log'
```

## Most recent observed evidence

- **PASS** scene created and registered, setup log `NSG-SETUP PASS`.
- **PASS** 21 original Markdown song files parsed.
- **PASS** 14,700 canonical note/trigger events.
- **PASS** 31,880 rhythm chart notes across Easy/Normal/Hard.
- **PASS** deterministic chart assignments and same-lane collision validation.
- **PASS** pitch conversion C4=60 / F#4=66 and malformed notation rejection.
- Windows build and live interactive runtime qualification: see session result; do not assume PASS until verified.

## Important limits

A successful editor smoke test proves parser/chart correctness for the selected songs; it does **not** prove audio output latency, keyboard rollover, player build success, physical MIDI hardware, or UI usability. All of those need separate observed qualification.
