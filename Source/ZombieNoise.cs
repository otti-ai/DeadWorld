using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    internal static class ZombieNoise
    {
        private const int LifetimeTicks = 1800;
        private const int MaxEventsPerMap = 64;
        private const int MinReactionCooldownTicks = 300;
        private const int MaxReactionCooldownTicks = 900;
        private static readonly ConditionalWeakTable<Map, List<NoiseEvent>> EventsByMap = new ConditionalWeakTable<Map, List<NoiseEvent>>();
        private static readonly Dictionary<int, int> ReactionCooldowns = new Dictionary<int, int>();
        private static bool loggedFirstNoise;
        private static bool loggedFirstInvestigation;
        private static bool loggedFirstHordeCall;

        public static void Record(Map map, IntVec3 position, float radius)
        {
            if (map == null || !position.InBounds(map) || Find.TickManager == null)
            {
                return;
            }

            List<NoiseEvent> events = GetEvents(map);
            Prune(events, Find.TickManager.TicksGame);
            if (events.Count >= MaxEventsPerMap)
            {
                events.RemoveAt(0);
            }

            events.Add(new NoiseEvent(position, radius, Find.TickManager.TicksGame));
            if (!loggedFirstNoise)
            {
                loggedFirstNoise = true;
                Log.Message("DeadWorld: recorded first sound event on map " + map);
            }
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

            int pawnId = pawn.thingIDNumber;
            int previousCooldownUntil = 0;
            if (ReactionCooldowns.TryGetValue(pawnId, out int cooldownUntil))
            {
                if (now < cooldownUntil)
                {
                    return false;
                }

                previousCooldownUntil = cooldownUntil;
                ReactionCooldowns.Remove(pawnId);
            }

            float bestScore = float.MaxValue;
            NoiseEvent bestNoise = null;
            foreach (NoiseEvent noise in events)
            {
                if (noise.Tick < previousCooldownUntil)
                {
                    noise.CheckedPawnIds.Add(pawnId);
                    continue;
                }

                float distance = pawn.Position.DistanceTo(noise.Position);
                if (distance <= 3f || distance > noise.Radius)
                {
                    continue;
                }

                if (!noise.CheckedPawnIds.Add(pawnId))
                {
                    continue;
                }

                float score = distance + (now - noise.Tick) * 0.02f;
                if (score < bestScore)
                {
                    bestScore = score;
                    position = noise.Position;
                    bestNoise = noise;
                }
            }

            if (bestNoise == null)
            {
                return false;
            }

            ReactionCooldowns[pawnId] = now + Rand.Range(MinReactionCooldownTicks, MaxReactionCooldownTicks + 1);

            if (!bestNoise.IsHordeCall && !bestNoise.HordeCallEmitted)
            {
                bestNoise.HordeCallEmitted = true;
                float hordeRange = DeadWorldSettings.Current?.HordeCallRange ?? 35f;
                if (hordeRange > 0f)
                {
                    if (events.Count >= MaxEventsPerMap)
                    {
                        events.RemoveAt(0);
                    }

                    events.Add(new NoiseEvent(pawn.Position, hordeRange, now, true));
                    if (!loggedFirstHordeCall)
                    {
                        loggedFirstHordeCall = true;
                        Log.Message("DeadWorld: first horde call emitted at " + pawn.Position + " with range " + hordeRange);
                    }
                }
            }

            return true;
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
            public readonly IntVec3 Position;
            public readonly float Radius;
            public readonly int Tick;
            public readonly bool IsHordeCall;
            public readonly HashSet<int> CheckedPawnIds = new HashSet<int>();
            public bool HordeCallEmitted;

            public NoiseEvent(IntVec3 position, float radius, int tick, bool isHordeCall = false)
            {
                Position = position;
                Radius = radius;
                Tick = tick;
                IsHordeCall = isHordeCall;
            }
        }
    }
}