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

        // ── Trees (40) — trunk + ellipsoid leaf canopy ────────────────
        for (int i = 0; i < 40; i++)
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

        // ── Stone boulders (4 = trees/10) — low mound, wide not tall ─
        // Shape: 2×2 footprint at surface + 1 cap block = 5 blocks total
        for (int i = 0; i < 4; i++)
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

        // ── Iron ore (1 = trees/40) — horizontal vein mostly buried ──
        // Looks like orange crust poking through the ground, not a column
        for (int i = 0; i < 1; i++)
        {
            int cx = rng.Next(4, sx - 4);
            int cz = rng.Next(4, sz - 4);
            int surface = SurfaceY(world, cx, cz);

            // 3 blocks underground (replace stone only)
            SetIfSolid(world, cx-1, surface-1, cz,   7);
            SetIfSolid(world, cx,   surface-1, cz,   7);
            SetIfSolid(world, cx+1, surface-1, cz,   7);
            // 2 blocks at surface as visible crust
            SetIfAir  (world, cx,   surface,   cz,   7);
            SetIfAir  (world, cx+1, surface,   cz,   7);
        }

        // ── Crafting table near world centre ─────────────────────────
        int wcx = sx / 2 + 4, wcz = sz / 2 + 4;
        int tableY = SurfaceY(world, wcx, wcz) + 1;
        if (world.InBounds(wcx, tableY, wcz))
            world.SetVoxel(wcx, tableY, wcz, 9);
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
