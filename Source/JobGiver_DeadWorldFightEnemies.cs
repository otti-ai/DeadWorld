using RimWorld;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    public sealed class JobGiver_DeadWorldFightEnemies : JobGiver_AIFightEnemies
    {
        protected override int TicksSinceEngageToLoseTarget => 1800;

        protected override Job TryGiveJob(Pawn pawn)
        {
            pawn.mindState.canFleeIndividual = false;
            UpdateEnemyTarget(pawn);

            Thing target = pawn.mindState.enemyTarget;
            if (target != null && target.Spawned && target.Map == pawn.Map)
            {
                Job doorJob = ZombieDoorBreach.TryGetDoorAttackJob(pawn, target.Position);
                if (doorJob != null)
                {
                    return doorJob;
                }
            }

            return base.TryGiveJob(pawn);
        }

        protected override bool ShouldLoseTarget(Pawn pawn)
        {
            Thing target = pawn.mindState.enemyTarget;
            if (target == null || target.Destroyed || !target.Spawned || target.Map != pawn.Map)
            {
                return true;
            }

            if (Find.TickManager.TicksGame - pawn.mindState.lastEngageTargetTick > TicksSinceEngageToLoseTarget)
            {
                return true;
            }

            if ((float)(pawn.Position - target.Position).LengthHorizontalSquared > targetKeepRadius * targetKeepRadius)
            {
                return true;
            }

            return (target as IAttackTarget)?.ThreatDisabled(pawn) ?? false;
        }
    }
}