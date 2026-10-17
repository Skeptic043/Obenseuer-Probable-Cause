using System;
using BepInEx;
using HarmonyLib;
#if BEPINEX6
using BaseUnityPlugin = BepInEx.Unity.Mono.BaseUnityPlugin;
#else
using BaseUnityPlugin = BepInEx.BaseUnityPlugin;
#endif

namespace ProbableCause
{
    [BepInPlugin(Guid, Name, Version)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.skeptic043.obenseuer.probablecause";
        public const string Name = "Probable Cause";
        public const string Version = "1.0.1";
        private Harmony harmony;
        private GameMembers game;
        private bool quitting;

        private void Awake()
        {
            // Only an unload before quit (another mod or a scene cleanup destroying the manager) is reported.
            try { UnityEngine.Application.quitting += OnQuitting; }
            catch { }
            try
            {
                string problem = PolicePatches.Resolve(out game);
                if (problem != null)
                {
                    Logger.LogWarning($"Probable Cause {Version} inactive: {problem} (game update?). Police use vanilla rules.");
                    return;
                }
                harmony = new Harmony(Guid);
                PolicePatches.Install(harmony, game);
                PolicePatches.Enabled = true;
                Logger.LogInfo($"Probable Cause {Version}: police and sleep arrests use your bounty only. " +
                    "Carried stolen goods still count once you are questioned or arrested.");
            }
            catch (Exception error)
            {
                PolicePatches.Enabled = false;
                Exception cleanupError = null;
                try { if (harmony != null) PolicePatches.RemoveOwnedPatches(harmony, game); }
                catch (Exception cleanup) { cleanupError = cleanup; }
                Logger.LogError($"Probable Cause {Version} could not install its patches; police use vanilla rules. " +
                    (cleanupError == null ? "" : "Patch cleanup also failed; any remaining patches are disabled. " + cleanupError + " ") + error);
            }
        }

        private void OnQuitting() => quitting = true;

        private void OnDestroy()
        {
            PolicePatches.Enabled = false;
            try { UnityEngine.Application.quitting -= OnQuitting; }
            catch { }
            try
            {
                try { if (harmony != null) PolicePatches.RemoveOwnedPatches(harmony, game); }
                catch (Exception error) { Logger.LogWarning("Could not remove all Probable Cause patches; they are disabled. " + error); }
                if (!quitting && harmony != null) Logger.LogWarning($"Probable Cause {Version} was unloaded; police use vanilla rules.");
            }
            catch { }
        }
    }
}
