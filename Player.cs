using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// First-person player: movement, voxel collision, mining, building.
public class Player
{
    public Vector3 Position;
    public float   Yaw;      // degrees, horizontal look
    public float   Pitch;    // degrees, vertical look
    public int     HP    = 100;
    public int     MaxHP = 100;

    // Simple hotbar: slot → (blockId, count). 0 = empty. 255 = gun.
    public (byte blockId, int count)[] HotbarBlocks = new (byte, int)[9];
    public int SelectedSlot = 0;

    public bool IsGunSelected => HotbarBlocks[SelectedSlot].blockId == 255;

    // Resources collected: blockId → count
    public Dictionary<byte, int> Inventory = new();

    // Mining progress: voxel → remaining HP
    readonly Dictionary<Vector3Int, int> _mineHP = new();

    const float MoveSpeed    = 5f;
    const float Gravity      = -20f;
    const float JumpSpeed    = 7f;
    const float Sensitivity  = 0.12f;
    const float MineRange    = 5f;
    const float PlaceRange   = 5f;
    const float Width        = 0.6f;
    const float Height       = 1.8f;
    const float EyeHeight    = 1.6f;

    float _velY;
    bool  _grounded;

    readonly VoxelWorld _world;

    public Player(VoxelWorld world, Vector3 startPos)
    {
        _world   = world;
        Position = startPos;

        Inventory[2] = 10; // stone
        Inventory[3] = 10; // wood
        Ammo = 30;
        HotbarBlocks[0] = (255, 0); // slot 0 = gun
        HotbarBlocks[1] = (5, 0);   // slot 1 = stone wall
        HotbarBlocks[2] = (4, 0);   // slot 2 = plank wall
    }

    public Vector3 EyePos => Position + new Vector3(0, EyeHeight, 0);

    // Camera direction from yaw/pitch
    public Vector3 Forward
    {
        get
        {
            float yR = Yaw * MathF.PI / 180f;
            float pR = Pitch * MathF.PI / 180f;
            return new Vector3(
                MathF.Cos(pR) * MathF.Sin(yR),
                MathF.Sin(pR),
                MathF.Cos(pR) * MathF.Cos(yR));
        }
    }

    public void Update(float dt)
    {
        HandleLook();
        HandleMovement(dt);
        HandleMining(dt);
        HandleHotbar();
    }

    // ── Look ─────────────────────────────────────────────────────────────────

    void HandleLook()
    {
        Vector2 md = GetMouseDelta();
        Yaw   -= md.X * Sensitivity;
        Pitch += md.Y * Sensitivity;
        Pitch  = Math.Clamp(Pitch, -89f, 89f);
    }

    // ── Movement + voxel AABB collision ──────────────────────────────────────

    void HandleMovement(float dt)
    {
        float yR = Yaw * MathF.PI / 180f;
        Vector3 forward = new(MathF.Sin(yR), 0, MathF.Cos(yR));
        Vector3 right   = new(MathF.Cos(yR), 0, -MathF.Sin(yR));

        Vector3 move = Vector3.Zero;
        if (IsKeyDown(KeyboardKey.W) || IsKeyDown(KeyboardKey.Up))    move += forward;
        if (IsKeyDown(KeyboardKey.S) || IsKeyDown(KeyboardKey.Down))  move -= forward;
        if (IsKeyDown(KeyboardKey.A) || IsKeyDown(KeyboardKey.Left))  move += right;
        if (IsKeyDown(KeyboardKey.D) || IsKeyDown(KeyboardKey.Right)) move -= right;
        if (move.LengthSquared() > 0) move = Vector3.Normalize(move);

        // Horizontal
        Vector3 hDelta = move * MoveSpeed * dt;
        MoveAndCollide(ref hDelta, dt, horizontal: true);
        Position.X += hDelta.X;
        Position.Z += hDelta.Z;

        // Vertical
        if (_grounded && IsKeyPressed(KeyboardKey.Space))
            _velY = JumpSpeed;

        _velY += Gravity * dt;
        _grounded = false;
        var vDelta = new Vector3(0, _velY * dt, 0);
        MoveAndCollide(ref vDelta, dt, horizontal: false);
        Position.Y += vDelta.Y;
    }

    void MoveAndCollide(ref Vector3 delta, float dt, bool horizontal)
    {
        // Try to move; walk back out of any solid voxels we entered
        const float hw = Width / 2f;

        if (horizontal)
        {
            // test X and Z separately
            float nx = Position.X + delta.X;
            if (OverlapsVoxels(nx, Position.Y, Position.Z))
                delta.X = 0;

            float nz = Position.Z + delta.Z;
            if (OverlapsVoxels(Position.X + delta.X, Position.Y, nz))
                delta.Z = 0;
        }
        else
        {
            float ny = Position.Y + delta.Y;
            if (OverlapsVoxels(Position.X, ny, Position.Z))
            {
                if (delta.Y < 0) _grounded = true;
                delta.Y = 0;
                _velY   = 0;
            }
        }
    }

    // Returns true if the player AABB at (x,y,z) overlaps any solid voxel
    bool OverlapsVoxels(float px, float py, float pz)
    {
        const float hw = Width / 2f;
        int x0 = (int)MathF.Floor(px - hw),     x1 = (int)MathF.Floor(px + hw);
        int y0 = (int)MathF.Floor(py),           y1 = (int)MathF.Floor(py + Height - 0.01f);
        int z0 = (int)MathF.Floor(pz - hw),     z1 = (int)MathF.Floor(pz + hw);

        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        for (int z = z0; z <= z1; z++)
            if (_world.IsSolid(x, y, z)) return true;

        return false;
    }

    // ── Mining & Building ─────────────────────────────────────────────────────

    public Vector3Int? TargetVoxel;    // voxel under crosshair
    public Vector3Int? PlaceVoxel;     // adjacent voxel for building

    void HandleMining(float dt)
    {
        // Raycast every frame to find target voxel
        TargetVoxel = null;
        PlaceVoxel  = null;

        if (_world.Raycast(EyePos, Forward, MineRange, out var hit, out var face))
        {
            TargetVoxel = hit;
            PlaceVoxel  = hit + face;

            // Left-hold = mine (only when not holding a weapon)
            if (!IsWeaponSelected && IsMouseButtonDown(MouseButton.Left))
            {
                if (!_mineHP.TryGetValue(hit, out int hp))
                {
                    var def = Blocks.Get(_world.GetVoxel(hit.X, hit.Y, hit.Z));
                    hp = def.MaxHP + _world.GlobalBlockHPBonus;
                }
                // Each frame held = 1 "tick" (simple: 1 tick per frame, MaxHP ticks to break)
                hp--;
                if (hp <= 0)
                {
                    _mineHP.Remove(hit);
                    var def = Blocks.Get(_world.GetVoxel(hit.X, hit.Y, hit.Z));
                    _world.SetVoxel(hit.X, hit.Y, hit.Z, 0);
                    // Add drops to inventory
                    if (def.DropId != 0)
                    {
                        int amount = new Random().Next(def.DropMin, def.DropMax + 1);
                        Inventory.TryGetValue((byte)def.DropId, out int cur);
                        Inventory[(byte)def.DropId] = cur + amount;
                    }
                }
                else
                {
                    _mineHP[hit] = hp;
                }
            }
            else
            {
                _mineHP.Remove(hit); // released before breaking
            }

            // Right-click = place block
            if (IsMouseButtonPressed(MouseButton.Right) && PlaceVoxel.HasValue)
            {
                var pv = PlaceVoxel.Value;
                const float hw = Width / 2f;
                bool wouldTrap =
                    pv.X < Position.X + hw  && pv.X + 1 > Position.X - hw &&
                    pv.Y < Position.Y + Height && pv.Y + 1 > Position.Y &&
                    pv.Z < Position.Z + hw  && pv.Z + 1 > Position.Z - hw;
                if (!wouldTrap)
                {
                    var slot = HotbarBlocks[SelectedSlot];
                    if (slot.blockId != 0 && slot.blockId != 255)
                    {
                        var def = Blocks.Get(slot.blockId);
                        if (def.IsBuildable)
                        {
                            byte costId = slot.blockId == 4 ? (byte)3 : (byte)2;
                            Inventory.TryGetValue(costId, out int have);
                            if (have >= 2)
                            {
                                Inventory[costId] = have - 2;
                                _world.SetVoxel(pv.X, pv.Y, pv.Z, slot.blockId);
                            }
                        }
                    }
                }
            }
        }
        else
        {
            _mineHP.Clear(); // not looking at anything
        }
    }

    // ── Hotbar ────────────────────────────────────────────────────────────────

    void HandleHotbar()
    {
        float scroll = GetMouseWheelMove();
        if (scroll > 0) SelectedSlot = (SelectedSlot - 1 + 9) % 9;
        if (scroll < 0) SelectedSlot = (SelectedSlot + 1) % 9;

        for (int i = 0; i < 9; i++)
            if (IsKeyPressed(KeyboardKey.One + i))
                SelectedSlot = i;
    }

    public int   Ammo;
    public int   ArmorTier = 0; // 0=none  1=wood(-15%)  2=stone(-35%)
    public float Hunger    = 100f;
    public float Thirst    = 100f;

    public bool IsMeleeSelected => HotbarBlocks[SelectedSlot].blockId is 253 or 254;
    public bool IsWeaponSelected => IsGunSelected || IsMeleeSelected;

    public void TakeDamage(int amount)
    {
        float reduction = ArmorTier == 2 ? 0.35f : ArmorTier == 1 ? 0.15f : 0f;
        amount = Math.Max(1, (int)(amount * (1f - reduction)));
        HP = Math.Max(0, HP - amount);
    }

    public bool IsDead => HP <= 0;
}
