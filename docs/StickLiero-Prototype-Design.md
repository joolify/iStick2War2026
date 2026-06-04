# StickLiero — Prototype Design Doc (v0.3)

**Status:** Pre-production (start after iStick2War Steam-ready slice)  
**Working title:** StickLiero  
**Genre:** Real-time 2D arena shooter with fully destructible terrain  
**Reference:** [Liero](https://en.wikipedia.org/wiki/Liero) (1998), MoleZ / NiL lineage  
**Terrain tech (planned):** [Destructible 2D](https://assetstore.unity.com/packages/tools/sprite-management/destructible-2d-18125) (Unity Asset Store)

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

**What success looks like philosophically:** A player says *“I outsmarted them through the tunnel I made”* — not *“I had the bigger gun.”*

---

## 3. Prototype goal

Validate **one question** in 2–3 weeks of focused work:

> Does *“dig + shoot through terrain”* feel as good as shotgun gib / bazooka impact feels in iStick2War?

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

## 6. Target audience

- **Primary:** 30–45, grew up on Flash / Liero / Worms-like destructible arena games.
- **Secondary:** Indie action fans who want short sessions (5–15 min) and emergent moments.
- **Store hook (future):** “The Liero-like you played in the browser — rebuilt with stickmen and modern destruction.”

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

Minigun, laser, mines, worm-style driller, rope / ninja line.

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

### Integration checklist (week 1 spike)

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

### Week 1 — Terrain spike

- Destructible 2D in empty scene.
- Capsule/stick placeholder moves and jumps on regenerating colliders.
- One manual “explosion” input destroys terrain.
- **Exit:** player can run, jump, blast a hole, fall into it without breaking.

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
| Collider regen vs character physics | Week 1 spike; simplify player collider if needed |
| Performance on low-end PC | Cap destruction rate; smaller arena |
| Bot feels stupid in tunnels | Accept for prototype; show LOS-breaking tactics still work for **human** |
| Scope creep (“just one more weapon”) | Weapon trio locked until pass |
| Asset incompatible with Unity 6 / URP | Test purchase spike before full commit |

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

Resolve in a **1-week networking spike** before committing months:

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

### 20.6 Out of scope until MP-2+

- Cross-play
- Spectator mode
- Ranked seasons
- User-generated maps with synced destruction
- Bot backfill in ranked online matches

### 20.7 iStick2War (reference only)

If multiplayer were added to **iStick2War** (no destructible terrain), effort is lower but still substantial:

| Mode | Gross effort (solo + Cursor) |
|------|------------------------------|
| Couch co-op (2 players, same screen) | **2–4 weeks** |
| Online co-op (2 players, waves + shop) | **2–4 months** |

StickLiero online remains harder because **terrain is gameplay state**.

---

## 21. Open questions (resolve in week 1 spike)

1. Napalm vs bouncy grenade as third weapon?
2. Side-view only (Liero classic) or slight aim arc / rope later?
3. Same stickman skeleton as iStick2War hero or slimmer “Liero worm” proportions?
4. Destructible 2D: single large sprite vs chunked tiles for performance?
5. Should prototype live in a branch/scene inside iStick2War repo or separate project from day one?

---

## 22. Document history

| Version | Date | Notes |
|---------|------|--------|
| v0.1 | 2026-06-02 | Initial prototype scope; post–iStick2War ship plan |
| v0.2 | 2026-06-02 | Added §20 Multiplayer roadmap (optional) with phases and time estimates |
| v0.3 | 2026-06-02 | Added §2 Why StickLiero exists; §19 pipeline: Local 2P before vertical slice |
