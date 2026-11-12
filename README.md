# skills-vegas_pro

**Powerful skills for Vegas Pro scripting — open, free, and battle-tested.**
مهارات قوية للسكربت في Vegas Pro — مفتوحة، مجانية، ومجرّبة أثناء العمل الفعلي.

> ⚡ Free for everyone. Fork it, extend it, build your own on top of it.

This repository teaches developers how to build, debug, and ship real
**Vegas Pro** scripts — what works, what crashes the engine, and how to
avoid the traps that cost months of trial-and-error.

This is not a list of API signatures. Every pattern documented here comes
from a real production script that repeatedly crashed **Vegas Pro 2026.0.105
(Boris FX / Magix)** while applying OFX Video Stabilization, and every rule
shown here is there because violating it reliably killed the host process.

---

## Repository layout

```
skills-vegas_pro/
├── README.md                  ← you are here
├── LICENSE                    ← Apache-2.0
├── CONTRIBUTING.md            ← how to contribute a skill or example
├── .gitignore
└── scripts/
    ├── SKILL.md               ← the main skill (start here)
    └── assets/
        ├── SmartBatchStabilization.cs   ← full working reference script
        └── DiagnoseVideoFX.cs          ← small diagnostic script
```

## Quick start

1. Copy `scripts/assets/SmartBatchStabilization.cs` into your Vegas Pro `Script Menu`
   folder (e.g. `C:\Users\<you>\Documents\Vegas Pro 2026\Script Menu` or the
   program's `Script Menu` folder — see `install.cfg` → `UserDataPath`).
2. Restart Vegas.
3. From the menu: `Tools > Scripting > Run Script > SmartBatchStabilization.cs`.

Read `scripts/SKILL.md` for everything the hard way — with explanations of **why**
each rule exists, and a map of every mistake we made so you don't repeat them.

## What you'll find in the skill

- The Vegas scripting model (API object model, file layout, how scripts compile).
- How to build a **safe** in-app dialog (modal vs modeless, and why one of them crashes).
- How to add/configure/remove OFX effects programmatically — Event FX, Media FX,
  subclips, parameters, presets, triggers, and reset behavior.
- How to keep state across a crash (save/autosave, undo-friendly patterns).
- How to play sound effects via `winmm` / MCI, and persist presets.
- A catalog of every common crash and the verified workaround.

## Contributing

See [`CONTRIBUTING.md`](CONTRIBUTING.md). We accept:
- New skills (frontmatter + content, English or Arabic).
- New example scripts with reproduction steps.
- Bug reports with crash logs (`.log` / `.dmp`) and Vegas version/build.

Please follow the same section structure as the existing `SKILL.md`.

## License

Licensed under the Apache License 2.0. See [`LICENSE`](LICENSE).
