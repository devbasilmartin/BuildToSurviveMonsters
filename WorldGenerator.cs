using System;

public static class WorldGenerator
{
    public static void Generate(VoxelWorld world, int seed = 42)
    {
        int sx = world.SizeX, sy = world.SizeY, sz = world.SizeZ;
        int groundBase  = 2;
        int hillAmp     = 4;
        float noiseScale = 0.06f;

        // Terrain: stone below, dirt on top
        for (int x = 0; x < sx; x++)
        for (int z = 0; z < sz; z++)
        {
            float n = Noise(x * noiseScale + seed, z * noiseScale + seed);
            int height = groundBase + (int)MathF.Round(n * hillAmp);
            height = Math.Clamp(height, 1, sy - 10);

            for (int y = 0; y < height; y++)
                world.SetVoxel(x, y, z, y < height - 1 ? (byte)2 : (byte)1);
        }

        var rng = new Random(seed);

        // ── Trees (40) ───────────────────────────────────────────────
        for (int i = 0; i < 40; i++)
        {
            int lx = rng.Next(3, sx - 3);
            int lz = rng.Next(3, sz - 3);
            int baseY = SurfaceY(world, lx, lz) + 1;
            int trunkH = 4 + rng.Next(0, 3); // 4-6 tall

            // Trunk
            for (int h = 0; h < trunkH; h++)
            {
                int y = baseY + h;
                if (y < sy) world.SetVoxel(lx, y, lz, 3); // wood
            }

            // Leaf canopy: flattened ellipsoid around top of trunk
            int trunkTop = baseY + trunkH - 1;
            for (int dy = 0; dy <= 4; dy++)
            for (int dx = -2; dx <= 2; dx++)
            for (int dz = -2; dz <= 2; dz++)
            {
                float r = dx*dx + (dy - 2)*(dy - 2) * 0.8f + dz*dz;
                if (r > 4.8f) continue;
                int lx2 = lx + dx, ly2 = trunkTop + dy, lz2 = lz + dz;
                if (world.InBounds(lx2, ly2, lz2) && world.GetVoxel(lx2, ly2, lz2) == 0)
                    world.SetVoxel(lx2, ly2, lz2, 6); // leaves
            }
        }

        // ── Stone rocks (4 = trees/10) ────────────────────────────────
        // Each formation: wide base of 3 + column of 2 on top = 5 blocks
        for (int i = 0; i < 4; i++)
        {
            int cx = rng.Next(2, sx - 2);
            int cz = rng.Next(2, sz - 2);
            int sy2 = SurfaceY(world, cx, cz) + 1;

            SetIfAir(world, cx,     sy2,     cz,     2); // center base
            SetIfAir(world, cx + 1, sy2,     cz,     2); // right
            SetIfAir(world, cx - 1, sy2,     cz,     2); // left
            SetIfAir(world, cx,     sy2 + 1, cz,     2); // middle
            SetIfAir(world, cx,     sy2 + 2, cz,     2); // top
        }

        // ── Iron ore (1 = trees/40, minimum 1) ───────────────────────
        // Embedded in terrain, partially visible
        for (int i = 0; i < 1; i++)
        {
            int cx = rng.Next(3, sx - 3);
            int cz = rng.Next(3, sz - 3);
            int surface = SurfaceY(world, cx, cz);

            // 2 blocks underground, 3 at surface level
            SetIfSolid(world, cx,     surface - 1, cz,     7);
            SetIfSolid(world, cx + 1, surface - 1, cz,     7);
            SetIfAir  (world, cx,     surface,     cz,     7);
            SetIfAir  (world, cx - 1, surface,     cz,     7);
            SetIfAir  (world, cx,     surface + 1, cz,     7);
        }
    }

    static void SetIfAir(VoxelWorld w, int x, int y, int z, byte id)
    {
        if (w.InBounds(x, y, z) && w.GetVoxel(x, y, z) == 0)
            w.SetVoxel(x, y, z, id);
    }

    static void SetIfSolid(VoxelWorld w, int x, int y, int z, byte id)
    {
        if (w.InBounds(x, y, z) && w.IsSolid(x, y, z))
            w.SetVoxel(x, y, z, id);
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
