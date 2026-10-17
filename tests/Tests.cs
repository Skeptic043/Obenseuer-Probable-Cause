using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using NPC;
using ProbableCause;

internal static class Tests
{
    private static int passed;
    private static readonly List<float> counted = new List<float>();

    internal static int Run()
    {
        Console.WriteLine("BepInEx: " + typeof(BepInEx.BepInPlugin).Assembly.FullName);
        Console.WriteLine("Harmony: " + typeof(Harmony).Assembly.FullName);
        Console.WriteLine("Game: " + typeof(Crime).Assembly.Location);
        LoaderTests();
        ResolutionTests();
        PatchedBehaviorTests();
        Console.WriteLine($"PASS: {passed} checks. Real game methods patched and run on desktop .NET Framework with synthetic objects. " +
            "Crime.CheckCrimesForArrest is resolved but not patched or run offline. No Unity native runtime or gameplay executed.");
        return 0;
    }

    private static void LoaderTests()
    {
        Check("selected loader references, Harmony, plugin base and version", () =>
        {
            var pluginAssembly = typeof(Plugin).Assembly;
            var references = pluginAssembly.GetReferencedAssemblies();
            var loaderReferences = references.Where(reference => reference.Name.StartsWith("BepInEx", StringComparison.OrdinalIgnoreCase)).ToArray();
            var harmony = references.Single(reference => reference.Name == "0Harmony");
#if BEPINEX6
            True(loaderReferences.Length == 2);
            True(loaderReferences.Any(reference => reference.Name == "BepInEx.Core" && reference.Version == new Version(6, 0, 0, 0)));
            True(loaderReferences.Any(reference => reference.Name == "BepInEx.Unity.Mono" && reference.Version == new Version(6, 0, 0, 0)));
            True(!loaderReferences.Any(reference => reference.Name == "BepInEx"));
            True(harmony.Version == new Version(2, 10, 2, 0));
            True(typeof(Plugin).BaseType == typeof(BepInEx.Unity.Mono.BaseUnityPlugin));
#else
            True(loaderReferences.Length == 1);
            var loader = loaderReferences.Single();
            True(loader.Name == "BepInEx" && loader.Version == new Version(5, 4, 23, 5));
            True(!loaderReferences.Any(reference => reference.Name == "BepInEx.Core" || reference.Name == "BepInEx.Unity.Mono"));
            True(harmony.Version == new Version(2, 9, 0, 0));
            True(typeof(Plugin).BaseType == typeof(BepInEx.BaseUnityPlugin));
#endif
            True(Plugin.Guid == "com.skeptic043.obenseuer.probablecause");
            True(Plugin.Version == "1.0.1");
            True(pluginAssembly.GetName().Version == new Version(1, 0, 1, 0));
        });
    }

    private static void ResolutionTests()
    {
        Check("no compile-time game reference; game members are resolved by name", () =>
            True(!typeof(Plugin).Assembly.GetReferencedAssemblies().Any(reference => reference.Name == "Assembly-CSharp")));
        Check("installed game resolves every target", () =>
        {
            True(PolicePatches.Resolve(out var game) == null);
            True(game.CountCrimes.Contains(AccessTools.Method(typeof(Crime), nameof(Crime.CountCrimes))));
            foreach (var method in new[]
            {
                AccessTools.Method(typeof(Act_Police), nameof(Act_Police.OnPlayerDetected)),
                AccessTools.Method(typeof(Act_Police), nameof(Act_Police.OnPlayerArrested)),
                AccessTools.Method(typeof(Act_Police), nameof(Act_Police.UnregisterDialogue)),
                AccessTools.Method(typeof(Crime), nameof(Crime.CheckCrimesForArrest)),
            }) True(method != null && game.Decisions.Contains(method));
        });
        Check("changed or missing members make the mod inactive with a reason", () =>
        {
            Inactive(PolicePatches.Find(typeof(RenamedFlagCrime), typeof(Act_Police), out var game), game, "countStolenGoods");
            Inactive(PolicePatches.Find(typeof(NoCountCrime), typeof(Act_Police), out game), game, "CountCrimes is missing");
            Inactive(PolicePatches.Find(typeof(Crime), typeof(NoArrestPolice), out game), game, "OnPlayerArrested is missing");
            Inactive(PolicePatches.Find(null, typeof(Act_Police), out game), game, "Crime is missing");
        });
    }

    private static void PatchedBehaviorTests()
    {
        True(PolicePatches.Resolve(out var resolved) == null);
        // Crime.CheckCrimesForArrest calls UnityEngine.Random's native method directly, so desktop .NET
        // cannot compile, patch or run it. Every other resolved target goes through the real install path.
        var checkCrimes = AccessTools.Method(typeof(Crime), nameof(Crime.CheckCrimesForArrest));
        var game = new GameMembers { CountCrimes = resolved.CountCrimes, Decisions = resolved.Decisions.Where(m => m != checkCrimes).ToArray() };
        var countCrimes = AccessTools.Method(typeof(Crime), nameof(Crime.CountCrimes));
        // Another owner on CountCrimes: records each completed count and must survive our uninstall.
        var recorder = new Harmony("probablecause.tests.recorder");
        recorder.Patch(countCrimes, postfix: new HarmonyMethod(typeof(Tests), nameof(RecordCount)));
        var harmony = new Harmony(Plugin.Guid);
        try
        {
            PolicePatches.Install(harmony, game);
            PolicePatches.Enabled = true;
            True((object)Inventory.instance == null);
            var crime = NewCrime(40, 60);
            Crime.instance = crime;
            var police = (Act_Police)FormatterServices.GetUninitializedObject(typeof(Act_Police));

            // Offline, any UnityEngine.Object comparison throws because its native static constructor cannot run.
            Check("arrest bookkeeping counts the bounty without reading the inventory", () =>
            {
                // OnPlayerArrested counts first, then compares its NPC (the offline throw).
                Throws<TypeInitializationException>(() => police.OnPlayerArrested());
                Counts(100f);
            });
            Check("direct counts outside a decision keep the vanilla stolen-goods path", () => OutsideDecisionIsVanilla(crime));
            Check("a decision that throws releases its scope", () =>
            {
                // OnPlayerDetected compares Crime.instance before counting, so it throws before CountCrimes.
                Throws<TypeInitializationException>(() => police.OnPlayerDetected(null, null));
                Counts();
                // OnPlayerArrested throws after CountCrimes ran inside the scope.
                Throws<TypeInitializationException>(() => police.OnPlayerArrested());
                Counts(100f);
                OutsideDecisionIsVanilla(crime);
            });
            Check("uninstall removes only this mod's patches and restores vanilla decisions", () =>
            {
                PolicePatches.RemoveOwnedPatches(harmony, game);
                foreach (var method in resolved.Targets) True(!Owns(method, harmony));
                True(Owns(countCrimes, recorder));
                Throws<NullReferenceException>(() => police.OnPlayerArrested());
                Counts();
            });
            Check("failed install removes the patches it already applied", () =>
            {
                var failing = new Harmony("probablecause.tests.failing");
                var broken = new GameMembers
                {
                    CountCrimes = game.CountCrimes,
                    Decisions = game.Decisions.Append(AccessTools.Method(typeof(Unpatchable), nameof(Unpatchable.Decide))).ToArray(),
                };
                Throws<Exception>(() => PolicePatches.Install(failing, broken));
                foreach (var method in resolved.Targets) True(!Owns(method, failing));
                True(Owns(countCrimes, recorder));
                Throws<NullReferenceException>(() => police.OnPlayerArrested());
                Counts();
            });
        }
        finally
        {
            PolicePatches.Enabled = false;
            Crime.instance = null;
            harmony.UnpatchSelf();
            recorder.UnpatchSelf();
        }
    }

    // Vanilla CountCrimes reads Inventory.instance for stolen goods; offline it is null, so the count throws.
    private static void OutsideDecisionIsVanilla(Crime crime)
    {
        Throws<NullReferenceException>(() => crime.CountCrimes());
        Throws<NullReferenceException>(() => crime.CountCrimes(true, 100f, 10));
        Counts();
        Equal(100f, crime.CountCrimes(false));
        Counts(100f);
    }

    private static Crime NewCrime(params int[] bounties)
    {
        var crime = (Crime)FormatterServices.GetUninitializedObject(typeof(Crime));
        crime.crimeRecord = bounties.Select(amount => new Crime.CrimeRecord(amount, Crime.CrimeType.Theft)).ToList();
        return crime;
    }

    private static void RecordCount(float __result) => counted.Add(__result);

    private static void Counts(params float[] expected)
    {
        try
        {
            if (counted.Count != expected.Length) throw new Exception($"Expected {expected.Length} completed counts, got {counted.Count}.");
            for (int i = 0; i < expected.Length; i++) Equal(expected[i], counted[i]);
        }
        finally { counted.Clear(); }
    }

    private static void Inactive(string problem, GameMembers game, string reason)
    {
        if (problem == null || !problem.Contains(reason)) throw new Exception($"Expected a reason containing '{reason}', got '{problem}'.");
        True(game == null);
    }

    private static bool Owns(MethodBase method, Harmony harmony) => Harmony.GetPatchInfo(method)?.Owners.Contains(harmony.Id) ?? false;

    private static void Check(string name, Action action) { counted.Clear(); action(); passed++; Console.WriteLine("PASS " + name); }
    private static void True(bool condition) { if (!condition) throw new Exception("Assertion failed."); }
    private static void Equal(float expected, float actual) { if (Math.Abs(expected - actual) > 0.0001f) throw new Exception($"Expected {expected}, got {actual}."); }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("Expected " + typeof(T).Name + ".");
    }

    // Lookalikes for changed game shapes.
    private sealed class RenamedFlagCrime
    {
        public float CountCrimes(bool includeStolen = true, float valueThreshold = 0f, int itemCountThreshold = 0) => includeStolen ? 1f : 0f;
        public bool CheckCrimesForArrest(float chance = 0.25f) => chance > 1f;
    }

    private sealed class NoCountCrime
    {
        public bool CheckCrimesForArrest(float chance = 0.25f) => chance > 1f;
    }

    private sealed class NoArrestPolice
    {
        public void OnPlayerDetected(UnityEngine.GameObject npc, NPCReference reference) { }
        public void UnregisterDialogue() { }
    }

    private abstract class Unpatchable
    {
        public abstract void Decide();
    }
}
