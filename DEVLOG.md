# BuildToSurviveMonsters — Dev Log

A 3D voxel zombie survival base-builder made with Raylib-cs (.NET 8).  
Built by Basil & nephew.  
Repo: https://github.com/devbasilmartin/BuildToSurviveMonsters

---

## Sprint 15 — Day Summary, Zombie Crawler  *(2026-06-21)*

**Day Summary overlay**
- Appears each dawn (after any real night) for 6 seconds; Space/Enter dismisses early
- Shows: "Night N — Survived!", kills that night, XP earned, "WAVE CLEARED!" if applicable
- Fades alpha in the last second; per-night kill counter resets at each night start
- `_xpBeforeNight` snapshot taken at night start; XP delta computed at dawn
- Timer and counter reset on R-to-restart

**Zombie Crawler** (spawns night 4+, ~1/6 of shambler pool)
- Stats: 25 HP, 5.0× speed scale (very fast), 6 damage — glass cannon
- Visual: flat dark brown body low to the ground + tiny head cube
- Bullet hitbox: tiny sphere at y+0.2, radius 0.28 — harder to shoot than shamblers
- No head hitbox — only body; melee still works at normal range
- 8 XP kill reward (less than shambler since they're plentiful and fast)
- Shows dark orange-brown on minimap; included in wave preview banner

---

## Sprint 14 — Sprint/Stamina, Double-Wave Night, Steel Sword  *(2026-06-21)*

**Sprint / Stamina**
- Hold Shift while moving to sprint at 1.8× base speed
- Stamina bar: 100 pts, drains 30/s while sprinting, regens 15/s when not
- Empty stamina locks sprinting until it recharges past 30 (prevents flicker)
- Sprinting 1.5× hunger/thirst drain on top of existing night multiplier
- Cyan stamina bar above XP bar; "SPRINT" label pulses bright while active

**Double-wave night** (20% chance, independent from fog)
- WaveSpawner.ForceExtraSpawn() called second time in OnNightStart (after normal spawn)
- Doubles total zombie count for that night
- "[DOUBLE WAVE!]" appended to wave preview banner; can combine with "[FOG]" for brutal nights

**Steel Sword** (blockId 248, recipe: 2 iron + 3 stone, Level 5 required)
- 200 melee damage, 0.35s cooldown — fastest and hardest hitting melee weapon
- Gold crossguard + dark grip + long bright-silver blade viewmodel
- 2× damage vs armoured zombies (like all melee) = 400 effective damage
- Appears as SSTD in hotbar; placed in first available slot on craft

---

## Sprint 13 — Armoured Zombie, Voxel Destruction, Fog Night  *(2026-06-21)*

**Armoured Zombie** (spawns night 3+, ~20% of shamblers)
- Silver/grey visual; takes only 25% bullet damage (gun is weak against them)
- Melee deals 2× normal damage — encourages mixing weapons vs armour type
- Spike traps bypass armour entirely (useful base-defence vs armoured hordes)
- Drops 20 XP (vs 10 for shamblers)
- Shows silver/grey on minimap (vs red shamblers, green runners, magenta boss)
- Wave preview includes armoured count: "Night N: X zombies + Y armoured + Z runners"
- `Zombie.IsArmoured` public property; constructor param `isArmoured = false`

**Explosive voxel destruction**
- Explosion now destroys nearby natural blocks (dirt, stone, wood, leaves, sand) in 2.5-unit radius
- Preserves player-built walls (ids 4,5,12), fixtures (9,10,13–17), and water (19)
- Useful for clearing trees, opening terrain paths, mining in emergencies

**Fog Night** (30% chance each night)
- Draw distance drops from 40 to 15 units — visually dramatic and tactically challenging
- "FOG NIGHT" appended to wave preview banner at night start
- "FOG" indicator drawn on screen right side while active
- Clears automatically on day start

---

## Sprint 12 — Thrown Explosive, Crafting Level Gates, HUD Fix  *(2026-06-21)*

**Thrown Explosive** (craft: 2 iron + 3 wood, Lv.4 required)
- G key tosses explosive at crosshair hit point (up to 20 units); lands at target or 15 units ahead
- AoE 220 → 0 damage falloff over 3.5-unit radius; all zombies in range take damage
- Orange/yellow sphere flash drawn in 3D for 0.5 seconds (grows and fades)
- EXPL count shown in HUD next to AMMO; hint text swaps to "G: Throw Explosive" when held
- `Player.Explosives` field; -9 OutputId in TryCraft

**Crafting level gates**
- `Recipe.RequiredLevel` positional field (default 0, no gate)
- Gated: Stone Armor Lv.1, Iron Sword Lv.2, Iron Pickaxe Lv.2, Iron Armor Lv.3, Healing Amulet Lv.3, Explosive Lv.4
- TryCraft returns early if player.Level < RequiredLevel
- Locked recipes show as dark grey with "Requires Level N" instead of cost; selection highlight turns red for locked

**HUD layout bug fix**
- Sprint 11 introduced SPD/heal indicators at x=220 which overlapped AMMO (also at 220)
- Fixed: AMMO + EXPL now on their own row at sh-30; Armor/SPD/HealRate on a compact row at sh-47
- All text is non-overlapping and visible at 1280×720

---

## Sprint 11 — Wave Preview, Level Variety, Larger World, Healing Amulet  *(2026-06-21)*

**Wave preview banner**
- On every night start: 4-second amber banner showing exact wave composition
  - "Night N: X zombies + Y runners + BOSS!"
- Computed from `WaveSpawner.GetWavePreview(night)` — same formula as actual spawn
- Gives players time to position before the horde arrives

**Level-up variety**
- Odd levels (1, 3, 5): +15 MaxHP + 15 healing (as before)
- Even levels (2, 4): +0.4 MoveSpeed bonus (cumulative, shows as "SPD+0.4" in HUD)
- `Player.SpeedBonus` added to `MoveSpeed` in `HandleMovement`
- Persists through respawns (progression reward)

**Healing Amulet** (new crafting recipe #12)
- Craft: 3 iron + 2 stone → +0.1 HP/s passive regen (stackable up to 1.0 HP/s)
- `_healAccum` float accumulates fractional healing until ≥1 whole HP
- "HP+0.1/s" shown in HUD when active; only applies when below max HP

**Larger world** (12×12 chunks → 192×192 blocks, was 128×128)
- ~2.25× more surface area to explore
- Content scaled: 70 trees, 7 boulders, 6 iron deposits, 11 crates, 4 ponds
- No performance hit — rendering range is unchanged; memory cost is ~1MB extra

---

## Sprint 10 — XP/Level System, Ponds, More Iron  *(2026-06-21)*

**XP / Level system**
- Kill XP: normal zombie = 10, runner = 15, boss = 100 (stored as `Zombie.XPReward`)
- 5 levels; thresholds: 50, 150, 300, 500, 750 XP (cumulative)
- Each level up: +15 MaxHP, heal 15 HP
- Gold "LEVEL UP! Level N +15 Max HP" banner fades for 3 seconds
- Compact XP progress bar above hunger bars; shows "Lv.N X/Y XP"; gold "Lv.MAX" at cap
- `CheckLevelUp()` recurses in case multiple thresholds cross in one boss kill

**World variety**
- Iron ore: 3 deposits per world (was 1) — much more accessible
- Ponds: 2 ponds per world — 5×5 water (non-solid, id 19) + 7×7 sand (id 18) shore ring
- Water (19): non-solid, non-minable (raycast skips it); shows blue on minimap
- Sand (18): solid, low-HP, no drops; shows golden on minimap
- Minimap MinimapSurface: water detected before IsSolid skip so ponds show as blue

---

## Sprint 9 — Pause Screen, Pickaxe Tools, Crafting Navigation  *(2026-06-21)*

**Pause screen** (ESC when not crafting)
- Shows Night / Kills / Deaths / playtime (timer pauses while paused)
- Q to quit cleanly; R to restart; ESC to resume
- `game.ShouldQuit` exposed to Program.cs so the main loop exits gracefully

**Mining tools** — two new craftable items
- **Stone Hatchet** (249): 2 stone + 2 wood → 2× mining damage per tick; wide stone-blade viewmodel
- **Iron Pickaxe** (250): 3 iron + 2 wood → 3× mining damage per tick; long silver-head viewmodel
- `Player.MineTickDamage` property drives tick damage for all held items
- Tools go to the first empty hotbar slot just like melee weapons

**Crafting UI navigation**
- ↑↓ arrow keys move selection cursor between recipes (wraps at ends)
- Enter / Numpad Enter crafts the highlighted recipe
- Number keys 1–9 still work for the first 9 entries
- Selected recipe highlighted with amber background strip
- Row height reduced 52→44px to fit 11 recipes comfortably at 720p
- Footer updated to show navigation keys

---

## Sprint 8 — Minimap, Zombie Wall-Avoidance, IsSolid Bug Fix  *(2026-06-21)*

**Bug fix: VoxelWorld.IsSolid**
- `VoxelWorld.IsSolid` was `GetVoxel != 0` — it ignored `Blocks.IsSolid` entirely
- Torches (16) and spike traps (17) were blocking player + zombie movement even though defined as non-solid
- Fixed to `Blocks.IsSolid(GetVoxel(x,y,z))` — one line, propagates correct semantics everywhere: player collision, bullet termination, zombie SurfaceY, spawn drops

**Minimap** (top-right corner, 80×80px)
- Shows a 20-block radius around the player, 2px per block
- Terrain colours: dirt brown, stone grey, leaves green, iron ore orange, walls white-grey, crates yellow, campfire orange, crafting table warm brown
- Red dots for live zombies; magenta for boss; white dot for player (always centred)
- Cache rebuilt every 1 second (cheap: 1600 color lookups at y≤20 scan depth)
- Stays dirty until rebuilt; force-dirtied on restart

**Zombie wall-avoidance**
- Zombies now slide along walls instead of clipping through player-built defences
- X/Z axes resolved separately; if direct path blocked, two perpendicular slide attempts tried at 60% speed
- Boss zombie uses wider collision radius (0.6 vs 0.35) matching its larger visual
- `BlockedAt(x, z)` checks y and y+1 heights so torso-level walls are respected

---

## Sprint 7 — Respawn, Spike Trap, Boss Warning  *(2026-06-21)*

**Player respawn** (replaces hard game-over)
- On death: player teleports back to world spawn point with 50 HP and 8 seconds invincibility
- `Player.Respawn(spawnPos)` resets velocity so no physics glitch on teleport
- Zombie melee checks `player.Invincible` before dealing damage
- Pulsing cyan screen-border + countdown timer shown while invincible
- Death count tracked (`_deathCount`) — shown in HUD top-right in red below kill count
- Bullets cleared, crafting menu closed, starvation timer reset on respawn

**Spike Trap** (blockId 17, hotbar slot 6)
- Non-solid — zombies and player walk through it; costs 2 stone to place from hotbar
- Deals 15 damage to any zombie standing on it every 0.5 seconds
- Boss zombies can be worn down by a ring of spikes
- 5 thin spike-columns drawn above each nearby trap in 3D for visibility

**Boss Warning Banner**
- On night 5+ start: 5-second pulsing orange "⚠ BOSS ZOMBIE INCOMING ⚠" text above screen centre
- Timer ticks in Update so the fade-out is smooth and frame-rate independent

---

## Sprint 6 — Crate Variety, Torch, Zombie Boss  *(2026-06-21)*

**Loot crate variety**
- 3 types now spawn (7 total per world): Food (yellow, 10), Ammo (red, 14), Supply (teal, 15)
- Food Crate: 4–8 food; Ammo Crate: 10–20 ammo; Supply Crate: 2–4 food + 5–10 ammo + 1–3 iron
- Context hint identifies crate type and previews drops before you open it
- `FindNearbyCrate` now checks all 3 blockIds (10/14/15)

**Torch block** (blockId 16)
- Non-solid (walk through it like leaves); costs 1 wood from hotbar slot 5
- Bobbing yellow glow sphere drawn above each nearby torch in 3D (drawn in 8-block radius)
- Use torches to mark your base or light paths between your campfire and crafting table

**Zombie Boss** (spawns night 5+, 1 per wave)
- 600 HP base (+40 per night beyond 5); 1.4 speed; 20 melee damage
- Visual: 2× height/width, dark purple colour with large red HP bar above
- Bullet hitboxes scale up: body sphere at y+1.5 r=1.0, head at y+3.5 r=0.55
- Kill rewards: +1–3 ammo (normal) + 5 iron + 5 bonus ammo
- Kill logic refactored into `AwardKill(Zombie z)` shared by gun and melee paths

---

## Sprint 5 — Campfire, Night Clear Bonus, Bandage & Quiver  *(2026-06-21)*

**Campfire block** (blockId 13)
- Craft from hotbar slot 4 using 3 wood per placement (right-click to place like walls)
- Walk within 3 blocks of a placed campfire: Hunger +4/s, Thirst +2.5/s
- Animated bobbing orange/yellow flame drawn above the block in 3D
- Context hint changes to orange "CAMPFIRE: Hunger + Thirst restoring" when in range

**Night clear bonus**
- If all zombies are killed during a night: +15 Ammo + 3 Food awarded automatically
- "WAVE CLEARED! +15 Ammo +3 Food" banner fades in green over 4 seconds
- 2-second startup guard prevents false trigger at night start before zombies spawn

**New crafting recipes** (replaced redundant wall recipes)
- **Quiver ×20**: 2 wood + 1 iron → better ammo-per-iron than Ammo×10 recipe
- **Bandage**: 3 food → heal +25 HP (trade hunger for health at the crafting table)

**Removed recipes**: PlankWall ×2 and StoneWall ×2 — these were redundant since walls are placed directly from hotbar already

**Bug fix**: Kill count, night-cleared flag, and banner timer now reset on R-to-restart

---

## Sprint 4 — Iron Tier, Kill Counter, Zombie Scaling  *(2026-06-21)*

**Iron tier**
- **Iron Wall** (blockId 12): silver-grey, 40 HP, durable — in hotbar slot 3 from start; costs 3 iron per block placed
- **Iron Sword** recipe (2 wood + 5 iron): 150 damage, 0.4s cooldown, longer blade viewmodel
- **Iron Armor** recipe (6 iron): −55% damage taken (ArmorTier 3)
- Full armor upgrade chain: Wood 15% → Stone 35% → Iron 55% (never downgrades)

**Kill counter**
- Kill count tracked across the entire run; shown top-right at all times
- Active zombie count shown below it during night
- Kill count displayed on the YOU DIED game-over screen alongside night count
- Bug fix: `wasDead` check prevents double-counting kills if bullet/melee hit a corpse

**Zombie scaling**
- Speed now scales per night (+7% per night) in addition to HP — zombies get relentlessly faster
- Runners (green) appear from **night 2** (was night 3) and make up 1/3 of the wave
- Runners are olive-green; shamblers stay dark-red — easy to tell apart
- Hit-flash still works on both types

**UI polish**
- Crafting panel rows tightened (64→52 px) so all 9 recipes fit in 720p without clipping
- Iron Armor HUD label added with blue-silver colour

---

## Sprint 3 — Survival Loop  *(2026-06-20)*

**Hunger & Thirst**
- Two meters (0–100) shown as bars above the HP bar
- Hunger drains at ~0.22/s, Thirst at ~0.38/s; both drain 1.5× faster at night
- When either hits zero, player takes 3 HP damage every 3 seconds
- Press **F** to eat 1 Food: +45 Hunger, +20 Thirst

**Loot Crates**
- 7 bright-yellow crates spawned randomly across the map at generation time (blockId 10)
- Walk up and press **E** — crate disappears, drops 2–5 Food and 5–10 Ammo
- Context hint ("E: Open Loot Crate") appears when you're close

**Zombie Ammo Drops**
- Every zombie kill drops 1–3 ammo automatically (gun kills and melee kills both count)

**Food item** (blockId 11) — inventory-only, collected from crates, consumed with F

---

## Sprint 2 — Weapons & Armor  *(2026-06-20)*

**Melee weapons** (crafted at table, go to hotbar)
- **Wood Club** (3 wood) — 35 damage, 0.6s cooldown, 72° forward cone, 2.2 unit range
- **Stone Sword** (1 wood + 3 stone) — 80 damage, 0.5s cooldown
- Both have 3D viewmodels with a swing animation on left-click
- Left-click with weapon equipped will not accidentally mine

**Armor** (passive, equip on craft — higher tier never downgrades)
- **Wood Armor** (5 wood) — −15% damage taken
- **Stone Armor** (4 stone + 2 wood) — −35% damage taken
- Shown in HUD: "ARMOR: Wood (−15%)" / "ARMOR: Stone (−35%)"

---

## Sprint 1 — Core Gameplay  *(2026-06-19)*

**World**
- Voxel world (128×32×128), Perlin-style terrain with stone + dirt layers
- 40 trees with wood trunks and ellipsoid leaf canopies
- 4 stone boulders (low mound shape, 5 blocks each)
- 1 iron ore vein (orange-rust horizontal, 5 blocks — underground + surface crust)
- 1 crafting table spawned near world centre

**Player**
- First-person WASD/arrow movement, mouse look, Space to jump
- AABB voxel collision (X/Z resolved separately; no getting stuck in blocks)
- Mining (hold left-click), building (right-click), hotbar scroll/number-keys
- Starting inventory: 10 stone, 10 wood, 30 ammo; slot 0 = Gun

**Combat**
- Gun: left-click shoots animated bullet sphere at 40 units/s, 50 damage
- Animated bullet visible in flight; disappears on voxel or zombie hit
- Zombie hit flash (white blend), HP bar above head, two-sphere hitbox (body + head)
- Gun viewmodel with recoil animation; pickaxe viewmodel otherwise

**Day/Night Cycle**
- 30s day → 5s warning (orange sky) → 120s night
- Zombies spawn in waves at night; zombie count shown in HUD
- Wave difficulty scales with night number

**Crafting Table** (press E when adjacent)
- Ammo ×10: 1 wood + 2 iron
- Plank Wall ×2: 2 wood
- Stone Wall ×2: 2 stone

**HUD**
- HP bar, ammo count, day/night timer, resource inventory, 9-slot hotbar

---

## Backlog

- [ ] Sound effects (gunshot, melee swing, zombie groan, campfire crackle)
- [ ] Save/load world state
- [ ] Chest block: placeable storage, right-click opens inventory UI
- [ ] Turret block: auto-fires at nearby zombies every 2s
- [ ] Hunger overeat: eating at full hunger grants temporary max-HP boost
- [ ] Night N difficulty cap: zombie count caps at a max to prevent unwinnable waves
