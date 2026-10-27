using System.Reflection;
using System.Security.Cryptography;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Characters;

namespace SilentMovementCompat;

/// <summary>Lets MoreSilentAnimations own the Silent body during attack motion.</summary>
#if !INTEGRATED
[ModInitializer(nameof(Initialize))]
#endif
public static class Entry
{
    /// <summary>Checks audited binaries before patching one MeleeAttack prefix.</summary>
    public static void Initialize()
    {
        var expected = new Dictionary<string, string>
        {
            ["MeleeAttack"] = "7BB0DE0F078D97EDFEBB0D4494B5B0A1224CF75DA940971EC6106870F4AF663B",
            ["MoreSilentAnimations"] = "1A86D4BFB5AA9260BF3F22E188DD50D43B9C2ACA39929A20FCC3D2C315956E41"
        };
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var pair in expected)
        {
            var assembly = assemblies.SingleOrDefault(a => a.GetName().Name == pair.Key);
            if (assembly is null || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))) != pair.Value)
            {
                GD.PushWarning("[SilentMovementCompat] Unsupported or missing " + pair.Key + "; no patch applied.");
                return;
            }
        }
        var melee = assemblies.Single(a => a.GetName().Name == "MeleeAttack");
        var target = AccessTools.Method(melee.GetType("MeleeAttack.SimpleTeleportPatch+CreatureCmdTriggerAnimPatch"), "Prefix")
                     ?? throw new MissingMethodException("MeleeAttack attack-animation prefix");
        new Harmony("local.sts2.silent-movement-compat").Patch(target, prefix: new HarmonyMethod(typeof(Entry), nameof(AllowOriginalForSilent)));
        GD.Print("[SilentMovementCompat] Loaded: Silent attack body/movement delegated to MoreSilentAnimations.");
    }

    private static bool AllowOriginalForSilent(Creature __0, ref bool __result)
    {
        if (__0?.Player?.Character is not Silent || __0.Player.Creature != __0) return true;
        // Return true from Melee's prefix: allow the native animation chain,
        // without Melee resetting alpha or taking over movement first.
        __result = true;
        return false;
    }
}
