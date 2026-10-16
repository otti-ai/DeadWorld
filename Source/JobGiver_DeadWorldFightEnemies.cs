using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    public sealed class JobGiver_DeadWorldFightEnemies : JobGiver_AIFightEnemies
    {
        protected override int TicksSinceEngageToLoseTarget => 1800;
        private const int AdjacentCombatDistanceSquared = 2;

        internal static Pawn FindAdjacentHostileTarget(Pawn pawn)
        {
            if (pawn?.Spawned != true || pawn.Faction == null)
            {
                return null;
            }

            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate != null && candidate != pawn && !candidate.Dead && !candidate.Downed &&
                    candidate.Faction != null && pawn.Faction.HostileTo(candidate.Faction) &&
                    (pawn.Position - candidate.Position).LengthHorizontalSquared <= AdjacentCombatDistanceSquared)
                {
                    return candidate;
                }
            }

            return null;
        }

        internal static bool HasAdjacentHostileTarget(Pawn pawn)
        {
            return FindAdjacentHostileTarget(pawn) != null;
        }

        internal static Pawn FindAdjacentPlayerTarget(Pawn pawn)
        {
            if (pawn?.Spawned != true || pawn.Faction == null || Faction.OfPlayer == null ||
                !pawn.Faction.HostileTo(Faction.OfPlayer))
            {
                return null;
            }

            IReadOnlyList<Pawn> pawns = pawn.Map.mapPawns.AllPawnsSpawned;
            Pawn closestTarget = null;
            int closestDistanceSquared = AdjacentCombatDistanceSquared + 1;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (candidate == null || candidate == pawn || candidate.Dead || candidate.Downed ||
                    candidate.Faction != Faction.OfPlayer)
                {
                    continue;
                }

                int distanceSquared = (pawn.Position - candidate.Position).LengthHorizontalSquared;
                if (distanceSquared <= AdjacentCombatDistanceSquared && distanceSquared < closestDistanceSquared)
                {
                    closestTarget = candidate;
                    closestDistanceSquared = distanceSquared;
                }
            }

            return closestTarget;
        }

        internal static bool HasValidPlayerTarget(Pawn pawn)
        {
            if (pawn?.mindState?.enemyTarget is not Pawn target)
            {
                return false;
            }

            return target.Faction == Faction.OfPlayer && !target.Dead && target.Spawned && target.Map == pawn.Map;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            pawn.mindState.canFleeIndividual = false;
            if (!HasValidPlayerTarget(pawn))
            {
                UpdateEnemyTarget(pawn);
            }

            Pawn adjacentPlayerTarget = FindAdjacentPlayerTarget(pawn);
            if (adjacentPlayerTarget != null)
            {
                pawn.mindState.enemyTarget = adjacentPlayerTarget;
                pawn.mindState.lastEngageTargetTick = Find.TickManager.TicksGame;
                return JobMaker.MakeJob(JobDefOf.AttackMelee, adjacentPlayerTarget);
            }

            Thing target = pawn.mindState.enemyTarget;
            if (HasValidPlayerTarget(pawn))
            {
                Job routeJob = ZombieDoorBreach.TryGetDoorAttackJob(pawn, target.Position);
                if (routeJob != null)
                {
                    return routeJob;
                }

                return base.TryGiveJob(pawn);
            }

            if (target != null && target.Spawned && target.Map == pawn.Map)
            {
                bool adjacentCombat = HasAdjacentHostileTarget(pawn);
                if (!adjacentCombat)
                {
                    Job noiseJob = JobGiver_InvestigateNoise.TryGiveNoiseJob(pawn);
                    if (noiseJob != null)
                    {
                        return noiseJob;
                    }
                }

                Job doorJob = ZombieDoorBreach.TryGetDoorAttackJob(pawn, target.Position);
                if (doorJob != null)
                {
                    return doorJob;
                }
            }
            else
            {
                Job noiseJob = JobGiver_InvestigateNoise.TryGiveNoiseJob(pawn);
                if (noiseJob != null)
                {
                    return noiseJob;
                }
            }

            return base.TryGiveJob(pawn);
        }

        protected override bool ShouldLoseTarget(Pawn pawn)
        {
            Thing target = pawn.mindState.enemyTarget;
            return target == null || target.Destroyed || !target.Spawned || target.Map != pawn.Map;
        }
    }
}