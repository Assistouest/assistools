# Debug Session: game-booster-null-ref

## 1. Issue Description
**Symptoms**: A `NullReferenceException` is thrown at `Assistools.Pages.PageGameBooster.Profile_Checked` (line 76) during application startup or when interacting with profile toggle buttons.
**Reproduction**: Run the app in debug mode. The crash happens either on startup due to `InitializeComponent` triggering `Profile_Checked` or when clicking a profile button.
**Environment**: WinUI 3, C#.

## 2. Hypotheses
1. **Hypothesis 1**: `_gameBoosterService` is being accessed before it's initialized in the constructor. (Falsified by the `if (_gameBoosterService == null)` check, but maybe the check itself throws if it's not a simple null check).
2. **Hypothesis 2**: The `sender` object is `null` or casting it to `ToggleButton` results in a null reference later in the line (e.g., `btn.IsChecked == true` throws if `btn` is somehow a weird proxy object, though unlikely).
3. **Hypothesis 3**: The exception is actually thrown inside `UpdateProfileSelection` which is called from line 86, and the stack trace is slightly misleading (common in async/optimized builds). Specifically, accessing `BtnGamer`, `BtnStreamer`, etc., before they are fully initialized by `InitializeComponent`.
4. **Hypothesis 4**: The `Tag` property of the `ToggleButton` is null, causing `btn.Tag is string tagStr` to fail in an unexpected way (unlikely to throw NRE, but possible).

## 3. Instrumentation Plan
I will add `System.Diagnostics.Debug.WriteLine` statements to trace the exact execution flow and variable states inside `Profile_Checked` and `UpdateProfileSelection`. Since this is a C# WinUI 3 app and the user is already attached with a debugger, native `Debug.WriteLine` is the most direct way to get evidence.

## 4. Execution Log
- **[2026-05-23]**: Created debug session. Adding instrumentation.

## 5. Next Steps
- Add instrumentation to `PageGameBooster.xaml.cs`.
- Ask the user to reproduce the crash and share the debug output.