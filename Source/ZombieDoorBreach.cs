using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    internal static class ZombieDoorBreach
    {
        private const float WallAttackChance = 0.05f;
        private const int WallAttemptCooldownTicks = 120;
        private static readonly Dictionary<int, int> WallAttemptCooldowns = new Dictionary<int, int>();
        private static TickManager trackedTickManager;

        public static Job TryGetDoorAttackJob(Pawn pawn, IntVec3 destination)
        {
            if (pawn == null || !pawn.Spawned || !destination.IsValid || !destination.InBounds(pawn.Map))
            {
                return null;
            }

            if (!pawn.CanReach(destination, PathEndMode.OnCell, Danger.Deadly, canBashDoors: false, canBashFences: false, TraverseMode.PassAllDestroyableThings))
            {
                return null;
            }

            using (PawnPath path = pawn.Map.pathFinder.FindPathNow(
                pawn.Position,
                destination,
                TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassAllDestroyableThings)))
            {
                Thing blocker = path.FirstBlockingBuilding(out IntVec3 cellBeforeBlocker, pawn);
                if (blocker is Building_Door door && !door.Open)
                {
                    return DigUtility.PassBlockerJob(pawn, blocker, cellBeforeBlocker, false, false);
                }

                if (IsWall(blocker))
                {
                    if (trackedTickManager != Find.TickManager)
                    {
                        WallAttemptCooldowns.Clear();
                        trackedTickManager = Find.TickManager;
                    }

                    int now = Find.TickManager.TicksGame;
                    int pawnId = pawn.thingIDNumber;
                    if (!WallAttemptCooldowns.TryGetValue(pawnId, out int cooldownUntil) || now >= cooldownUntil)
                    {
                        WallAttemptCooldowns[pawnId] = now + WallAttemptCooldownTicks;
                        if (Rand.Chance(WallAttackChance))
                        {
                            return DigUtility.PassBlockerJob(pawn, blocker, cellBeforeBlocker, false, false);
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsWall(Thing blocker)
        {
            return blocker != null && blocker.def.building != null && blocker.def.building.isEdifice &&
                (blocker.def.defName == "Wall" || blocker.def.building.isNaturalRock);
        }
    }
}