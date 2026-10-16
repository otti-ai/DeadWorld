using RimWorld;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    public sealed class JobGiver_InvestigateNoise : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            return TryGiveNoiseJob(pawn);
        }

        internal static Job TryGiveNoiseJob(Pawn pawn)
        {
            if (JobGiver_DeadWorldFightEnemies.HasValidPlayerTarget(pawn) ||
                JobGiver_DeadWorldFightEnemies.HasAdjacentHostileTarget(pawn))
            {
                return null;
            }

            if (!ZombieNoise.TryFindFor(pawn, out IntVec3 position))
            {
                return null;
            }

            Job doorJob = ZombieDoorBreach.TryGetDoorAttackJob(pawn, position, forceWallBreach: true);
            if (doorJob != null)
            {
                return doorJob;
            }

            if (pawn.Position.DistanceTo(position) <= 3f)
            {
                return null;
            }

            ZombieNoise.LogFirstInvestigation(pawn, position);
            Job job = JobMaker.MakeJob(JobDefOf.Goto, position, 1800, true);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            return job;
        }
    }
}