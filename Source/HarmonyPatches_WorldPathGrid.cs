using Harmony;
using RimWorld;
using RimWorld.Planet;
using System.Text;
using Verse;

namespace ImpassableMapMaker
{
    partial class HarmonyPatches
    {
        [HarmonyPatch(typeof(WorldPathGrid), "HillinessMovementDifficultyOffset")]
        static class Patch_WorldPathGrid_HillinessMovementDifficultyOffset
        {
            static void Postfix(ref float __result, Hilliness hilliness)
            {
                if (hilliness == Hilliness.Impassable)
                {
                    __result = Settings.MovementDifficulty;
                }
            }
        }

        [HarmonyPatch(typeof(WorldPathGrid), "CalculatedMovementDifficultyAt")]
        static class Patch_WorldPathGrid_CalculatedMovementDifficultyAt
        {
            static bool Prefix(ref float __result, int tile, bool perceivedStatic, int? ticksAbs = null, StringBuilder explanation = null)
            {
                Tile tile2 = Find.WorldGrid[tile];
                if (tile2.biome.impassable || tile2.hilliness == Hilliness.Impassable)
                {
                    if (explanation != null && explanation.Length > 0)
                    {
                        explanation.AppendLine();
                    }

                    float num = tile2.biome.movementDifficulty;
                    if (explanation != null)
                    {
                        explanation.Append(tile2.biome.LabelCap + ": " + tile2.biome.movementDifficulty.ToStringWithSign("0.#"));
                    }
                    float num2 = Settings.MovementDifficulty;
                    num += num2;
                    if (explanation != null)
                    {
                        explanation.AppendLine();
                        explanation.Append(tile2.hilliness.GetLabelCap() + ": " + num2.ToStringWithSign("0.#"));
                    }
                    num += WorldPathGrid.GetCurrentWinterMovementDifficultyOffset(tile, new int?((!ticksAbs.HasValue) ? GenTicks.TicksAbs : ticksAbs.Value), explanation);
                    __result = num;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(TileFinder), "IsValidTileForNewSettlement")]
        static class Patch_TileFinder_IsValidTileForNewSettlement
        {
            static void Postfix(ref bool __result, int tile, StringBuilder reason)
            {
                if (__result == false)
                {
                    if (Find.WorldGrid[tile].hilliness == Hilliness.Impassable)
                    {
                        if (reason != null)
                        {
                            reason.Remove(0, reason.Length);
                        }
                        __result = true;

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
                                    reason.Append("BaseAlreadyThere".Translate(new object[] { settlement.Faction.Name }));
                                }
                            }
                            __result = false;
                        }
                        if (Find.WorldObjects.AnySettlementBaseAtOrAdjacent(tile))
                        {
                            if (reason != null)
                            {
                                reason.Append("FactionBaseAdjacent".Translate());
                            }
                            __result = false;
                        }
                        if (Find.WorldObjects.AnyMapParentAt(tile) || Current.Game.FindMap(tile) != null)
                        {
                            if (reason != null)
                            {
                                reason.Append("TileOccupied".Translate());
                            }
                            __result = false;
                        }
                    }
                }
            }
        }
    }
}
