using System;
using System.Collections.Generic;
using System.Numerics;

public class WaveSpawner
{
    public List<Zombie> Active = new();

    readonly VoxelWorld    _world;
    readonly DayNightCycle _dnc;
    readonly Random        _rng = new();

    const float SpawnRadius = 35f;
    int _baseCount = 8;

    public WaveSpawner(VoxelWorld world, DayNightCycle dnc)
    {
        _world = world;
        _dnc   = dnc;
        dnc.OnNightStart += SpawnWave;
        dnc.OnDayStart   += DespawnAll;
    }

    void SpawnWave()
    {
        int night = _dnc.NightCount;
        int total = _baseCount + (night - 1) * 4;
        int runners = night >= 2 ? total / 3 : 0;
        int shamblers = total - runners;

        for (int i = 0; i < shamblers; i++) SpawnOne(night, isRunner: false);
        for (int i = 0; i < runners;   i++) SpawnOne(night, isRunner: true);

        // Boss zombie every night from night 5 onward
        if (night >= 5) SpawnBoss(night);
    }

    void SpawnBoss(int night)
    {
        float angle = (float)(_rng.NextDouble() * Math.PI * 2);
        Vector3 pos = new(
            _world.SizeX / 2f + MathF.Cos(angle) * SpawnRadius,
            20f,
            _world.SizeZ / 2f + MathF.Sin(angle) * SpawnRadius);
        int ix = (int)pos.X, iz = (int)pos.Z;
        for (int y = _world.SizeY - 1; y >= 0; y--)
            if (_world.IsSolid(ix, y, iz)) { pos.Y = y + 1f; break; }
        Active.Add(new Zombie(_world, pos, night, isRunner: false, isBoss: true));
    }

    void SpawnOne(int night, bool isRunner)
    {
        float angle = (float)(_rng.NextDouble() * Math.PI * 2);
        Vector3 pos = new(
            _world.SizeX / 2f + MathF.Cos(angle) * SpawnRadius,
            20f,
            _world.SizeZ / 2f + MathF.Sin(angle) * SpawnRadius);

        // Drop to surface
        int ix = (int)pos.X, iz = (int)pos.Z;
        for (int y = _world.SizeY - 1; y >= 0; y--)
        {
            if (_world.IsSolid(ix, y, iz)) { pos.Y = y + 1f; break; }
        }

        Active.Add(new Zombie(_world, pos, night, isRunner));
    }

    void DespawnAll() => Active.Clear();

    public (int shamblers, int runners, bool hasBoss) GetWavePreview(int night)
    {
        int total     = _baseCount + (night - 1) * 4;
        int runners   = night >= 2 ? total / 3 : 0;
        int shamblers = total - runners;
        return (shamblers, runners, night >= 5);
    }

    public void Update(float dt, Player player)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            Active[i].Update(dt, player);
            if (Active[i].IsDead) Active.RemoveAt(i);
        }
    }

    public void Draw()
    {
        foreach (var z in Active) z.Draw();
    }
}
