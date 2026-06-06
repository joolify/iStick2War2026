# AGENTS.md — iStick2War (Unity)

Short orientation for humans and coding agents working in this repository.

## Project

- **Unity** editor version: see `ProjectSettings/ProjectVersion.txt` (e.g. `6000.4.1f1`).
- **Primary gameplay C#** lives under `Assets/Scripts/` (`Game_V2`, `Hero_V2`, `Enemies`, …).

## Where to start reading

| Topic | Location |
|--------|----------|
| Wave loop, shop, enemy spawn, telemetry, object pool | `Assets/Scripts/Game_V2/` — look for **`NAVIGATION (Game_V2)`** in file headers. |
| Hero | `Assets/Scripts/Hero_V2/Hero_V2.cs` (composition root) and linked systems. |
| Flying enemies & shared aircraft patterns | `Assets/Scripts/Enemies/` — unit folders + `AircraftBaseClasses/`. |
| Cursor / agent conventions | `.cursor/rules/istick2war-unity-skills.mdc` (always applied). |

## Build and validation

- **Authoritative compile**: open the project in the **Unity Editor** and let scripts compile. Unity manages assemblies and package references.
- **`iStick2War.Game.csproj`**: may be **auto-generated**; paths can lag folder renames until Unity refreshes the project. If `dotnet build` fails on missing paths, **re-sync/regenerate** from Unity rather than hand-editing large include lists unless you intend to patch the generator.
- Prefer **Play Mode** or project-specific **Edit Mode** tests (if present) for behaviour verification over CLI-only assumptions.

## Conventions (summary)

- **V2 C# comments**: file-level `/* */` and `//` line comments; **no** `///` XML documentation for game code (see Cursor skill `istick-v2-csharp-documentation`).
- **Prefab safety**: when **moving** `.cs` files, preserve the **`.meta` GUID`** so existing prefabs keep script references.
- **Architecture**: keep gameplay out of **View**; use existing Model / Controller / StateMachine splits (see rule file above).

## Git workflow (solo / Unity)

The maintainer uses **one branch only** (`master`) — **no feature branches** — to avoid painful Unity merge conflicts (scenes, prefabs, `.meta`).

| Practice | Detail |
|----------|--------|
| **Scope** | One feature at a time (~1–2 hours of Cursor work). |
| **Success** | Play Mode / manual verify → **commit + push** to `master`. |
| **Failure** | Discard all changes; **`git reset --hard`** to latest commit (worst case: lose that session). |
| **Commits** | Small, frequent when working; message should state *why* (e.g. fix vs feature). |
| **Agents** | Do not create branches or long-lived WIP unless the user explicitly asks. Prefer a focused diff that can be committed or fully reverted in one step. |

Optional safety: tag milestones before risky refactors (e.g. `ea-0.1.0-candidate`). Before `reset --hard`, confirm no untracked assets the user meant to keep (audio, scenes).

## After structural changes

- If you add a new **composition root** or move a major **entry-point** file, update the file’s **header navigation** block and, if relevant, `.cursor/rules/istick2war-unity-skills.mdc` or this file in the same PR.
