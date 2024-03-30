using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using CPrompt;
using HarmonyLib;


namespace MyFirstPlugin
{
    [BepInPlugin("UnrestrictedAgeAdvances", "UnrestrictedAgeAdvances", "1.0.0")]
    [BepInProcess("Millennia.exe")]
    public class Plugin : BaseUnityPlugin
    {
        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin UnrestrictedAgeAdvances is loaded!");
            DoPatching();
        }

        public static void DoPatching()
        {
            var harmony = new Harmony("UnrestrictedAgeAdvances");
            harmony.PatchAll();
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
        static ManualLogSource Logger;
        public static void Postfix()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource("AResearchDialog.SetupAndValidate");
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
                    Logger.LogError(e.ToString());
                    break;
                }
            }
            BepInEx.Logging.Logger.Sources.Remove(Logger);
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
                Logger.LogInfo($"Processing base age: {age.ID}");
                List<ACard> availableNextAges = new List<ACard>();
                ageAdvanceMap.Add(age, availableNextAges);

                foreach (ACard ageAdvance in ATechManager.Instance.GetPossibleAgesFrom(age))
                {
                    Logger.LogInfo($"Age Advance Tech found: {ageAdvance.ID}");
                    ACard nextAge = ATechManager.Instance.GetAgeTechFromAdvanceTech(ageAdvance);
                    Logger.LogInfo($"Target Age: {nextAge.ID}");
                    availableNextAges.Add(nextAge);

                    // Logger.LogInfo("Removing requirements");
                    // remove the base requirement, e.g. killing 6 units for blood age
                    // ageAdvance.Choices[0].Requirements = new List<ACardRequirement>();

                    if (!advanceMap.ContainsKey(nextAge))
                    {
                        Logger.LogInfo($"Registering {ageAdvance.ID} as advance card for {nextAge.ID}");
                        advanceMap.Add(nextAge, ageAdvance);
                    }
                }
            }

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
                        Logger.LogInfo($"Adding {advance.Value.ID}  to {ageAdvance.Key.ID}");
                        ageDeckFromTechName.AddCardCopy(advance.Value, DeckZone.DZ_Definition);
                    }

                }
            }
            return advanceMap.Keys.ToList<ACard>();
        }
    }

    //[HarmonyPatch(typeof(AResearchPresentAge), "SetForcedFutureAge")]
    //class PreventForcedFutureAge
    //{
    //    static void Prefix(ref string forcedFutureAgeName)
    //    {
    //        var logger = BepInEx.Logging.Logger.CreateLogSource("AResearchPresentAge.SetForcedFutureAge");
    //        logger.LogInfo($"Preventing forced future age: {forcedFutureAgeName}");
    //        forcedFutureAgeName = "";
    //        BepInEx.Logging.Logger.Sources.Remove(logger);
    //    }
    //}
}
