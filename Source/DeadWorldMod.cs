using HarmonyLib;
using UnityEngine;
using Verse;

namespace DeadWorld
{
    public sealed class DeadWorldMod : Mod
    {
        private readonly DeadWorldSettings settings;

        public DeadWorldMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<DeadWorldSettings>();
            DeadWorldSettings.Current = settings;
            new Harmony("DeadWorld.ZombieApocalypse").PatchAll();
            Log.Message("DeadWorld: zombie hearing patches initialized.");
        }

        public override string SettingsCategory()
        {
            return "DeadWorld_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("DeadWorld_Settings_ShotHearingRange".Translate(settings.ShotHearingRange.ToString("F0")));
            settings.ShotHearingRange = listing.Slider(settings.ShotHearingRange, 20f, 200f);
            listing.Gap(12f);

            listing.Label("DeadWorld_Settings_ExplosionHearingRange".Translate(settings.ExplosionHearingRange.ToString("F0")));
            settings.ExplosionHearingRange = listing.Slider(settings.ExplosionHearingRange, 20f, 300f);
            listing.Gap(12f);

            listing.Label("DeadWorld_Settings_HordeCallRange".Translate(settings.HordeCallRange.ToString("F0")));
            settings.HordeCallRange = listing.Slider(settings.HordeCallRange, 0f, 100f);
            listing.Gap(12f);

            listing.Label("DeadWorld_Settings_ScratchInfectionChance".Translate(settings.ScratchInfectionChance.ToStringPercent()));
            settings.ScratchInfectionChance = listing.Slider(settings.ScratchInfectionChance, 0f, 1f);
            listing.Gap(12f);

            listing.Label("DeadWorld_Settings_InfectionIncubationDays".Translate(settings.InfectionIncubationDays.ToString("F1")));
            settings.InfectionIncubationDays = listing.Slider(settings.InfectionIncubationDays, 1f, 3f);
            listing.Gap(12f);

            listing.CheckboxLabeled("DeadWorld_Settings_BitingFrenzyEnabled".Translate(), ref settings.BitingFrenzyEnabled);
            listing.Gap(12f);

            listing.CheckboxLabeled("DeadWorld_Settings_ZombiesResurrect".Translate(), ref settings.ZombiesResurrect);
            if (settings.ZombiesResurrect)
            {
                listing.Label("DeadWorld_Settings_ZombieResurrectionMaxDays".Translate(settings.ZombieResurrectionMaxDays.ToString("F1")));
                settings.ZombieResurrectionMaxDays = listing.Slider(settings.ZombieResurrectionMaxDays, 1f, 3f);
            }

            listing.End();
        }
    }
}