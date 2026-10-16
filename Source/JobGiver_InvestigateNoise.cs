using RimWorld;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    public sealed class JobGiver_InvestigateNoise : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!ZombieNoise.TryFindFor(pawn, out IntVec3 position))
            {
                return null;
            }

            Job doorJob = ZombieDoorBreach.TryGetDoorAttackJob(pawn, position);
            if (doorJob != null)
            {
                return doorJob;
            }

            ZombieNoise.LogFirstInvestigation(pawn, position);
            Job job = JobMaker.MakeJob(JobDefOf.Goto, position, 600, true);
            job.locomotionUrgency = LocomotionUrgency.Walk;
            return job;
        }
    }
}