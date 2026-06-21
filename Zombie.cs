using System;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

public class Zombie
{
    public Vector3 Position;
    public int     HP;
    public int     MaxHP;
    public bool    IsDead => HP <= 0;

    float _speed;
    float _damage;
    float _attackTimer;
    float _attackRate  = 1f;
    float _hitFlash    = 0f;  // 0..1, set to 1 on hit, decays

    readonly VoxelWorld _world;

    public Zombie(VoxelWorld world, Vector3 pos, int nightCount = 1, bool isRunner = false)
    {
        _world   = world;
        Position = pos;
        float scale = 1f + 0.1f * nightCount;
        HP = MaxHP = (int)((isRunner ? 30 : 60) * scale);
        _speed   = (isRunner ? 3.5f : 1.5f);
        _damage  = (isRunner ? 8 : 10) * scale;
    }

    public void Update(float dt, Player player)
    {
        if (IsDead) return;
        _hitFlash = Math.Max(0f, _hitFlash - dt * 6f);

        Vector3 toPlayer = player.Position - Position;
        float dist = toPlayer.Length();

        // Walk toward player (straight line — no pathfinding in first pass)
        if (dist > 1.5f)
        {
            Vector3 dir = Vector3.Normalize(new Vector3(toPlayer.X, 0, toPlayer.Z));
            Vector3 move = dir * _speed * dt;
            // Simple horizontal movement — no gravity for now (keep on surface)
            Position.X += move.X;
            Position.Z += move.Z;
            // Snap Y to terrain surface
            Position.Y = SurfaceY(Position.X, Position.Z);
        }
        else
        {
            // Attack
            _attackTimer += dt;
            if (_attackTimer >= 1f / _attackRate)
            {
                _attackTimer = 0f;
                player.TakeDamage((int)_damage);
            }
        }
    }

    public void TakeDamage(int amount) { HP = Math.Max(0, HP - amount); _hitFlash = 1f; }

    float SurfaceY(float x, float z)
    {
        int ix = (int)MathF.Floor(x), iz = (int)MathF.Floor(z);
        for (int y = _world.SizeY - 1; y >= 0; y--)
            if (_world.IsSolid(ix, y, iz)) return y + 1f;
        return 0f;
    }

    public void Draw()
    {
        if (IsDead) return;
        byte flash = (byte)(int)(_hitFlash * 200);
        var bodyColor = new Color((byte)(180 + flash), (byte)(30 + flash), (byte)(30 + flash), (byte)255);
        var headColor = new Color((byte)(200 + flash/2), (byte)(150 + flash), (byte)(100 + flash), (byte)255);
        DrawCube(Position + new Vector3(0, 0.9f, 0),  0.6f,  1.5f, 0.6f,  bodyColor);
        DrawCube(Position + new Vector3(0, 1.85f, 0), 0.45f, 0.45f, 0.45f, headColor);

        // HP bar above head
        float hpFrac = (float)HP / MaxHP;
        DrawCube(Position + new Vector3(0, 2.6f, 0), 0.6f, 0.08f, 0.06f, Color.DarkGray);
        DrawCube(Position + new Vector3(-0.3f + 0.3f * hpFrac, 2.6f, 0), 0.6f * hpFrac, 0.08f, 0.07f, Color.Green);
    }
}
