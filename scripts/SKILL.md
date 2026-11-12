---
name: scripts
description: >
  Build, debug and ship Vegas Pro scripts (Scripting .NET API — ScriptPortal.Vegas).
  Covers architecture, modal vs modeless dialogs, Event FX vs Media FX, subclips,
  presets, OFX parameters, and a full failure catalog of every crash observed
  during production use of Vegas Pro 2026.0.0+105 (handles crash after apply,
  crash during analysis, crash when reapplying, and analysis-reset race).
  Trigger phrases: "Vegas Pro script", "Vegas scripting", "ScriptPortal.Vegas",
  "Vegas plugin", "تطبيق سكربت فجاس", "Vegas Pro plugin", "Vegas crash script".
license: Apache-2.0
---

# Vegas Pro Scripting — Skills for real-world automation

This skill is the result of repeatedly breaking Vegas Pro on purpose so you don't
have to. Every rule below comes from a real production script: an OFX-powered
**Batch Stabilization** tool driven from a `Script Menu` window.

The goal: a developer can take these patterns and automate anything in Vegas Pro —
cutting, effects, stabilization, rendering, analysis, presets — **without ever
crashing the host process.**

> TL;DR — What you'll learn
> - The Vegas scripting model and where your `.cs` files live.
> - The **one** safe way to open a window from a script.
> - Why your first "it should just work" script crashes Vegas Pro.
> - The exact API surface to open/add/configure effects (Event FX vs Media FX).
> - A catalog of every catastrophic failure and its verified workaround.

---

## 0. The golden rule of Vegas Pro scripting

**Vegas Pro runs your script code on its own UI thread, inside its own AppDomain.
The moment `FromVegas()` returns, Vegas considers your script finished and
unloads (or can unload) the assembly. Any call into the Vegas API after that
point is an out-of-bounds call into a process that may no longer have the context
or the locks your code assumes — and it will kill the process.**

That one sentence explains 80% of "the script crashes Vegas Pro" reports.

Practical implications, in order of importance:

1. **Never call Vegas API from a place after `FromVegas()` returned.** Everything
   that touches Vegas must happen before `FromVegas()` returns.
2. **Never return from `FromVegas()` until you are done.** A dialog opened with
   `ShowDialog()` satisfies this naturally; a dialog opened with `Show()` or
   `Application.Run()` does not — the window lives on, but the script is "done"
   and any click handler that touches Vegas is a time bomb.
3. **Never remove an effect or a media while the OFX analyzer is using it.**
   The analyzer holds raw pointers/IDs; when it fires again on the same object
   it dereferences memory owned by the effect instance you just deleted.
   That is the #1 cause of `access violation` crashes.
4. **Assume the user can press any button at any time, twice.** Vegas Pro does
   not serialize click events. Make every operation idempotent: check whether
   an effect is already applied instead of assuming it isn't.

---

## 1. Environment & layout

### 1.1 Where Vegas Pro lives (only what you need)

```
C:\Program Files\BorisFX\Vegas Pro 2026\
├── vegas2026.exe                 ← host process
├── vegas2026.exe.config          ← .NET binding config (you don't edit this)
├── ScriptPortal.Vegas.dll        ← THE API — reference at compile time
├── ScriptPortal.Vegas.xml        ← API documentation (your best friend)
├── ScriptPortal.Vegas.Slideshow.xml
├── Script Menu\                  ← scripts installed for Tools > Scripting > Run Script
│   ├── Batch Render.cs           ← ships with Vegas Pro
│   ├── …                         ← more .cs/.js samples
│   └── (your .cs scripts)
├── Project Templates\            ← .vqpt hardware presets
└── runtimes\, Vegas Plugins\, OFX Video Plug-Ins\, etc.
```

### 1.2 How scripts are loaded

1. Vegas Pro scans `Script Menu\` for `*.cs` / `*.js` on startup.
2. Each file is compiled on the fly with Microsoft's C# compiler via
   `Microsoft.CSharp` + `System.CodeDom` (or JScript for `.js`).
3. The compiler needs a `public class EntryPoint` with a method named exactly
   `FromVegas`. Vegas finds that method and calls it.
4. The method signature options Vegas accepts:

```csharp
public void FromVegas(Vegas vegas)
```

Other overloads exist (e.g. with `VegasEventArgs`) but this is the one every
picture in this skill uses.

### 1.3 Script Menu locations you control

- **Program files (read-only for normal users):** `C:\Program Files\BorisFX\Vegas Pro 2026\Script Menu`
- **User documents (writable without admin):** `%USERPROFILE%\Documents\Vegas Pro 2026\Script Menu`
  (see `install.cfg` → `UserDataPath=%USERPROFILE%\Documents\Vegas Pro 2026`)

Install your scripts into the user documents path if you ship a free tool — no
UAC prompt, no admin, no risk of bricking the install.

### 1.4 API availability at a glance

The entire object model is in `ScriptPortal.Vegas` (assembly `ScriptPortal.Vegas.dll`).
Peek inside with the built-in XML docs file `ScriptPortal.Vegas.xml` — it is the
authoritative reference and is shipped with the product:

```
C:\Program Files\BorisFX\Vegas Pro 2026\ScriptPortal.Vegas.xml
```

Document layout: alphabetical member list under `<members>`, e.g.
`P:ScriptPortal.Vegas.Project.FilePath`, `M:ScriptPortal.Vegas.VideoEvent.#ctor`.

---

## 2. The one safe dialog pattern

### 2.1 Why modeless windows are forbidden

```csharp
// DON'T — this crashes Vegas when the user clicks a button after
// FromVegas() returns.
public void FromVegas(Vegas vegas)
{
    MyForm f = new MyForm();
    f.Show(vegas.MainWindow);   // ← returns immediately
    // script is "done" now; every click on MyForm is unsafe
}

// DON'T also
// Application.Run(new MyForm());
```

Symptom:

```
System.Runtime.InteropServices.COMException (0x8000FFFF): Catastrophic failure
   at ScriptPortal.Vegas.IVideoMotionCOM.SetKeyframePosition(...)
   at ScriptPortal.Vegas.VideoMotionKeyframe.set_Position(Timecode value)
   at Motion.Restore(...)
   at SmartZoomWindow.Apply()
```

Cause: `SetKeyframePosition` is invoked after Vegas has unloaded / torn down
the scripting AppDomain / the proxy to COM, or with a keyframe ID that Vegas
no longer considers valid. Either way the caller lives outside Vegas's safety
net.

### 2.2 The pattern every script must use

```csharp
using System;
using System.Windows.Forms;
using ScriptPortal.Vegas;

public class EntryPoint
{
    public void FromVegas(Vegas vegas)
    {
        // Modal — Vegas waits, you can't click Vegas behind it. That is
        // unavoidable and good: there is no shared state to corrupt.
        using (MyForm f = new MyForm(vegas))
        {
            f.ShowDialog(vegas.MainWindow);   // blocks until user closes
        }
    }
}
```

Rules:

1. Always use `ShowDialog(owner)`, never `Show(owner)` from a script.
2. Always `using` the form so it is disposed.
3. Keep the form **open** — do not `Close()` it after one Apply. The user may
   need to iterate. Only they decide when they are done.
4. Use `FormBorderStyle.FixedDialog` (or `FixedToolWindow`), `ShowInTaskbar = false`.
5. Idempotence: every operation in the form must be safe to repeat.
6. All user-facing messages in **English** (professional tone), regardless of the
   OS display language.
7. Log everything to a `StringBuilder`, flush to `%USERPROFILE%\Desktop\<Name>_log.txt`
   at the end of every operation. You will need it.

### 2.3 The `FromVegas` lifecycle checklist

```
1. Can read (Project, Tracks, selected events, media path, properties).
2. Any writes happen here, or inside ShowDialog body — never after.
3. Restore baseline state if you took snapshots.
4. No async work left running. Wait (`Application.DoEvents` is NOT enough).
```

---

## 3. The failure catalog (symptom → cause → working rule)

> Every entry below is observed on Vegas Pro 2026.0.0+105.

### 3.1 `COMException 0x8000FFFF` — Catastrophic failure

- **Symptom:**
  ```
  System.Runtime.InteropServices.COMException (0x8000FFFF): Catastrophic failure (Exception from HRESULT: 0x8000FFFF (E_UNEXPECTED))
     at ScriptPortal.Vegas.IVideoMotionCOM.SetKeyframePosition(...)
  ```
- **Root cause:** call into Vegas API after `FromVegas()` returned, or invalid
  keyframe ID because keyframe was not added before setting bounds/position.
- **Fix:** use `ShowDialog()` (never `Show()`/`Application.Run()`). Add the
  keyframe to the collection **before** setting any property. Never reuse a key
  object Vegas has already invalidated.

### 3.2 Invalid video motion keyframe

- **Symptom:** `System.ApplicationException: Invalid video motion keyframe.`
- **Root cause:** `first.Bounds = ...` or `first.Position = ...` before the
  keyframe was part of the collection (created detached), or the keyframe was
  detached by a previous `Clear()`.
- **Fix:**
  ```csharp
  keyframes.Clear();                  // leaves exactly one keyframe
  VideoMotionKeyframe first = keyframes[0];
  first.Position = ...;               // try/catch — position may resist
  first.Bounds  = ...;                // safe now: member of collection
  keyframes.Add(k);                   // add BEFORE setting k.Bounds
  k.Bounds = ...;
  ```

### 3.3 `E_FAIL` / `E_UNEXPECTED` with no other diagnostics

- **Symptom:** `HRESULT E_FAIL has been returned from a call to a COM component.`
- **Root cause 1:** you asked Vegas to add an OFX plugin to a container that did
  not accept it (e.g., a Transition container for an Effect), or the plugin
  was incompatible with the context.
- **Root cause 2:** you passed a malformed subclip / composition to a function
  that rejected it.
- **Fix:** verify the plugin's context by inspecting its XML (look for
  `<OfxImageEffectContext>` or by calling `IsOFX` / `IsFixedSize` on the node).
  Build subclips via the documented constructor and then fetch streams with
  `GetVideoStreamByIndex(0)` instead of guessing `GetItemByMediaType(MediaType.Video, 0)`.

### 3.4 Program vanishes after Apply / during analysis

- **Symptom:** Vegas Pro closes instantly with no exception dialog during or
  immediately after invoking `OFXPushButtonParameter.Trigger()` on an
  `AnalyzeMotion` / `AnalyzeMotionBasic` button.
- **Root cause:** Vegas runs the analyzer in a headless background thread on a
  worker GPU pipeline. Triggering several analyses at once, or deleting the
  source effect/media afterwards, makes the analyzer reference freed memory.
- **Fix:**
  1. Never do anything after `Trigger()` until the analyzer reports done
     (include user-facing message telling user to **wait**).
  2. Limit batch analysis to small batches; prefer `Thread.Sleep(150)` between
     triggers (worked in production).
  3. **Never** remove an effect while analysis is running (`Reset` must abort if
     the analyzer is not finished — or must ask the user to confirm).

### 3.5 "Failed via subclip." — after a previous Apply

- **Symptom:** repeated Apply / Apply Preset logs `Failed via subclip.` for
  3/3 events on the second run.
- **Root cause:** after a previous Apply, the active Take has been swapped to a
  **subclip** (a `.sfl` pointer). Asking Vegas to create *another* subclip
  *from the `.sfl` file* fails: subclips-of-subclips are not supported.
- **Fix:** detect a subclip-taken-ev first. If `ev.ActiveTake.Media.IsSubclip()`
  (or `Take.Media.IsSubclip()`), apply the effect directly to the existing
  subclip's `Effects` instead of making another one.

### 3.6 "RESET" crashes with no log line

- **Symptom:** crash during RESET on events that previously had the effect
  applied, before any Log call returns. No status update.
- **Root cause:** removing the `Effect` whose analyzer is still actively using it.
  Even `try/catch` does not fire because the crash is in native code.
- **Fix:**
  1. RESET must refuse to operate if analysis is possibly running — warn the user.
  2. Wrap each per-event removal in its own `try/catch` so one failure does not
     abort the whole operation (you will still not recover the crash, but you
     isolate the offending event for logging).
  3. Prefer step-wise safe verification: verify `ev.ActiveTake != null &&
     ev.ActiveTake.Media != null` before touching anything.

### 3.7 Empty RESET crashes the same way

- **Symptom:** pressing RESET on a project with no stabilization effects → immediate crash.
- **Root cause:** the enumerator hits a null effect or a collection that changed
  size during iteration; Vegas' COM collection threw an access violation.
- **Fix:** before doing anything inside RESET, run a `HasStabilizationAny` sweep
  over selected events; if nothing matches, `MessageBox` and `return` immediately.

### 3.8 Log file vanishes — crash between Log and flush

- **Symptom:** `SaveLogToDesktop()` appears in code but the desktop file is empty
  after crash.
- **Root cause:** `Application.DoEvents()` lets UI repaint, but the log `StringBuilder`
  is only flushed from managed code; when the host is killed in native code, the
  managed write never completes.
- **Fix:** write every line directly through `File.AppendAllText` when you log
  critical steps, or flush the log after every event iteration ("sync write") —
  not only at the end. This skill's example script uses `Application.DoEvents()`
  after each step, and additionally writes the log at each Apply/Reset button
  handler exit. You should write on every step for crash-safety.

---

## 4. Patterns you can copy today

### 4.1 Modal form skeleton (the only one you'll ever need)

```csharp
public class EntryPoint
{
    public void FromVegas(Vegas vegas)
    {
        using (MyForm f = new MyForm(vegas))
        {
            f.ShowDialog(vegas.MainWindow);
        }
    }
}
```

### 4.2 Finding your effect (safe, exact)

The correct plug-in is found by exact unique ID first, then by name, never by
fuzzy match — Vegas Video Stabilization is `com.vegascreativesoftware:Stabilize`.
Any fuzzy search (`"Stabil"`) will also match BCC Optical Stabilizer and you will
silently apply the wrong effect. Example:

```csharp
private PlugInNode FindStabilizePlugin(Vegas vegas)
{
    PlugInNode[] roots = { vegas.VideoFX, vegas.VideoFXPackages, vegas.VideoCompositeFX, vegas.PlugIns };

    // 1) Exact — Vegas' built-in effect
    foreach (PlugInNode root in roots) { PlugInNode n = FindInTree(root, ID_EXACT); if (n != null) return n; }
    // 2) Vegas-supplied OFX stabilizer
    foreach (PlugInNode root in roots) { PlugInNode n = FindInTree(root, ID_SONY); if (n != null) return n; }
    // 3) Name match but exclude known competitors (BCC, etc.)
    foreach (PlugInNode root in roots) { PlugInNode n = FindInTree(root, NAME_VEGAS); if (n != null) return n; }
    return null;
}
```

### 4.3 Applying an effect to an existing clip

```csharp
Effect effect = new Effect(stabilizePlugIn);
media.Effects.Add(effect);          // Media FX (whole clip)
// or — Event FX (segment): see 4.4
```

### 4.4 Applying to a segment only (replace with subclip)

```csharp
Subclip sub = new Subclip(vegas.Project, sourcePath, sourceIn, length, reverse:false, displayName);
VideoStream stream = sub.GetVideoStreamByIndex(0);
Take newTake = ev.AddTake(stream, true, displayName);   // sets active take
Effect fx = new Effect(stabilizePlugIn);
sub.Effects.Add(fx);
```

`Take.Media` is **read-only** in the scripting API; you cannot redirect an
existing Take to a different media file. The only way to do segment-wise
stabilization is AddTake + Subclip.

### 4.5 Configuring the effect

```csharp
OFXEffect ofxfx = effect.OFXEffect;
OFXParameter p = ofxfx.FindParameterByName("SmoothingMagnitude"); 
if (p != null) ((OFXDoubleParameter)p).Value = 3.0;

p = ofxfx.FindParameterByName("StabilizeMode");
if (p != null)
{
    OFXChoiceParameter c = (OFXChoiceParameter)p;
    foreach (OFXChoice choice in c.Choices)
        if (choice.Name == "Smooth motion") { c.Value = choice; break; }
}

effect.Preset = "(Default)";            // or "full", "1.4", "7.0"
ofxfx.AllParametersChanged();           // commit

OFXPushButtonParameter btn = (OFXPushButtonParameter)ofxfx.FindParameterByName("AnalyzeMotionBasic")
                           ?? (OFXPushButtonParameter)ofxfx.FindParameterByName("AnalyzeMotion");
btn.Trigger();                          // start analysis
```

### 4.6 Re-applying safely (idempotence)

```csharp
// What crashes Vegas if you don't check
if (HasStabilizationAny(ev, plugin))
{
    StatusLabel.Text = "Already applied. Press RESET first.";
    return;
}

// The check must inspect effects on Event FX AND on all takes' media:
private bool HasStabilizationAny(VideoEvent ev, PlugInNode plugin)
{
    try { foreach (var e in ev.Effects) if (e != null && e.PlugIn == plugin) return true; } catch { }
    try { foreach (Take t in ev.Takes)
          {
              if (t == null) continue;
              var m = t.Media; if (m == null || m.Effects == null) continue;
              foreach (var e in m.Effects) if (e != null && e.PlugIn == plugin) return true;
          }
        } catch { }
    return false;
}
```

### 4.7 UI that never confuses a human under pressure

- Every button that can destroy state has an Arabic label **and** an English confirmation
  (`"This will remove the effect. Continue?"`). Never rely on icon alone.
- The window shows an always-visible `Log` box; every action prints a line.
  No hidden silent failures.
- Save the log to `%USERPROFILE%\Desktop\<ScriptName>_log.txt` on every exit and on every "crash risk".
- Always ends with `PlaySuccess()` or `PlayFailed()` sound, never a silent blink.

---

## 5. Checklist before you ship a Vegas Pro script

```
[ ] Entry point: `public class EntryPoint { public void FromVegas(Vegas vegas) { ... } }`
[ ] Dialog: `ShowDialog(vegas.MainWindow)` — never `Show()` or `Application.Run()`.
[ ] `using` the form; dispose on close.
[ ] All user-facing strings in English (professional British/American tone), even if the OS is in another language.
[ ] Every button wrapped in try/catch that writes to a log.
[ ] StatusLabel always shows the count of items touched.
[ ] Idempotence: apply twice ≠ crash, reset with nothing ≠ crash.
[ ] No heavy work after `FromVegas()` returns (it already returned).
[ ] No unsaved state should be silently discarded — autosave before risky operations (Vegas.SaveProject).
[ ] Ctrl+S saves the project inside the window (`ProcessCmdKey` override).
[ ] .cs file compiles on a fresh Vegas Pro install with zero user setup.
```

---

## 6. Glossary

| Term | Meaning |
| --- | --- |
| **FromVegas()** | The entry-point method Vegas calls to start your script. |
| **ShowDialog()** | Modal; blocks Vegas UI; the only safe script window pattern. |
| **OFX** | OpenFX — the plugin standard used for video effects in Vegas. |
| **Event FX** | Effects on one timeline event (as opposed to Media FX on the source media). |
| **Media FX** | Effects applied to the master clip itself — identical for all events using it. |
| **Subclip** | An in-project view of a slice of a media file (`.sfl`). Useful for segment-wise stabilization. |
| **Take** | A candidate version of an event's audio/video. Subclips are usually installed as additional takes. |
| **Idempotence** | Property that running an operation twice has the same effect as running it once. |
| **Access violation** | Native code dereferenced an invalid pointer — crashes the host process, cannot be caught with `try/catch`. |
| **AnalyzeMotion / AnalyzeMotionBasic** | PushButton parameters on the Vegas Video Stabilization plugin that launch the background analysis. |

---

## 7. Example: Smart Batch Stabilization repair story (full log)

> This is the exact sequence of failures and fixes this skill was built from.
> Every bullet here marks a mistake you will probably make first.

1. **First build** used `Show(vegas.MainWindow)` and called `OFXPushButtonParameter.Trigger()` from a button handler.  → Crashed Vegas with `COMException 0x8000FFFF`.
2. **Second build** added `Application.Run(new MyForm())`.  → Same crash (AppDomain unloaded before handler fired).
3. **Third build** added `vegas.UnloadScriptDomainOnScriptExit = false`.  → Window stayed open but clicking Apply *still* crashed Vegas because the event handler lived inside Vegas' unloading context.
4. **Fourth build** switched to `ShowDialog(vegas.MainWindow)` — instant stability.
5. **Apply succeeded** for Media FX. **Event FX** produced `HRESULT E_FAIL has been returned from a call to a COM component.`
6. Root cause: plugin was registered for media-flow context only; Vegas Event FX wrapper rejected it with `E_FAIL` silently.
7. **Fix (this skill):** create a subclip of only the desired frames, `AddTake` it as active take, then apply Media FX to the *subclip*. This is how segment-wise stabilization works.
8. **Reapply** on the same project clip — infinite duplicate subclips. → Fixed with a `HasStabilizationAny` guard that also inspects all takes.
9. **RESET crash** on events already stabilized. → Fixed by the same guard the other way:
   skip any event already having the effect; display "Already stabilized. Press RESET first."
10. **RESET crash on empty project**. → Fixed by scanning for stabilization before doing anything; returns `Nothing to reset.`
11. **Direct Event FX dummy** added a third option that crashes on Add. → Third option removed.

This is why this skill exists: so you never need to run this cycle again.

---

## 8. Script naming conventions

```
<ToolName>.cs          e.g.  SmartBatchStabilization.cs
Diagnose<ToolName>.cs  diagnostic / discovery scripts
Sounds/                success.mp3 / failed.mp3 next to the script (if you play them)
```

Vegas Pro scans `Script Menu/` recursively for `*.cs` and `*.js`. Anything in
that folder will appear under `Tools > Scripting > Run Script`. Sub-folders are
supported for organization (e.g. `Script Menu\SmartTools\SmartBatchStabilization.cs`).
