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
    bool  _isRunner;

    public bool IsBoss     { get; private set; }
    public bool IsArmoured { get; private set; }
    public bool IsCrawler  { get; private set; }
    public bool IsShaman   { get; private set; }
    public bool IsPoison   { get; private set; }
    public bool IsGigant   { get; private set; }
    public bool IsGhost    { get; private set; }
    public int  XPReward   { get; private set; }
    public float SpeedMult = 1f;
    readonly VoxelWorld _world;

    public Zombie(VoxelWorld world, Vector3 pos, int nightCount = 1, bool isRunner = false, bool isBoss = false, bool isArmoured = false, bool isCrawler = false, bool isShaman = false, bool isPoison = false, bool isGigant = false, bool isGhost = false)
    {
        _world     = world;
        Position   = pos;
        _isRunner  = isRunner;
        IsBoss     = isBoss;
        IsArmoured = isArmoured;
        IsCrawler  = isCrawler;
        IsShaman   = isShaman;
        IsPoison   = isPoison;
        IsGigant   = isGigant;
        IsGhost    = isGhost;

        if (isBoss)
        {
            HP = MaxHP = 600 + nightCount * 40;
            _speed    = 1.4f + 0.05f * (nightCount - 5);
            _damage   = 20f;
            XPReward  = 100;
        }
        else if (isGhost)
        {
            HP = MaxHP = 50;
            _speed      = 3.0f * (1f + 0.05f * (nightCount - 1));
            _damage     = 8f;
            _attackRate = 1.2f;
            XPReward    = 15;
        }
        else if (isGigant)
        {
            HP = MaxHP = 2000 + nightCount * 100;
            _speed      = 0.5f * (1f + 0.04f * (nightCount - 1));
            _damage     = 40f;
            _attackRate = 0.8f; // slow swing
            XPReward    = 300;
        }
        else if (isPoison)
        {
            float scale = 1f + 0.1f * nightCount;
            HP = MaxHP = (int)(40 * scale);
            _speed      = 1.8f * (1f + 0.07f * (nightCount - 1));
            _damage     = 3f;
            _attackRate = 0.5f; // attacks every 2s
            XPReward    = 12;
        }
        else if (isShaman)
        {
            HP = MaxHP = 120;
            _speed    = 0.8f * (1f + 0.05f * (nightCount - 1));
            _damage   = 0f;   // shamans don't attack directly
            XPReward  = 30;
        }
        else if (isCrawler)
        {
            float speedScale = 1f + 0.07f * (nightCount - 1);
            HP = MaxHP = 25;
            _speed    = 5.0f * speedScale;
            _damage   = 6f;
            XPReward  = 8;
        }
        else
        {
            float scale = 1f + 0.1f * nightCount;
            float speedScale = 1f + 0.07f * (nightCount - 1);
            HP = MaxHP = (int)((isRunner ? 30 : 60) * scale);
            _speed    = (isRunner ? 3.8f : 1.5f) * speedScale;
            _damage   = (isRunner ? 8 : 10) * scale;
            XPReward  = isRunner ? 15 : isArmoured ? 20 : 10;
        }
    }

    public void Update(float dt, Player player)
    {
        if (IsDead) return;
        _hitFlash = Math.Max(0f, _hitFlash - dt * 6f);

        Vector3 toPlayer = player.Position - Position;
        float dist = toPlayer.Length();

        // Walk toward player — slide along walls instead of clipping through
        if (dist > 1.5f)
        {
            Vector3 dir = Vector3.Normalize(new Vector3(toPlayer.X, 0, toPlayer.Z));
            float step = _speed * SpeedMult * dt;

            // X axis: try direct, else try perpendicular slides
            float nx = Position.X + dir.X * step;
            if (!BlockedAt(nx, Position.Z))
                Position.X = nx;
            else
            {
                float slide = dir.Z * step * 0.6f;
                if (!BlockedAt(Position.X + slide, Position.Z))       Position.X += slide;
                else if (!BlockedAt(Position.X - slide, Position.Z))  Position.X -= slide;
            }

            // Z axis: try direct, else try perpendicular slides
            float nz = Position.Z + dir.Z * step;
            if (!BlockedAt(Position.X, nz))
                Position.Z = nz;
            else
            {
                float slide = dir.X * step * 0.6f;
                if (!BlockedAt(Position.X, Position.Z + slide))       Position.Z += slide;
                else if (!BlockedAt(Position.X, Position.Z - slide))  Position.Z -= slide;
            }

            Position.Y = SurfaceY(Position.X, Position.Z) + (IsGhost ? 0.5f : 0f);
        }
        else
        {
            // Attack (skip if player is invincible after respawn)
            _attackTimer += dt;
            if (_attackTimer >= 1f / _attackRate)
            {
                _attackTimer = 0f;
                if (!player.Invincible)
                {
                    player.TakeDamage((int)_damage);
                    if (IsPoison)
                        player.PoisonTimer = Math.Max(player.PoisonTimer, 10f);
                }
            }
        }
    }

    public void TakeDamage(int amount) { HP = Math.Max(0, HP - amount); _hitFlash = 1f; }

    bool BlockedAt(float x, float z)
    {
        if (IsGhost) return false; // ghosts phase through all blocks
        float r = IsBoss ? 0.6f : 0.35f;
        int y = (int)MathF.Floor(Position.Y);
        for (int dy = 0; dy <= 1; dy++)
        {
            if (_world.IsSolid((int)MathF.Floor(x - r), y + dy, (int)MathF.Floor(z)) ||
                _world.IsSolid((int)MathF.Floor(x + r), y + dy, (int)MathF.Floor(z)) ||
                _world.IsSolid((int)MathF.Floor(x), y + dy, (int)MathF.Floor(z - r)) ||
                _world.IsSolid((int)MathF.Floor(x), y + dy, (int)MathF.Floor(z + r)))
                return true;
        }
        return false;
    }

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

        if (IsGhost)
        {
            float t  = (float)GetTime();
            float fl = 0.7f + 0.3f * MathF.Sin(t * 4f + Position.X);
            byte  ga = (byte)(int)(fl * 170);
            var   gc = new Color((byte)220,(byte)230,(byte)255,ga);
            DrawCube(Position + new Vector3(0, 1.2f, 0), 0.5f, 1.4f, 0.5f, gc);
            DrawCube(Position + new Vector3(0, 2.1f, 0), 0.4f, 0.4f, 0.4f, gc);
            float gf2 = (float)HP / MaxHP;
            DrawCube(Position + new Vector3(0, 2.9f, 0), 0.5f, 0.07f, 0.06f, Color.DarkGray);
            DrawCube(Position + new Vector3(-0.25f+0.25f*gf2, 2.9f, 0), 0.5f*gf2, 0.07f, 0.07f, Color.LightGray);
        }
        else if (IsGigant)
        {
            var gc = new Color((byte)Math.Min(255,25+flash),(byte)Math.Min(255,5+flash),(byte)Math.Min(255,5+flash),(byte)255);
            DrawCube(Position + new Vector3(0, 3.0f, 0), 3.0f, 6.0f, 3.0f, gc);  // massive body
            DrawCube(Position + new Vector3(0, 6.9f, 0), 2.2f, 2.2f, 2.2f, gc);  // head
            float gf = (float)HP / MaxHP;
            DrawCube(Position + new Vector3(0, 9.8f, 0), 3.0f, 0.18f, 0.16f, Color.DarkGray);
            DrawCube(Position + new Vector3(-1.5f+1.5f*gf, 9.8f, 0), 3.0f*gf, 0.18f, 0.18f, Color.Red);
        }
        else if (IsPoison)
        {
            var pc = new Color((byte)Math.Min(255, 40+flash), (byte)Math.Min(255, 200+flash), (byte)Math.Min(255, 60+flash), (byte)255);
            DrawCube(Position + new Vector3(0, 0.9f, 0),  0.6f, 1.5f, 0.6f, pc);
            DrawCube(Position + new Vector3(0, 1.85f, 0), 0.45f, 0.45f, 0.45f,
                new Color((byte)Math.Min(255, 30+flash),(byte)Math.Min(255, 180+flash),(byte)Math.Min(255, 50+flash),(byte)255));
            float pf = (float)HP / MaxHP;
            DrawCube(Position + new Vector3(0, 2.6f, 0), 0.6f, 0.08f, 0.06f, Color.DarkGray);
            DrawCube(Position + new Vector3(-0.3f+0.3f*pf, 2.6f, 0), 0.6f*pf, 0.08f, 0.07f, Color.Green);
        }
        else if (IsShaman)
        {
            var sc = new Color((byte)Math.Min(255,140+flash),(byte)Math.Min(255,20+flash),(byte)Math.Min(255,200+flash),(byte)255);
            DrawCube(Position + new Vector3(0, 1.2f, 0), 0.38f, 2.0f, 0.38f, sc);   // tall body
            DrawCube(Position + new Vector3(0, 2.4f, 0), 0.32f, 0.32f, 0.32f, sc);  // head
            DrawCube(Position + new Vector3(0, 2.75f, 0), 0.28f, 0.38f, 0.28f,
                new Color((byte)60,(byte)5,(byte)100,(byte)255));                      // pointed hat
            // Pulsing healing orb
            float t = (float)GetTime();
            byte ga = (byte)(int)(Math.Abs(MathF.Sin(t * 2.5f)) * 160 + 60);
            DrawSphere(Position + new Vector3(0, 3.3f, 0), 0.12f,
                new Color((byte)50,(byte)220,(byte)80,ga));
            // HP bar
            float sf = (float)HP / MaxHP;
            DrawCube(Position + new Vector3(0, 3.7f, 0), 0.5f, 0.08f, 0.06f, Color.DarkGray);
            DrawCube(Position + new Vector3(-0.25f+0.25f*sf, 3.7f, 0), 0.5f*sf, 0.08f, 0.07f, Color.Green);
        }
        else if (IsCrawler)
        {
            var cCol = new Color((byte)Math.Min(255, 90+flash), (byte)Math.Min(255, 45+flash), (byte)Math.Min(255, 10+flash), (byte)255);
            DrawCube(Position + new Vector3(0, 0.2f, 0), 0.5f, 0.38f, 0.5f, cCol);  // flat body
            DrawCube(Position + new Vector3(0, 0.57f, 0), 0.28f, 0.28f, 0.28f, cCol); // small head
            float cf2 = (float)HP / MaxHP;
            DrawCube(Position + new Vector3(0, 1.0f, 0), 0.5f, 0.06f, 0.05f, Color.DarkGray);
            DrawCube(Position + new Vector3(-0.25f + 0.25f * cf2, 1.0f, 0), 0.5f * cf2, 0.06f, 0.06f, Color.Green);
        }
        else if (IsBoss)
        {
            // Boss: dark purple, 2x scale
            var bBody = new Color((byte)Math.Min(255, 80  + flash), (byte)Math.Min(255, 10 + flash), (byte)Math.Min(255, 100 + flash), (byte)255);
            var bHead = new Color((byte)Math.Min(255, 100 + flash), (byte)Math.Min(255, 20 + flash), (byte)Math.Min(255, 120 + flash), (byte)255);
            DrawCube(Position + new Vector3(0, 1.5f, 0),  1.2f, 3.0f, 1.2f, bBody);
            DrawCube(Position + new Vector3(0, 3.5f, 0),  0.9f, 0.9f, 0.9f, bHead);
            float hpF = (float)HP / MaxHP;
            DrawCube(Position + new Vector3(0, 4.6f, 0),  1.2f, 0.12f, 0.1f, Color.DarkGray);
            DrawCube(Position + new Vector3(-0.6f + 0.6f * hpF, 4.6f, 0), 1.2f * hpF, 0.12f, 0.11f, Color.Red);
        }
        else
        {
            // Armoured=silver, Runners=olive-green, Shamblers=dark red
            Color bodyColor, headColor;
            if (IsArmoured)
            {
                bodyColor = new Color((byte)Math.Min(255, 150 + flash), (byte)Math.Min(255, 155 + flash), (byte)Math.Min(255, 165 + flash), (byte)255);
                headColor = new Color((byte)Math.Min(255, 130 + flash), (byte)Math.Min(255, 135 + flash), (byte)Math.Min(255, 145 + flash), (byte)255);
            }
            else if (_isRunner)
            {
                bodyColor = new Color((byte)Math.Min(255, 80  + flash), (byte)Math.Min(255, 120 + flash), (byte)Math.Min(255, 30  + flash), (byte)255);
                headColor = new Color((byte)Math.Min(255, 100 + flash), (byte)Math.Min(255, 140 + flash), (byte)Math.Min(255, 60  + flash), (byte)255);
            }
            else
            {
                bodyColor = new Color((byte)Math.Min(255, 180 + flash), (byte)Math.Min(255, 30  + flash), (byte)Math.Min(255, 30  + flash), (byte)255);
                headColor = new Color((byte)Math.Min(255, 200 + flash/2), (byte)Math.Min(255, 150 + flash), (byte)Math.Min(255, 100 + flash), (byte)255);
            }
            DrawCube(Position + new Vector3(0, 0.9f, 0),  0.6f,  1.5f, 0.6f,  bodyColor);
            DrawCube(Position + new Vector3(0, 1.85f, 0), 0.45f, 0.45f, 0.45f, headColor);
            float hpFrac = (float)HP / MaxHP;
            DrawCube(Position + new Vector3(0, 2.6f, 0), 0.6f, 0.08f, 0.06f, Color.DarkGray);
            DrawCube(Position + new Vector3(-0.3f + 0.3f * hpFrac, 2.6f, 0), 0.6f * hpFrac, 0.08f, 0.07f, Color.Green);
        }
    }
}
