using System;

// Fills VoxelWorld with Perlin-ish terrain at startup.
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
            height = Math.Clamp(height, 1, sy - 2);

            for (int y = 0; y < height; y++)
                world.SetVoxel(x, y, z, y < height - 1 ? (byte)2 : (byte)1); // stone / dirt
        }

        // Scatter wood columns (trees / resources)
        var rng = new Random(seed);
        for (int i = 0; i < 40; i++)
        {
            int lx = rng.Next(2, sx - 2);
            int lz = rng.Next(2, sz - 2);
            int baseY = SurfaceY(world, lx, lz) + 1;
            for (int h = 0; h < 4; h++)
            {
                int y = baseY + h;
                if (y < sy) world.SetVoxel(lx, y, lz, 3); // wood
            }
        }
    }

    static int SurfaceY(VoxelWorld world, int x, int z)
    {
        for (int y = world.SizeY - 1; y >= 0; y--)
            if (world.IsSolid(x, y, z)) return y;
        return 0;
    }

    // Simple smooth noise (no Unity dependency)
    static float Noise(float x, float z)
    {
        float a = MathF.Sin(x * 1.3f + z * 0.9f) * 0.5f + 0.5f;
        float b = MathF.Sin(x * 0.7f - z * 1.1f) * 0.5f + 0.5f;
        float c = MathF.Sin(x * 2.1f + z * 1.7f) * 0.25f + 0.25f;
        return (a + b + c) / 1.5f - 0.5f;   // roughly -0.5..0.5
    }
}
