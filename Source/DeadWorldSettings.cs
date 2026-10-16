using Verse;

namespace DeadWorld
{
    public sealed class DeadWorldSettings : ModSettings
    {
        public static DeadWorldSettings Current;

        public float ScratchInfectionChance = 0.1f;
        public float InfectionIncubationDays = 3f;
        public bool ZombiesResurrect = true;
        public bool BitingFrenzyEnabled = true;
        public float ZombieResurrectionMaxDays = 3f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ScratchInfectionChance, "scratchInfectionChance", 0.1f);
            Scribe_Values.Look(ref InfectionIncubationDays, "infectionIncubationDays", 3f);
            Scribe_Values.Look(ref ZombiesResurrect, "zombiesResurrect", true);
            Scribe_Values.Look(ref BitingFrenzyEnabled, "bitingFrenzyEnabled", true);
            Scribe_Values.Look(ref ZombieResurrectionMaxDays, "zombieResurrectionMaxDays", 3f);
            ScratchInfectionChance = UnityEngine.Mathf.Clamp01(ScratchInfectionChance);
            InfectionIncubationDays = UnityEngine.Mathf.Clamp(InfectionIncubationDays, 1f, 3f);
            ZombieResurrectionMaxDays = UnityEngine.Mathf.Clamp(ZombieResurrectionMaxDays, 1f, 3f);
        }
    }
}