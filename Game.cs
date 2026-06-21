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

    bool  _gameOver        = false;
    bool  _craftingOpen    = false;
    float _gunRecoil       = 0f;
    float _meleeSwing      = 0f;
    float _meleeCooldown   = 0f;
    float _starvationTimer = 0f;
    int   _killCount        = 0;
    bool  _nightCleared     = false;
    float _nightStartDelay  = 0f;   // prevents false clear at night start
    float _clearBannerTimer = 0f;   // how long to show "WAVE CLEARED!" banner
    readonly Random _rng    = new();

    struct Bullet { public Vector3 Pos; public Vector3 Dir; public float Life; }
    readonly System.Collections.Generic.List<Bullet> _bullets = new();

    // ── Crafting recipes ─────────────────────────────────────────────
    // OutputId -1 = ammo (special). Cost = (blockId, count) pairs.
    record struct Ingredient(byte Id, int Amt);
    record struct Recipe(string Name, int OutputId, int OutputCount, Ingredient[] Cost);

    static readonly Recipe[] Recipes =
    {
        new("Ammo ×10",       -1, 10, new[]{ new Ingredient(3,1), new Ingredient(8,2) }),  // 1 wood + 2 iron
        new("Quiver ×20",     -1, 20, new[]{ new Ingredient(3,2), new Ingredient(8,1) }),  // 2 wood + 1 iron  (better rate)
        new("Bandage",        -5,  1, new[]{ new Ingredient(11,3) }),                       // 3 food → heal 25 HP
        new("Wood Club",      253,  1, new[]{ new Ingredient(3,3) }),                        // 3 wood
        new("Stone Sword",    254,  1, new[]{ new Ingredient(3,1), new Ingredient(2,3) }),  // 1 wood + 3 stone
        new("Wood Armor",      -2,  1, new[]{ new Ingredient(3,5) }),                         // 5 wood
        new("Stone Armor",     -3,  1, new[]{ new Ingredient(2,4), new Ingredient(3,2) }),  // 4 stone + 2 wood
        new("Iron Sword",     252,  1, new[]{ new Ingredient(3,2), new Ingredient(8,5) }),  // 2 wood + 5 iron
        new("Iron Armor",      -4,  1, new[]{ new Ingredient(8,6) }),                        // 6 iron
    };

    public void Init()
    {
        // World
        _world = new VoxelWorld(chunksX: 8, chunksY: 2, chunksZ: 8);
        WorldGenerator.Generate(_world, seed: Environment.TickCount);

        // Find spawn — stand on surface at world center
        int cx = _world.SizeX / 2, cz = _world.SizeZ / 2;
        float spawnY = 0;
        for (int y = _world.SizeY - 1; y >= 0; y--)
            if (_world.IsSolid(cx, y, cz)) { spawnY = y + 1f; break; }

        _player = new Player(_world, new Vector3(cx, spawnY, cz));

        // Day/night
        _dnc = new DayNightCycle();
        _dnc.OnNightStart += () => { _nightCleared = false; _nightStartDelay = 2f; };
        _dnc.OnDayStart   += () => { _nightCleared = false; };

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

        // Hunger / Thirst drain — faster at night
        float drainMult = _dnc.Phase == DayPhase.Night ? 1.5f : 1f;
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
        if (IsKeyPressed(KeyboardKey.Escape))
            _craftingOpen = false;

        // Craft on number keys when menu is open
        if (_craftingOpen)
        {
            for (int i = 0; i < Recipes.Length; i++)
                if (IsKeyPressed(KeyboardKey.One + i)) TryCraft(i);
            return; // block everything else while menu is open
        }

        // Left-click shoots / swings depending on weapon
        if (_player.IsGunSelected && IsMouseButtonPressed(MouseButton.Left)) Shoot();

        _meleeCooldown = Math.Max(0f, _meleeCooldown - dt);
        if (_player.IsMeleeSelected && IsMouseButtonPressed(MouseButton.Left) && _meleeCooldown <= 0f)
            MeleeAttack();

        _gunRecoil  = Math.Max(0f, _gunRecoil  - dt * 8f);
        _meleeSwing = Math.Max(0f, _meleeSwing - dt * 6f);
        UpdateBullets(dt);

        // Sync camera to player look
        _camera.Position = _player.EyePos;
        _camera.Target   = _player.EyePos + _player.Forward;

        if (_player.IsDead)
        {
            _gameOver = true;
            EnableCursor();
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
        int  dmg  = wid == 252 ? 150 : wid == 254 ? 80 : 35;   // iron sword : stone sword : wood club
        float cd  = wid == 252 ? 0.4f : wid == 254 ? 0.5f : 0.6f;
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
                z.TakeDamage(dmg);
                if (z.IsDead && !wasDead) { _player.Ammo += _rng.Next(1, 4); _killCount++; }
            }
        }
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

            // Hit zombie? Check body + head as separate spheres
            if (!dead)
            {
                foreach (var z in _waves.Active)
                {
                    if (z.IsDead) continue;
                    bool hitBody = Vector3.Distance(b.Pos, z.Position + new Vector3(0, 0.75f, 0)) < 0.55f;
                    bool hitHead = Vector3.Distance(b.Pos, z.Position + new Vector3(0, 1.85f, 0)) < 0.3f;
                    if (hitBody || hitHead)
                    {
                        bool wasDead = z.IsDead;
                        z.TakeDamage(GunDamage);
                        if (z.IsDead && !wasDead) { _player.Ammo += _rng.Next(1, 4); _killCount++; }
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
            if (_world.GetVoxel(cx, cy, cz) == 10)
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
        _world.SetVoxel(pos.X, pos.Y, pos.Z, 0);
        int food = _rng.Next(2, 6);
        int ammo = _rng.Next(5, 11);
        _player.Inventory.TryGetValue(11, out int curFood);
        _player.Inventory[11] = curFood + food;
        _player.Ammo += ammo;
    }

    void TryCraft(int index)
    {
        var r = Recipes[index];
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
        else if (r.OutputId >= 250)
        {
            // Weapon — put in first empty hotbar slot
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
        _nightCleared     = false;
        _clearBannerTimer = 0f;
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

        _world.Draw(_player.EyePos);
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

        // Campfire flames
        DrawCampfireFlames();

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
            else
            {
                // Pickaxe viewmodel
                Vector3 pickBase = _player.EyePos
                    + right * 0.24f - up * 0.22f + fwd * 0.42f;

                // Handle (wood)
                DrawCube(pickBase, 0.04f, 0.04f, 0.38f,
                    new Color((byte)101,(byte)67,(byte)33,(byte)255));
                // Head cross-bar (stone)
                DrawCube(pickBase + fwd * 0.2f + up * 0.03f, 0.22f, 0.055f, 0.055f,
                    new Color((byte)128,(byte)128,(byte)128,(byte)255));
                // Pick point (dark, angled down)
                DrawCube(pickBase + fwd * 0.26f - up * 0.05f, 0.04f, 0.12f, 0.04f,
                    new Color((byte)90,(byte)90,(byte)90,(byte)255));
            }
        }

        EndMode3D();

        DrawHUD();

        if (_craftingOpen) DrawCraftingUI();
        if (_gameOver)     DrawGameOver();

        EndDrawing();
    }

    void DrawHUD()
    {
        int sw = GetScreenWidth(), sh = GetScreenHeight();

        // Crosshair
        DrawLine(sw/2 - 10, sh/2, sw/2 + 10, sh/2, Color.White);
        DrawLine(sw/2, sh/2 - 10, sw/2, sh/2 + 10, Color.White);

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

        // Ammo
        Color ammoCol = _player.Ammo > 0 ? Color.White : Color.Red;
        DrawText($"AMMO: {_player.Ammo}", 220, sh - 28, 16, ammoCol);

        // Armor
        if (_player.ArmorTier > 0)
        {
            string armorLabel = _player.ArmorTier == 3 ? "ARMOR: Iron (-55%)"
                              : _player.ArmorTier == 2 ? "ARMOR: Stone (-35%)"
                              : "ARMOR: Wood (-15%)";
            Color armorCol = _player.ArmorTier == 3
                ? new Color((byte)190,(byte)195,(byte)210,(byte)255)
                : _player.ArmorTier == 2
                    ? new Color((byte)160,(byte)160,(byte)180,(byte)255)
                    : new Color((byte)180,(byte)140,(byte)80,(byte)255);
            DrawText(armorLabel, 330, sh - 28, 16, armorCol);
        }

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

        // Kill count always visible
        DrawText($"Kills: {_killCount}", sw - 150, 10, 18, Color.Orange);

        // Active zombie count during night
        if (_dnc.Phase == DayPhase.Night)
            DrawText($"Zombies: {_waves.Active.Count}", sw - 180, 32, 18, Color.Orange);

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
        bool ncr = FindNearbyCrate().HasValue;
        bool ncf = NearCampfire();
        string hint = ncr ? "E: Open Loot Crate"
                    : nc  ? "E: Open Crafting Table"
                    : ncf ? "CAMPFIRE: Hunger + Thirst restoring"
                    : "WASD/Arrows move  LClick shoot/mine/swing  RClick build  F eat food  Space jump";
        Color hintCol = (ncr || nc) ? Color.Yellow
                      : ncf         ? new Color((byte)255,(byte)150,(byte)50,(byte)255)
                      : Color.Gray;
        DrawText(hint, 10, sh - 18, 12, hintCol);

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
        int pw = 440, ph = 80 + Recipes.Length * 52;
        int px = sw/2 - pw/2, py = sh/2 - ph/2;

        DrawRectangle(px, py, pw, ph, new Color((byte)20,(byte)15,(byte)10,(byte)230));
        DrawRectangleLines(px, py, pw, ph, new Color((byte)160,(byte)100,(byte)40,(byte)255));
        DrawText("CRAFTING TABLE", px+pw/2 - MeasureText("CRAFTING TABLE",20)/2, py+12, 20,
            new Color((byte)220,(byte)160,(byte)60,(byte)255));
        DrawLine(px+10, py+38, px+pw-10, py+38, new Color((byte)100,(byte)70,(byte)30,(byte)255));

        for (int i = 0; i < Recipes.Length; i++)
        {
            var r   = Recipes[i];
            int ry  = py + 48 + i * 52;
            bool can = CanAfford(r);

            Color nameCol = can ? Color.White : Color.Gray;
            DrawText($"[{i+1}]  {r.Name}", px+16, ry, 18, nameCol);

            string costStr = CostString(r);
            Color costCol  = can
                ? new Color((byte)120,(byte)220,(byte)120,(byte)255)
                : new Color((byte)200,(byte)80,(byte)80,(byte)255);
            DrawText(costStr, px+32, ry+22, 14, costCol);
        }

        DrawText("ESC to close", px+pw/2 - MeasureText("ESC to close",13)/2, py+ph-22, 13, Color.DarkGray);
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
