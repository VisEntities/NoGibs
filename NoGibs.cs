/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using HarmonyLib;
using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Oxide.Plugins
{
    [Info("No Gibs", "VisEntities", "1.0.0")]
    [Description("Prevents debris from spawning when entities decay, are killed by admins, demolished, or collapsed due to stability.")]
    public class NoGibs : RustPlugin
    {
        #region Fields

        private static NoGibs _plugin;
        private static Configuration _config;
        private Harmony _harmony;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Disable Debris Spawn For Decaying Entities")]
            public bool DisableDebrisSpawnForDecayingEntities { get; set; }

            [JsonProperty("Disable Debris Spawn For Admin Kills")]
            public bool DisableDebrisSpawnForAdminKills { get; set; }

            [JsonProperty("Disable Debris Spawn For Demolished Structures")]
            public bool DisableDebrisSpawnForDemolishedStructures { get; set; }

            [JsonProperty("Disable Debris Spawn For Stability Collapse")]
            public bool DisableDebrisSpawnForStabilityCollapse { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                DisableDebrisSpawnForDecayingEntities = true,
                DisableDebrisSpawnForAdminKills = true,
                DisableDebrisSpawnForDemolishedStructures = true,
                DisableDebrisSpawnForStabilityCollapse = true
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            _harmony = new Harmony(Name + "PATCH");
            _harmony.PatchAll();
        }

        private void Unload()
        {
            _harmony.UnpatchAll(Name + "PATCH");
            _config = null;
            _plugin = null;
        }

        private object OnAdminKill(BaseNetworkable baseNetworkable)
        {
            if (baseNetworkable == null)
                return null;

            if (!_config.DisableDebrisSpawnForAdminKills)
                return null;

            baseNetworkable.Kill(BaseNetworkable.DestroyMode.None);
            return true;
        }

        private object OnStructureDemolish(StabilityEntity stabilityEntity, BasePlayer player)
        {
            if (stabilityEntity == null || player == null)
                return null;

            if (!_config.DisableDebrisSpawnForDemolishedStructures)
                return null;

            stabilityEntity.Kill(BaseNetworkable.DestroyMode.None);
            return true;
        }

        private object OnDecayEntityKilled(DecayEntity decayEntity)
        {
            if (decayEntity == null)
                return null;

            if (!_config.DisableDebrisSpawnForDecayingEntities)
                return null;

            decayEntity.Kill(BaseNetworkable.DestroyMode.None);
            return true;
        }

        #endregion Oxide Hooks

        #region Harmony Patches

        [HarmonyPatch(typeof(DecayEntity), "OnKilled")]
        public static class DecayEntity_OnKilled_Patch
        {
            public static bool Prefix(DecayEntity __instance)
            {
                if (Interface.CallHook("OnDecayEntityKilled", __instance) != null)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(StabilityEntity), "StabilityCheck")]
        public static class StabilityEntity_StabilityCheck_Patch
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codeInstructions = new List<CodeInstruction>(instructions);
                var methodKill = AccessTools.Method(typeof(BaseNetworkable), "Kill");

                for (int instructionIndex = 0; instructionIndex < codeInstructions.Count; instructionIndex++)
                {
                    if (codeInstructions[instructionIndex].opcode == OpCodes.Call && codeInstructions[instructionIndex].operand.Equals(methodKill))
                    {
                        if (_config.DisableDebrisSpawnForStabilityCollapse)
                            codeInstructions[instructionIndex - 1] = new CodeInstruction(OpCodes.Ldc_I4_0);
                    }
                }

                return codeInstructions;
            }
        }

        [HarmonyPatch(typeof(BaseNetworkable), "AdminKill")]
        public static class BaseNetworkable_AdminKill_Patch
        {
            public static bool Prefix(BaseNetworkable __instance)
            {
                if (Interface.CallHook("OnAdminKill", __instance) != null)
                {
                    return false;
                }

                return true;
            }
        }

        #endregion Harmony Patches
    }
}