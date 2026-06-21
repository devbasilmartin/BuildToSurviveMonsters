using System;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

public class Game
{
    VoxelWorld    _world    = null!;
    Player        _player   = null!;
    DayNightCycle _dnc      = null!;
    WaveSpawner   _waves    = null!;
    Camera3D      _camera;

    const int   GunDamage = 50;
    const float GunRange  = 50f;

    bool    _gameOver          = false;
    bool    _craftingOpen      = false;
    bool    _pauseOpen         = false;
    int     _selectedRecipe    = 0;
    float   _timePlayed        = 0f;
    public  bool ShouldQuit    = false;
    float   _gunRecoil         = 0f;
    float   _meleeSwing        = 0f;
    float   _meleeCooldown     = 0f;
    float   _starvationTimer   = 0f;
    int     _killCount         = 0;
    int     _deathCount        = 0;
    bool    _nightCleared      = false;
    float   _nightStartDelay   = 0f;
    float   _clearBannerTimer  = 0f;
    float   _bossWarningTimer  = 0f;
    float   _invincibleTimer   = 0f;
    float   _spikeTimer        = 0f;
    float   _levelUpTimer      = 0f;
    string  _levelUpMsg        = "";
    float   _waveBannerTimer   = 0f;
    string  _waveBannerMsg     = "";
    float   _healAccum         = 0f;
    float   _explosionTimer    = 0f;
    Vector3 _explosionPos;
    bool    _fogNight          = false;
    Vector3 _spawnPos;

    static readonly int[] LevelThresholds = { 50, 150, 300, 500, 750 };

    // Minimap cache
    const int MM_RANGE = 20;
    const int MM_SCALE = 2;
    const int MM_SIZE  = MM_RANGE * 2 * MM_SCALE; // 80px
    readonly Color[,] _mmColors = new Color[MM_RANGE * 2, MM_RANGE * 2];
    float _mmAge = 0f;
    bool  _mmDirty = true;

    readonly Random _rng       = new();

    struct Bullet { public Vector3 Pos; public Vector3 Dir; public float Life; }
    readonly System.Collections.Generic.List<Bullet> _bullets = new();

    // ── Crafting recipes ─────────────────────────────────────────────
    // OutputId -1 = ammo (special). Cost = (blockId, count) pairs.
    record struct Ingredient(byte Id, int Amt);
    record struct Recipe(string Name, int OutputId, int OutputCount, Ingredient[] Cost, int RequiredLevel = 0);

    static readonly Recipe[] Recipes =
    {
        new("Ammo ×10",       -1, 10, new[]{ new Ingredient(3,1), new Ingredient(8,2) }),  // 1 wood + 2 iron
        new("Quiver ×20",     -1, 20, new[]{ new Ingredient(3,2), new Ingredient(8,1) }),  // 2 wood + 1 iron  (better rate)
        new("Bandage",        -5,  1, new[]{ new Ingredient(11,3) }),                       // 3 food → heal 25 HP
        new("Wood Club",      253,  1, new[]{ new Ingredient(3,3) }),                        // 3 wood
        new("Stone Sword",    254,  1, new[]{ new Ingredient(3,1), new Ingredient(2,3) }),  // 1 wood + 3 stone
        new("Wood Armor",      -2,  1, new[]{ new Ingredient(3,5) }),                         // 5 wood
        new("Stone Armor",     -3,  1, new[]{ new Ingredient(2,4), new Ingredient(3,2) },  RequiredLevel:1),
        new("Iron Sword",     252,  1, new[]{ new Ingredient(3,2), new Ingredient(8,5) },  RequiredLevel:2),
        new("Iron Armor",      -4,  1, new[]{ new Ingredient(8,6) },                        RequiredLevel:3),
        new("Stone Hatchet",  249,  1, new[]{ new Ingredient(2,2), new Ingredient(3,2) }),
        new("Iron Pickaxe",   250,  1, new[]{ new Ingredient(8,3), new Ingredient(3,2) },  RequiredLevel:2),
        new("Healing Amulet",  -8,  1, new[]{ new Ingredient(8,3), new Ingredient(2,2) },  RequiredLevel:3),
        new("Explosive",       -9,  1, new[]{ new Ingredient(8,2), new Ingredient(3,3) },  RequiredLevel:4),
        new("Steel Sword",    248,  1, new[]{ new Ingredient(8,2), new Ingredient(2,3) },  RequiredLevel:5),
    };

    public void Init()
    {
        // World
        _world = new VoxelWorld(chunksX: 12, chunksY: 2, chunksZ: 12);
        WorldGenerator.Generate(_world, seed: Environment.TickCount);

        // Find spawn — stand on surface at world center
        int cx = _world.SizeX / 2, cz = _world.SizeZ / 2;
        float spawnY = 0;
        for (int y = _world.SizeY - 1; y >= 0; y--)
            if (_world.IsSolid(cx, y, cz)) { spawnY = y + 1f; break; }

        _spawnPos = new Vector3(cx, spawnY, cz);
        _player = new Player(_world, _spawnPos);

        // Day/night
        _dnc = new DayNightCycle();
        _dnc.OnNightStart += () => {
            _nightCleared    = false;
            _nightStartDelay = 2f;
            _fogNight        = _rng.Next(10) < 3; // 30% fog chance
            if (_dnc.NightCount >= 5) _bossWarningTimer = 5f;
            var (s, r, a, b) = _waves.GetWavePreview(_dnc.NightCount);
            _waveBannerMsg = $"Night {_dnc.NightCount}:  {s} zombies";
            if (r > 0) _waveBannerMsg += $"  +  {r} runners";
            if (a > 0) _waveBannerMsg += $"  +  {a} armoured";
            if (b)     _waveBannerMsg += "  +  BOSS!";
            if (_fogNight) _waveBannerMsg += "   [FOG]";
            if (_rng.Next(5) == 0) // 20% double wave
            {
                _waves.ForceExtraSpawn();
                _waveBannerMsg += "   [DOUBLE WAVE!]";
            }
            _waveBannerTimer = 4f;
        };
        _dnc.OnDayStart += () => { _nightCleared = false; _fogNight = false; };

        // Enemies
        _waves = new WaveSpawner(_world, _dnc);

        // Camera — updated every frame from player position/look
        _camera = new Camera3D(
            _player.EyePos, _player.EyePos + _player.Forward,
            Vector3.UnitY, 70f, CameraProjection.Perspective);

        DisableCursor();
    }

    public void Update(float dt)
    {
        if (_gameOver)
        {
            if (IsKeyPressed(KeyboardKey.R)) Restart();
            return;
        }

        // Fast-forward day (F key, debug)
        _dnc.SetFastForward(IsKeyDown(KeyboardKey.F));
        _dnc.Update(dt);

        _player.Update(dt);

        _waves.Update(dt, _player);

        // Hunger / Thirst drain — faster at night, faster while sprinting
        float drainMult = _dnc.Phase == DayPhase.Night ? 1.5f : 1f;
        if (_player.Sprinting) drainMult *= 1.5f;
        _player.Hunger = Math.Max(0f, _player.Hunger - 0.22f * drainMult * dt);
        _player.Thirst = Math.Max(0f, _player.Thirst - 0.38f * drainMult * dt);

        if (_player.Hunger <= 0 || _player.Thirst <= 0)
        {
            _starvationTimer += dt;
            if (_starvationTimer >= 3f) { _starvationTimer = 0f; _player.TakeDamage(3); }
        }
        else _starvationTimer = 0f;

        // Campfire: restore hunger + thirst when nearby
        if (NearCampfire())
        {
            _player.Hunger = Math.Min(100f, _player.Hunger + 4f * dt);
            _player.Thirst = Math.Min(100f, _player.Thirst + 2.5f * dt);
        }

        // Night clear bonus
        if (_nightStartDelay > 0) _nightStartDelay -= dt;
        if (_dnc.Phase == DayPhase.Night && !_nightCleared
            && _nightStartDelay <= 0 && _waves.Active.Count == 0)
        {
            _nightCleared     = true;
            _clearBannerTimer = 4f;
            _player.Ammo += 15;
            _player.Inventory.TryGetValue(11, out int cf);
            _player.Inventory[11] = cf + 3;
        }
        if (_clearBannerTimer > 0) _clearBannerTimer -= dt;

        // F = eat food
        if (IsKeyPressed(KeyboardKey.F))
        {
            _player.Inventory.TryGetValue(11, out int food);
            if (food > 0)
            {
                _player.Inventory[11] = food - 1;
                _player.Hunger = Math.Min(100f, _player.Hunger + 45f);
                _player.Thirst = Math.Min(100f, _player.Thirst + 20f);
            }
        }

        _timePlayed += dt;

        // Escape: close crafting or toggle pause
        if (IsKeyPressed(KeyboardKey.Escape))
        {
            if (_craftingOpen) { _craftingOpen = false; _selectedRecipe = 0; }
            else               { _pauseOpen = !_pauseOpen; }
        }

        if (_pauseOpen)
        {
            if (IsKeyPressed(KeyboardKey.Q)) ShouldQuit = true;
            if (IsKeyPressed(KeyboardKey.R)) Restart();
            return;
        }

        // E: open loot crate first, else toggle crafting table
        bool nearTable = NearCraftingTable();
        Vector3Int? nearCrate = FindNearbyCrate();
        if (IsKeyPressed(KeyboardKey.E))
        {
            if (nearCrate.HasValue)
                OpenCrate(nearCrate.Value);
            else if (nearTable || _craftingOpen)
                _craftingOpen = !_craftingOpen;
        }

        // Craft: arrow-key navigation + Enter; number keys 1-9 for first 9
        if (_craftingOpen)
        {
            if (IsKeyPressed(KeyboardKey.Up))
                _selectedRecipe = (_selectedRecipe - 1 + Recipes.Length) % Recipes.Length;
            if (IsKeyPressed(KeyboardKey.Down))
                _selectedRecipe = (_selectedRecipe + 1) % Recipes.Length;
            if (IsKeyPressed(KeyboardKey.Enter) || IsKeyPressed(KeyboardKey.KpEnter))
                TryCraft(_selectedRecipe);
            for (int i = 0; i < Math.Min(9, Recipes.Length); i++)
                if (IsKeyPressed(KeyboardKey.One + i)) { _selectedRecipe = i; TryCraft(i); }
            return; // block everything else while menu is open
        }

        // G = throw explosive
        if (IsKeyPressed(KeyboardKey.G)) ThrowExplosive();
        if (_explosionTimer > 0) _explosionTimer -= dt;

        // Left-click shoots / swings depending on weapon
        if (_player.IsGunSelected && IsMouseButtonPressed(MouseButton.Left)) Shoot();

        _meleeCooldown = Math.Max(0f, _meleeCooldown - dt);
        if (_player.IsMeleeSelected && IsMouseButtonPressed(MouseButton.Left) && _meleeCooldown <= 0f)
            MeleeAttack();

        _gunRecoil  = Math.Max(0f, _gunRecoil  - dt * 8f);
        _meleeSwing = Math.Max(0f, _meleeSwing - dt * 6f);
        UpdateBullets(dt);

        // Spike traps: damage zombies standing on them every 0.5s
        _spikeTimer += dt;
        if (_spikeTimer >= 0.5f) { _spikeTimer = 0f; UpdateSpikeTraps(); }

        // Invincibility countdown
        if (_invincibleTimer > 0)
        {
            _invincibleTimer -= dt;
            if (_invincibleTimer <= 0) _player.Invincible = false;
        }

        // Boss warning timer
        if (_bossWarningTimer > 0) _bossWarningTimer -= dt;

        // Level-up banner timer
        if (_levelUpTimer > 0) _levelUpTimer -= dt;

        // Wave preview banner timer
        if (_waveBannerTimer > 0) _waveBannerTimer -= dt;

        // Passive healing from Healing Amulet
        if (_player.HealRate > 0 && _player.HP > 0 && _player.HP < _player.MaxHP)
        {
            _healAccum += _player.HealRate * dt;
            if (_healAccum >= 1f)
            {
                int heal = (int)_healAccum;
                _player.HP = Math.Min(_player.MaxHP, _player.HP + heal);
                _healAccum -= heal;
            }
        }

        // Minimap refresh
        _mmAge += dt;
        if (_mmAge >= 1f) { _mmAge = 0f; _mmDirty = true; }

        // Sync camera to player look
        _camera.Position = _player.EyePos;
        _camera.Target   = _player.EyePos + _player.Forward;

        // Respawn instead of game-over
        if (_player.IsDead) DoRespawn();
    }

    void DoRespawn()
    {
        _player.Respawn(_spawnPos);
        _invincibleTimer  = 8f;
        _starvationTimer  = 0f;
        _craftingOpen     = false;
        _bullets.Clear();
        _deathCount++;
    }

    void ThrowExplosive()
    {
        if (_player.Explosives <= 0) return;
        _player.Explosives--;

        // Land at crosshair hit point or max range ahead
        Vector3 target;
        if (_world.Raycast(_player.EyePos, _player.Forward, 20f, out var hit, out _))
            target = new Vector3(hit.X + 0.5f, hit.Y + 0.5f, hit.Z + 0.5f);
        else
            target = _player.EyePos + _player.Forward * 15f;

        _explosionPos   = target;
        _explosionTimer = 0.5f;

        // Destroy nearby natural voxels (not walls, fixtures, or water)
        var ep = VoxelWorld.WorldToVoxel(target);
        for (int dx = -2; dx <= 2; dx++)
        for (int dy = -1; dy <= 2; dy++)
        for (int dz = -2; dz <= 2; dz++)
        {
            int vx = ep.X+dx, vy = ep.Y+dy, vz = ep.Z+dz;
            float vd = Vector3.Distance(target, new Vector3(vx+0.5f, vy+0.5f, vz+0.5f));
            if (vd > 2.5f) continue;
            byte b = _world.GetVoxel(vx, vy, vz);
            if (b == 1 || b == 2 || b == 3 || b == 6 || b == 18) // dirt, stone, wood, leaves, sand
                _world.SetVoxel(vx, vy, vz, 0);
        }

        // AoE damage with falloff
        const float Radius = 3.5f;
        foreach (var z in _waves.Active)
        {
            if (z.IsDead) continue;
            float dist = Vector3.Distance(target, z.Position + new Vector3(0, 1f, 0));
            if (dist >= Radius) continue;
            bool wasDead = z.IsDead;
            z.TakeDamage((int)(220f * (1f - dist / Radius)));
            if (z.IsDead && !wasDead) AwardKill(z);
        }
    }

    void UpdateSpikeTraps()
    {
        foreach (var z in _waves.Active)
        {
            if (z.IsDead) continue;
            var vox = VoxelWorld.WorldToVoxel(z.Position);
            if (_world.GetVoxel(vox.X, vox.Y, vox.Z) == 17)
            {
                bool wasDead = z.IsDead;
                z.TakeDamage(15);
                if (z.IsDead && !wasDead) AwardKill(z);
            }
        }
    }

    void Shoot()
    {
        if (_player.Ammo <= 0) return;
        _player.Ammo--;
        _gunRecoil = 1f;
        _bullets.Add(new Bullet {
            Pos  = _player.EyePos + _player.Forward * 0.6f,
            Dir  = _player.Forward,
            Life = GunRange / 40f
        });
    }

    void MeleeAttack()
    {
        byte wid  = _player.HotbarBlocks[_player.SelectedSlot].blockId;
        int  dmg  = wid == 248 ? 200 : wid == 252 ? 150 : wid == 254 ? 80 : 35;
        float cd  = wid == 248 ? 0.35f : wid == 252 ? 0.4f : wid == 254 ? 0.5f : 0.6f;
        _meleeCooldown = cd;
        _meleeSwing    = 1f;

        Vector3 fwd2D = new(_player.Forward.X, 0, _player.Forward.Z);
        if (fwd2D.LengthSquared() > 0) fwd2D = Vector3.Normalize(fwd2D);

        foreach (var z in _waves.Active)
        {
            if (z.IsDead) continue;
            float dist = Vector3.Distance(_player.Position, z.Position);
            if (dist > 2.2f) continue;
            Vector3 toZ = Vector3.Normalize(z.Position - _player.Position);
            toZ = new Vector3(toZ.X, 0, toZ.Z);
            if (toZ.LengthSquared() > 0 && Vector3.Dot(fwd2D, Vector3.Normalize(toZ)) > 0.3f)
            {
                bool wasDead = z.IsDead;
                int meleeDmg = z.IsArmoured ? dmg * 2 : dmg; // melee bypasses + bonus vs armoured
                z.TakeDamage(meleeDmg);
                if (z.IsDead && !wasDead) AwardKill(z);
            }
        }
    }

    void AwardKill(Zombie z)
    {
        _killCount++;
        _player.Ammo += _rng.Next(1, 4);
        if (z.IsBoss)
        {
            _player.Ammo += 5;
            _player.Inventory.TryGetValue(8, out int iron);
            _player.Inventory[8] = iron + 5;
        }
        _player.XP += z.XPReward;
        CheckLevelUp();
    }

    void CheckLevelUp()
    {
        if (_player.Level >= LevelThresholds.Length) return;
        if (_player.XP < LevelThresholds[_player.Level]) return;
        _player.Level++;
        if (_player.Level % 2 == 0)
        {
            // Even levels: speed boost
            _player.SpeedBonus += 0.4f;
            _levelUpMsg = $"LEVEL UP!  Level {_player.Level}  +Speed";
        }
        else
        {
            // Odd levels: max HP boost
            _player.MaxHP += 15;
            _player.HP = Math.Min(_player.HP + 15, _player.MaxHP);
            _levelUpMsg = $"LEVEL UP!  Level {_player.Level}  +15 Max HP";
        }
        _levelUpTimer = 3f;
        CheckLevelUp(); // recurse for multi-threshold boss kills
    }

    void UpdateBullets(float dt)
    {
        const float Speed = 40f;
        for (int i = _bullets.Count - 1; i >= 0; i--)
        {
            var b = _bullets[i];
            b.Pos  += b.Dir * Speed * dt;
            b.Life -= dt;

            bool dead = b.Life <= 0;

            // Hit solid voxel?
            var vox = VoxelWorld.WorldToVoxel(b.Pos);
            if (_world.IsSolid(vox.X, vox.Y, vox.Z)) dead = true;

            // Hit zombie? Check body + head as separate spheres (boss has larger spheres)
            if (!dead)
            {
                foreach (var z in _waves.Active)
                {
                    if (z.IsDead) continue;
                    bool hitBody, hitHead;
                    if (z.IsBoss)
                    {
                        hitBody = Vector3.Distance(b.Pos, z.Position + new Vector3(0, 1.5f, 0)) < 1.0f;
                        hitHead = Vector3.Distance(b.Pos, z.Position + new Vector3(0, 3.5f, 0)) < 0.55f;
                    }
                    else
                    {
                        hitBody = Vector3.Distance(b.Pos, z.Position + new Vector3(0, 0.75f, 0)) < 0.55f;
                        hitHead = Vector3.Distance(b.Pos, z.Position + new Vector3(0, 1.85f, 0)) < 0.3f;
                    }
                    if (hitBody || hitHead)
                    {
                        bool wasDead = z.IsDead;
                        int bulletDmg = z.IsArmoured ? GunDamage / 4 : GunDamage;
                        z.TakeDamage(bulletDmg);
                        if (z.IsDead && !wasDead) { AwardKill(z); }
                        dead = true;
                        break;
                    }
                }
            }

            if (dead) _bullets.RemoveAt(i);
            else       _bullets[i] = b;
        }
    }

    bool NearCraftingTable()
    {
        var v = VoxelWorld.WorldToVoxel(_player.Position);
        for (int dx = -3; dx <= 3; dx++)
        for (int dy = -1; dy <= 3; dy++)
        for (int dz = -3; dz <= 3; dz++)
            if (_world.GetVoxel(v.X+dx, v.Y+dy, v.Z+dz) == 9) return true;
        return false;
    }

    Vector3Int? FindNearbyCrate()
    {
        var v = VoxelWorld.WorldToVoxel(_player.Position);
        for (int dx = -2; dx <= 2; dx++)
        for (int dy = -1; dy <= 2; dy++)
        for (int dz = -2; dz <= 2; dz++)
        {
            int cx = v.X+dx, cy = v.Y+dy, cz = v.Z+dz;
            byte b = _world.GetVoxel(cx, cy, cz);
            if (b == 10 || b == 14 || b == 15)
                return new Vector3Int(cx, cy, cz);
        }
        return null;
    }

    bool NearCampfire()
    {
        var v = VoxelWorld.WorldToVoxel(_player.Position);
        for (int dx = -3; dx <= 3; dx++)
        for (int dy = -1; dy <= 3; dy++)
        for (int dz = -3; dz <= 3; dz++)
            if (_world.GetVoxel(v.X+dx, v.Y+dy, v.Z+dz) == 13) return true;
        return false;
    }

    void OpenCrate(Vector3Int pos)
    {
        byte crateType = _world.GetVoxel(pos.X, pos.Y, pos.Z);
        _world.SetVoxel(pos.X, pos.Y, pos.Z, 0);
        _player.Inventory.TryGetValue(11, out int curFood);
        _player.Inventory.TryGetValue(8,  out int curIron);

        switch (crateType)
        {
            case 10: // Food Crate
                _player.Inventory[11] = curFood + _rng.Next(4, 9);
                break;
            case 14: // Ammo Crate
                _player.Ammo += _rng.Next(10, 21);
                break;
            case 15: // Supply Crate
                _player.Inventory[11] = curFood + _rng.Next(2, 5);
                _player.Ammo += _rng.Next(5, 11);
                _player.Inventory[8] = curIron + _rng.Next(1, 4);
                break;
        }
    }

    void TryCraft(int index)
    {
        var r = Recipes[index];
        if (_player.Level < r.RequiredLevel) return; // level gate
        foreach (var ing in r.Cost)
        {
            _player.Inventory.TryGetValue(ing.Id, out int have);
            if (have < ing.Amt) return;
        }
        foreach (var ing in r.Cost)
            _player.Inventory[ing.Id] -= ing.Amt;

        if (r.OutputId == -1)
        {
            _player.Ammo += r.OutputCount;
        }
        else if (r.OutputId == -2)
        {
            _player.ArmorTier = Math.Max(_player.ArmorTier, 1);
        }
        else if (r.OutputId == -3)
        {
            _player.ArmorTier = Math.Max(_player.ArmorTier, 2);
        }
        else if (r.OutputId == -4)
        {
            _player.ArmorTier = Math.Max(_player.ArmorTier, 3);
        }
        else if (r.OutputId == -5)
        {
            _player.HP = Math.Min(_player.MaxHP, _player.HP + 25);
        }
        else if (r.OutputId == -8)
        {
            _player.HealRate = Math.Min(1f, _player.HealRate + 0.1f); // caps at 1 HP/s
        }
        else if (r.OutputId == -9)
        {
            _player.Explosives++;
        }
        else if (r.OutputId >= 248)
        {
            // Weapon/tool — put in first empty hotbar slot
            for (int s = 0; s < _player.HotbarBlocks.Length; s++)
            {
                if (_player.HotbarBlocks[s].blockId == 0)
                {
                    _player.HotbarBlocks[s] = ((byte)r.OutputId, 1);
                    break;
                }
            }
        }
        else
        {
            _player.Inventory.TryGetValue((byte)r.OutputId, out int cur);
            _player.Inventory[(byte)r.OutputId] = cur + r.OutputCount;
        }
    }

    void Restart()
    {
        _craftingOpen     = false;
        _killCount        = 0;
        _deathCount       = 0;
        _nightCleared     = false;
        _clearBannerTimer = 0f;
        _bossWarningTimer = 0f;
        _invincibleTimer  = 0f;
        _spikeTimer       = 0f;
        _mmDirty          = true;
        _mmAge            = 0f;
        _pauseOpen        = false;
        _selectedRecipe   = 0;
        _timePlayed       = 0f;
        _levelUpTimer     = 0f;
        _waveBannerTimer  = 0f;
        _healAccum        = 0f;
        _explosionTimer   = 0f;
        _fogNight         = false;
        Init();
        _gameOver = false;
    }

    public void Draw()
    {
        // Sky color shifts with day phase
        Color sky = _dnc.Phase == DayPhase.Night
            ? new Color((byte)10,  (byte)10,  (byte)30,  (byte)255)
            : _dnc.Phase == DayPhase.Warning
                ? new Color((byte)200, (byte)100, (byte)30,  (byte)255)
                : new Color((byte)80,  (byte)160, (byte)240, (byte)255);

        BeginDrawing();
        ClearBackground(sky);

        BeginMode3D(_camera);

        _world.Draw(_player.EyePos, _fogNight ? 15f : 40f);
        _waves.Draw();

        // Highlight target voxel
        if (_player.TargetVoxel.HasValue)
        {
            var v = _player.TargetVoxel.Value;
            DrawCubeWires(new Vector3(v.X + 0.5f, v.Y + 0.5f, v.Z + 0.5f),
                1.02f, 1.02f, 1.02f, Color.Yellow);
        }
        if (_player.PlaceVoxel.HasValue && IsMouseButtonDown(MouseButton.Right))
        {
            var v = _player.PlaceVoxel.Value;
            var slot = _player.HotbarBlocks[_player.SelectedSlot];
            if (slot.blockId != 0)
            {
                var c = Blocks.Get(slot.blockId).Color;
                DrawCube(new Vector3(v.X + 0.5f, v.Y + 0.5f, v.Z + 0.5f), 1f, 1f, 1f,
                    new Color(c.R, c.G, c.B, (byte)100));
            }
        }

        // Bullets
        foreach (var b in _bullets)
            DrawSphere(b.Pos, 0.07f, new Color((byte)255,(byte)220,(byte)50,(byte)255));

        // Explosion flash
        if (_explosionTimer > 0)
        {
            float t = _explosionTimer / 0.5f; // 1=fresh, 0=gone
            float rad = (1f - t) * 3.8f + 0.2f;
            DrawSphere(_explosionPos, rad,
                new Color((byte)255,(byte)(int)(60+t*100),(byte)10,(byte)(int)(t*170)));
            DrawSphere(_explosionPos, rad * 0.4f,
                new Color((byte)255,(byte)230,(byte)80,(byte)(int)(t*140)));
        }

        // Campfire flames + torch glow + spike decorations
        DrawCampfireFlames();
        DrawTorchGlow();
        DrawSpikeDecorations();

        // Viewmodel
        {
            Vector3 fwd   = _player.Forward;
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, fwd));
            Vector3 up    = Vector3.Cross(fwd, right);

            byte selId = _player.HotbarBlocks[_player.SelectedSlot].blockId;

            if (_player.IsGunSelected)
            {
                float   kick    = _gunRecoil * 0.12f;
                Vector3 gunBase = _player.EyePos
                    + right * 0.22f - up * 0.18f
                    + fwd * (0.45f - kick) + up * (kick * 0.5f);

                DrawCube(gunBase, 0.07f, 0.07f, 0.22f, new Color((byte)60,(byte)60,(byte)60,(byte)255));
                DrawCube(gunBase + fwd * 0.18f, 0.04f, 0.04f, 0.14f, new Color((byte)40,(byte)40,(byte)40,(byte)255));
                DrawCube(gunBase - up * 0.07f - fwd * 0.05f, 0.06f, 0.1f, 0.06f, new Color((byte)80,(byte)50,(byte)30,(byte)255));
                if (_gunRecoil > 0.8f)
                    DrawCube(gunBase + fwd * 0.28f, 0.12f, 0.12f, 0.06f, new Color((byte)255,(byte)220,(byte)50,(byte)200));
            }
            else if (selId == 252) // Iron Sword
            {
                float swing = _meleeSwing * 0.45f;
                Vector3 sBase = _player.EyePos
                    + right * 0.20f - up * (0.14f - swing) + fwd * (0.38f + swing * 0.15f);
                // Guard
                DrawCube(sBase, 0.22f, 0.05f, 0.05f,
                    new Color((byte)150,(byte)155,(byte)165,(byte)255));
                // Handle
                DrawCube(sBase - fwd * 0.13f, 0.04f, 0.04f, 0.18f,
                    new Color((byte)80,(byte)50,(byte)20,(byte)255));
                // Blade — longer and brighter than stone sword
                DrawCube(sBase + fwd * 0.16f, 0.048f, 0.058f, 0.36f,
                    new Color((byte)200,(byte)210,(byte)220,(byte)255));
                // Blade tip
                DrawCube(sBase + fwd * 0.36f + up * 0.01f, 0.025f, 0.03f, 0.08f,
                    new Color((byte)220,(byte)230,(byte)240,(byte)255));
            }
            else if (selId == 253) // Wood Club
            {
                float swing = _meleeSwing * 0.35f;
                Vector3 clubBase = _player.EyePos
                    + right * 0.22f - up * (0.15f - swing) + fwd * (0.4f + swing * 0.2f);
                // Handle
                DrawCube(clubBase, 0.045f, 0.045f, 0.32f,
                    new Color((byte)120,(byte)75,(byte)30,(byte)255));
                // Thick knob head
                DrawCube(clubBase + fwd * 0.18f + up * 0.02f, 0.1f, 0.1f, 0.14f,
                    new Color((byte)139,(byte)90,(byte)40,(byte)255));
            }
            else if (selId == 254) // Stone Sword
            {
                float swing = _meleeSwing * 0.4f;
                Vector3 swordBase = _player.EyePos
                    + right * 0.20f - up * (0.14f - swing) + fwd * (0.38f + swing * 0.15f);
                // Guard (crossguard)
                DrawCube(swordBase, 0.18f, 0.045f, 0.045f,
                    new Color((byte)101,(byte)67,(byte)33,(byte)255));
                // Handle
                DrawCube(swordBase - fwd * 0.12f, 0.04f, 0.04f, 0.16f,
                    new Color((byte)120,(byte)75,(byte)30,(byte)255));
                // Blade (stone-grey, long thin)
                DrawCube(swordBase + fwd * 0.14f, 0.045f, 0.055f, 0.3f,
                    new Color((byte)160,(byte)160,(byte)165,(byte)255));
                // Blade tip (darker)
                DrawCube(swordBase + fwd * 0.3f + up * 0.01f, 0.025f, 0.03f, 0.1f,
                    new Color((byte)110,(byte)110,(byte)115,(byte)255));
            }
            else if (selId == 248) // Steel Sword — long polished silver blade
            {
                float swing = _meleeSwing * 0.45f;
                Vector3 ssBase = _player.EyePos
                    + right * 0.20f - up * (0.14f - swing) + fwd * (0.38f + swing * 0.15f);
                // Guard — wide, gold-tinted
                DrawCube(ssBase, 0.26f, 0.055f, 0.055f,
                    new Color((byte)200,(byte)180,(byte)60,(byte)255));
                // Handle — wrapped grip
                DrawCube(ssBase - fwd * 0.14f, 0.04f, 0.04f, 0.2f,
                    new Color((byte)60,(byte)40,(byte)20,(byte)255));
                // Blade — longer and brighter than iron
                DrawCube(ssBase + fwd * 0.19f, 0.05f, 0.06f, 0.42f,
                    new Color((byte)220,(byte)235,(byte)245,(byte)255));
                // Tip
                DrawCube(ssBase + fwd * 0.42f + up * 0.01f, 0.02f, 0.03f, 0.1f,
                    new Color((byte)240,(byte)248,(byte)255,(byte)255));
            }
            else if (selId == 249) // Stone Hatchet — wide stone blade, short handle
            {
                Vector3 hBase = _player.EyePos + right * 0.24f - up * 0.20f + fwd * 0.40f;
                DrawCube(hBase, 0.04f, 0.04f, 0.28f,
                    new Color((byte)101,(byte)67,(byte)33,(byte)255)); // handle
                DrawCube(hBase + fwd * 0.14f + up * 0.04f, 0.30f, 0.18f, 0.06f,
                    new Color((byte)110,(byte)110,(byte)115,(byte)255)); // stone blade
                DrawCube(hBase + fwd * 0.20f - up * 0.06f, 0.1f, 0.08f, 0.05f,
                    new Color((byte)90,(byte)90,(byte)95,(byte)255));   // lower edge
            }
            else if (selId == 250) // Iron Pickaxe — longer, silver head
            {
                Vector3 pBase = _player.EyePos + right * 0.24f - up * 0.22f + fwd * 0.44f;
                DrawCube(pBase, 0.04f, 0.04f, 0.40f,
                    new Color((byte)101,(byte)67,(byte)33,(byte)255)); // handle
                DrawCube(pBase + fwd * 0.22f + up * 0.03f, 0.24f, 0.055f, 0.055f,
                    new Color((byte)190,(byte)195,(byte)205,(byte)255)); // iron crossbar
                DrawCube(pBase + fwd * 0.29f - up * 0.06f, 0.04f, 0.14f, 0.04f,
                    new Color((byte)170,(byte)175,(byte)185,(byte)255)); // pick point
            }
            else
            {
                // Default pickaxe viewmodel (stone)
                Vector3 pickBase = _player.EyePos
                    + right * 0.24f - up * 0.22f + fwd * 0.42f;
                DrawCube(pickBase, 0.04f, 0.04f, 0.38f,
                    new Color((byte)101,(byte)67,(byte)33,(byte)255));
                DrawCube(pickBase + fwd * 0.2f + up * 0.03f, 0.22f, 0.055f, 0.055f,
                    new Color((byte)128,(byte)128,(byte)128,(byte)255));
                DrawCube(pickBase + fwd * 0.26f - up * 0.05f, 0.04f, 0.12f, 0.04f,
                    new Color((byte)90,(byte)90,(byte)90,(byte)255));
            }
        }

        EndMode3D();

        DrawHUD();

        if (_craftingOpen) DrawCraftingUI();
        if (_pauseOpen)    DrawPauseScreen();
        if (_gameOver)     DrawGameOver();

        EndDrawing();
    }

    void DrawHUD()
    {
        int sw = GetScreenWidth(), sh = GetScreenHeight();

        // Crosshair
        DrawLine(sw/2 - 10, sh/2, sw/2 + 10, sh/2, Color.White);
        DrawLine(sw/2, sh/2 - 10, sw/2, sh/2 + 10, Color.White);

        // Stamina bar
        {
            bool depleted = _player.Stamina <= 0;
            Color stCol = depleted ? Color.DarkGray
                        : _player.Sprinting ? new Color((byte)0,(byte)220,(byte)255,(byte)255)
                        : new Color((byte)0,(byte)160,(byte)200,(byte)255);
            DrawRectangle(10, sh - 110, 160, 8, Color.DarkGray);
            DrawRectangle(10, sh - 110, (int)(160f * _player.Stamina / 100f), 8, stCol);
            DrawText(_player.Sprinting ? "SPRINT" : "STAM", 176, sh - 112, 11, stCol);
        }

        // XP bar
        {
            bool maxed = _player.Level >= LevelThresholds.Length;
            int nextXP = maxed ? LevelThresholds[^1] : LevelThresholds[_player.Level];
            float xpFrac = maxed ? 1f : Math.Min(1f, _player.XP / (float)nextXP);
            DrawRectangle(10, sh - 95, 160, 10, Color.DarkGray);
            DrawRectangle(10, sh - 95, (int)(160f * xpFrac), 10,
                new Color((byte)100,(byte)200,(byte)100,(byte)255));
            string xpLabel = maxed ? "Lv.MAX" : $"Lv.{_player.Level}  {_player.XP}/{nextXP} XP";
            DrawText(xpLabel, 176, sh - 96, 12,
                maxed ? new Color((byte)255,(byte)215,(byte)0,(byte)255) : Color.LightGray);
        }

        // Hunger bar
        DrawRectangle(10, sh - 72, 160, 13, Color.DarkGray);
        DrawRectangle(10, sh - 72, (int)(160f * _player.Hunger / 100f), 13,
            _player.Hunger < 25f ? Color.Red : new Color((byte)180,(byte)130,(byte)40,(byte)255));
        DrawText("FOOD", 174, sh - 72, 12, _player.Hunger < 25f ? Color.Red : Color.Gray);

        // Thirst bar
        DrawRectangle(10, sh - 55, 160, 13, Color.DarkGray);
        DrawRectangle(10, sh - 55, (int)(160f * _player.Thirst / 100f), 13,
            _player.Thirst < 25f ? Color.Red : new Color((byte)40,(byte)120,(byte)200,(byte)255));
        DrawText("H2O", 174, sh - 55, 12, _player.Thirst < 25f ? Color.Red : Color.Gray);

        // Health bar
        DrawRectangle(10, sh - 30, 200, 20, Color.DarkGray);
        DrawRectangle(10, sh - 30, (int)(200f * _player.HP / _player.MaxHP), 20, Color.Red);
        DrawText($"HP: {_player.HP}/{_player.MaxHP}", 15, sh - 28, 16, Color.White);

        // Row: AMMO | EXPL
        Color ammoCol = _player.Ammo > 0 ? Color.White : Color.Red;
        DrawText($"AMMO: {_player.Ammo}", 215, sh - 30, 16, ammoCol);
        if (_player.Explosives > 0)
            DrawText($"EXPL: {_player.Explosives}", 315, sh - 30, 16,
                new Color((byte)255,(byte)140,(byte)0,(byte)255));

        // Row: Armor | Speed | HealRate
        int statsX = 215;
        if (_player.ArmorTier > 0)
        {
            string arm = _player.ArmorTier == 3 ? "ARM:Iron"
                       : _player.ArmorTier == 2 ? "ARM:Stone"
                       : "ARM:Wood";
            Color armCol = _player.ArmorTier == 3
                ? new Color((byte)190,(byte)195,(byte)210,(byte)255)
                : _player.ArmorTier == 2
                    ? new Color((byte)160,(byte)160,(byte)180,(byte)255)
                    : new Color((byte)180,(byte)140,(byte)80,(byte)255);
            DrawText(arm, statsX, sh - 47, 14, armCol);
            statsX += MeasureText(arm, 14) + 10;
        }
        if (_player.SpeedBonus > 0)
        {
            string spd = $"SPD+{_player.SpeedBonus:0.0}";
            DrawText(spd, statsX, sh - 47, 14, new Color((byte)100,(byte)220,(byte)255,(byte)255));
            statsX += MeasureText(spd, 14) + 10;
        }
        if (_player.HealRate > 0)
            DrawText($"HP+{_player.HealRate:0.0}/s", statsX, sh - 47, 14,
                new Color((byte)100,(byte)255,(byte)150,(byte)255));

        // Day/night
        string phase = _dnc.Phase switch
        {
            DayPhase.Day     => $"Day {_dnc.NightCount + 1}  {TimeStr(_dnc.DayRemaining)}",
            DayPhase.Warning => $"NIGHT INCOMING  {TimeStr(_dnc.DayRemaining)}",
            DayPhase.Night   => $"Night {_dnc.NightCount}  {TimeStr(_dnc.NightRemaining)}",
            _                => ""
        };
        Color phaseColor = _dnc.Phase == DayPhase.Night ? Color.Orange
                         : _dnc.Phase == DayPhase.Warning ? new Color((byte)255,(byte)200,(byte)50,(byte)255)
                         : Color.White;
        DrawText(phase, sw/2 - MeasureText(phase, 20)/2, 10, 20, phaseColor);

        // Fog night indicator
        if (_fogNight)
            DrawText("FOG", sw - MM_SIZE - 10, sh/2 - MeasureText("FOG", 20)/2, 20,
                new Color((byte)180,(byte)180,(byte)220,(byte)200));

        // Kill count + death count (left of minimap)
        DrawText($"Kills: {_killCount}", sw - MM_SIZE - 100, 10, 18, Color.Orange);
        if (_deathCount > 0)
            DrawText($"Deaths: {_deathCount}", sw - MM_SIZE - 100, 30, 15,
                new Color((byte)200,(byte)80,(byte)80,(byte)255));

        // Active zombie count during night
        if (_dnc.Phase == DayPhase.Night)
            DrawText($"Zombies: {_waves.Active.Count}", sw - MM_SIZE - 110, 48, 15, Color.Orange);

        // Minimap (top-right corner)
        DrawMinimap(sw - MM_SIZE - 10, 10);

        // Inventory
        DrawText("Resources:", 10, 10, 16, Color.White);
        int row = 0;
        foreach (var kv in _player.Inventory)
        {
            if (kv.Value <= 0) continue;
            string label = $"{Blocks.Get(kv.Key).Name}: {kv.Value}";
            DrawText(label, 10, 30 + row * 18, 15, Color.LightGray);
            row++;
        }

        // Hotbar
        int hotbarW = 9 * 44 + 8;
        int hotbarX = sw/2 - hotbarW/2;
        int hotbarY = sh - 60;
        for (int i = 0; i < 9; i++)
        {
            int bx = hotbarX + i * 44;
            bool sel = i == _player.SelectedSlot;
            DrawRectangle(bx, hotbarY, 40, 40, sel ? Color.White : Color.DarkGray);
            DrawRectangleLines(bx, hotbarY, 40, 40, sel ? Color.Yellow : Color.Gray);
            var slot = _player.HotbarBlocks[i];
            if (slot.blockId == 255)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)80,(byte)200,(byte)80,(byte)255));
                DrawText("GUN", bx+6, hotbarY+14, 12, Color.White);
            }
            else if (slot.blockId == 248)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)180,(byte)200,(byte)220,(byte)255));
                DrawText("SSTD", bx+2, hotbarY+14, 10, Color.White);
            }
            else if (slot.blockId == 249)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)90,(byte)90,(byte)95,(byte)255));
                DrawText("HATC", bx+2, hotbarY+14, 10, Color.White);
            }
            else if (slot.blockId == 250)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)160,(byte)165,(byte)175,(byte)255));
                DrawText("PICK", bx+4, hotbarY+14, 10, Color.White);
            }
            else if (slot.blockId == 252)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)190,(byte)195,(byte)205,(byte)255));
                DrawText("ISWD", bx+2, hotbarY+14, 10, Color.White);
            }
            else if (slot.blockId == 253)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)139,(byte)90,(byte)40,(byte)255));
                DrawText("CLUB", bx+2, hotbarY+14, 11, Color.White);
            }
            else if (slot.blockId == 254)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)160,(byte)160,(byte)165,(byte)255));
                DrawText("SWRD", bx+2, hotbarY+14, 11, Color.White);
            }
            else if (slot.blockId == 11)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)200,(byte)80,(byte)50,(byte)255));
                DrawText("FOOD", bx+2, hotbarY+14, 11, Color.White);
            }
            else if (slot.blockId == 13)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)180,(byte)60,(byte)15,(byte)255));
                DrawText("FIRE", bx+4, hotbarY+14, 11, Color.White);
            }
            else if (slot.blockId == 16)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)100,(byte)70,(byte)10,(byte)255));
                DrawText("TRCH", bx+2, hotbarY+14, 10, new Color((byte)255,(byte)210,(byte)60,(byte)255));
            }
            else if (slot.blockId == 17)
            {
                DrawRectangle(bx+4, hotbarY+4, 32, 32, new Color((byte)50,(byte)50,(byte)60,(byte)255));
                DrawText("SPIK", bx+2, hotbarY+14, 10, new Color((byte)160,(byte)160,(byte)180,(byte)255));
            }
            else if (slot.blockId != 0)
            {
                var col = Blocks.Get(slot.blockId).Color;
                DrawRectangle(bx+4, hotbarY+4, 32, 32, col);
                string name = Blocks.Get(slot.blockId).Name;
                DrawText(name[..Math.Min(4,name.Length)], bx+2, hotbarY+26, 10, Color.White);
            }
        }

        // Context hint or controls
        bool nc  = NearCraftingTable();
        var  nearCratePos = FindNearbyCrate();
        bool ncr = nearCratePos.HasValue;
        bool ncf = NearCampfire();

        string crateHint = "E: Open Loot Crate";
        if (nearCratePos.HasValue)
        {
            byte ct = _world.GetVoxel(nearCratePos.Value.X, nearCratePos.Value.Y, nearCratePos.Value.Z);
            crateHint = ct == 14 ? "E: Open Ammo Crate  (10-20 ammo)"
                      : ct == 15 ? "E: Open Supply Crate  (food + ammo + iron)"
                      : "E: Open Food Crate  (4-8 food)";
        }

        string hint = ncr ? crateHint
                    : nc  ? "E: Open Crafting Table"
                    : ncf ? "CAMPFIRE: Hunger + Thirst restoring"
                    : _player.Explosives > 0
            ? "G: Throw Explosive  WASD move  LClick shoot/swing  RClick build  F eat"
            : "WASD/Arrows move  LClick shoot/mine/swing  RClick build  F eat  Space jump";
        Color hintCol = (ncr || nc) ? Color.Yellow
                      : ncf         ? new Color((byte)255,(byte)150,(byte)50,(byte)255)
                      : Color.Gray;
        DrawText(hint, 10, sh - 18, 12, hintCol);

        // Level-up banner
        if (_levelUpTimer > 0)
        {
            byte alpha = (byte)(int)(Math.Min(1f, _levelUpTimer) * 255);
            int luw = MeasureText(_levelUpMsg, 26);
            DrawRectangle(sw/2 - luw/2 - 14, sh/2 + 50, luw + 28, 42,
                new Color((byte)0,(byte)0,(byte)0,(byte)(alpha/2)));
            DrawText(_levelUpMsg, sw/2 - luw/2, sh/2 + 58, 26,
                new Color((byte)255,(byte)215,(byte)0,alpha));
        }

        // Wave preview banner
        if (_waveBannerTimer > 0)
        {
            byte alpha = (byte)(int)(Math.Min(1f, _waveBannerTimer) * 255);
            int wbw = MeasureText(_waveBannerMsg, 22);
            DrawRectangle(sw/2 - wbw/2 - 12, sh/2 - 190, wbw + 24, 36,
                new Color((byte)0,(byte)0,(byte)0,(byte)(alpha/2)));
            DrawText(_waveBannerMsg, sw/2 - wbw/2, sh/2 - 183, 22,
                new Color((byte)255,(byte)180,(byte)0,alpha));
        }

        // Wave cleared banner
        if (_clearBannerTimer > 0)
        {
            byte alpha = (byte)(int)(Math.Min(1f, _clearBannerTimer) * 255);
            string banner = "WAVE CLEARED!  +15 Ammo  +3 Food";
            int bw = MeasureText(banner, 28);
            DrawRectangle(sw/2 - bw/2 - 12, sh/2 - 80, bw + 24, 44,
                new Color((byte)0,(byte)0,(byte)0,(byte)(alpha/2)));
            DrawText(banner, sw/2 - bw/2, sh/2 - 70, 28,
                new Color((byte)80,(byte)255,(byte)80,alpha));
        }

        // Boss warning banner
        if (_bossWarningTimer > 0)
        {
            float pulse = (MathF.Sin((float)GetTime() * 8f) + 1f) * 0.5f;
            byte alpha = (byte)(int)(Math.Min(1f, _bossWarningTimer) * 220);
            string warn = "⚠  BOSS ZOMBIE INCOMING  ⚠";
            int ww = MeasureText(warn, 26);
            DrawRectangle(sw/2 - ww/2 - 16, sh/2 - 130, ww + 32, 44,
                new Color((byte)0,(byte)0,(byte)0,(byte)160));
            DrawText(warn, sw/2 - ww/2, sh/2 - 120, 26,
                new Color((byte)255,(byte)(int)(100 + pulse*80),(byte)0,alpha));
        }

        // Invincibility overlay (pulsing cyan border)
        if (_player.Invincible)
        {
            float pulse = (MathF.Sin((float)GetTime() * 10f) + 1f) * 0.5f;
            byte ba = (byte)(int)(pulse * 100 + 30);
            DrawRectangle(0, 0, sw, 10, new Color((byte)0,(byte)200,(byte)255,ba));
            DrawRectangle(0, sh-10, sw, 10, new Color((byte)0,(byte)200,(byte)255,ba));
            DrawRectangle(0, 0, 10, sh, new Color((byte)0,(byte)200,(byte)255,ba));
            DrawRectangle(sw-10, 0, 10, sh, new Color((byte)0,(byte)200,(byte)255,ba));
            string inv = $"INVINCIBLE  {_invincibleTimer:0.0}s";
            DrawText(inv, sw/2 - MeasureText(inv, 16)/2, 14, 16,
                new Color((byte)0,(byte)220,(byte)255,(byte)255));
        }
    }

    void DrawPauseScreen()
    {
        int sw = GetScreenWidth(), sh = GetScreenHeight();
        DrawRectangle(0, 0, sw, sh, new Color((byte)0,(byte)0,(byte)0,(byte)160));

        string title = "PAUSED";
        DrawText(title, sw/2 - MeasureText(title,48)/2, sh/2 - 130, 48, Color.White);

        string stats = $"Night {_dnc.NightCount}   Kills: {_killCount}   Deaths: {_deathCount}";
        DrawText(stats, sw/2 - MeasureText(stats,22)/2, sh/2 - 55, 22, Color.LightGray);

        int mins = (int)_timePlayed / 60, secs = (int)_timePlayed % 60;
        string timeStr = $"Time: {mins}:{secs:D2}";
        DrawText(timeStr, sw/2 - MeasureText(timeStr,18)/2, sh/2 - 20, 18, Color.Gray);

        DrawText("ESC — Resume",
            sw/2 - MeasureText("ESC — Resume",22)/2, sh/2 + 20, 22, Color.White);
        DrawText("R — Restart",
            sw/2 - MeasureText("R — Restart",22)/2, sh/2 + 50, 22, Color.White);
        DrawText("Q — Quit Game",
            sw/2 - MeasureText("Q — Quit Game",22)/2, sh/2 + 80, 22,
            new Color((byte)210,(byte)70,(byte)70,(byte)255));
    }

    void DrawGameOver()
    {
        int sw = GetScreenWidth(), sh = GetScreenHeight();
        DrawRectangle(0, 0, sw, sh, new Color((byte)0,(byte)0,(byte)0,(byte)150));
        string msg = "YOU DIED";
        DrawText(msg, sw/2 - MeasureText(msg, 60)/2, sh/2 - 50, 60, Color.Red);
        string sub = $"Survived {_dnc.NightCount} night(s)  |  Kills: {_killCount}  |  Press R to restart";
        DrawText(sub, sw/2 - MeasureText(sub, 24)/2, sh/2 + 30, 24, Color.White);
    }

    void DrawSpikeDecorations()
    {
        var vp = VoxelWorld.WorldToVoxel(_player.EyePos);
        var spikeCol = new Color((byte)90,(byte)90,(byte)100,(byte)255);
        for (int dx = -10; dx <= 10; dx++)
        for (int dy = -2;  dy <= 6;  dy++)
        for (int dz = -10; dz <= 10; dz++)
        {
            int cx = vp.X+dx, cy = vp.Y+dy, cz = vp.Z+dz;
            if (_world.GetVoxel(cx, cy, cz) != 17) continue;
            float bx = cx, by = cy + 1f, bz = cz;
            DrawCube(new Vector3(bx+0.2f, by+0.2f, bz+0.2f), 0.07f, 0.42f, 0.07f, spikeCol);
            DrawCube(new Vector3(bx+0.8f, by+0.2f, bz+0.2f), 0.07f, 0.42f, 0.07f, spikeCol);
            DrawCube(new Vector3(bx+0.2f, by+0.2f, bz+0.8f), 0.07f, 0.42f, 0.07f, spikeCol);
            DrawCube(new Vector3(bx+0.8f, by+0.2f, bz+0.8f), 0.07f, 0.42f, 0.07f, spikeCol);
            DrawCube(new Vector3(bx+0.5f, by+0.2f, bz+0.5f), 0.07f, 0.42f, 0.07f, spikeCol);
        }
    }

    void DrawTorchGlow()
    {
        var v = VoxelWorld.WorldToVoxel(_player.EyePos);
        float t = (float)GetTime();
        for (int dx = -8; dx <= 8; dx++)
        for (int dy = -3; dy <= 6; dy++)
        for (int dz = -8; dz <= 8; dz++)
        {
            int cx = v.X+dx, cy = v.Y+dy, cz = v.Z+dz;
            if (_world.GetVoxel(cx, cy, cz) != 16) continue;
            float bob = MathF.Sin(t * 3f + cx * 0.9f + cz * 1.1f) * 0.03f;
            DrawSphere(new Vector3(cx+0.5f, cy+1.1f+bob, cz+0.5f), 0.18f,
                new Color((byte)255,(byte)210,(byte)60,(byte)200));
        }
    }

    void DrawMinimap(int ox, int oy)
    {
        // Rebuild terrain cache once per second
        if (_mmDirty)
        {
            int pcx = (int)_player.Position.X, pcz = (int)_player.Position.Z;
            for (int dz = 0; dz < MM_RANGE * 2; dz++)
            for (int dx = 0; dx < MM_RANGE * 2; dx++)
            {
                int wx = pcx + dx - MM_RANGE;
                int wz = pcz + dz - MM_RANGE;
                _mmColors[dx, dz] = MinimapSurface(wx, wz);
            }
            _mmDirty = false;
        }

        // Border
        DrawRectangle(ox - 2, oy - 2, MM_SIZE + 4, MM_SIZE + 4,
            new Color((byte)0,(byte)0,(byte)0,(byte)200));
        DrawText("MAP", ox + MM_SIZE/2 - MeasureText("MAP",10)/2, oy - 13, 10, Color.DarkGray);

        // Terrain
        for (int dz = 0; dz < MM_RANGE * 2; dz++)
        for (int dx = 0; dx < MM_RANGE * 2; dx++)
            DrawRectangle(ox + dx * MM_SCALE, oy + dz * MM_SCALE,
                MM_SCALE, MM_SCALE, _mmColors[dx, dz]);

        // Zombie dots
        int pcxd = (int)_player.Position.X, pczd = (int)_player.Position.Z;
        foreach (var z in _waves.Active)
        {
            if (z.IsDead) continue;
            int zdx = (int)(z.Position.X - pcxd) + MM_RANGE;
            int zdz = (int)(z.Position.Z - pczd) + MM_RANGE;
            if (zdx < 0 || zdx >= MM_RANGE*2 || zdz < 0 || zdz >= MM_RANGE*2) continue;
            Color dot = z.IsBoss     ? Color.Magenta
                      : z.IsArmoured ? new Color((byte)160,(byte)165,(byte)180,(byte)255)
                      : Color.Red;
            DrawRectangle(ox + zdx * MM_SCALE - 1, oy + zdz * MM_SCALE - 1, 3, 3, dot);
        }

        // Player dot (always centre, white)
        DrawRectangle(ox + MM_RANGE * MM_SCALE - 2, oy + MM_RANGE * MM_SCALE - 2,
            4, 4, Color.White);
    }

    Color MinimapSurface(int x, int z)
    {
        for (int y = Math.Min(_world.SizeY - 1, 20); y >= 0; y--)
        {
            byte b = _world.GetVoxel(x, y, z);
            if (b == 0) continue;
            // Show non-solid special blocks before skipping
            if (b == 19) return new Color((byte)30,(byte)100,(byte)200,(byte)255); // water
            if (!_world.IsSolid(x, y, z)) continue;
            return b switch {
                1        => new Color((byte)120,(byte)72,(byte)0,(byte)255),
                2        => new Color((byte)90,(byte)90,(byte)90,(byte)255),
                3        => new Color((byte)80,(byte)50,(byte)20,(byte)255),
                6        => new Color((byte)34,(byte)120,(byte)34,(byte)255),
                7        => new Color((byte)160,(byte)80,(byte)20,(byte)255),
                9        => new Color((byte)180,(byte)100,(byte)40,(byte)255),
                10 or 14 or 15
                         => new Color((byte)220,(byte)160,(byte)30,(byte)255),
                13       => new Color((byte)200,(byte)80,(byte)20,(byte)255),
                18       => new Color((byte)210,(byte)180,(byte)100,(byte)255), // sand
                4 or 5 or 12
                         => new Color((byte)180,(byte)180,(byte)200,(byte)255),
                _        => new Color((byte)70,(byte)70,(byte)80,(byte)255),
            };
        }
        return new Color((byte)15,(byte)15,(byte)20,(byte)255);
    }

    void DrawCampfireFlames()
    {
        var v = VoxelWorld.WorldToVoxel(_player.EyePos);
        float t = (float)GetTime();
        for (int dx = -10; dx <= 10; dx++)
        for (int dy = -3;  dy <= 8;  dy++)
        for (int dz = -10; dz <= 10; dz++)
        {
            int cx = v.X+dx, cy = v.Y+dy, cz = v.Z+dz;
            if (_world.GetVoxel(cx, cy, cz) != 13) continue;
            float bob = MathF.Sin(t * 5f + cx * 1.3f + cz * 0.7f) * 0.05f;
            // Outer orange flame
            DrawCube(new Vector3(cx+0.5f, cy+1.25f+bob, cz+0.5f), 0.22f, 0.32f, 0.22f,
                new Color((byte)255,(byte)120,(byte)10,(byte)210));
            // Inner yellow tip
            DrawCube(new Vector3(cx+0.5f, cy+1.5f+bob, cz+0.5f), 0.12f, 0.22f, 0.12f,
                new Color((byte)255,(byte)220,(byte)50,(byte)180));
        }
    }

    void DrawCraftingUI()
    {
        int sw = GetScreenWidth(), sh = GetScreenHeight();
        int pw = 440, ph = 80 + Recipes.Length * 44;
        int px = sw/2 - pw/2, py = sh/2 - ph/2;

        DrawRectangle(px, py, pw, ph, new Color((byte)20,(byte)15,(byte)10,(byte)230));
        DrawRectangleLines(px, py, pw, ph, new Color((byte)160,(byte)100,(byte)40,(byte)255));
        DrawText("CRAFTING TABLE", px+pw/2 - MeasureText("CRAFTING TABLE",20)/2, py+12, 20,
            new Color((byte)220,(byte)160,(byte)60,(byte)255));
        DrawLine(px+10, py+38, px+pw-10, py+38, new Color((byte)100,(byte)70,(byte)30,(byte)255));

        for (int i = 0; i < Recipes.Length; i++)
        {
            var r   = Recipes[i];
            int ry   = py + 48 + i * 44;
            bool locked = _player.Level < r.RequiredLevel;
            if (i == _selectedRecipe)
                DrawRectangle(px+6, ry-4, pw-12, 42,
                    locked ? new Color((byte)60,(byte)15,(byte)15,(byte)160)
                           : new Color((byte)60,(byte)40,(byte)15,(byte)160));
            bool can = !locked && CanAfford(r);

            Color nameCol = locked ? Color.DarkGray : (can ? Color.White : Color.Gray);
            string prefix = i < 9 ? $"[{i+1}]" : "[ ]";
            DrawText($"{prefix}  {r.Name}", px+16, ry, 18, nameCol);

            if (locked)
                DrawText($"Requires Level {r.RequiredLevel}", px+32, ry+22, 13, Color.DarkGray);
            else
            {
                string costStr = CostString(r);
                Color costCol  = can
                    ? new Color((byte)120,(byte)220,(byte)120,(byte)255)
                    : new Color((byte)200,(byte)80,(byte)80,(byte)255);
                DrawText(costStr, px+32, ry+22, 13, costCol);
            }
        }

        DrawText("↑↓ Navigate   Enter Craft   ESC Close", px+pw/2 - MeasureText("↑↓ Navigate   Enter Craft   ESC Close",12)/2, py+ph-22, 12, Color.DarkGray);
    }

    bool CanAfford(Recipe r)
    {
        foreach (var ing in r.Cost)
        {
            _player.Inventory.TryGetValue(ing.Id, out int have);
            if (have < ing.Amt) return false;
        }
        return true;
    }

    string CostString(Recipe r)
    {
        var sb = new System.Text.StringBuilder("Costs: ");
        for (int i = 0; i < r.Cost.Length; i++)
        {
            var ing = r.Cost[i];
            _player.Inventory.TryGetValue(ing.Id, out int have);
            sb.Append($"{have}/{ing.Amt} {Blocks.Get(ing.Id).Name}");
            if (i < r.Cost.Length - 1) sb.Append("  +  ");
        }
        return sb.ToString();
    }

    static string TimeStr(float secs)
    {
        int m = (int)secs / 60, s = (int)secs % 60;
        return $"{m}:{s:D2}";
    }
}
