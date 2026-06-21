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

    bool  _gameOver = false;
    float _gunRecoil = 0f;

    struct Bullet { public Vector3 Pos; public Vector3 Dir; public float Life; }
    readonly System.Collections.Generic.List<Bullet> _bullets = new();

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
        _dnc.OnNightStart += () => { /* could flash screen */ };

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

        // Left-click shoots when gun is selected, mines otherwise (handled in Player)
        if (_player.IsGunSelected && IsMouseButtonPressed(MouseButton.Left)) Shoot();

        _gunRecoil = Math.Max(0f, _gunRecoil - dt * 8f);
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

            // Hit zombie?
            if (!dead)
            {
                foreach (var z in _waves.Active)
                {
                    if (z.IsDead) continue;
                    if (Vector3.Distance(b.Pos, z.Position + new Vector3(0, 0.9f, 0)) < 0.9f)
                    {
                        z.TakeDamage(GunDamage);
                        dead = true;
                        break;
                    }
                }
            }

            if (dead) _bullets.RemoveAt(i);
            else       _bullets[i] = b;
        }
    }

    void Restart()
    {
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

        // Viewmodel
        {
            Vector3 fwd   = _player.Forward;
            Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, fwd));
            Vector3 up    = Vector3.Cross(fwd, right);

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

        if (_gameOver) DrawGameOver();

        EndDrawing();
    }

    void DrawHUD()
    {
        int sw = GetScreenWidth(), sh = GetScreenHeight();

        // Crosshair
        DrawLine(sw/2 - 10, sh/2, sw/2 + 10, sh/2, Color.White);
        DrawLine(sw/2, sh/2 - 10, sw/2, sh/2 + 10, Color.White);

        // Health bar
        DrawRectangle(10, sh - 30, 200, 20, Color.DarkGray);
        DrawRectangle(10, sh - 30, (int)(200f * _player.HP / _player.MaxHP), 20, Color.Red);
        DrawText($"HP: {_player.HP}/{_player.MaxHP}", 15, sh - 28, 16, Color.White);

        // Ammo
        Color ammoCol = _player.Ammo > 0 ? Color.White : Color.Red;
        DrawText($"AMMO: {_player.Ammo}", 220, sh - 28, 16, ammoCol);

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

        // Wave count
        if (_dnc.Phase == DayPhase.Night)
            DrawText($"Zombies: {_waves.Active.Count}", sw - 160, 10, 18, Color.Orange);

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
            else if (slot.blockId != 0)
            {
                var col = Blocks.Get(slot.blockId).Color;
                DrawRectangle(bx+4, hotbarY+4, 32, 32, col);
                string name = Blocks.Get(slot.blockId).Name;
                DrawText(name[..Math.Min(4,name.Length)], bx+2, hotbarY+26, 10, Color.White);
            }
        }

        // Controls reminder
        DrawText("WASD/Arrows move  Mouse look  1=Gun: LClick shoot  2-9=Blocks: LClick mine  RClick build  F fast-fwd", 10, sh - 18, 12, Color.Gray);
    }

    void DrawGameOver()
    {
        int sw = GetScreenWidth(), sh = GetScreenHeight();
        DrawRectangle(0, 0, sw, sh, new Color((byte)0,(byte)0,(byte)0,(byte)150));
        string msg = "YOU DIED";
        DrawText(msg, sw/2 - MeasureText(msg, 60)/2, sh/2 - 50, 60, Color.Red);
        string sub = $"Survived {_dnc.NightCount} night(s)    Press R to restart";
        DrawText(sub, sw/2 - MeasureText(sub, 24)/2, sh/2 + 30, 24, Color.White);
    }

    static string TimeStr(float secs)
    {
        int m = (int)secs / 60, s = (int)secs % 60;
        return $"{m}:{s:D2}";
    }
}
