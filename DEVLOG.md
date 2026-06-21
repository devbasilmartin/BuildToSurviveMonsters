# BuildToSurviveMonsters — Dev Log

A 3D voxel zombie survival base-builder made with Raylib-cs (.NET 8).  
Built by Basil & nephew.  
Repo: https://github.com/devbasilmartin/BuildToSurviveMonsters

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

- [ ] Player respawn at spawn with 50 HP on death (instead of hard game-over)
- [ ] Sound effects (gunshot, melee swing, zombie groan, campfire crackle)
- [ ] Save/load world state
- [ ] Minimap (top-down 2D overlay, updates as player explores)
- [ ] Boss warning banner when boss zombie spawns night 5+
- [ ] Chest block: placeable storage, right-click deposits/withdraws items
- [ ] Spike trap: placed on ground, damages zombies that walk over it
