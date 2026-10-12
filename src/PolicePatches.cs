using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ProbableCause
{
    internal sealed class GameMembers
    {
        // Every Crime.CountCrimes overload that takes the native countStolenGoods flag.
        internal MethodInfo[] CountCrimes;
        // Police and arrest decisions during which carried stolen goods are left out.
        internal MethodInfo[] Decisions;
        internal IEnumerable<MethodInfo> Targets => CountCrimes.Concat(Decisions);
    }

    // Open while a police or arrest decision runs; per thread, so other threads keep vanilla counts.
    internal static class Scope
    {
        [ThreadStatic] private static int depth;
        internal static int Depth => depth;

        internal static void Enter(out bool __state)
        {
            depth++;
            __state = true;
        }

        // Runs even when the decision throws or another mod's prefix skipped ours (then __state stays false).
        internal static void Exit(bool __state)
        {
            if (__state && depth > 0) depth--;
        }
    }

    internal static class PolicePatches
    {
        internal const string StolenGoodsFlag = "countStolenGoods";
        internal static bool Enabled;

        private static readonly (string type, string method)[] DecisionNames =
        {
            ("NPC.Act_Police", "OnPlayerDetected"),
            ("NPC.Act_Police", "OnPlayerArrested"),
            ("NPC.Act_Police", "UnregisterDialogue"),
            ("Crime", "CheckCrimesForArrest"),
        };

        // Game members are looked up by name once, so a game update can only make the mod inactive.
        internal static string Resolve(out GameMembers found)
        {
            found = null;
            try
            {
                return Find(Type.GetType("Crime, Assembly-CSharp", false),
                    Type.GetType("NPC.Act_Police, Assembly-CSharp", false), out found);
            }
            catch (Exception error) { return "member lookup failed: " + error.Message; }
        }

        internal static string Find(Type crime, Type police, out GameMembers found)
        {
            found = null;
            if (crime == null) return "type Crime is missing";
            if (police == null) return "type NPC.Act_Police is missing";

            var counts = Methods(crime, "CountCrimes");
            if (counts.Length == 0) return "Crime.CountCrimes is missing";
            var flagged = counts.Where(HasStolenGoodsFlag).ToArray();
            if (flagged.Length == 0) return "Crime.CountCrimes has no bool " + StolenGoodsFlag + " parameter";

            var decisions = new List<MethodInfo>();
            foreach (var (typeName, methodName) in DecisionNames)
            {
                var type = typeName == "Crime" ? crime : police;
                var methods = Methods(type, methodName);
                if (methods.Length == 0) return typeName + "." + methodName + " is missing";
                decisions.AddRange(methods);
            }
            found = new GameMembers { CountCrimes = flagged, Decisions = decisions.ToArray() };
            return null;
        }

        private static bool HasStolenGoodsFlag(MethodInfo method)
            => method.GetParameters().Any(p => p.Name == StolenGoodsFlag && p.ParameterType == typeof(bool));

        private static MethodInfo[] Methods(Type type, string name)
            => type.GetMethods(AccessTools.allDeclared)
                .Where(m => m.Name == name && !m.IsAbstract && !m.ContainsGenericParameters && m.GetMethodBody() != null).ToArray();

        // Harmony binds this parameter by name to the native flag.
        internal static void BeforeCountCrimes(ref bool countStolenGoods)
        {
            if (Enabled && Scope.Depth > 0) countStolenGoods = false;
        }

        // All or nothing: a failure removes whatever this instance already patched.
        internal static void Install(Harmony harmony, GameMembers game)
        {
            var count = new HarmonyMethod(typeof(PolicePatches), nameof(BeforeCountCrimes));
            var enter = new HarmonyMethod(typeof(Scope), nameof(Scope.Enter));
            var exit = new HarmonyMethod(typeof(Scope), nameof(Scope.Exit));
            try
            {
                foreach (var method in game.CountCrimes) harmony.Patch(method, prefix: count);
                foreach (var method in game.Decisions) harmony.Patch(method, prefix: enter, finalizer: exit);
            }
            catch (Exception error)
            {
                try { RemoveOwnedPatches(harmony, game); }
                catch (Exception cleanup) { throw new AggregateException("Probable Cause patch install failed and cleanup was incomplete.", error, cleanup); }
                throw;
            }
        }

        internal static void RemoveOwnedPatches(Harmony harmony, GameMembers game)
        {
            // Visit the known targets explicitly; a wrapper that failed to generate can keep patch records.
            if (game == null) return;
            var errors = new List<Exception>();
            foreach (var method in game.Targets)
            {
                try { harmony.Unpatch(method, HarmonyPatchType.All, harmony.Id); }
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count != 0) throw new AggregateException("Could not remove all Probable Cause patch records.", errors);
        }
    }
}
