using Harmony;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Reflection;
using Verse;

namespace ImpassableMapMaker
{
    [StaticConstructorOnStartup]
    public partial class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = HarmonyInstance.Create("com.impassablemapmaker.rimworld.mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message(
                "ImpassableMapMaker Harmony Patches:" + Environment.NewLine +
                "  Postfix:" + Environment.NewLine +
                "    GenStep_ElevationFertility.Generate" + Environment.NewLine +
                "    WorldPathGrid.CalculatedCostAt" + Environment.NewLine +
                "    TileFinder.IsValidTileForNewSettlement");
        }
    }

    internal interface ITerrainOverride
    {
        int HighZ { get; }
        int LowZ { get; }
        int HighX { get; }
        int LowX { get; }

        bool IsInside(IntVec3 i, int rand = 0);
    }

    internal class TerrainOverrideRound : ITerrainOverride
    {
        public readonly IntVec3 Center;
        public readonly int Radius;

        public TerrainOverrideRound(IntVec3 center, int radius)
        {
            this.Center = center;
            this.Radius = radius;
        }

        public bool IsInside(IntVec3 i, int rand = 0)
        {
            if (i.x <= this.Center.x - Radius || i.x >= this.Center.x + Radius ||
                i.z <= this.Center.z - Radius || i.z >= this.Center.z + Radius)
            {
                return false;
            }

            int x = i.x - this.Center.x;
            int z = i.z - this.Center.z;
            return Math.Pow(x, 2) + Math.Pow(z, 2) < Math.Pow(this.Radius, 2);
        }

        public int LowX { get { return this.Center.x - this.Radius; } }
        public int HighX { get { return this.Center.x + this.Radius; } }
        public int LowZ { get { return this.Center.z - this.Radius; } }
        public int HighZ { get { return this.Center.z + this.Radius; } }
    }

    internal class TerrainOverrideSquare : ITerrainOverride
    {
        public readonly IntVec3 Low, High;
        public TerrainOverrideSquare(int lowX, int lowZ, int highX, int highZ)
        {
            this.Low = new IntVec3(lowX, 0, lowZ);
            this.High = new IntVec3(highX, 0, highZ);
        }

        public bool IsInside(IntVec3 i, int rand = 0)
        {
            return (
                i.x >= this.Low.x + rand && i.x <= this.High.x + rand &&
                i.z >= this.Low.z + rand && i.z <= this.High.z + rand);
        }

        public int LowX { get { return this.Low.x; } }
        public int HighX { get { return this.High.x; } }
        public int LowZ { get { return this.Low.z; } }
        public int HighZ { get { return this.High.z; } }
    }

    [HarmonyPatch(typeof(GenStep_ElevationFertility), "Generate")]
    static class Patch_GenStep_Terrain
    {
        static void Postfix(Map map)
        {
            if (map.TileInfo.hilliness == Hilliness.Impassable)
            {
                int radius = (int)(((float)map.Size.x + map.Size.z) * 0.25f);

                int middleWallSmoothness = Settings.MiddleWallSmoothness;
                Random r = new Random((Find.World.info.name + map.Tile).GetHashCode());

                ITerrainOverride middleArea = null;
                if (Settings.HasMiddleArea)
                {
                    middleArea = GenerateMiddleArea(r, map);
                }

                ITerrainOverride quaryArea = null;
                if (Settings.IncludeQuarySpot)
                {
                    quaryArea = DetermineQuary(r, map, middleArea);
                }
#if DEBUG
                    Log.Warning(
                        "size " + map.Size.x + 
                        " basePatchX " + basePatchX + " basePatchZ " + basePatchZ);
#endif
                MapGenFloatGrid fertility = MapGenerator.Fertility;
                MapGenFloatGrid elevation = MapGenerator.Elevation;
                foreach (IntVec3 current in map.AllCells)
                {
                    float elev = 0;
                    if (IsMountain(current, map, radius))
                    {
                        elev = 3.40282347E+38f;
                    }
                    else if (Settings.ScatteredRocks && IsScatteredRock(current, r, map, radius))
                    {
                        elev = 0.75f;
                    }
                    else
                    {
                        elev = 0.57f;
                    }

                    if (quaryArea != null && quaryArea.IsInside(current))
                    {
                        // Gravel
                        elev = 0.57f;
                    }
                    else if (middleArea != null)
                    {
                        int i = (middleWallSmoothness == 0) ? 0 : r.Next(middleWallSmoothness);
                        if (middleArea.IsInside(current, i))
                        {
                            elev = 0;
                        }
                    }

                    /*if (current.x == 0 || current.x == map.Size.x - 1 ||
                        current.z == 0 || current.z == map.Size.z - 1)
                    {
                        map.fogGrid.Notify_FogBlockerRemoved(current);
                    }*/
                    elevation[current] = elev;
                }
            }
        }

        private static ITerrainOverride GenerateMiddleArea(Random r, Map map)
        {
            int basePatchX = RandomBasePatch(r, map.Size.x);
            int basePatchZ = RandomBasePatch(r, map.Size.z);

            if (Settings.OpenAreaShape == ImpassableShape.Square)
            {
                int halfXSize = Settings.OpenAreaSizeX / 2;
                int halfZSize = Settings.OpenAreaSizeZ / 2;
                return new TerrainOverrideSquare(basePatchX - halfXSize, basePatchZ - halfZSize, basePatchX + halfXSize, basePatchZ + halfZSize);
            }
            return new TerrainOverrideRound(new IntVec3(basePatchX, 0, basePatchZ), Settings.OpenAreaSizeX);
        }

        private static bool IsMountain(IntVec3 i, Map map, int radius)
        {
            if (Settings.shape == ImpassableShape.Round)
            {
                // Round
                if (i.x <= 4 || i.x >= map.Size.x - 5 ||
                    i.z <= 4 || i.z >= map.Size.z - 5)
                {
                    return false;
                }

                int x = i.x - (int)(map.Size.x * 0.5f);
                int z = i.z - (int)(map.Size.z * 0.5f);
                return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(z, 2)) < radius;
            }
            // Square
            return
                i.x > Settings.PeremeterBuffer &&
                i.x < map.Size.x - (Settings.PeremeterBuffer + 1) &&
                i.z > Settings.PeremeterBuffer &&
                i.z < map.Size.z - (Settings.PeremeterBuffer + 1);
        }

        private static bool IsScatteredRock(IntVec3 i, Random r, Map map, int radius)
        {
            if (r.Next(42) >= 5)
            {
                return false;
            }

            if (Settings.shape == ImpassableShape.Round)
            {
                // Round
                int x = i.x - (int)(map.Size.x * 0.5f);
                int z = i.z - (int)(map.Size.z * 0.5f);
                return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(z, 2)) < radius + 8;
            }

            // Square
            return true;
        }

        static int RandomBasePatch(Random r, int size)
        {
            int half = size / 2;
            int delta = r.Next((int)(0.01 * Settings.PercentOffset * half));
            int sign = r.Next(2);
            if (sign == 0)
            {
                delta *= -1;
            }
            return half + delta;
        }

        static ITerrainOverride DetermineQuary(Random r, Map map, ITerrainOverride middleArea)
        {
            int quarterMapX = map.Size.x / 4;
            int quarterMapZ = map.Size.z / 4;
            int lowX, highX, lowZ, highZ;
            int quarySize = Settings.QuarySize;

            if (middleArea == null)
            {
                middleArea = new TerrainOverrideSquare(quarterMapX * 2, quarterMapZ * 2, quarterMapX * 2, quarterMapZ * 2);
            }

            if (r.Next(2) == 0)
            {
                highX = middleArea.LowX - 2;
                lowX = highX - quarterMapX;
                int x = DetermineRandomPlacement(lowX, highX, quarySize, r);
                lowX = x - quarySize;
                highX = x;
            }
            else
            {
                lowX = middleArea.HighX + 2;
                highX = lowX + quarterMapX;
                int x = DetermineRandomPlacement(lowX, highX, quarySize, r);
                lowX = x;
                highX = x + quarySize;
            }

            if (r.Next(2) == 0)
            {
                highZ = middleArea.LowZ - 2;
                lowZ = highZ - quarterMapZ;
                int z = DetermineRandomPlacement(lowZ, highZ, quarySize, r);
                lowZ = z - quarySize;
                highZ = z;
            }
            else
            {
                lowZ = middleArea.HighZ + 2;
                highZ = lowZ + quarterMapZ;
                int z = DetermineRandomPlacement(lowZ, highZ, quarySize, r);
                lowZ = z;
                highZ = z + quarySize;
            }

            return new TerrainOverrideSquare(lowX, lowZ, highX, highZ);
        }

        static int DetermineRandomPlacement(int low, int high, int size, Random r)
        {
            low += size;
            high -= size;
            return r.Next(high - low) + low;
        }
    }
}