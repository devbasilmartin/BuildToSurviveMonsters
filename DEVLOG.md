# BuildToSurviveMonsters — Dev Log

A 3D voxel zombie survival base-builder made with Raylib-cs (.NET 8).  
Built by Basil & nephew.  
Repo: https://github.com/devbasilmartin/BuildToSurviveMonsters

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

- [ ] More loot crate variety (weapon crate, ammo crate, food crate with distinct colours)
- [ ] Campfire block: craft from 3 wood + 1 stone; restores hunger/thirst when nearby
- [ ] Night survival bonus: extra ammo or food rewarded for clearing all zombies
- [ ] Player respawn at spawn with half HP (instead of hard game-over)
- [ ] Sound effects (gunshot, melee swing, zombie groan)
- [ ] Save/load world state
- [ ] Minimap (top-down 2D overlay of explored area)
