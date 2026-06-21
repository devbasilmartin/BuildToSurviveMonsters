using Raylib_cs;

// All block data in one place — replaces Unity ScriptableObjects.
public record struct BlockDef(
    string Name,
    Color  Color,
    int    MaxHP,
    bool   IsBuildable,
    int    DropId,      // block id dropped when mined (0 = none)
    int    DropMin,
    int    DropMax
);

public static class Blocks
{
    // Index = block id. 0 is always Air.
    public static readonly BlockDef[] All =
    {
        new("Air",       new Color(0,   0,   0,   0),   0,  false, 0, 0, 0),  // 0
        new("Dirt",      new Color(120, 72,  0,   255), 2,  false, 1, 1, 2),  // 1  drops dirt
        new("Stone",     new Color(128, 128, 128, 255), 5,  false, 2, 1, 1),  // 2  drops stone
        new("Wood",      new Color(101, 67,  33,  255), 3,  false, 3, 2, 3),  // 3  drops wood
        new("PlankWall", new Color(205, 133, 63,  255), 8,  true,  3, 1, 1),  // 4  buildable
        new("StoneWall", new Color(105, 105, 105, 255), 20, true,  2, 1, 1),  // 5  buildable
    };

    public static BlockDef Get(byte id) =>
        id < All.Length ? All[id] : All[0];

    public static bool IsSolid(byte id) => id != 0;
}
