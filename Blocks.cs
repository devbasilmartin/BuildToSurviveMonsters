using Raylib_cs;

public record struct BlockDef(
    string Name,
    Color  Color,
    int    MaxHP,
    bool   IsBuildable,
    int    DropId,
    int    DropMin,
    int    DropMax
);

public static class Blocks
{
    public static readonly BlockDef[] All =
    {
        new("Air",           new Color(  0,   0,   0,   0),   0, false, 0, 0, 0),  // 0
        new("Dirt",          new Color(120,  72,   0, 255),   2, false, 1, 1, 2),  // 1
        new("Stone",         new Color(128, 128, 128, 255),   5, false, 2, 1, 1),  // 2
        new("Wood",          new Color(101,  67,  33, 255),   3, false, 3, 2, 3),  // 3
        new("PlankWall",     new Color(205, 133,  63, 255),   8, true,  3, 1, 1),  // 4  buildable
        new("StoneWall",     new Color(105, 105, 105, 255),  20, true,  2, 1, 1),  // 5  buildable
        new("Leaves",        new Color( 34, 139,  34, 255),   1, false, 0, 0, 0),  // 6
        new("IronOre",       new Color(180,  90,  20, 255),  15, false, 8, 1, 2),  // 7  drops iron
        new("Iron",          new Color(190, 175, 155, 255),   0, false, 0, 0, 0),  // 8  inventory only
        new("CraftingTable", new Color( 60,  35,  10, 255), 999, false, 0, 0, 0),  // 9  indestructible fixture
        new("Crate",         new Color(220, 160,  30, 255), 999, false, 0, 0, 0),  // 10 loot crate, open with E
        new("Food",          new Color(200,  80,  50, 255),   0, false, 0, 0, 0),  // 11 inventory only
        new("IronWall",      new Color(170, 175, 185, 255),  40, true,  8, 1, 2),  // 12 buildable, durable
        new("Campfire",      new Color(200,  80,  20, 255),   3, true,  3, 1, 2),  // 13 buildable, drops wood
        new("AmmoCrate",     new Color(200,  50,  50, 255), 999, false, 0, 0, 0),  // 14 ammo loot crate
        new("SupplyCrate",   new Color( 50, 180, 180, 255), 999, false, 0, 0, 0),  // 15 supply loot crate
        new("Torch",         new Color(255, 200,  40, 255),   1, true,  3, 1, 1),  // 16 cosmetic, non-solid
    };

    public static BlockDef Get(byte id) => id < All.Length ? All[id] : All[0];
    public static bool IsSolid(byte id) => id != 0 && id != 8 && id != 11 && id != 16;
}
