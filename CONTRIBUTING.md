# Contributing to skills-vegas_pro

Thanks for helping. This repository is a collection of **battle-tested** Vegas Pro
scripting knowledge. We want documentation and example scripts that someone can
use on a Tuesday afternoon against a real project without reading the manual.

## Ground rules

1. **Do not paste Boris FX / Magix / Sony code.** Only use the public scripting
   API surface that ships with Vegas Pro (e.g. `ScriptPortal.Vegas.dll` and the
   XML/OFX descriptor files already inside the installation). Your contributions
   are licensed under Apache-2.0 — don't publish code you don't have the right
   to license.

2. **Reproduction first.** Every "don't do X" in the skill must include the
   observable symptom (error message / crash / misbehavior), the Vegas build
   it happens on (e.g. `2026.0.0+105`), and a minimal repro or at least the
   exact operation sequence.

3. **No false promises.** If a section says "this crashes", say precisely when
   and why, and what to do instead. If something is unknown / untested, mark it
   `TODO:`.

4. **Keep it real.** Example scripts must compile against Vegas Pro 2026
   (`ScriptPortal.Vegas.dll`) and not import non-public APIs.

## File structure for new skills

```
skills/<skill-name>/
├── SKILL.md           ← main skill (frontmatter + sections)
└── assets/            ← example scripts / files (optional)
```

Frontmatter format:

```
---
name: <lowercase-with-dashes>
description: >
  One sentence. What it does + when to use it. Trigger phrases inside.
license: Apache-2.0
---
```

Section structure for consistency:

- **0. TL;DR** — what this prevents / teaches
- **1. Environment & layout** — folders, API, which Vegas versions tested
- **2. Core patterns** — how to write a correct dialog/script
- **3. Failure catalog** — symptom → cause → fix (MANDATORY)
- **4. Example assets** — annotated examples
- **5. Checklist** — pre-`Send` QA

## Adding or updating a script asset

- Keep it single-file (`.cs`) self-contained.
- Name it descriptively (e.g. `DiagnoseVideoFX.cs`).
- Add a header comment with: purpose, tested Vegas version, last known crash.
- If the script is likely to crash on some machine state, guard those regions
  with early returns + clear message boxes, never silent pass.

## Style

- English preferred; Arabic is accepted for section intros.
- Code fences must compile. No `YOUR_CODE`, no pseudocode in `assets/`.
- No `TODO` left in committed `SKILL.md` without filing an issue.
- Keep lines ≤ 100 chars where practical.

## Reporting crashes

When opening an issue, include:

1. Vegas version/build (e.g. `2026.0.0+105`).
2. OS build (`winver`).
3. Which script / which button / which sequence.
4. The log file if the script wrote one (look in `%USERPROFILE%\Documents\Vegas Pro <year>\`).
5. Whether it crashed during/after OFX analysis (this distinguishes
   managed-vs-native failures).
