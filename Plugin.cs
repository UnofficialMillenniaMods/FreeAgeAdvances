using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CPrompt;
using HarmonyLib;


namespace UnrestrictedAgeAdvances
{
    [BepInPlugin("UnrestrictedAgeAdvances", "UnrestrictedAgeAdvances", "1.1.0")]
    [BepInProcess("Millennia.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public ConfigEntry<bool> configAllowAlternateAgeAdvance;
        public ConfigEntry<int> configRemoveAgeAdvanceRestrictions;
        public ConfigEntry<bool> configDisableCrisisAgeLock;

        public static Plugin Instance;

        private void Awake()
        {
            Instance = this;

            // Plugin startup logic
            Logger.LogInfo($"Plugin UnrestrictedAgeAdvances is getting intiated!");

            // config
            configAllowAlternateAgeAdvance = Config.Bind(
                "General",// The section under which the option is shown
                "AllowAlternateAgeAdvance",  // The key of the configuration option in the configuration file
                true, // The default value
                "Allow advancing from alternate ages to alternate ages" // Description of the option to show in the config file
            );

            configRemoveAgeAdvanceRestrictions = Config.Bind(
                "General",
                "RemoveAgeAdvanceRestrictions",
                0,
                "Remove the restrictions to advance into a specific age.\n" +
                "0 - disabled\n" +
                "1 - remove restrictions of variant and victory ages\n" +
                "2 - remove resitrictions for all ages (disables crisis age lock)"
            );
            configDisableCrisisAgeLock = Config.Bind(
                "General",
                "DisableCrisisAgeLock",
                false,
                "Disables age locking caused by triggering crisis conditions"
            );

            Logger.LogInfo($"AllowAlternateAgeAdvance: {configAllowAlternateAgeAdvance.Value}");
            Logger.LogInfo($"RemoveAgeAdvanceRestrictions: {configRemoveAgeAdvanceRestrictions.Value}");
            Logger.LogInfo($"DisableCrisisAgeLock: {configDisableCrisisAgeLock.Value}");

            Logger.LogInfo($"Plugin UnrestrictedAgeAdvances is loaded!");

            // apply patches
            DoPatching();
            Logger.LogInfo($"Plugin UnrestrictedAgeAdvances finished patching!");
        }

        public void DoPatching()
        {
            var harmony = new Harmony("UnrestrictedAgeAdvances");

            if (configAllowAlternateAgeAdvance.Value || configRemoveAgeAdvanceRestrictions.Value != 0)
            {
                harmony.PatchAll();
            }
        }
    }

    [HarmonyPatch(typeof(ATechManager), nameof(ATechManager.GetResearchedTechCountFromBase))]
    class ResearchTechCountPatch
    {
        public static void Prefix(ref string baseTech, APlayer plr)
        {
            if (!plr.HasResearched(baseTech))
            {
                int ageNum = ATechManager.Instance.GetAgeForBaseTech(baseTech);
                baseTech = ATechManager.Instance.GetChosenAge(ageNum - 1);
            }
        }
    }

    [HarmonyPatch(typeof(AResearchDialog), "SetupAndValidate")]
    class AgePatch
    {
        static ManualLogSource MLogger;
        public static void Postfix()
        {
            MLogger = Logger.CreateLogSource("AResearchDialog.SetupAndValidate");
            // get the first age, then map from there until the last age
            List<ACard> currentAges = new List<ACard>
            {
                ATechManager.Instance.GetBaseTechFromChosenAge(0)
            };
            while (currentAges.Count > 0)
            {
                try
                {
                    currentAges = patchAges(currentAges);
                }
                catch (Exception e)
                {
                    MLogger.LogError(e.ToString());
                    break;
                }
            }
            Logger.Sources.Remove(MLogger);
        }

        public static List<ACard> patchAges(List<ACard> currentAges)
        {
            // current age: next ages
            Dictionary<ACard, List<ACard>> ageAdvanceMap = new Dictionary<ACard, List<ACard>>();
            // next age: previous age advance card
            Dictionary<ACard, ACard> advanceMap = new Dictionary<ACard, ACard>(); ;

            // get all possible next ages & remove the requirements
            foreach (ACard age in currentAges)
            {
                MLogger.LogInfo($"  Processing base age: {age.ID}");
                List<ACard> availableNextAges = new List<ACard>();
                ageAdvanceMap.Add(age, availableNextAges);

                foreach (ACard ageAdvance in ATechManager.Instance.GetPossibleAgesFrom(age))
                {
                    MLogger.LogInfo($"    Age Advance Tech found: {ageAdvance.ID}");
                    ACard nextAge = ATechManager.Instance.GetAgeTechFromAdvanceTech(ageAdvance);
                    MLogger.LogInfo($"    Target Age: {nextAge.ID}");
                    availableNextAges.Add(nextAge);

                    if (
                        (Plugin.Instance.configRemoveAgeAdvanceRestrictions.Value == 1 && !ATechManager.Instance.DoesAgeBaseHaveTag(nextAge, ATechManager.cCardTagAgeCrisis)) ||
                        (Plugin.Instance.configRemoveAgeAdvanceRestrictions.Value == 2)
                    )
                    {
                        MLogger.LogInfo("    Removing requirements");
                        // MLogger.LogInfo(String.Join(", ", nextAge.CardTags.Tags));
                        // remove the base requirement, e.g. killing 6 units for blood age
                        ageAdvance.Choices[0].Requirements = new List<ACardRequirement>();
                    }


                    if (!advanceMap.ContainsKey(nextAge))
                    {
                        MLogger.LogInfo($"    Registering {ageAdvance.ID} as advance card for {nextAge.ID}");
                        advanceMap.Add(nextAge, ageAdvance);
                    }
                }
            }

            if (Plugin.Instance.configAllowAlternateAgeAdvance.Value)
            {
                // add all age advance to the current ages
                // advance:
                //  key - next age
                //  value - advance tech card
                foreach (var advance in advanceMap)
                {
                    // age advance:
                    //  key - current age
                    //  value(s) - next ages
                    foreach (var ageAdvance in ageAdvanceMap)
                    {
                        ADeck ageDeckFromTechName = ATechManager.Instance.GetAgeDeckFromTechName(ageAdvance.Key.ID);

                        if (!ageAdvance.Value.Contains(advance.Key))
                        {
                            MLogger.LogInfo($"  Adding {advance.Value.ID}  to {ageAdvance.Key.ID}");
                            ageDeckFromTechName.AddCardCopy(advance.Value, DeckZone.DZ_Definition);
                        }

                    }
                }
            }

            return advanceMap.Keys.ToList<ACard>();
        }
    }

    [HarmonyPatch(typeof(ATechManager), "IsCrisisLocked")]
    class DisableCrisisAgeLock
    {
        static void Postfix(ref bool __result)
        {
            if (__result && (
                Plugin.Instance.configDisableCrisisAgeLock.Value || Plugin.Instance.configRemoveAgeAdvanceRestrictions.Value == 2
             ))
            {
                var MLogger = Logger.CreateLogSource("PreventForcedFutureAge");
                MLogger.LogInfo("Preventing crisis lock!");
                __result = false;
                Logger.Sources.Remove(MLogger);
            }
        }
    }
}
