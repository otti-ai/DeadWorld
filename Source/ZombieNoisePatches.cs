using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace DeadWorld
{
    [HarmonyPatch(typeof(Apparel), nameof(Apparel.Notify_PawnKilled))]
    internal static class Patch_ZombieApparelTaintedOnDeath
    {
        private static void Postfix(Apparel __instance)
        {
            if (__instance?.Wearer?.def?.defName == "DeadWorld_Zombie")
            {
                __instance.WornByCorpse = true;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Notify_Downed))]
    internal static class Patch_ZombieApparelTaintedWhenDowned
    {
        private static void Postfix(Pawn __instance)
        {
            if (__instance?.def?.defName != "DeadWorld_Zombie" || __instance.apparel == null)
            {
                return;
            }

            foreach (Apparel apparel in __instance.apparel.WornApparel)
            {
                apparel.WornByCorpse = true;
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    internal static class Patch_ZombieMeleeInfection
    {
        private struct InfectionAttempt
        {
            public Pawn Victim;
            public HashSet<Hediff> ExistingHediffs;
        }

        private static void Prefix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target, out InfectionAttempt __state)
        {
            __state = default;
            Pawn attacker = __instance.CasterPawn;
            Pawn victim = target.Pawn;
            if (attacker == null || victim == null || !victim.RaceProps.Humanlike || victim.Dead ||
                attacker.def.defName != "DeadWorld_Zombie" || __instance.verbProps.meleeDamageDef == null)
            {
                return;
            }

            string damageDefName = __instance.verbProps.meleeDamageDef.defName;
            if (damageDefName == "Bite" ||
                (damageDefName == "Scratch" && Rand.Chance(DeadWorldSettings.Current?.ScratchInfectionChance ?? 0.1f)))
            {
                __state = new InfectionAttempt
                {
                    Victim = victim,
                    ExistingHediffs = new HashSet<Hediff>(victim.health.hediffSet.hediffs)
                };
            }
        }

        private static void Postfix(InfectionAttempt __state)
        {
            Pawn victim = __state.Victim;
            if (victim?.health == null || __state.ExistingHediffs == null)
            {
                return;
            }

            List<Hediff> hediffs = victim.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!__state.ExistingHediffs.Contains(hediffs[i]) && hediffs[i] is Hediff_Injury injury && injury.Part != null)
                {
                    ZombieInfection.Apply(victim, injury.Part);
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    internal static class Patch_BitingFrenzyBiteDamage
    {
        private const float InfectionChance = 0.1f;

        private static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            Pawn attacker = __instance.CasterPawn;
            Pawn victim = target.Pawn;
            if (attacker?.mindState?.mentalStateHandler?.CurStateDef?.defName != "DeadWorld_BitingFrenzy" ||
                victim == null || victim == attacker || victim.Dead || !victim.RaceProps.Humanlike)
            {
                return;
            }

            HashSet<Hediff> existingHediffs = new HashSet<Hediff>(victim.health.hediffSet.hediffs);
            victim.TakeDamage(new DamageInfo(DamageDefOf.Bite, 5f, 0f, -1f, attacker));

            if (victim.Dead || !Rand.Chance(InfectionChance))
            {
                return;
            }

            List<Hediff> hediffs = victim.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!existingHediffs.Contains(hediffs[i]) && hediffs[i] is Hediff_Injury injury &&
                    injury.def.defName == "Bite" && injury.Part != null)
                {
                    ZombieInfection.Apply(victim, injury.Part);
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_InfectedPawnDeath
    {
        private static void Postfix(Pawn __instance)
        {
            if (__instance == null || !__instance.Dead || __instance.def.defName != "DeadWorld_Zombie" ||
                !(DeadWorldSettings.Current?.ZombiesResurrect ?? true))
            {
                return;
            }

            HediffDef resurrectionDef = DefDatabase<HediffDef>.GetNamed("DeadWorld_ZombieResurrection", false);
            if (resurrectionDef != null && !__instance.health.hediffSet.HasHediff(resurrectionDef))
            {
                __instance.health.AddHediff(HediffMaker.MakeHediff(resurrectionDef, __instance));
            }
        }

    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    internal static class Patch_ZombieRetargetOnPlayerAttack
    {
        private static void Prefix(Pawn __instance, ref DamageInfo dinfo)
        {
            Pawn attacker = dinfo.Instigator as Pawn;
            if (__instance?.def?.defName != "DeadWorld_Zombie" || __instance.Dead ||
                attacker == null || attacker == __instance || attacker.Dead || attacker.Faction != Faction.OfPlayer)
            {
                return;
            }

            __instance.mindState.enemyTarget = attacker;
            __instance.mindState.lastEngageTargetTick = Find.TickManager.TicksGame;
        }
    }

    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    internal static class Patch_TickManager_ProcessZombieNoiseRallies
    {
        private static void Postfix()
        {
            ZombieNoise.ProcessPendingRallies();
        }
    }

    [HarmonyPatch(typeof(Corpse), "SpawnSetup", new[] { typeof(Map), typeof(bool) })]
    internal static class Patch_InfectedCorpseSpawn
    {
        private static void Postfix(Corpse __instance, bool respawningAfterLoad)
        {
            if (!respawningAfterLoad)
            {
                return;
            }

            Pawn innerPawn = __instance?.InnerPawn;
            Hediff_ZombieInfection infection = innerPawn?.health?.hediffSet.GetFirstHediff<Hediff_ZombieInfection>();
            if (infection != null)
            {
                infection.CheckCorpseReanimation();
            }
        }
    }

    [HarmonyPatch(typeof(Corpse), "TickRare")]
    internal static class Patch_ZombieCorpseResurrectionTick
    {
        private static void Postfix(Corpse __instance)
        {
            Hediff_ZombieInfection infection = __instance?.InnerPawn?.health?.hediffSet.GetFirstHediff<Hediff_ZombieInfection>();
            infection?.CheckCorpseReanimation();
            if (__instance?.InnerPawn?.Dead != true)
            {
                return;
            }

            Hediff_ZombieResurrection timer = __instance?.InnerPawn?.health?.hediffSet.GetFirstHediff<Hediff_ZombieResurrection>();
            timer?.CheckResurrection();
        }
    }

    [HarmonyPatch(typeof(GenClamor), nameof(GenClamor.DoClamor), new[] { typeof(Thing), typeof(float), typeof(ClamorDef) })]
    internal static class Patch_GenClamor_DoClamor
    {
        private static void Postfix(Thing source, ClamorDef type)
        {
            if (source != null && source.MapHeld != null && type != ClamorDefOf.Movement)
            {
            ZombieNoise.Record(source.MapHeld, source.PositionHeld);
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), "Launch", new[]
    {
        typeof(Thing), typeof(UnityEngine.Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo),
        typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef)
    })]
    internal static class Patch_Projectile_Launch_RecordNoise
    {
        private static void Postfix(Thing launcher)
        {
            if (launcher?.MapHeld != null)
            {
                ZombieNoise.Record(launcher.MapHeld, launcher.PositionHeld);
            }
        }
    }

    [HarmonyPatch(typeof(Verb_Shoot), "TryCastShot")]
    internal static class Patch_VerbShoot_TryCastShot
    {
        private static void Postfix(Verb_Shoot __instance, bool __result)
        {
            if (!__result || !__instance.CasterIsPawn)
            {
                return;
            }

            Pawn shooter = __instance.CasterPawn;
            if (shooter != null && shooter.Spawned)
            {
                ZombieNoise.Record(shooter.Map, shooter.Position);
            }
        }
    }

    [HarmonyPatch(typeof(GenExplosion), nameof(GenExplosion.DoExplosion))]
    internal static class Patch_GenExplosion_DoExplosion
    {
        private static void Postfix(IntVec3 center, Map map, float radius, bool doSoundEffects)
        {
            if (doSoundEffects)
            {
                ZombieNoise.Record(map, center);
            }
        }
    }
}