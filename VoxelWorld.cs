using System;
using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

// Owns voxel data. Rendering is via DrawCube — simple but fast enough for a small world.
public class VoxelWorld
{
    public const int ChunkSize = 16;

    public readonly int ChunksX, ChunksY, ChunksZ;
    public readonly int SizeX, SizeY, SizeZ;

    // Flat byte array: id = voxels[x + y*SizeX + z*SizeX*SizeY]
    readonly byte[] _voxels;

    public int GlobalBlockHPBonus = 0;

    public VoxelWorld(int chunksX = 8, int chunksY = 2, int chunksZ = 8)
    {
        ChunksX = chunksX; ChunksY = chunksY; ChunksZ = chunksZ;
        SizeX = chunksX * ChunkSize;
        SizeY = chunksY * ChunkSize;
        SizeZ = chunksZ * ChunkSize;
        _voxels = new byte[SizeX * SizeY * SizeZ];
    }

    // ── Data API ─────────────────────────────────────────────────────────────

    public byte GetVoxel(int x, int y, int z)
    {
        if (!InBounds(x, y, z)) return 0;
        return _voxels[Idx(x, y, z)];
    }

    public void SetVoxel(int x, int y, int z, byte id)
    {
        if (!InBounds(x, y, z)) return;
        _voxels[Idx(x, y, z)] = id;
    }

    public bool IsSolid(int x, int y, int z) => Blocks.IsSolid(GetVoxel(x, y, z));

    public bool IsWalkable(int x, int y, int z) =>
        !IsSolid(x, y, z) && IsSolid(x, y - 1, z);

    public bool InBounds(int x, int y, int z) =>
        x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;

    int Idx(int x, int y, int z) => x + y * SizeX + z * SizeX * SizeY;

    public static Vector3Int WorldToVoxel(Vector3 p) =>
        new((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y), (int)MathF.Floor(p.Z));

    public Vector3 VoxelCenter(int x, int y, int z) =>
        new(x + 0.5f, y + 0.5f, z + 0.5f);

    // ── Rendering ─────────────────────────────────────────────────────────────
    // DrawCube per solid voxel. Simple, correct, fast enough for a small world.

    public void Draw(Vector3 viewPos, float viewDist = 40f)
    {
        int x0 = Math.Max(0,       (int)(viewPos.X - viewDist));
        int x1 = Math.Min(SizeX-1, (int)(viewPos.X + viewDist));
        int y0 = Math.Max(0,       (int)(viewPos.Y - viewDist));
        int y1 = Math.Min(SizeY-1, (int)(viewPos.Y + viewDist));
        int z0 = Math.Max(0,       (int)(viewPos.Z - viewDist));
        int z1 = Math.Min(SizeZ-1, (int)(viewPos.Z + viewDist));

        for (int z = z0; z <= z1; z++)
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            byte id = GetVoxel(x, y, z);
            if (id == 0) continue;

            // Skip faces that are fully buried (tiny perf win)
            bool exposed =
                !IsSolid(x+1,y,z) || !IsSolid(x-1,y,z) ||
                !IsSolid(x,y+1,z) || !IsSolid(x,y-1,z) ||
                !IsSolid(x,y,z+1) || !IsSolid(x,y,z-1);
            if (!exposed) continue;

            var def = Blocks.Get(id);
            DrawCube(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), 1f, 1f, 1f, def.Color);
            DrawCubeWires(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f), 1.001f, 1.001f, 1.001f,
                new Color(0, 0, 0, 40));
        }
    }

    // ── DDA Voxel Raycast (Amanatides & Woo) ─────────────────────────────────

    public bool Raycast(Vector3 origin, Vector3 dir, float maxDist,
        out Vector3Int hitVoxel, out Vector3Int faceNormal)
    {
        hitVoxel   = Vector3Int.Zero;
        faceNormal = Vector3Int.Zero;

        dir = Vector3.Normalize(dir);
        var cur = WorldToVoxel(origin);

        int sx = dir.X >= 0 ? 1 : -1;
        int sy = dir.Y >= 0 ? 1 : -1;
        int sz = dir.Z >= 0 ? 1 : -1;

        float tMaxX = dir.X != 0 ? (sx > 0 ? MathF.Ceiling(origin.X) - origin.X : origin.X - MathF.Floor(origin.X)) / MathF.Abs(dir.X) : float.MaxValue;
        float tMaxY = dir.Y != 0 ? (sy > 0 ? MathF.Ceiling(origin.Y) - origin.Y : origin.Y - MathF.Floor(origin.Y)) / MathF.Abs(dir.Y) : float.MaxValue;
        float tMaxZ = dir.Z != 0 ? (sz > 0 ? MathF.Ceiling(origin.Z) - origin.Z : origin.Z - MathF.Floor(origin.Z)) / MathF.Abs(dir.Z) : float.MaxValue;

        float tdx = dir.X != 0 ? MathF.Abs(1f / dir.X) : float.MaxValue;
        float tdy = dir.Y != 0 ? MathF.Abs(1f / dir.Y) : float.MaxValue;
        float tdz = dir.Z != 0 ? MathF.Abs(1f / dir.Z) : float.MaxValue;

        for (int i = 0; i < 200; i++)
        {
            if (tMaxX < tMaxY && tMaxX < tMaxZ)
            {
                if (tMaxX > maxDist) break;
                cur.X += sx; tMaxX += tdx; faceNormal = new(-sx, 0, 0);
            }
            else if (tMaxY < tMaxZ)
            {
                if (tMaxY > maxDist) break;
                cur.Y += sy; tMaxY += tdy; faceNormal = new(0, -sy, 0);
            }
            else
            {
                if (tMaxZ > maxDist) break;
                cur.Z += sz; tMaxZ += tdz; faceNormal = new(0, 0, -sz);
            }

            if (IsSolid(cur.X, cur.Y, cur.Z))
            {
                hitVoxel = cur;
                return true;
            }
        }
        return false;
    }
}

// Simple integer 3-vector (replaces System.Numerics gap)
public record struct Vector3Int(int X, int Y, int Z)
{
    public static readonly Vector3Int Zero = new(0, 0, 0);
    public Vector3 ToV3() => new(X, Y, Z);
    public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new(a.X+b.X, a.Y+b.Y, a.Z+b.Z);
}
