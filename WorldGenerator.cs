using System;

public static class WorldGenerator
{
    public static void Generate(VoxelWorld world, int seed = 42)
    {
        int sx = world.SizeX, sy = world.SizeY, sz = world.SizeZ;
        float noiseScale = 0.06f;

        // Terrain: stone below, dirt on top
        for (int x = 0; x < sx; x++)
        for (int z = 0; z < sz; z++)
        {
            float n = Noise(x * noiseScale + seed, z * noiseScale + seed);
            int height = 2 + (int)MathF.Round(n * 4);
            height = Math.Clamp(height, 1, sy - 10);
            for (int y = 0; y < height; y++)
                world.SetVoxel(x, y, z, y < height - 1 ? (byte)2 : (byte)1);
        }

        var rng = new Random(seed);

        // ── Trees (70) — trunk + ellipsoid leaf canopy ────────────────
        for (int i = 0; i < 70; i++)
        {
            int tx = rng.Next(3, sx - 3);
            int tz = rng.Next(3, sz - 3);
            int baseY  = SurfaceY(world, tx, tz) + 1;
            int trunkH = 4 + rng.Next(0, 3);

            for (int h = 0; h < trunkH; h++)
                if (baseY + h < sy) world.SetVoxel(tx, baseY + h, tz, 3);

            int top = baseY + trunkH - 1;
            for (int dy = 0; dy <= 4; dy++)
            for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
            {
                float r = dx*dx + (dy - 2)*(dy - 2) * 0.8f + dz*dz;
                if (r > 4.8f) continue;
                int lx = tx+dx, ly = top+dy, lz = tz+dz;
                if (world.InBounds(lx,ly,lz) && world.GetVoxel(lx,ly,lz) == 0)
                    world.SetVoxel(lx, ly, lz, 6);
            }
        }

        // ── Stone boulders (7) — low mound, wide not tall ────────────
        // Shape: 2×2 footprint at surface + 1 cap block = 5 blocks total
        for (int i = 0; i < 7; i++)
        {
            int cx = rng.Next(3, sx - 3);
            int cz = rng.Next(3, sz - 3);
            int sy2 = SurfaceY(world, cx, cz) + 1;

            SetIfAir(world, cx,   sy2,   cz,   2);
            SetIfAir(world, cx+1, sy2,   cz,   2);
            SetIfAir(world, cx,   sy2,   cz+1, 2);
            SetIfAir(world, cx+1, sy2,   cz+1, 2);
            SetIfAir(world, cx,   sy2+1, cz,   2); // single cap — off-centre, looks natural
        }

        // ── Iron ore deposits (6) — horizontal vein mostly buried ──
        for (int i = 0; i < 6; i++)
        {
            int cx = rng.Next(4, sx - 4);
            int cz = rng.Next(4, sz - 4);
            int surface = SurfaceY(world, cx, cz);

            SetIfSolid(world, cx-1, surface-1, cz,   7);
            SetIfSolid(world, cx,   surface-1, cz,   7);
            SetIfSolid(world, cx+1, surface-1, cz,   7);
            SetIfAir  (world, cx,   surface,   cz,   7);
            SetIfAir  (world, cx+1, surface,   cz,   7);
        }

        // ── Ponds (4): water + sand shore ────────────────────────────
        for (int i = 0; i < 4; i++)
        {
            int pcx = rng.Next(10, sx - 10);
            int pcz = rng.Next(10, sz - 10);
            int pcy = SurfaceY(world, pcx, pcz);

            // Water in 5×5 centre
            for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
                if (world.InBounds(pcx+dx, pcy, pcz+dz))
                    world.SetVoxel(pcx+dx, pcy, pcz+dz, 19);

            // Sand shore ring (7×7 excluding centre)
            for (int dx = -3; dx <= 3; dx++)
            for (int dz = -3; dz <= 3; dz++)
            {
                if (Math.Abs(dx) <= 2 && Math.Abs(dz) <= 2) continue;
                int lx = pcx+dx, lz = pcz+dz;
                int ly = SurfaceY(world, lx, lz);
                if (world.InBounds(lx, ly, lz))
                    world.SetVoxel(lx, ly, lz, 18);
            }
        }

        // ── Crafting table near world centre ─────────────────────────
        int wcx = sx / 2 + 4, wcz = sz / 2 + 4;
        int tableY = SurfaceY(world, wcx, wcz) + 1;
        if (world.InBounds(wcx, tableY, wcz))
            world.SetVoxel(wcx, tableY, wcz, 9);

        // ── Loot crates: 5 food (10), 3 ammo (14), 3 supply (15) ────
        byte[] crateTypes = { 10, 10, 10, 10, 10, 14, 14, 14, 15, 15, 15 };
        foreach (byte ct in crateTypes)
        {
            int crx = rng.Next(4, sx - 4);
            int crz = rng.Next(4, sz - 4);
            int cry = SurfaceY(world, crx, crz) + 1;
            if (world.InBounds(crx, cry, crz))
                world.SetVoxel(crx, cry, crz, ct);
        }
    }

    static void SetIfAir(VoxelWorld w, int x, int y, int z, byte id)
    {
        if (w.InBounds(x,y,z) && w.GetVoxel(x,y,z) == 0) w.SetVoxel(x,y,z,id);
    }

    static void SetIfSolid(VoxelWorld w, int x, int y, int z, byte id)
    {
        if (w.InBounds(x,y,z) && w.IsSolid(x,y,z)) w.SetVoxel(x,y,z,id);
    }

    static int SurfaceY(VoxelWorld world, int x, int z)
    {
        for (int y = world.SizeY - 1; y >= 0; y--)
            if (world.IsSolid(x, y, z)) return y;
        return 0;
    }

    static float Noise(float x, float z)
    {
        float a = MathF.Sin(x * 1.3f + z * 0.9f) * 0.5f + 0.5f;
        float b = MathF.Sin(x * 0.7f - z * 1.1f) * 0.5f + 0.5f;
        float c = MathF.Sin(x * 2.1f + z * 1.7f) * 0.25f + 0.25f;
        return (a + b + c) / 1.5f - 0.5f;
    }
}
