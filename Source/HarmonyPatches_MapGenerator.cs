using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Verse;

namespace ImpassableMapMaker
{
    [StaticConstructorOnStartup]
    public partial class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("com.impassablemapmaker.rimworld.mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            /*Log.Message(
                "ImpassableMapMaker Harmony Patches:" + Environment.NewLine +
                "  Postfix:" + Environment.NewLine +
                "    GenStep_ElevationFertility.Generate" + Environment.NewLine +
                "    WorldPathGrid.CalculatedCostAt" + Environment.NewLine +
                "    TileFinder.IsValidTileForNewSettlement");*/
        }
    }

    internal interface ITerrainOverride
    {
        int HighZ { get; }
        int LowZ { get; }
        int HighX { get; }
        int LowX { get; }
        IntVec3 Center { get; }

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

        public int LowX => this.Center.x - this.Radius;
        public int HighX => this.Center.x + this.Radius;
        public int LowZ => this.Center.z - this.Radius;
        public int HighZ => this.Center.z + this.Radius;
        IntVec3 ITerrainOverride.Center => this.Center;
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

        public int LowX => this.Low.x;
        public int HighX => this.High.x;
        public int LowZ => this.Low.z;
        public int HighZ => this.High.z;
        public IntVec3 Center => new IntVec3((int)(this.HighZ - this.LowX * 0.5f) + this.LowX, 0, (int)(this.HighZ - this.LowZ * 0.5f) + this.LowZ);
    }

    [HarmonyPatch(typeof(GenStep_ElevationFertility), "Generate")]
    static class Patch_GenStep_Terrain
    {
        public static IntVec2? middleAreaCenter = null;
        private static HashSet<string> middleAreaCells = null;
        public static ITerrainOverride QuestArea = null;
        static void Postfix(Map map)
        {
            //Log.Error($"Roof Edge: {Settings.RoofEdgeDepth}");
            middleAreaCenter = null;
            if (map.TileInfo.hilliness == Hilliness.Impassable)
            {
                int radius = (int)(((float)map.Size.x + map.Size.z) * 0.25f) + Settings.OuterRadius;

                int middleWallSmoothness = Settings.MiddleWallSmoothness;
                Random r;
                if (Settings.TrueRandom)
                    r = new Random(Guid.NewGuid().GetHashCode());
                else
                    r = new Random((Find.World.info.name + map.Tile).GetHashCode());

                ITerrainOverride middleArea = null;
                if (Settings.HasMiddleArea)
                {
                    middleArea = GenerateMiddleArea(r, map);
                }

                QuestArea = null;
                if (Patch_MapGenerator_Generate.IsQuestMap)
                {
                    Log.Message("[Impassable Map Maker] map is for a quest. An open area will be created to support it.");
                    QuestArea = GenerateAreaForQuests(r, map);
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
                IntVec2 roofXMinMax = new IntVec2(Settings.RoofEdgeDepth, map.Size.x - Settings.RoofEdgeDepth);
                IntVec2 roofZMinMax = new IntVec2(Settings.RoofEdgeDepth, map.Size.z - Settings.RoofEdgeDepth);
                foreach (IntVec3 current in map.AllCells)
                {
                    float elev = 0;
                    if (IsMountain(current, map, radius))
                    {
                        elev = 3.40282347E+38f;
                        map.roofGrid.SetRoof(current, RoofDefOf.RoofRockThick);
                    }
                    else if (Settings.ScatteredRocks && IsScatteredRock(current, r, map, radius))
                    {
                        elev = 0.75f;
                    }
                    else
                    {
                        elev = 0.57f;
                        map.roofGrid.SetRoof(current, null);
                    }

                    if (QuestArea?.IsInside(current) == true)
                    {
                        elev = 0;
                        map.roofGrid.SetRoof(current, null);
                    }
                    else if (quaryArea?.IsInside(current) == true)
                    {
                        // Gravel
                        elev = 0.57f;
                        map.roofGrid.SetRoof(current, null);
                    }
                    else if (middleArea != null)
                    {
                        int i = (middleWallSmoothness == 0) ? 0 : r.Next(middleWallSmoothness);
                        if (middleArea.IsInside(current, i))
                        {
                            AddMiddleAreaCell(current);
                            elev = 0;
                            map.roofGrid.SetRoof(current, null);
                        }
                    }

                    /*if (current.x == 0 || current.x == map.Size.x - 1 ||
                        current.z == 0 || current.z == map.Size.z - 1)
                    {
                        map.fogGrid.Notify_FogBlockerRemoved(current);
                    }*/
                    elevation[current] = elev;

                    if (Settings.OuterShape == ImpassableShape.Fill && roofXMinMax.x > 0)
                    {
                        if (current.x <= roofXMinMax.x ||
                            current.x >= roofXMinMax.z ||
                            current.z <= roofZMinMax.x ||
                            current.z >= roofZMinMax.z)
                        {
                            elevation[current] = 0.75f;
                            map.roofGrid.SetRoof(current, null);
                        }
                    }
                }
            }
        }

        private static void AddMiddleAreaCell(IntVec3 c)
        {
            if (middleAreaCells == null)
                middleAreaCells = new HashSet<string>();
            middleAreaCells.Add($"{c.x},{c.z}");
        }

        public static void ClearMiddleAreaCells()
        {
            middleAreaCells?.Clear();
        }

        public static bool IsInMiddleArea(IntVec3 c)
        {
            return middleAreaCells?.Contains($"{c.x},{c.z}") == true;
        }

        private static ITerrainOverride GenerateMiddleArea(Random r, Map map)
        {
            int basePatchX = RandomBasePatch(r, map.Size.x);
            int basePatchZ = RandomBasePatch(r, map.Size.z);
            middleAreaCenter = new IntVec2(basePatchX, basePatchZ);

            if (Settings.OpenAreaShape == ImpassableShape.Square)
            {
                int halfXSize = Settings.OpenAreaSizeX / 2;
                int halfZSize = Settings.OpenAreaSizeZ / 2;
                return new TerrainOverrideSquare(basePatchX - halfXSize, basePatchZ - halfZSize, basePatchX + halfXSize, basePatchZ + halfZSize);
            }
            return new TerrainOverrideRound(new IntVec3(basePatchX, 0, basePatchZ), Settings.OpenAreaSizeX);
        }

        private static ITerrainOverride GenerateAreaForQuests(Random r, Map map)
        {
            int x = DetermineRandomPlacement(30, map.Size.x - 30, 0, r);
            int z = DetermineRandomPlacement(30, map.Size.z - 30, 0, r);

            return new TerrainOverrideRound(new IntVec3(x, 0, z), 20);
        }

        private static bool IsMountain(IntVec3 i, Map map, int radius)
        {
            // Fill
            if (Settings.OuterShape == ImpassableShape.Fill)
            {
                //if (!Settings.HasMiddleArea || !Settings.StartInMiddleArea)
                //{
                if (Settings.RoofEdgeDepth > 0 && IsWithinCornerOfMap(i, map.Size.x, map.Size.z))
                {
                    return false;
                }
                //}
                return true;
            }

            if (IsWithinCornerOfMap(i, map.Size.x, map.Size.z))
            {
                return false;
            }

            int buffer = Settings.PeremeterBuffer;
            if (Settings.OuterShape == ImpassableShape.Round)
            {
                // Round
                if (buffer != 0)
                {
                    if (i.x < buffer || i.x > map.Size.x - buffer - 1 ||
                        i.z < buffer || i.z > map.Size.z - buffer - 1)
                    {
                        return false;
                    }
                }

                int x = i.x - (int)(map.Size.x * 0.5f);
                int z = i.z - (int)(map.Size.z * 0.5f);
                return Math.Sqrt(Math.Pow(x, 2) + Math.Pow(z, 2)) < radius;
            }
            if (buffer == 0)
            {
                return true;
            }
            return
                i.x > buffer &&
                i.x < map.Size.x - (buffer + 1) &&
                i.z > buffer &&
                i.z < map.Size.z - (buffer + 1);
        }

        public static bool IsWithinCornerOfMap(IntVec3 i, int xMax, int zMax)
        {
            const int min = 8;
            return (i.x < min && i.z < min) ||
                (i.x < min && i.z > zMax - min) ||
                (i.x > xMax - min && i.z < min) ||
                (i.x > xMax - min && i.z > zMax - min);
        }

        private static bool IsScatteredRock(IntVec3 i, Random r, Map map, int radius)
        {
            if (r.Next(42) >= 5)
            {
                return false;
            }

            if (Settings.OuterShape == ImpassableShape.Round)
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

    [HarmonyPatch(typeof(GenStep_ScattererBestFit), "TryFindScatterCell")]
    static class Patch_GenStep_ScattererBestFit
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(GenStep_ScattererBestFit __instance, ref bool __result, Map map, ref IntVec3 result)
        {
            if (__instance.def.defName.Contains("Archonexus"))
            {
                ITerrainOverride o = Patch_GenStep_Terrain.QuestArea;
                result = new IntVec3(o.Center.x, o.Center.y, o.Center.z);
                Stack<Thing> s;
                for (int x = o.LowX; x <= o.HighX; ++x)
                {
                    for (int z = o.LowZ; z <= o.HighZ; ++z)
                    {
                        var i = new IntVec3(x, 0, z);
                        if (o.IsInside(i))
                        {
                            s = new Stack<Thing>(map.thingGrid.ThingsAt(i));
                            while (s.Count > 0)
                            {
                                var t = s.Pop();
                                if (!t.def.destroyable)
                                    t.DeSpawn();
                            }
                        }
                    }
                }
                __result = true;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MapGenerator), "GenerateMap")]
    public class Patch_MapGenerator_Generate
    {
        public static bool IsQuestMap = false;

        [HarmonyPriority(Priority.First)]
        static void Prefix(MapParent parent, MapGeneratorDef mapGenerator, IEnumerable<GenStepWithParams> extraGenStepDefs)
        {
            Patch_SettleInEmptyTileUtility_Settle.Prefix();

            Patch_GenStep_Terrain.ClearMiddleAreaCells();
            foreach (var q in Current.Game.questManager.QuestsListForReading)
            {
                if (q.State == QuestState.Ongoing)
                {
                    foreach (var qt in q.QuestLookTargets)
                    {
                        if (qt.Tile == parent.Tile)
                        {
                            IsQuestMap = true;
                            return;
                        }
                    }
                }
            }

            foreach (var d in mapGenerator.genSteps)
            {
                if (d.genStep.def.defName.Contains("Archonexus"))
                {
                    IsQuestMap = true;
                    return;
                }
            }

            if (extraGenStepDefs != null)
            {
                foreach (var d in extraGenStepDefs)
                {
                    if (d.def.defName.Contains("Archonexus"))
                    {
                        IsQuestMap = true;
                        return;
                    }
                }
            }
            IsQuestMap = false;
            return;
        }

        [HarmonyPriority(Priority.First)]
        static void Postfix(ref Map __result, MapGeneratorDef mapGenerator)
        {
            if (__result.TileInfo.hilliness == Hilliness.Impassable && Settings.OuterShape == ImpassableShape.Fill)
            {
                int maxX = __result.Size.x - 1;
                int maxZ = __result.Size.z - 1;
                foreach (IntVec3 current in __result.AllCells)
                {
                    if (Settings.CoverRoadAndRiver && __result.roofGrid.RoofAt(current) == null)
                    {
                        if (Patch_GenStep_Terrain.IsWithinCornerOfMap(current, maxX + 1, maxZ + 1))
                        {
                            __result.roofGrid.SetRoof(current, null);
                            continue;
                        }
                        else if (Patch_GenStep_Terrain.IsInMiddleArea(current))
                        {
                            __result.roofGrid.SetRoof(current, null);
                            continue;
                        }
                        __result.roofGrid.SetRoof(current, RoofDefOf.RoofRockThick);
                    }

                    if (Settings.RoofEdgeDepth > 0)
                    {
                        if (current.x == 0 ||
                            current.x == maxX ||
                            current.z == 0 ||
                            current.z == maxZ)
                        {
                            __result.roofGrid.SetRoof(current, null);
                        }
                    }
                    if (Settings.HasMiddleArea == false)
                    {
                        for (var x = Math.Max(0, MapGenerator.PlayerStartSpot.x - 5); x < Math.Min(__result.Size.x, MapGenerator.PlayerStartSpot.x + 5); ++x)
                            for (var z = Math.Max(0, MapGenerator.PlayerStartSpot.z - 5); z < Math.Min(__result.Size.z, MapGenerator.PlayerStartSpot.z + 5); ++z)
                            {
                                var i = new IntVec3(x, 0, z);
                                foreach (var t in __result.thingGrid.ThingsAt(i))
                                    if (t.def.passability == Traversability.Impassable)
                                        t.Destroy();
                                __result.roofGrid.SetRoof(i, null);
                            }
                    }
                    else
                    {
                        __result.roofGrid.SetRoof(MapGenerator.PlayerStartSpot, null);
                    }
                }
                /*
                var map = __result;
                Patch_GenStep_Terrain.ClearMiddleAreaCells();
                Task.Delay(new TimeSpan(0, 0, 3)).ContinueWith(t => {
                    try
                    {
                        FloodFillerFog.DebugRefogMap(map);
                    }
                    catch { }
                });*/
            }
        }

        [HarmonyFinalizer]
        static void Finally()
        {
            Patch_SettleInEmptyTileUtility_Settle.Finally();
        }
    }

    [HarmonyPatch(typeof(GenStep_FindPlayerStartSpot), "Generate")]
    static class Patch_GenStep_FindPlayerStartSpot_Generate
    {
        public static ITerrainOverride QuestArea = null;
        static void Postfix(Map map)
        {
            if (map.TileInfo.hilliness == Hilliness.Impassable &&
                Patch_GenStep_Terrain.middleAreaCenter != null &&
                Settings.HasMiddleArea &&
                Settings.StartInMiddleArea)
            {
                float centerX = Patch_GenStep_Terrain.middleAreaCenter.Value.x;
                float centerZ = Patch_GenStep_Terrain.middleAreaCenter.Value.x;
                float halfX = Settings.OpenAreaSizeX * 0.5f;
                float halfZ = Settings.OpenAreaSizeZ * 0.5f;
                float minX = centerX - halfX;
                float minZ = centerZ - halfZ;
                float maxX = centerX + halfX;
                float maxZ = centerZ + halfZ;

                ;
                if (CellFinderLoose.TryFindRandomNotEdgeCellWith(
                    (int)Math.Max(0, map.Size.x - centerX - halfX + 1), 
                    (IntVec3 i) => !i.Roofed(map) && i.x >= minX && i.x <= maxX && i.z >= minZ && i.z <= maxZ, 
                    map, 
                    out IntVec3 result))
                {
                    MapGenerator.PlayerStartSpot = result;
                }
                else
                {
                    Log.Error("Unable to start in the middle. Sorry!");
                }
            }
        }
    }

    [HarmonyPatch(typeof(SettleInEmptyTileUtility), "Settle")]
    static class Patch_SettleInEmptyTileUtility_Settle
    {
        public static ImpassableShape OutterShape = ImpassableShape.NotSet;

        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            OutterShape = Settings.OuterShape;
            if (OutterShape == ImpassableShape.Fill)
                Settings.OuterShape = (Rand.Bool) ? ImpassableShape.Round : ImpassableShape.Square;
        }

        public static void Finally()
        {
            if (OutterShape != ImpassableShape.NotSet)
                Settings.OuterShape = OutterShape;
            OutterShape = ImpassableShape.NotSet;
        }
    }

    [HarmonyPatch(typeof(CaravanEnterMapUtility), "Enter")]
    [HarmonyPatch(new Type[] { typeof(Caravan), typeof(Map), typeof(CaravanEnterMode), typeof(CaravanDropInventoryMode), typeof(bool), typeof(Predicate<IntVec3>)  })]
    static class Patch_CaravanEnterMapUtility_Enter
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix(Map map, ref CaravanEnterMode enterMode, bool draftColonists)
        {
            if (map.TileInfo.hilliness == Hilliness.Impassable)
            {
                if (draftColonists)
                    enterMode = CaravanEnterMode.Center;
                else
                    enterMode = CaravanEnterMode.Edge;
            }
        }
    }
}
