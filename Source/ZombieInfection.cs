using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    internal static class ZombieInfection
    {
        private static HediffDef InfectionDef => DefDatabase<HediffDef>.GetNamed("DeadWorld_ZombieInfection");
        private static HediffDef SystemicInfectionDef => DefDatabase<HediffDef>.GetNamed("DeadWorld_ZombieInfectionSystemic");

        public static void Apply(Pawn pawn, BodyPartRecord bodyPart)
        {
            if (pawn?.health == null || bodyPart == null || HasInfection(pawn))
            {
                return;
            }

            bool systemic = IsTerminalPart(bodyPart);
            Hediff_ZombieInfection infection = systemic
                ? (Hediff_ZombieInfection)HediffMaker.MakeHediff(SystemicInfectionDef, pawn)
                : (Hediff_ZombieInfection)HediffMaker.MakeHediff(InfectionDef, pawn, bodyPart);
            infection.InitializePrimary(systemic);
            pawn.health.AddHediff(infection);
        }

        private static bool HasInfection(Pawn pawn)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_ZombieInfection)
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsTerminalPart(BodyPartRecord part)
        {
            return part?.def?.defName == "Torso" || part?.def?.defName == "Head";
        }

        internal static void BecomeSystemic(Hediff_ZombieInfection source)
        {
            Pawn pawn = source?.pawn;
            if (pawn?.health == null)
            {
                return;
            }

            Hediff_ZombieInfection systemic = (Hediff_ZombieInfection)HediffMaker.MakeHediff(SystemicInfectionDef, pawn);
            systemic.InitializeSystemic(source.Severity, source.IncubationTicks, source.ReanimateAtTick);
            RemoveAllInfections(pawn);
            pawn.health.AddHediff(systemic);
        }

        internal static void AddLocalMarker(Pawn pawn, BodyPartRecord bodyPart, float severity, int incubationTicks, int reanimateAtTick, int nextSpreadAtTick)
        {
            Hediff_ZombieInfection infection = (Hediff_ZombieInfection)HediffMaker.MakeHediff(InfectionDef, pawn, bodyPart);
            infection.InitializeSecondary(severity, incubationTicks, reanimateAtTick, nextSpreadAtTick);
            pawn.health.AddHediff(infection);
        }

        public static void Reanimate(Pawn victim, Hediff_ZombieInfection infection)
        {
            if (victim == null || infection == null || victim.health == null ||
                !victim.health.hediffSet.hediffs.Contains(infection))
            {
                return;
            }

            FactionDef zombieFactionDef = DefDatabase<FactionDef>.GetNamed("DeadWorld_ZombieHorde", false);
            Faction zombieFaction = zombieFactionDef == null ? null : Find.FactionManager.FirstFactionOfDef(zombieFactionDef);
            PawnKindDef zombieKind = DefDatabase<PawnKindDef>.GetNamed("DeadWorld_Zombie", false);
            ThingDef zombieRace = zombieKind?.race;
            if (zombieFaction == null || zombieKind == null || zombieRace == null)
            {
                Log.Error("DeadWorld: Could not reanimate infected pawn because the zombie faction, kind, or race is unavailable.");
                return;
            }

            if (!victim.Dead)
            {
                RemoveAllInfections(victim);
                victim.Kill(null);
            }

            Corpse corpse = victim.Corpse;
            if (corpse == null || !corpse.SpawnedOrAnyParentSpawned)
            {
                Log.Warning("DeadWorld: Could not reanimate infected pawn because its corpse is not on an active map or spawned holder: " + victim);
                return;
            }

            ResurrectionParams parameters = new ResurrectionParams
            {
                restoreMissingParts = false,
                removeDiedThoughts = true
            };
            if (!ResurrectionUtility.TryResurrect(victim, parameters))
            {
                return;
            }

            RemoveAllInfections(victim);
            victim.def = zombieRace;
            victim.ChangeKind(zombieKind);
            victim.SetFaction(zombieFaction);
            if (!victim.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("DeadWorld_ZombieRot", false)))
            {
                HediffDef rotDef = DefDatabase<HediffDef>.GetNamed("DeadWorld_ZombieRot", false);
                if (rotDef != null)
                {
                    victim.health.AddHediff(rotDef);
                }
            }
        }

        private static void RemoveAllInfections(Pawn pawn)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (hediffs[i] is Hediff_ZombieInfection)
                {
                    pawn.health.RemoveHediff(hediffs[i]);
                }
            }
        }
    }

    public sealed class Hediff_ZombieInfection : HediffWithComps
    {
        private const int DefaultIncubationTicks = 3 * GenDate.TicksPerDay;
        private const int SpreadStepsPerIncubation = 10;
        private int reanimateAtTick = -1;
        private int legacyTicksRemaining = -1;
        private int incubationTicks = -1;
        private int nextSpreadAtTick = -1;
        private bool isPrimary = true;
        private bool isSystemic;

        internal int IncubationTicks => incubationTicks;
        internal int ReanimateAtTick => reanimateAtTick;

        public void InitializePrimary(bool systemic)
        {
            float maxDays = DeadWorldSettings.Current?.InfectionIncubationDays ?? 3f;
            float days = Rand.Range(1f, UnityEngine.Mathf.Clamp(maxDays, 1f, 3f));
            incubationTicks = UnityEngine.Mathf.RoundToInt(days * GenDate.TicksPerDay);
            int now = Find.TickManager?.TicksGame ?? 0;
            reanimateAtTick = now + incubationTicks;
            isSystemic = systemic;
            nextSpreadAtTick = systemic ? -1 : now + SpreadIntervalTicks;
            isPrimary = true;
        }

        internal void InitializeSystemic(float severity, int ticks, int reanimateTick)
        {
            InitializeSecondary(severity, ticks, reanimateTick, -1);
            isPrimary = true;
            isSystemic = true;
        }

        public void InitializeSecondary(float severity)
        {
            isPrimary = false;
            Severity = severity;
        }

        internal void InitializeSecondary(float severity, int ticks, int reanimateTick, int nextSpreadTick)
        {
            InitializeSecondary(severity);
            incubationTicks = ticks;
            reanimateAtTick = reanimateTick;
            nextSpreadAtTick = nextSpreadTick;
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!isPrimary && !HasPrimaryInfection())
            {
                isPrimary = true;
                EnsureSchedule();
            }

            if (!isPrimary)
            {
                return;
            }

            EnsureSchedule();
            CheckReanimationDeadline();
        }

        private void CheckReanimationDeadline()
        {
            if (pawn == null || Find.TickManager == null)
            {
                return;
            }

            EnsureSchedule();
            int elapsedTicks = Find.TickManager.TicksGame - (reanimateAtTick - incubationTicks);
            Severity = UnityEngine.Mathf.Clamp01((float)elapsedTicks / incubationTicks);
            UpdateLocalSeverity();

            int now = Find.TickManager.TicksGame;
            if (!isSystemic && now >= nextSpreadAtTick && now < reanimateAtTick)
            {
                nextSpreadAtTick = now + SpreadIntervalTicks;
                SpreadToNextPart();
            }

            if (now >= reanimateAtTick)
            {
                ZombieInfection.Reanimate(pawn, this);
            }
        }

        internal void CheckCorpseReanimation()
        {
            if (pawn?.Dead != true || Find.TickManager == null)
            {
                return;
            }

            EnsureSchedule();
            if (Find.TickManager.TicksGame >= reanimateAtTick)
            {
                ZombieInfection.Reanimate(pawn, this);
            }
        }

        private void EnsureSchedule()
        {
            if (pawn == null || Find.TickManager == null)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (incubationTicks <= 0)
            {
                float maxDays = DeadWorldSettings.Current?.InfectionIncubationDays ?? 3f;
                float days = Rand.Range(1f, UnityEngine.Mathf.Clamp(maxDays, 1f, 3f));
                incubationTicks = UnityEngine.Mathf.RoundToInt(days * GenDate.TicksPerDay);
            }

            if (reanimateAtTick < 0)
            {
                int remainingTicks = UnityEngine.Mathf.RoundToInt(incubationTicks * (1f - Severity));
                reanimateAtTick = now + UnityEngine.Mathf.Max(1, remainingTicks);
            }

            if (nextSpreadAtTick < 0)
            {
                nextSpreadAtTick = isSystemic ? -1 : now + SpreadIntervalTicks;
            }
        }

        private int SpreadIntervalTicks
        {
            get
            {
                return UnityEngine.Mathf.Max(1, incubationTicks / SpreadStepsPerIncubation);
            }
        }

        private bool HasPrimaryInfection()
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_ZombieInfection infection && infection.isPrimary)
                {
                    return true;
                }
            }

            return false;
        }

        private void UpdateLocalSeverity()
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_ZombieInfection infection && !infection.isPrimary)
                {
                    infection.Severity = Severity;
                }
            }
        }

        private void SpreadToNextPart()
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            List<BodyPartRecord> infectedParts = new List<BodyPartRecord>();
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_ZombieInfection infection && infection.Part != null && !infectedParts.Contains(infection.Part))
                {
                    infectedParts.Add(infection.Part);
                }
            }

            if (infectedParts.Count == 0)
            {
                foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
                {
                    if (!pawn.health.hediffSet.PartIsMissing(part))
                    {
                        ZombieInfection.AddLocalMarker(pawn, part, Severity, incubationTicks, reanimateAtTick, nextSpreadAtTick);
                        return;
                    }
                }

                return;
            }

            List<BodyPartRecord> parentCandidates = new List<BodyPartRecord>();
            foreach (BodyPartRecord infectedPart in infectedParts)
            {
                AddSpreadCandidate(parentCandidates, infectedPart.parent);
            }

            List<BodyPartRecord> candidates = parentCandidates;
            if (candidates.Count == 0)
            {
                candidates = new List<BodyPartRecord>();
            }

            if (parentCandidates.Count == 0)
            {
                foreach (BodyPartRecord infectedPart in infectedParts)
                {
                    foreach (BodyPartRecord part in pawn.RaceProps.body.AllParts)
                    {
                        if (part.parent == infectedPart)
                        {
                            AddSpreadCandidate(candidates, part);
                        }
                    }
                }
            }

            if (candidates.Count > 0)
            {
                BodyPartRecord nextPart = candidates[Rand.Range(0, candidates.Count)];
                if (ZombieInfection.IsTerminalPart(nextPart))
                {
                    ZombieInfection.BecomeSystemic(this);
                }
                else
                {
                    ZombieInfection.AddLocalMarker(pawn, nextPart, Severity, incubationTicks, reanimateAtTick, nextSpreadAtTick);
                }
            }
        }

        private void AddSpreadCandidate(List<BodyPartRecord> candidates, BodyPartRecord part)
        {
            if (part == null || pawn.health.hediffSet.PartIsMissing(part) || candidates.Contains(part))
            {
                return;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_ZombieInfection infection && infection.Part == part)
                {
                    return;
                }
            }

            candidates.Add(part);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref reanimateAtTick, "reanimateAtTick", -1);
            Scribe_Values.Look(ref incubationTicks, "incubationTicks", -1);
            Scribe_Values.Look(ref nextSpreadAtTick, "nextSpreadAtTick", -1);
            Scribe_Values.Look(ref isPrimary, "isPrimary", true);
            Scribe_Values.Look(ref isSystemic, "isSystemic", false);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref legacyTicksRemaining, "ticksRemaining", -1);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (isPrimary && incubationTicks < 0)
                {
                    incubationTicks = DefaultIncubationTicks;
                }

                if (isPrimary && reanimateAtTick < 0)
                {
                    int remaining = legacyTicksRemaining >= 0 ? legacyTicksRemaining : incubationTicks;
                    reanimateAtTick = (Find.TickManager?.TicksGame ?? 0) + remaining;
                }

                if (isPrimary && nextSpreadAtTick < 0)
                {
                    nextSpreadAtTick = (Find.TickManager?.TicksGame ?? 0) + SpreadIntervalTicks;
                }

                legacyTicksRemaining = -1;
            }
        }
    }

    public sealed class HediffCompProperties_BitingFrenzy : HediffCompProperties
    {
        public HediffCompProperties_BitingFrenzy()
        {
            compClass = typeof(HediffComp_BitingFrenzy);
        }
    }

    public sealed class HediffComp_BitingFrenzy : HediffComp
    {
        private const int MinDelayTicks = 6000;
        private const int MaxDelayTicks = 12000;
        private const float OutbreakChance = 0.3f;
        private int triggerAtTick = -1;
        private bool outbreakSelected;
        private bool selectionMade;
        private bool triggered;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (triggered || parent?.pawn == null || Find.TickManager == null ||
                !(DeadWorldSettings.Current?.BitingFrenzyEnabled ?? true))
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (triggerAtTick < 0)
            {
                triggerAtTick = now + Rand.Range(MinDelayTicks, MaxDelayTicks + 1);
                outbreakSelected = Rand.Chance(OutbreakChance);
                selectionMade = true;
            }

            if (now < triggerAtTick)
            {
                return;
            }

            if (!selectionMade)
            {
                outbreakSelected = Rand.Chance(OutbreakChance);
                selectionMade = true;
            }

            if (!outbreakSelected)
            {
                triggered = true;
                return;
            }

            MentalStateDef bitingFrenzy = DefDatabase<MentalStateDef>.GetNamed("DeadWorld_BitingFrenzy", false);
            if (bitingFrenzy != null && parent.pawn.mindState.mentalStateHandler.TryStartMentalState(bitingFrenzy, null, true))
            {
                triggered = true;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref triggerAtTick, "bitingFrenzyTriggerAtTick", -1);
            Scribe_Values.Look(ref outbreakSelected, "bitingFrenzyOutbreakSelected", false);
            Scribe_Values.Look(ref selectionMade, "bitingFrenzySelectionMade", false);
            Scribe_Values.Look(ref triggered, "bitingFrenzyTriggered", false);
        }
    }

    public sealed class MentalState_BitingFrenzy : MentalState_Berserk
    {
        private const float TargetSearchRadius = 12f;

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (pawn?.Spawned != true || pawn.Downed || pawn.jobs == null)
            {
                return;
            }

            Job currentJob = pawn.jobs.curJob;
            if (currentJob != null && currentJob.def == JobDefOf.AttackMelee)
            {
                return;
            }

            Thing target = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Deadly),
                TargetSearchRadius,
                thing => thing is Pawn other && other != pawn && !other.Dead && other.RaceProps.Humanlike);

            if (target != null)
            {
                pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.AttackMelee, target), JobTag.Misc);
            }
        }
    }

    public sealed class Hediff_ZombieResurrection : Hediff
    {
        private int resurrectAtTick = -1;
        private int cooldownTicks = -1;

        public override string SeverityLabel => Severity.ToStringPercent();

        public override void PostMake()
        {
            base.PostMake();
            if (cooldownTicks < 0)
            {
                float maxDays = DeadWorldSettings.Current?.ZombieResurrectionMaxDays ?? 3f;
                float days = Rand.Range(1f, UnityEngine.Mathf.Clamp(maxDays, 1f, 3f));
                cooldownTicks = UnityEngine.Mathf.RoundToInt(days * GenDate.TicksPerDay);
            }

            if (resurrectAtTick < 0)
            {
                resurrectAtTick = (Find.TickManager?.TicksGame ?? 0) + cooldownTicks;
            }
        }

        public void CheckResurrection()
        {
            if (pawn == null || !pawn.Dead || resurrectAtTick < 0 || cooldownTicks <= 0 || Find.TickManager == null)
            {
                return;
            }

            int startTick = resurrectAtTick - cooldownTicks;
            int elapsedTicks = Find.TickManager.TicksGame - startTick;
            Severity = UnityEngine.Mathf.Clamp01((float)elapsedTicks / cooldownTicks);

            if (!(DeadWorldSettings.Current?.ZombiesResurrect ?? true) || Find.TickManager.TicksGame < resurrectAtTick)
            {
                return;
            }

            Corpse corpse = pawn.Corpse;
            if (corpse == null || !corpse.SpawnedOrAnyParentSpawned)
            {
                return;
            }

            if (ResurrectionUtility.TryResurrect(pawn, new ResurrectionParams
            {
                restoreMissingParts = false,
                removeDiedThoughts = true
            }))
            {
                pawn.health.RemoveHediff(this);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref resurrectAtTick, "resurrectAtTick", -1);
            Scribe_Values.Look(ref cooldownTicks, "cooldownTicks", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (cooldownTicks < 0)
                {
                    float maxDays = DeadWorldSettings.Current?.ZombieResurrectionMaxDays ?? 3f;
                    float days = Rand.Range(1f, UnityEngine.Mathf.Clamp(maxDays, 1f, 3f));
                    cooldownTicks = UnityEngine.Mathf.RoundToInt(days * GenDate.TicksPerDay);
                }

                if (resurrectAtTick < 0)
                {
                    resurrectAtTick = (Find.TickManager?.TicksGame ?? 0) + cooldownTicks;
                }
            }
        }
    }
}