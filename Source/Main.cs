/*
 * MIT License
 * 
 * Copyright (c) [2017] [Travis Offtermatt]
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using Harmony;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Reflection;
using Verse;

namespace ImpassableMapMaker
{
    [StaticConstructorOnStartup]
    public class Main
    {
        static Main()
        {
            var harmony = HarmonyInstance.Create("com.impassablemapmaker.rimworld.mod");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message("ImpassableMapMaker: Adding Harmony Postfix to GenStep_ElevationFertility.Generate");
            Log.Message("ImpassableMapMaker: Adding Harmony Postfix to WorldPathGrid.CalculatedCostAt");
            Log.Message("ImpassableMapMaker: Adding Harmony Prefix to TileFinder.IsValidTileForNewSettlement");
        }

        [HarmonyPatch(typeof(GenStep_ElevationFertility), "Generate")]
        static class Patch_GenStep_Terrain
        {
            static void Postfix(Map map)
            {
                if (map.TileInfo.hilliness == Hilliness.Impassable)
                {
                    Random r = new Random((Find.World.info.name + map.Tile).GetHashCode());
                    int basePatchX = RandomBasePatch(r, map.Size.x);
                    int basePatchZ = RandomBasePatch(r, map.Size.z);
                    IntVec3 basePatchLow = new IntVec3(basePatchX - 27, 0, basePatchZ - 27);
                    IntVec3 basePatchHigh = new IntVec3(basePatchX + 27, 0, basePatchZ + 27);
                    /*
                    Log.Warning(
                        "size " + map.Size.x + 
                        " basePatchX " + basePatchX + " basePatchZ " + basePatchZ);
                    */
                    MapGenFloatGrid elevation = MapGenerator.FloatGridNamed("Elevation", map);
                    foreach (IntVec3 current in map.AllCells)
                    {
                        float f = 0;
                        if ((current.x > 6 &&
                            current.x < map.Size.x - 7 &&
                            current.z > 6 &&
                            current.z < map.Size.z - 7))
                        {
                            if (current.x == 0 || current.x == map.Size.x - 1 ||
                                current.z == 0 || current.z == map.Size.z - 1)
                            {
                                map.fogGrid.Notify_FogBlockerRemoved(current);
                            }
                            f = 3.40282347E+38f;
                        }
                        else if (r.Next(42) < 5)
                        {
                            f = 0.75f;
                        }
                        else
                        {
                            f = 0.6f;
                        }

                        int i = r.Next(10);
                        if (current.x > basePatchLow.x + i && current.x < basePatchHigh.x + i &&
                            current.z > basePatchLow.z + i && current.z < basePatchHigh.z + i)
                        {
                            f = 0;
                        }

                        elevation[current] = f;
                    }
                }
            }

            static int RandomBasePatch(Random r, int size)
            {
                int middle = size / 2;
                int halfMiddle = middle / 2;
                int delta = r.Next(halfMiddle);
                int sign = r.Next(2);
                if (sign == 0)
                {
                    Console.Write("- ");
                    delta *= -1;
                }
                return middle + delta;
            }
        }
        
        [HarmonyPatch(typeof(WorldPathGrid), "CalculatedCostAt")]
        static class Patch_CompLaunchable
        {
            static void Postfix(ref int __result)
            {
                if (__result == 1000000)
                    __result -= 1;
            }
        }
        
        [HarmonyPatch(typeof(TileFinder), "IsValidTileForNewSettlement")]
        static class Patch_TileFinder_IsValidTileForNewSettlement
        {
            static bool Prefix(ref bool __result, int tile, System.Text.StringBuilder reason)
            {
                if (tile >= 0)
                {
                    Tile t = Find.WorldGrid[tile];
                    if (t.hilliness == Hilliness.Impassable)
                    {
                        Settlement settlement = Find.WorldObjects.SettlementAt(tile);
                        if (settlement != null)
                        {
                            if (reason != null)
                            {
                                if (settlement.Faction == null)
                                {
                                    reason.Append("TileOccupied".Translate());
                                }
                                else if (settlement.Faction == Faction.OfPlayer)
                                {
                                    reason.Append("YourBaseAlreadyThere".Translate());
                                }
                                else
                                {
                                    reason.Append("BaseAlreadyThere".Translate(new object[]
                                    {
                                        settlement.Faction.Name
                                    }));
                                }
                            }
                            __result = false;
                        }
                        else if (Find.WorldObjects.AnySettlementAtOrAdjacent(tile))
                        {
                            if (reason != null)
                            {
                                reason.Append("FactionBaseAdjacent".Translate());
                            }
                            __result = false;
                        }
                        else // Can settle on the impassable terrain
                        {
                            __result = true;
                        }
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
