using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    internal static class ZombieNoise
    {
        private const int LifetimeTicks = 1800;
        private const int CoalesceWindowTicks = 30;
        private const int RallyCommitmentTicks = 300;
        private const float CoalesceDistance = 2f;
        private static readonly ConditionalWeakTable<Map, List<NoiseEvent>> EventsByMap = new ConditionalWeakTable<Map, List<NoiseEvent>>();
        private static readonly Dictionary<Map, IntVec3> PendingRallies = new Dictionary<Map, IntVec3>();
        private static readonly List<KeyValuePair<Map, IntVec3>> RalliesToProcess = new List<KeyValuePair<Map, IntVec3>>();
        private static bool loggedFirstNoise;
        private static bool loggedFirstInvestigation;

        public static void Record(Map map, IntVec3 position)
        {
            if (map == null || !position.InBounds(map) || Find.TickManager == null)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            List<NoiseEvent> events = GetEvents(map);
            Prune(events, now);
            if (TryCoalesce(events, position, now))
            {
                if (PendingRallies.ContainsKey(map))
                {
                    PendingRallies[map] = position;
                }

                return;
            }

            if (events.Count > 0 && now - events[events.Count - 1].Tick < RallyCommitmentTicks)
            {
                return;
            }

            events.Clear();
            events.Add(new NoiseEvent(position, now));
            PendingRallies[map] = position;
            if (!loggedFirstNoise)
            {
                loggedFirstNoise = true;
                Log.Message("DeadWorld: recorded first sound event on map " + map);
            }
        }

        internal static void ProcessPendingRallies()
        {
            if (PendingRallies.Count == 0)
            {
                return;
            }

            RalliesToProcess.Clear();
            foreach (KeyValuePair<Map, IntVec3> rally in PendingRallies)
            {
                RalliesToProcess.Add(rally);
            }

            PendingRallies.Clear();
            for (int i = 0; i < RalliesToProcess.Count; i++)
            {
                KeyValuePair<Map, IntVec3> rally = RalliesToProcess[i];
                if (rally.Key != null)
                {
                    RallyAllZombies(rally.Key, rally.Value);
                }
            }

            RalliesToProcess.Clear();
        }

        public static bool TryFindFor(Pawn pawn, out IntVec3 position)
        {
            position = IntVec3.Invalid;
            if (pawn == null || !pawn.Spawned || Find.TickManager == null)
            {
                return false;
            }

            List<NoiseEvent> events = GetEvents(pawn.Map);
            int now = Find.TickManager.TicksGame;
            Prune(events, now);
            if (events.Count > 0)
            {
                NoiseEvent noise = events[events.Count - 1];
                position = noise.Position;
                return true;
            }

            return false;
        }

        public static void LogFirstInvestigation(Pawn pawn, IntVec3 position)
        {
            if (!loggedFirstInvestigation)
            {
                loggedFirstInvestigation = true;
                Log.Message("DeadWorld: zombie " + pawn.LabelShort + " is investigating a sound at " + position);
            }
        }

        private static List<NoiseEvent> GetEvents(Map map)
        {
            return EventsByMap.GetValue(map, _ => new List<NoiseEvent>());
        }

        private static bool TryCoalesce(List<NoiseEvent> events, IntVec3 position, int now)
        {
            if (events.Count == 0)
            {
                return false;
            }

            NoiseEvent latest = events[events.Count - 1];
            if (now >= latest.Tick && now - latest.Tick <= CoalesceWindowTicks &&
                latest.Position.DistanceTo(position) <= CoalesceDistance)
            {
                latest.Position = position;
                latest.Tick = now;
                return true;
            }

            return false;
        }

        private static void RallyAllZombies(Map map, IntVec3 position)
        {
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            int rallied = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn?.def?.defName != "DeadWorld_Zombie" || pawn.Dead || pawn.Downed)
                {
                    continue;
                }

                Pawn adjacentPlayerTarget = JobGiver_DeadWorldFightEnemies.FindAdjacentPlayerTarget(pawn);
                if (adjacentPlayerTarget != null)
                {
                    pawn.mindState.enemyTarget = adjacentPlayerTarget;
                    pawn.mindState.lastEngageTargetTick = Find.TickManager.TicksGame;
                    pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackMelee, adjacentPlayerTarget), JobTag.Misc);
                    continue;
                }

                if (JobGiver_DeadWorldFightEnemies.HasValidPlayerTarget(pawn))
                {
                    continue;
                }

                Pawn adjacentTarget = JobGiver_DeadWorldFightEnemies.FindAdjacentHostileTarget(pawn);
                if (adjacentTarget != null)
                {
                    pawn.mindState.enemyTarget = adjacentTarget;
                    pawn.mindState.lastEngageTargetTick = Find.TickManager.TicksGame;
                    pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackMelee, adjacentTarget), JobTag.Misc);
                    continue;
                }

                Job job = ZombieDoorBreach.TryGetDoorAttackJob(pawn, position, forceWallBreach: true);
                if (job == null)
                {
                    if (pawn.Position.DistanceTo(position) <= 3f)
                    {
                        continue;
                    }

                    job = JobMaker.MakeJob(JobDefOf.Goto, position, LifetimeTicks, true);
                }

                if (pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc))
                {
                    rallied++;
                }
            }

            Log.Message("DeadWorld: noise at " + position + " rallied " + rallied + " zombies on " + map);
        }

        private static void Prune(List<NoiseEvent> events, int now)
        {
            for (int i = events.Count - 1; i >= 0; i--)
            {
                if (now - events[i].Tick > LifetimeTicks)
                {
                    events.RemoveAt(i);
                }
            }
        }

        private sealed class NoiseEvent
        {
            public IntVec3 Position;
            public int Tick;

            public NoiseEvent(IntVec3 position, int tick)
            {
                Position = position;
                Tick = tick;
            }
        }
    }
}