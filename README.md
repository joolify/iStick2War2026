# iStick2War

A 2D wave-based stickman war game built in Unity, targeting **Early Access** scope: short sessions, bunker defense, shop progression between waves, and escalating air/ground threats.

This repository is the active **Unity 6** codebase (`6000.4.1f1`). Gameplay logic is organized under `Assets/Scripts/` with a consistent **V2** architecture (composition roots, state machines, Spine-driven presentation).

**Current phase:** Feature complete → QA & polish → Early Access.  
**Release notes (v0.1.0 EA):** [docs/releases/0.1.0.md](docs/releases/0.1.0.md) — settings, run save/load, project status, and release gate checklists (EN/SV).

### Project metrics (at a glance)

| Metric | Value |
|--------|--------|
| Development time | ~3 months (focused, daily iteration) |
| Author background | Senior C# / .NET; new to Unity game shipping |
| V2 C# scripts (`*_V2.cs` under `Assets/Scripts/`) | 160+ |
| Hero weapon types (distinct gameplay) | 6 (Colt45, Thompson, Ithaca, Bazooka, Tesla, Flamethrower) |
| Enemy archetypes | 6 (paratrooper, bomb drone, kamikaze drone, helicopter, bomb plane, mech boss) |
| Boss encounters | 1 (mech robot) |
| Waves | 10 implemented in main scene; **15** planned for Early Access |
| Unity version | **6000.4.1f1** |
| AI tools | **Cursor** (in-repo implementation) + **ChatGPT** (scope, balance, review) |
| Rendering / animation | URP 2D, Spine (spine-unity) |

---

## Case study: from .NET developer to shippable 2D game in ~3 months

This document is written as a **technical project case / postmortem**, not as hype. It describes **what was built**, **how it was built**, and **what conclusions are reasonable**—including what the project does *not* prove.

### Why this story is useful (beyond the game)

Many portfolios say: *“I built a game in three months.”* That alone is hard to evaluate.

More informative for employers, consulting clients, investors, and other developers:

> An experienced **.NET developer** learned **Unity**, **Spine**, **game feel**, **wave design**, and **AI-assisted delivery**, and shipped a **playable multi-system product** in roughly three months—with explicit scope control and human ownership of architecture and playtesting.

Whether iStick2War becomes a commercial hit is almost secondary. The primary artifact is evidence of **learning velocity** and a **repeatable delivery pattern** in a new domain.

### Repeatable pattern

```text
Senior software developer
+
AI assistance (Cursor + ChatGPT)
+
Tight product scope
+
Daily development + playtesting
≈
Playable product in a few months (for this product shape)
```

The same pattern applies outside games—for example SaaS tools, internal apps, coaching products, or e-commerce—when scope is bounded and a senior engineer owns product and architecture.

### Who owned what

AI generated a lot of code in this repo; the product still had a clear owner. Typical split:

| Area | Owner |
|------|--------|
| Product vision | Human |
| Scope / feature freeze | Human |
| Architecture (V2 layers, prefab rules) | Human |
| Playtesting & feel calls | Human |
| Prioritization (EA vs feature creep) | Human |
| Implementation | Human + AI (Cursor) |
| Refactoring & cross-file fixes | AI + Human review |
| Bug hunting & integration | AI + Human verification |

**AI was a force multiplier—not a substitute for design, testing, or scope.** Without daily playtests and saying “no” to scope creep, the same tools would likely have produced a flashy demo, not a coherent game loop.

### Starting point

The project author brought **years of C# / .NET experience**, system design, and debugging discipline—but **not** a background as a game developer. Before this repo, there was little hands-on work in:

- Unity production workflows  
- 2D game architecture and “game feel”  
- Wave balancing and encounter design  
- Integrated audio/VFX pipelines  
- Steam / EA release logistics  

Roughly **three months** of focused, daily work—with a **tight scope** and heavy use of **AI-assisted development** (Cursor for in-repo implementation, ChatGPT for design sparring and review)—produced a **playable, multi-system game**, not a single-mechanic prototype.

### What this demonstrates

| Claim | Supported by this project |
|--------|---------------------------|
| A senior backend developer can learn a new domain quickly | Unity, URP 2D, Spine, physics layers, and game loops were adopted while shipping features |
| AI tools accelerate implementation when humans own architecture | V2 layer rules, prefab safety, and scope control stayed human-led; AI filled in boilerplate, refactors, and bug hunts |
| “Any game in 3 months” | **No** — see [Scope boundaries](#scope-boundaries) below |
| “A scoped 2D wave shooter to EA-quality loop in ~3 months” | **Yes, with remaining polish** — core loop, content skeleton, and production systems exist |

The durable outcome is not only a Steam SKU—it is a **repeatable pattern**: experienced engineer + clear product boundary + AI pair programming + daily playtesting.

---

### What was built

**Player / hero**

- Composition-root hero stack (`Hero_V2`, model, controller, state machine, view)  
- Movement, jump, grounding, bunker interaction  
- Spine body-part hitboxes (bounding boxes) and weapon-specific presentation  
- Multiple weapons with distinct rules: pistol, SMG, shotgun (pellets + falloff), bazooka, Tesla beam, flamethrower  
- Rocket/explosive projectiles, reload/ammo, dry-fire feedback  
- Death, continue / life-over flow, auto-aim assist (`AutoHero_V2`) for testing and accessibility  

**Enemies**

- **Paratrooper** infantry: deploy/glide/land, shoot, grenades, loot drops (mp40, helmet), gibbing, Tesla stun, facing toward hero  
- **Aircraft family** with shared bases: bomb drone, kamikaze drone, helicopter, bomb plane (horizontal flight, Spine events, pooling)  
- **Mech robot boss** with missiles and dedicated weapon/damage stack  

**Game loop**

- Wave configuration and `WaveManager_V2`  
- `EnemySpawner_V2` (drops, ground troopers, aircraft, difficulty multipliers)  
- **Shop** between waves: weapon unlock, ammo, UI presenters  
- Telemetry hooks for balance and debug (`WaveRunTelemetry_V2`)  
- Object pooling, safety despawn, hit-stop, health bars, main menu navigation  

**Presentation & audio**

- Esoteric **Spine** runtime integration (tracks, events, IK/crosshair on enemies)  
- `AudioManager_V2` with music/SFX categories, weapon loops, menu feedback  
- Impact VFX, explosions, shell casings, 2D lightning (plugin) where used  

**Engineering quality**

- 160+ `*_V2` C# scripts with documented navigation blocks in key areas  
- `AGENTS.md` and Cursor rules for consistent agent/human collaboration  
- Edit-mode / combat-matrix test harnesses under `Assets/Scripts/Testing/`  
- Explicit physics policies (e.g. hero vs bunker, walk-through infantry/loot, airborne bunker exclusion)  

**Content status (at time of writing)**

- **10** authored wave configs in the main scene; **15** waves planned for EA, including a final boss wave  
- Ongoing: wave balance, bug fixes, playtest passes, Steam packaging  

---

### Architecture snapshot

The V2 design favors **separation of concerns** over monolithic `MonoBehaviour` logic:

```text
Composition root (e.g. Paratrooper, Hero_V2, BombDrone_V2)
    → Model (data, HP, mirrored state)
    → StateMachine (StickmanBodyState / unit states)
    → Controller (AI, timing, weapon gates)
    → View (Spine clips, VFX only)
    → DamageReceiver / WeaponSystem / DeathHandler
    → BodyPart hitboxes (raycast targets)
Spine events → Forwarder → Controller (never gameplay rules in raw Spine callbacks)
```

Shared aircraft abstractions live in `Assets/Scripts/Enemies/AircraftBaseClasses/`. Game-wide loop code lives in `Assets/Scripts/Game_V2/` (see file headers with **NAVIGATION (Game_V2)** blocks).

For agent and contributor orientation, read **[AGENTS.md](AGENTS.md)**.

---

### How AI was used (and what stayed human)

**Cursor (in the repo)**

- Implement features and fixes across many files with project rules loaded  
- Search/refactor V2 patterns, prefab-safe serialization (`FormerlySerializedAs`), integration tests  
- Repetitive tuning (audio wiring, shop UI, collision exclusions)  

**ChatGPT (outside the repo)**

- Scope checks (“EA-ready vs feature creep”)  
- Balance and UX discussion  
- Retrospectives and release prioritization  

**Human-owned decisions**

- Architecture boundaries (no gameplay in View)  
- Fun/feel priorities and feature freeze  
- Accept/reject AI diffs, playtest interpretation  
- Final ship/no-ship and wave design  

Without **daily playtesting** and **feature discipline**, the same tools would likely have produced an impressive demo that does not hold together as a game.

---

### Scope boundaries

This project is evidence for **one product shape**:

- 2D, side-view, wave-based session length  
- Limited enemy roster with deep systems rather than hundreds of assets  
- Single-player, no live ops  

It is **not** evidence that the same calendar time applies to:

- Large RPGs, MMO-scale content, or 100+ hour progression  
- Real-time multiplayer and dedicated server stacks  
- Custom engine work or heavy procedural worlds  

---

### Tech stack

| Area | Choice |
|------|--------|
| Engine | Unity **6000.4.1f1** |
| Rendering | URP 2D |
| Animation | Spine (spine-unity) |
| Language | C# (.NET profile per Unity player settings) |
| Primary scene | `Assets/Scenes/SampleScene.unity` |

Authoritative compile validation: open the project in the Unity Editor. `iStick2War.Game.csproj` may be auto-generated and can lag folder moves until Unity refreshes.

---

### Repository map

| Path | Contents |
|------|----------|
| `Assets/Scripts/Game_V2/` | Waves, shop, spawner, audio, UI, telemetry, pooling |
| `Assets/Scripts/Hero_V2/` | Player composition root and combat |
| `Assets/Scripts/Enemies/` | Paratrooper, aircraft, mech boss, shared bases |
| `Assets/Data/` | Weapons, wave assets |
| `Assets/Scenes/` | Main gameplay and menu scenes |
| `AGENTS.md` | Short orientation for humans and coding agents |
| `docs/releases/` | Versioned release notes (e.g. [0.1.0.md](docs/releases/0.1.0.md)) |
| `.cursor/rules/` | Project conventions for Cursor |

---

### Building and running

1. Install Unity **6000.4.1f1** (see `ProjectSettings/ProjectVersion.txt`).  
2. Clone the repo and open the project folder in Unity Hub.  
3. Open `Assets/Scenes/SampleScene.unity` (or the scene your team uses as entry).  
4. Press Play; use the in-game shop/wave flow documented in scene wiring.  

For scripted test harnesses, see `Assets/Scripts/Testing/`.

---

### Roadmap to Early Access (high level)

1. **Feature freeze** — finish planned waves (target **15**), critical bugs only.  
2. **Balance pass** — 15–20 full playthroughs with notes (death cause, wave, economy).  
3. **Outsider playtest** — one build, 30 minutes, no verbal coaching.  
4. **Steam** — store page, build pipeline, known-issues list.  

---

### License and commercial status

Add your license and Steam/store links here when published. Until then, treat this repository as the private/source tree for the iStick2War product.

---

### Portfolio framing

If the project reaches **15 waves**, a **stable build**, **external playtest**, and **Steam release**, iStick2War is a strong portfolio case for:

> How an experienced **C# / .NET developer** used AI to become productive in **Unity** quickly and deliver a **playable game with multiple cooperating systems**—wave loop, shop economy, distinct weapons, air/ground enemies, boss, audio, and UI.

That narrative is often more interesting to technical audiences than the stickman theme alone.

### Summary

**iStick2War** is a concrete example of an experienced software developer learning Unity game development quickly, with AI as a force multiplier—not a substitute for design, testing, or scope. The codebase is structured for continued iteration (V2 patterns, agent docs, shared enemy bases). Finishing EA is primarily a question of **content completion, polish, and validation**, not of proving the core technical approach.
