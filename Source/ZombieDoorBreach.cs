using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    internal static class ZombieDoorBreach
    {
        private const float WallAttackChance = 0.05f;
        private const float WallAttackChanceIncrease = 0.15f;
        private const float MaxWallAttackChance = 1f;
        private const int WallAttemptCooldownTicks = 120;
        private const int CooldownCleanupIntervalTicks = 600;
        private static readonly Dictionary<int, int> WallAttemptCooldowns = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> WallAttemptCounts = new Dictionary<int, int>();
        private static readonly List<int> ExpiredCooldownIds = new List<int>();
        private static TickManager trackedTickManager;
        private static int nextCooldownCleanupTick;

        public static Job TryGetDoorAttackJob(Pawn pawn, IntVec3 destination, bool forceWallBreach = false)
        {
            if (pawn == null || !pawn.Spawned || !destination.IsValid || !destination.InBounds(pawn.Map))
            {
                return null;
            }

            bool canReach = pawn.CanReach(destination, PathEndMode.OnCell, Danger.Deadly, canBashDoors: false, canBashFences: false, TraverseMode.PassAllDestroyableThings);

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
                    int now = Find.TickManager.TicksGame;
                    PruneWallAttemptCooldowns(now);
                    int pawnId = pawn.thingIDNumber;
                    if (forceWallBreach || !canReach || !WallAttemptCooldowns.TryGetValue(pawnId, out int cooldownUntil) || now >= cooldownUntil)
                    {
                        WallAttemptCooldowns[pawnId] = now + WallAttemptCooldownTicks;
                        WallAttemptCounts.TryGetValue(pawnId, out int attemptCount);
                        float chance = UnityEngine.Mathf.Min(MaxWallAttackChance, WallAttackChance + attemptCount * WallAttackChanceIncrease);
                        if (forceWallBreach || !canReach || Rand.Chance(chance))
                        {
                            WallAttemptCounts.Remove(pawnId);
                            return DigUtility.PassBlockerJob(pawn, blocker, cellBeforeBlocker, false, false);
                        }

                        WallAttemptCounts[pawnId] = attemptCount + 1;
                    }
                }
            }

            return null;
        }

        private static void PruneWallAttemptCooldowns(int now)
        {
            if (trackedTickManager != Find.TickManager)
            {
                WallAttemptCooldowns.Clear();
                WallAttemptCounts.Clear();
                trackedTickManager = Find.TickManager;
                nextCooldownCleanupTick = now + CooldownCleanupIntervalTicks;
                return;
            }

            if (now < nextCooldownCleanupTick)
            {
                return;
            }

            ExpiredCooldownIds.Clear();
            foreach (KeyValuePair<int, int> cooldown in WallAttemptCooldowns)
            {
                if (now >= cooldown.Value)
                {
                    ExpiredCooldownIds.Add(cooldown.Key);
                }
            }

            for (int i = 0; i < ExpiredCooldownIds.Count; i++)
            {
                WallAttemptCooldowns.Remove(ExpiredCooldownIds[i]);
                WallAttemptCounts.Remove(ExpiredCooldownIds[i]);
            }

            nextCooldownCleanupTick = now + CooldownCleanupIntervalTicks;
        }

        private static bool IsWall(Thing blocker)
        {
            return blocker != null && blocker.def.building != null && blocker.def.building.isEdifice &&
                (blocker.def.defName == "Wall" || blocker.def.building.isNaturalRock);
        }
    }
}