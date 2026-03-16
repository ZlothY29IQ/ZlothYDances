using System.Reflection;
using HarmonyLib;

namespace ZlothYDances.Patches
{
    public abstract class HarmonyPatches
    {
        private const   string  InstanceId = Constants.Guid;
        private static Harmony instance;

        private static bool IsPatched { get; set; }

        internal static void ApplyHarmonyPatches()
        {
            if (IsPatched)
                return;

            instance ??= new Harmony(InstanceId);

            instance.PatchAll(Assembly.GetExecutingAssembly());
            IsPatched = true;
        }

        internal static void RemoveHarmonyPatches()
        {
            if (instance == null || !IsPatched)
                return;

            instance.UnpatchSelf();
            IsPatched = false;
        }
    }
}