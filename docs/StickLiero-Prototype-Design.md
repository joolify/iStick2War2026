# StickLiero — Prototype Design Doc (v0.13)

**Status:** Pre-production (start after iStick2War Steam-ready slice)  
**Working title:** StickLiero  
**Genre:** Real-time 2D arena shooter with fully destructible terrain  
**Reference:** [Liero](https://en.wikipedia.org/wiki/Liero) (1998), MoleZ / NiL lineage  
**Terrain tech (planned):** [Destructible 2D](https://assetstore.unity.com/packages/tools/sprite-management/destructible-2d-18125) (Unity Asset Store)  
**Prior validation:** University group project [**LieroRemake**](https://github.com/gardsgard/LieroRemake) (*Interface programming 2*) — simple StickLiero-style clone with **Destructible 2D + Spine**; dig/shoot + terrain destruction proved in practice.

**Red thread (whole plan):** **Ship first. Prove fun. Then build the esport layer.** (iStick2War EA → StickLiero feel → local 2P → online → ranking / spectator / replays.)

---

## 1. Elevator pitch

> **Stickman Liero:** spräng hål i marken, gräv dig till skydd, skjut fienden genom ditt eget tunnelnät — i realtid, med modern game feel.

**3-sekundersklipp (marketing / pass-fail):** spelaren detonerar terräng → hoppar ner i hålet → skjuter upp/igenom och träffar motståndaren. Det ska kännas **roligt att spela**, inte bara se coolt ut.

---

## 2. Why StickLiero exists

StickLiero is **not** trying to recreate Liero feature-for-feature.

**iStick2War** proved that stickman + modern game feel + readable 2D action works. StickLiero applies that craft to a **different fantasy**:

| Liero lineage | StickLiero intent |
|---------------|-------------------|
| Destructible arena, realtime chaos | Same **emergent** terrain play |
| 1998 UX, opaque readability | **Modern** readability: clear silhouettes, juice, camera, feedback |
| Huge weapon roster as identity | Small curated set where **each weapon reshapes the map** |
| Multiplayer-first heritage | Validate **feel first** (SP → local 2P → online later) |

**The goal is to capture this fantasy:**

- **Creating your own cover** — holes and tunnels you chose, not pre-placed props  
- **Tunneling through the battlefield** — the map is a weapon you sculpt mid-fight  
- **Using terrain as a weapon** — force angles, bury threats, shoot through what you opened  

…while keeping the **readability and game feel** of a 2020s action game (iStick2War-style feedback, not retro jank).

**Why now (for this studio):** After shipping iStick2War, StickLiero is the next bet with a **clearer hook** (“spräng och gräv”) and reusable stickman / VFX / weapon learnings — without repeating the wave-shooter loop.

**Already de-risked (university prototype):** [**LieroRemake**](https://github.com/gardsgard/LieroRemake) — a simple StickLiero-style clone built with student teammates using **Destructible 2D and Spine** (course: *Interface programming 2*). Destructible terrain, basic combat, and the core dig/shoot loop worked.

That shifts the question from:

- ❌ *“Can we do Liero-like destruction in Unity?”* — **answered yes** by LieroRemake  
- ✅ *“Can we do it with **iStick2War production quality**?”* — juice, Spine stickman, bots, Unity 6 / URP 2D, ship discipline

The **post–iStick2War prototype** is about the second question, not proving the asset from scratch.

**What success looks like philosophically:** A player says *“I outsmarted them through the tunnel I made”* — not *“I had the bigger gun.”*

That line is a **design north star**: it separates StickLiero from stat-heavy shooters and keeps terrain tactics central (see **§21** for long-term product direction — not prototype scope).

### 2.1 Scope gates (do not skip)

The document is structured as **gates**, not a feature wishlist. Each step validates before the next investment:

| Gate | What it proves | Doc reference |
|------|----------------|---------------|
| **iStick2War EA first** | Shipping muscle, stickman feel, Steam, reuse pool | Current repo; start StickLiero after EA slice |
| **StickLiero prototype** | Dig + shoot **feel** (SP + bot) | §3–§4, §17 |
| **Local 2P (MP-0)** | Humans create tactics bots never will | §19, §20 |
| **Vertical slice** | Content + Steam hook (clip, page) | §19.2 |
| **Online experiment (MP-1+)** | Terrain sync + latency still fun | §20 |
| **Ranking / spectator / replays** | Competitive product layer | §21 (5+ years) |

```text
iStick2War EA
    ↓
StickLiero prototype
    ↓
Local 2P
    ↓
Vertical slice
    ↓
Online experiment
    ↓
Ranking / spectator / replays
```

Each gate reduces the risk of building ELO, spectator, or netcode before the core dig/shoot loop is fun. **Objective pass/fail numbers:** **§24 Success Metrics**. **Done criteria:** **§18.3**.

**Cornerstone decision (most important in this doc):** **Feel first, multiplayer later.**  
Online MP is **5–10× complexity**; destructible **terrain sync alone is a project** (§20.2). Do not skip to netcode to “save” a prototype that does not feel good offline. Think like a **producer**, not only a programmer: prove fun before investing in the esport layer.

### 2.2 Why this is a multi-year project (internal)

StickLiero is **not just an idea** when viewed together:

- University **LieroRemake** ([GitHub](https://github.com/gardsgard/LieroRemake)) — core loop de-risked  
- **iStick2War** — production stickman combat, weapons, juice, Cursor/ChatGPT delivery pattern  
- Explicit **scope gates**, MP phases, and time estimates in this doc  

The prototype doc stays narrow; **§21** holds the 5+ year vision so feature creep during iStick2War EA does not blur the endgame.

---

## 3. Prototype goal

Validate **one question** in 2–3 weeks of focused work:

> Does *“dig + shoot through terrain”* feel as good as shotgun gib / bazooka impact feels in iStick2War?

**Not** in scope for this prototype: “Does Destructible 2D work at all?” — that was answered in [**LieroRemake**](https://github.com/gardsgard/LieroRemake). Week 1 is **re-integration** in the iStick2War stack (Unity 6, URP 2D, collider tuning), not a first-principles terrain spike.

If yes → greenlight **post-prototype pipeline** (§19): local 2P first, then vertical slice. If no → tune movement, destruction radius, and weapons before adding content or multiplayer.

---

## 4. Pass / fail criteria

### Pass (prototype succeeds)

- [ ] **5-minute session** is fun without menus, shop, or progression.
- [ ] Player can **deliberately** create cover (hole/tunnel), reposition, and get a kill **using that hole**.
- [ ] Destruction is **readable** (player always knows where solid ground ends).
- [ ] At least **one bot** provides pressure (shoots, moves, sometimes uses terrain).
- [ ] **Three weapons** each change how you use terrain (dig, blast, area deny).
- [ ] Controls feel **responsive** after terrain colliders regenerate (no constant snagging).

### Fail (stop and rethink core)

- Digging feels like a gimmick; players ignore terrain and stand-shoot.
- Character constantly stuck in self-made holes or jitter on regenerated colliders.
- Bot is either a dummy or unfairly omniscient through walls.
- Frame time collapses after ~30 explosions on target hardware.

---

## 5. Non-goals (explicitly out of scope for prototype)

| Out of scope | Why |
|--------------|-----|
| Online multiplayer | 5–10× complexity; terrain sync alone is a project |
| Matchmaking, ranks, accounts | Product, not prototype |
| Campaign, waves, shop, meta | iStick2War domain |
| More than **1 arena** | Content won't save a bad dig/shoot feel |
| More than **1 bot** | AI in deformable terrain is hard enough for prototype |
| Full Liero weapon roster (10+ weapons) | Proves nothing until core loop works |
| Custom destructible engine | Use Destructible 2D; build gameplay, not R&D |
| Steam page / trailer | After pass criteria met |
| **Local 2P** | **Not prototype** — first step in §19 post-prototype pipeline (~1 week after pass) |

**Do not** bundle local 2P into the 2–3 week prototype unless pass criteria are already met early and SP feels solid.

---

## 6. Target audience & market (internal)

> **Audience-facing copy** belongs on a future Steam page. This section is an **internal product note** for scope and validation — not release text.

### 6.1 Target audience

| Segment | Who | Why they might care |
|---------|-----|---------------------|
| **Primary** | 30–45, grew up on Flash / Liero / OpenLieroX / Worms-like arena games | Nostalgia + “finally modern” |
| **Secondary** | Indie action fans, short-session PvP (5–15 min) | Emergent terrain moments |
| **Tertiary** | Ranking / ladder players | ELO loop (post-prototype, not v0) |
| **Growth** | Clip / stream viewers | Spectacular bazooka / tunnel kills (needs deliberate clip design) |

**Store hook (future, draft):** “The Liero-like you played in the browser — rebuilt with stickmen and modern destruction.”

### 6.2 What empty OpenLieroX servers mean (and do not mean)

**Observation (2026):** OpenLieroX often shows **no active online players**. That is **useful signal**, but easy to misread.

| Interpretation | Verdict |
|----------------|---------|
| “Nobody wants Liero-like games anymore” | **Too strong** — the *legacy product* may be inactive, not the fantasy |
| “We win automatically because there is no competition” | **Wrong** — empty servers can also mean “market never grew” or “distribution failed” |
| “There may be room for a modern take” | **Plausible** — if we validate fun + clips + discoverability |

**Why the old lineage faded (likely):** 1998-era UX, dated presentation, shrunk community, no modern ranking, no spectator, weak YouTube/Twitch culture around the game, no active marketing. Historically [Liero / OpenLieroX did build a real multiplayer community](https://en.wikipedia.org/wiki/Liero) — the gap is **product era**, not proof that destructible realtime arena is inherently unwanted.

**Safer internal framing:**

> There appears to be **white space for a modern, competitive Liero-like** — not proof that StickLiero will be popular.

### 6.3 StickLiero vs “competing with OpenLieroX”

We are **not** shipping a feature-parity clone. We are testing whether this stack can own a niche:

```text
Liero fantasy (destructible terrain, realtime dig/shoot)
+
Modern Unity / URP 2D + Spine + juice (from iStick2War)
+
Readable 1v1 first → local 2P → online later (§19–§20)
+
(Future) ranked ELO, spectator, Steam, clip-friendly moments
```

**Design rule in games:** reviving a **small / dormant genre** is often easier than entering an **oversaturated** one — but only if the new game creates **new demand** (feel, clips, ranked loop), not just prettier sprites.

**What we are not claiming:** empty OpenLieroX lobbies ⇒ automatic Steam success.

**What we are claiming:** iStick2War de-risks **engine, stickman combat feel, and shipping muscle**; StickLiero tests a **different hook** (“spräng och gräv”) with reuse, after EA slice is stable.

### 6.4 Validation before big MP investment

Do **not** use OpenLieroX player counts as the only metric. Cheap checks:

| Test | Pass signal |
|------|-------------|
| **Feel test** | 5–10 people who like physics PvP play local 1v1 / SP+bot and ask for “one more match” |
| **Clip test** | 30 s highlight is understandable **without** game context (tunnel → blast → kill reads on YouTube) |
| **Steam interest** | After iStick2War EA: wishlist / Discord / page clicks on “Liero-like” positioning — not legacy clone CCU |

**YouTube / stream moments** need explicit design: short rounds, readable silhouettes, replay or spectator (later), obvious kill feedback — not VFX alone.

### 6.5 Recommended studio sequence (unchanged intent)

```text
1. Ship iStick2War EA (QA & polish — current repo focus)
2. StickLiero prototype (§17) — dig + shoot feel
3. Local 2P (§19) — humans exploit tunnels bots never will
4. Vertical slice → Steam page with 3-second destruction clip
5. Ranking + spectator + online (§20) — only if earlier gates pass; long-term product layer in §21
```

---

## 7. Core loop (prototype)

```text
Match start (full HP, fixed loadout or 3-weapon pick)
    ↓
Real-time combat on destructible map
    ↓
Use weapons to alter terrain + damage opponent
    ↓
Kill opponent OR timeout → winner / rematch
```

No between-round shop. Optional: ammo crates on map (stretch).

---

## 8. Match rules (v0)

| Rule | Prototype value |
|------|-----------------|
| Mode | Deathmatch, 1v1 vs bot |
| Win condition | First to **3 kills** (or last alive if using lives) |
| Respawn | Yes, **3–5 s** delay, spawn away from killer if possible |
| Match time cap | **5 min** → sudden death or highest kills |
| Friendly fire | N/A (1v1) |
| Sudden death | Optional: shrink playable depth or rising “lava” (stretch) |

Keep rules visible on a minimal HUD: kills, timer, weapon name.

---

## 9. Arena (1 map)

**Theme:** Side-view cavern / dirt hill (single Destructible 2D sprite or tiled destructible chunks).

**Layout goals:**

- **Horizontal play band** ~2–3 screen widths (similar readability to iStick2War).
- **Vertical depth** enough to dig 2–3 “floors” of tunnels.
- **Pre-made features:** a few indestructible pillars or border walls so the map cannot become empty cheese.
- **Spawn points:** left / right, elevated enough to avoid instant spawn kills.

**Validation:** After 3 minutes of combat, map should still support movement and cover — not Swiss cheese with no tactics.

---

## 10. Player

### Controls (keyboard first)

| Action | Default key |
|--------|-------------|
| Move left / right | A / D |
| Jump | W or Space |
| Aim | Mouse |
| Fire | Left mouse |
| Switch weapon | 1 / 2 / 3 or scroll |
| Dig / jet (if separate from napalm) | Hold Right mouse or dedicated key |

Exact bindings TBD in first playable; aim should match iStick2War muscle memory where possible.

### Movement feel (must-have)

- Coyote time / jump buffer (small, iStick2War-style).
- Clear **max fall speed**; no infinite tumble in pits.
- **Collision after destroy:** test early with Destructible 2D — hero must not clip into void or stick in crater edges.

### Health

- **100 HP** prototype default.
- No regen. Optional small medkits on map (stretch).

### Presentation

- Reuse **stickman Spine** silhouette from iStick2War where licensing/art pipeline allows.
- Facing from aim direction; simple run / jump / aim / shoot states (no full iStick2War weapon animation matrix yet).

---

## 11. Weapons (prototype trio)

Design rule: **each weapon must change the map or force a different angle.**

### Weapon 1 — Gauss / hitscan rifle (starter)

- **Role:** Reliable damage, low terrain change.
- **Terrain:** Minimal (small chip on impact optional).
- **Why:** Baseline combat; bot and player can finish kills.

### Weapon 2 — Bazooka (reuse iStick2War learnings)

- **Role:** Blast **large** destructible hole; primary terrain weapon.
- **Terrain:** Circle destroy radius tuned for “new room” not “delete half the map.”
- **Reuse:** Projectile + explosion pattern from `HeroRocketProjectile_V2` / explosion VFX / shake — reimplemented against Destructible 2D API, not copied blindly.

### Weapon 3 — Napalm **or** Bouncy grenade (pick one for prototype)

| Option | Terrain interaction | Emergent moment |
|--------|---------------------|-----------------|
| **Napalm** | Burns / melts terrain over time (if asset supports) or lingers as hazard | Area deny, force reposition |
| **Bouncy grenade** | Delayed blast, ricochet off walls | Bank shots through tunnels |

**Recommendation:** Start with **bouncy grenade** if napalm needs custom terrain paint; napalm is better for vertical slice if Destructible 2D supports sustained damage zones easily.

### Explicitly later (not prototype)

Minigun, laser, mines, worm-style driller. **Ninja Rope / ninja line** — core pillar (§21); target vertical slice / post-prototype, not the weapon trio here.

---

## 12. Bot AI (minimal)

**Goal:** Pressure + target practice, not esports-grade.

| Behavior | Priority |
|----------|----------|
| Move toward player when line-of-sight | P0 |
| Strafe / jump when under fire | P0 |
| Shoot when aimed roughly at player | P0 |
| **Use bazooka on terrain** when player behind cover (simple: no LOS + cooldown) | P1 |
| Avoid standing in own napalm | P1 |
| Pathfind through **existing** tunnels | P2 (post-prototype) |

**Implementation hint:** Start with **direct ray LOS** + random strafe; add “shoot wall near player” heuristic before full nav mesh in destructible colliders.

**Difficulty knob:** reaction time + aim error + grenade usage frequency.

---

## 13. Terrain & Destructible 2D

### Prior validation ([LieroRemake](https://github.com/gardsgard/LieroRemake))

| Already shown | Still to verify in iStick2War-era stack |
|---------------|----------------------------------------|
| Destructible 2D destroys terrain on blast | Same asset on **Unity 6** + **URP 2D** as iStick2War |
| Basic dig/shoot / Liero-like loop is fun enough to finish a student project | Collider regen vs **Spine stickman** Rigidbody2D (snagging) |
| Team shipped a simple clone end-to-end (~123 commits; C# + design docs in repo) | Performance budget after ~30 explosions on target EA hardware |
| — | Wiring destruction to **iStick2War** bazooka / shake / SFX patterns |

Treat [LieroRemake](https://github.com/gardsgard/LieroRemake) as **proof of concept**, not a drop-in codebase — Unity version, rendering, and hero pipeline will differ. Repo includes `Library/` in history; prefer a clean fork or export `Assets/` + `ProjectSettings` when porting ideas.

### Integration checklist (week 1 — stack re-validation)

- [ ] Import asset into **URP 2D** test scene (same Unity major as iStick2War).
- [ ] Destroy circle on explosion; measure **ms** cost per blast.
- [ ] Regenerated **PolygonCollider2D** vs player **Rigidbody2D** — snagging test.
- [ ] Pool / limit destruction events if needed.
- [ ] Layer matrix: player, projectile, indestructible border, destructible terrain.

### Tuning knobs (document in Inspector)

- Destroy radius per weapon.
- Max destructions per second.
- Indestructible border thickness.

---

## 14. Reuse from iStick2War (conceptual)

| Reuse | Notes |
|-------|--------|
| Stickman / Spine pipeline | Art + animation workflow |
| Bazooka projectile & explosion **ideas** | Re-wire to terrain destruction |
| World / screen shake, hit feedback | `WorldShake_V2`, impact SFX patterns |
| Object pooling patterns | `SimplePrefabPool_V2` |
| 2D physics lessons | Layer exclusions, thin colliders |
| V2 architecture habit | Model / Controller / View split when codebase grows |

| Do **not** reuse as-is | Why |
|------------------------|-----|
| WaveManager, shop, telemetry | Wrong game loop |
| Bunker / cover colliders | Terrain is cover |
| Aircraft / paratrooper stacks | Wrong enemy model |

**Repo strategy:** New Unity project recommended after prototype greenlight; keep iStick2War repo clean. This doc lives here as **design reference** until then.

---

## 15. Audio & juice (prototype minimum)

- Weapon fire / explosion / dig SFX (subset of iStick2War library OK).
- Short hit-stop on heavy explosions (optional, 0.03–0.05 s).
- Camera shake on large blasts (smaller than iStick2War bazooka if map is larger).
- No music required until pass criteria; one loop OK.

---

## 16. UI (prototype minimum)

- Kills (P1 / BOT).
- Match timer.
- Current weapon + **magazine** (normal ammo; no infinite Colt45-style exception unless design changes).
- Pause → restart match.
- No main menu art; **Play** button in scene is enough.

---

## 17. Milestone plan (2–3 weeks)

### Week 1 — Stack integration (terrain already proven at uni)

- Destructible 2D in empty scene on **Unity 6 / URP 2D** (re-validate, do not rediscover basics).
- Stick placeholder or early Spine hero moves and jumps on regenerating colliders.
- One manual “explosion” input destroys terrain; note any collider snagging vs university build.
- **Exit:** player can run, jump, blast a hole, fall into it without breaking — on **this** engine stack.

### Week 2 — Combat

- Gauss + bazooka vs static target dummy.
- HP, damage, death, respawn.
- First bot: move + shoot hitscan.
- **Exit:** player can kill dummy/bot in open arena.

### Week 3 — Liero moment

- Full small arena, 3 weapons, kill limit 3.
- Bot uses bazooka on cover (P1 heuristic).
- Tune destruction radii + spawn rules.
- **Exit:** pass criteria checklist reviewed honestly.

---

## 18. Risks

| Risk | Mitigation |
|------|------------|
| Collider regen vs character physics | Week 1 re-validation; university build solved basics — tune for Spine stickman |
| Performance on low-end PC | Cap destruction rate; smaller arena |
| Bot feels stupid in tunnels | Accept for prototype; show LOS-breaking tactics still work for **human** |
| Scope creep (“just one more weapon”) | Weapon trio locked until pass |
| Asset incompatible with Unity 6 / URP | **Lower** — already used Destructible 2D in a shipped uni clone; still run week 1 checklist on iStick2War Unity version |
| “We already built it at uni” → skip feel pass | **Do not** skip §4 — production juice and bot pressure are the new unknowns |

### 18.1 Top risks (priority matrix)

> **Purpose:** Prioritize engineering and design time. **P0** = address before or during the phase listed; do not defer without revising this table.

| # | Risk | Probability | Impact | Priority | When it bites | Mitigation / doc |
|---|------|-------------|--------|----------|---------------|------------------|
| 1 | **Terrain sync** (online desync, wrong holes on peers) | **High** | **High** | **P0** | MP-1+ | §20.2, §20.5 event stream; §24.4 desync metric |
| 2 | **Collider regeneration** (snagging, jitter, fall-through after blast) | **Medium** | **High** | **P0** | Prototype week 1 | §13 checklist; [LieroRemake](https://github.com/gardsgard/LieroRemake) baseline; tune Spine stickman collider |
| 3 | **Ninja Rope feel** (swing reads badly, breaks competitive fairness) | **Medium** | **High** | **P1** | Vertical slice+ | §21 core pillar — **after** dig/shoot pass; spike before online |
| 4 | **Spectator architecture** (bolt-on replay path, double net stack) | **Low** | **Medium** | **P2** | MP-2 / 2028+ | §20.5 single event stream from MP-1 planning; §21 spectator |

**How to use:** If calendar slips, cut **P2** first (spectator can wait). Never start **MP-1** while **#2** is red on target hardware. **#1** owns the networking spike (§20.5) before multi-month MP-1 commit.

### 18.2 Technical debt allowed (by gate)

> **Purpose:** Pair with **§18.1** — risks say *what hurts*; this table says *what mess is OK for how long*. Pay down debt **at the gate boundary**, not “later when we have time.”

| Gate | Technical debt **allowed** | Pay down before next gate |
|------|---------------------------|---------------------------|
| **Prototype** (SP + bot) | ✓ Quick, ugly code OK — one scene, hardcoded values, minimal abstractions | Feel pass (**§4**, **§24.1**) |
| **Local 2P** (MP-0) | ✓ Duplicate input paths OK; no clean net layer yet | **§24.2** — humans prefer 2P over bot |
| **Vertical slice** | ✓ **Refactor gameplay systems** — Model/Controller split, weapon/terrain APIs, content pipeline | **§24.3** — wishlists / trailer signal |
| **MP-1** (online friends) | ✓ **Stabilize networking contract** — event stream, authority, destruction replay; gameplay code should not churn | **§24.4** — latency + desync metrics |
| **MP-2** (ship-quality online) | ✓ Bug fixes and polish only — **no major architecture rework** | **§24.5** — ship gate |

**Not allowed (any phase):** second networking path for spectator/replay (§20.5); skipping collider week-1 pass because LieroRemake worked (**§18.1 #2**); ELO/spectator code before MP-1 contract exists (**§18.1 #4**).

**Cursor rule of thumb:** prototype = speed; vertical slice = structure; MP-1 = freeze gameplay APIs and nail the event log; MP-2 = harden, do not redesign.

### 18.3 Definition of Done (DoD)

> **Purpose:** Closes the loop with **§18.1** (risks), **§18.2** (debt paydown), and **§24** (metrics). A feature or gate is not “done” at *implemented* — only when the checklist below is true.

**A feature is DONE when:**

- [ ] **Implemented** — runs in a playable build (Editor or standalone)
- [ ] **Playtested** — at least one human session beyond the author (except solo spike work explicitly marked WIP)
- [ ] **No known blocker bugs** — crashes, soft-locks, or fairness breaks for that feature; minor polish can remain if logged
- [ ] **Metrics updated if applicable** — gate KPIs in **§24** recorded (pass/fail or raw numbers)
- [ ] **Document updated** — this doc, open questions (**§22**), or in-code `//` only when Inspector-facing fields change

**A scope gate is DONE when:**

| Gate | DoD = feature checklist **plus** |
|------|----------------------------------|
| Prototype | **§4** pass + **§24.1** metrics hit; debt OK per **§18.2** |
| Local 2P | **§24.2** metrics hit |
| Vertical slice | Gameplay refactor landed (**§18.2**); **§24.3** metrics hit |
| MP-1 | Networking contract documented (**§20.5**); **§24.4** metrics hit |
| MP-2 | **§24.5** ship gate; **§18.2** — no major architecture rework |

**Not done:** “Compiles in Unity” alone; “works for me once”; merging without playtest on gameplay-facing changes.

---

## 19. Success after prototype

If **§4 pass criteria** are met, follow this order (see also **§20** for MP detail):

```text
Prototype (SP + bot)
    ↓
Local 2P (MP-0)          ~1 week — cheap validation with a real opponent
    ↓
Vertical slice             2 maps, 5 weapons, 2 bot personalities
    ↓
New repo + Steam page      3-second destruction clip
    ↓
Online experiment (MP-1)   only if local 2P proved tactical/social fun
```

### 19.1 Why local 2P before vertical slice

- **Low cost** (~3–10 days): same simulation, second input + spawn — no netcode.
- **High signal:** humans exploit tunnels and angles bots never will; instant “is this actually Liero-fun?” feedback.
- **De-risks content:** if 2P on one map gets stale, more maps/weapons won't save online MP either.

### 19.2 Vertical slice (after local 2P validates)

- **2 maps**, **5 weapons**, **2 bot personalities** (for SP / filler).
- Pin Unity version; new repo `StickLiero` (or similar).
- Monetization band: **~5–10 €** impulse buy for nostalgia audience.

### 19.3 If prototype fails

- Do not add local 2P, maps, or online.
- Iterate only movement + destruction + one weapon until dig/shoot feels good **or** pivot concept.

---

## 20. Multiplayer roadmap (optional)

**Not part of the prototype.** Start **MP-0 (local 2P)** immediately after **§4 pass criteria**; start **MP-1 (online)** only after **§19** local 2P + vertical slice direction feels worth the investment.

**Prerequisite:** Core loop works offline. Online does not fix bad dig/shoot feel.

### 20.1 Multiplayer types (complexity order)

| Type | Complexity | Notes |
|------|------------|--------|
| **Local 2P** (same PC, shared/split screen) | Low | Same simulation; duplicate input + spawn |
| **LAN / direct invite** | Medium–high | Still need net stack; smaller audience than internet |
| **Internet PvP** (2–8 players) | Very high | **Destructible terrain sync** is the hard problem |
| **Matchmaking, ranks, dedicated servers** | Extreme | Separate product scope |

### 20.2 Why online StickLiero is hard

Standard 2D shooters sync position, HP, and projectiles. StickLiero must also keep **the same holes in the ground** on every peer:

- Destructible mask / mesh updates
- Regenerated colliders (movement + line-of-sight)
- Explosion order and timing under latency

Cursor / ChatGPT speed up **boilerplate** (RPC stubs, serializers, lobby UI). They do **not** remove **desync debugging**, bandwidth tuning, or the authority model decision.

### 20.3 Time estimates (solo dev, part-time, Cursor/ChatGPT as accelerator)

Assumes **§17 prototype complete** and pass criteria met. Times are **calendar effort**, not calendar dates.

| Phase | Scope | Gross effort |
|-------|--------|--------------|
| **MP-0 — Local 2P** | Two local players, shared map, same destruction, kill rules from §8 | **3–10 days** |
| **MP-1 — Online 1v1 (friends only)** | Netcode/Mirror/Photon or similar; sync movement, shots, **destruction events**; host-authoritative | **6–12 weeks** |
| **MP-2 — Online 1v1 ship quality** | Client prediction, basic reconnect, edge-case desync fixes, NAT-friendly invite | **+3–6 months** (cumulative from MP-1 start) |
| **MP-3 — 2–4 players + lobby** | More players, spawn fairness, destruction load | **+3–6 months** |
| **MP-4 — Dedicated servers + ranking** | Infra, cost, moderation, anti-cheat beyond basics | **+6–12 months** |

**Cursor/ChatGPT impact:** roughly **20–40 % faster** on setup and glue code; **~5–15 %** on terrain sync and desync hunts (still requires playtesting and logs).

**Commercial v1 recommendation:** ship **singleplayer + bots**; add **local 2P** as a bonus if cheap; treat **internet MP** as a post-launch experiment unless MP-1 feels fun with simulated latency early.

### 20.4 Recommended order (aligned with §19)

```text
1. Prototype SP + bot (§17)           2–3 weeks
2. Local 2P (MP-0)                    ~1 week   ← validate before content/online
3. Vertical slice (§19.2)             maps + weapons + bots
4. Private online 1v1 (MP-1)          6–12 weeks
5. Decision gate: ship SP/local or invest in MP-2+
```

**Decision gate (after MP-1):** Play 1v1 with **80–150 ms simulated latency**. If digging, blasting, and aiming feel unfair or mushy — **stop** or stay local-only. No amount of AI coding fixes wrong net architecture.

### 20.5 MP-1 technical direction (draft — pick in spike)

#### Networking philosophy (architecture target)

Design online, spectator, and replay around **one authoritative event stream** from day one of MP-1 planning (not the prototype):

```text
Networking philosophy:

- Host authoritative (MP-1 default; dedicated server only if MP-4 demands it)
- Sync destruction EVENTS, not full terrain state each frame
- Spectator and replay consume the SAME ordered event stream as gameplay clients
- Log desync: replay N destruction events on two builds and compare collider samples
```

This gives Cursor and future you a stable contract: `(tick, eventType, payload…)` for explosions, kills, rope attach/detach, etc. **§21** spectator/replay/ELO features attach to that stream — do not invent a second replication path later.

Resolve stack and spike details in a **1-week networking spike** before committing months:

| Decision | Options |
|----------|---------|
| Authority | Host-authoritative (simplest) vs dedicated server |
| Terrain sync | Destruction **events** `(x, y, radius, weaponId, tick)` replayed on all clients vs periodic mask diff |
| Stack | Unity Netcode for GameObjects, Mirror, Photon Fusion, Steam Networking — choose one doc ecosystem and stay |
| Prediction | Minimal for MP-1 (host truth); add prediction in MP-2 if needed |

**Destruction event sync (likely MP-1 approach):**

- Server (or host) validates explosion → applies to local Destructible 2D → broadcasts event.
- Clients apply same event in same order; reject client-initiated terrain edits.
- Log and replay desync tests: two clients, 100 explosions, compare collision samples.

### 20.6 Out of scope until MP-2+ (see §21 for long-term)

- Cross-play
- Spectator mode
- Ranked seasons
- User-generated maps with synced destruction
- Bot backfill in ranked online matches

*(§21 tracks when spectator / ELO / replays enter the product roadmap after online 1v1.)*

### 20.7 iStick2War (reference only)

If multiplayer were added to **iStick2War** (no destructible terrain), effort is lower but still substantial:

| Mode | Gross effort (solo + Cursor) |
|------|------------------------------|
| Couch co-op (2 players, same screen) | **2–4 weeks** |
| Online co-op (2 players, waves + shop) | **2–4 months** |

StickLiero online remains harder because **terrain is gameplay state**.

### 20.8 Calendar FAQ — “When is 1v1 / MP-2 done?” (internal)

> **Effort** (weeks in §20.3) ≠ **calendar dates**. This subsection translates phases into calendar examples. Adjust if iStick2War EA slips or StickLiero is part-time.

**What “1v1 multiplayer” usually means:**

| Milestone | Gross effort | What you get |
|-----------|--------------|--------------|
| **Local 1v1 (MP-0)** | 3–10 days | Same PC, no netcode |
| **Online 1v1 friends (MP-1)** | 6–12 weeks | Host-auth, destruction events, invite a friend |
| **Ship-quality online 1v1 (MP-2)** | +3–6 months **from MP-1 start** | Prediction, reconnect, NAT-friendly, desync hardening |

MP-2 is **not** “flip on multiplayer.” It assumes prototype pass, local 2P fun, and vertical-slice direction — then MP-1 work, then polish.

**Prerequisite chain before MP-1:**

| Phase | Effort | Notes |
|-------|--------|--------|
| Prototype (SP + bot) | 2–3 weeks | §17 |
| Local 2P | ~1 week | §19 |
| Vertical slice | ~4–8 weeks (solo; not fixed in doc) | 2 maps, 5 weapons — §19.2 |

**Example: StickLiero start 2026-07** *(iStick2War EA stable; StickLiero is primary focus)*

```text
2026-07        Prototype + local 2P
2026-08–09     Vertical slice
2026-10        MP-1 start
2027-01        Earliest MP-2 window (3 months from MP-1 start)
2027-03        MP-2 possible — optimistic (needs short MP-1 + low-end MP-2)
2027-06        Safer internal plan for MP-2 ship quality
```

**Same start, part-time (~50 %):** MP-1 start slips to **2026-11–12**; MP-2 ship quality more likely **2027-06–09** than **2027-03**.

**If iStick2War EA / polish runs Jul–Sep 2026:** shift the whole StickLiero calendar **~2–4 months**.

**Planning rule:** target **~2027-06 for MP-2**; treat **2027-03** as stretch only when gates pass early and StickLiero is near full-time.

---

## 21. Long-term vision & indicative timeline (internal)

> **Not prototype scope.** This section sets **direction** for years 2–5+. Nothing here overrides §4 pass/fail or §5 non-goals for the 2–3 week prototype.

### 21.1 Long-term vision (5+ years)

**Core pillars** (product identity — not all in the 2–3 week prototype):

```text
Core pillars:

- Destructible terrain
- Emergent tunnel gameplay
- High-skill movement (Ninja Rope)
- Competitive 1v1
```

**Ninja Rope** is a **Liero signature** and belongs in the long-term skill ceiling (vertical slice / post-prototype), not the prototype weapon trio (§11). Rope + tunnels enable clip-worthy outplays and separate StickLiero from generic arena shooters.

StickLiero aims to become:

- The **modern Liero-like** — readable, juicy, stream-friendly  
- A **competitive 1v1** game (primary mode)  
- **High-skill movement** — **Ninja Rope** (and related mobility) once core dig/shoot passes  
- **ELO-based ranking** and seasonal ladders  
- **Spectator mode** and **replay system** (clip culture, tournaments) — built on §20.5 event stream  
- **Community tournaments** (official or supported)  
- **YouTube / Twitch friendly** — tunnel + rope plays and bazooka moments readable on camera  
- **Optional premium subscription** for advanced features (TBD; not required for core buy-to-play)

Monetization and subscription details stay **out of prototype**; note them here so ranked/spectator architecture is not designed away accidentally.

### 21.2 Indicative timeline (calendar sketch)

Rough order of operations — dates slip with iStick2War EA and gate results:

```text
2026
  ✓ Ship iStick2War on Steam (EA)

2026–2027
  ✓ Fork → StickLiero (new repo after prototype greenlight)
  ✓ Destructible terrain (Destructible 2D) + iStick2War-grade feel
  ✓ Ninja rope / mobility (when core dig/shoot passes — not prototype trio)
  ✓ Local multiplayer (MP-0)

2027
  ✓ Online 1v1 (MP-1 friends-only → ship-quality if gate passes)

2028+
  ✓ ELO / ranked matchmaking
  ✓ Spectator
  ✓ Replays
  ✓ Tournaments
  ✓ Optional premium subscription tier (advanced features — scope TBD)
```

Aligns with **§19** pipeline and **§20** MP phases. **Ranking, spectator, and replays** remain **post–online 1v1** unless local 2P + vertical slice prove demand earlier.

### 21.3 Relationship to §6 market note

OpenLieroX empty servers suggest **white space**, not automatic success (§6.2). Long-term vision targets **new demand** (clips, ranked loop, modern UX) — not feature parity with a 1998 clone.

---

## 22. Open questions (resolve in week 1 integration)

1. Napalm vs bouncy grenade as third weapon?
2. Side-view only (Liero classic) or slight aim arc / rope later? *(Ninja Rope: §21 core pillar; prototype stays dig/shoot trio — §11.)*
3. Same stickman skeleton as iStick2War hero or slimmer “Liero worm” proportions?
4. Destructible 2D: single large sprite vs chunked tiles for performance? *(Partially informed by university prototype — re-benchmark on Unity 6.)*
5. Should prototype live in a branch/scene inside iStick2War repo or separate project from day one?
6. Any reusable code/assets from [**LieroRemake**](https://github.com/gardsgard/LieroRemake) worth porting, or clean-room from iStick2War patterns only? *(Review `Assets/` vs iStick2War V2 stack before week 1.)*

---

## 24. Success metrics (internal)

> **Purpose:** Make scope gates (**§2.1**) **measurable**, not vibes-only. A gate advances only when **§18.3** DoD is satisfied **and** metrics below hit **pass** (or thresholds are explicitly revised here).

Metrics are **internal** — not Steam store promises.

### 24.1 Prototype (SP + bot)

*Gate: §3–§4, §17. Advance to local 2P only if **pass**.*

| Metric | Pass target |
|--------|-------------|
| Play-again intent | **≥ 8 / 10** test players say they would play again (same session or next day) |
| Wow moments | **≥ 3** spontaneous “wow” moments per match *(tunnel kill, blast opening, emergent hole play — observed, not prompted)* |
| Terrain as gameplay | Dig + shoot used **actively** — players create/use holes deliberately, not only stand-and-shoot |

### 24.2 Local 2P (MP-0)

*Gate: §19, §20. Advance to vertical slice only if **pass**.*

| Metric | Pass target |
|--------|-------------|
| Session length | Two humans play **> 30 min voluntarily** (same sitting, rematches) |
| vs bot | Neither player asks to go back to **bot-only** immediately after the session |
| Tactics | At least one kill per session clearly uses **terrain the players created** |

### 24.3 Vertical slice

*Gate: §19.2. Advance to MP-1 planning only if **pass**.*

| Metric | Pass target |
|--------|-------------|
| Steam wishlists | **≥ 100** on store page (or equivalent interest funnel TBD) |
| Discord / closed testers | **≥ 10** active testers giving structured feedback |
| Trailer / clip | **Positive** feedback on a **3-second destruction clip** or short trailer (not “looks cool” only — “I want to try this”) |

### 24.4 MP-1 (online 1v1, friends)

*Gate: §20.3–§20.5. Invest in MP-2 only if **pass**.*

| Metric | Pass target |
|--------|-------------|
| Latency feel | **80–150 ms** simulated (or real) latency still feels **playable** in a full match |
| Desync | **< 1 %** of internal test matches show terrain/collider desync (logged; define test protocol in networking spike) |
| Fun under lag | Both testers finish a match without calling the result “unfair” due to net |

### 24.5 MP-2 (ship-quality online 1v1) — draft

*Optional targets when MP-1 passes; refine before MP-2 kickoff.*

| Metric | Pass target |
|--------|-------------|
| Stability | **≥ 95 %** of friend matches complete without disconnect |
| Desync | **< 0.1 %** desync in expanded test matrix (longer matches, more explosions) |
| Ship gate | Willing to show online 1v1 on Steam page without “friends only, buggy” disclaimer |

---

## 25. Document history

| Version | Date | Notes |
|---------|------|--------|
| v0.1 | 2026-06-02 | Initial prototype scope; post–iStick2War ship plan |
| v0.2 | 2026-06-02 | Added §20 Multiplayer roadmap (optional) with phases and time estimates |
| v0.3 | 2026-06-02 | Added §2 Why StickLiero exists; §19 pipeline: Local 2P before vertical slice |
| v0.4 | 2026-06-02 | Expanded §6: target segments, OpenLieroX market note, validation gates (internal) |
| v0.5 | 2026-06-02 | University StickLiero + Destructible 2D prior validation; week 1 reframed as stack re-integration |
| v0.6 | 2026-06-02 | §2.1 scope gates + feel-first cornerstone; §21 long-term vision & 2026–2028+ timeline |
| v0.7 | 2026-06-02 | Linked university prototype: [gardsgard/LieroRemake](https://github.com/gardsgard/LieroRemake) |
| v0.8 | 2026-06-02 | Core pillars + Ninja Rope in §21; §20.5 networking philosophy; red thread; LieroRemake question shift |
| v0.9 | 2026-06-02 | §20.8 calendar FAQ (1v1 / MP-2; example start 2026-07) |
| v0.10 | 2026-06-02 | §24 Success Metrics — objective gate targets per phase |
| v0.11 | 2026-06-02 | §18.1 top risks priority matrix (terrain sync, collider, rope, spectator) |
| v0.12 | 2026-06-02 | §18.2 technical debt allowed per gate (pairs with risk matrix) |
| v0.13 | 2026-06-02 | §18.3 Definition of Done (features + gates; links §24 KPIs) |
