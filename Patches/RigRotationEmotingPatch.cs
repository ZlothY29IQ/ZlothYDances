using System.Linq;
using Colossal;
using Colossal.MakeItFuckingWork;
using ExitGames.Client.Photon;
using GorillaExtensions;
using GorillaLocomotion;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace ZlothYDances.Patches;

[HarmonyPatch(typeof(VRRig), nameof(VRRig.PostTick))]
public static class RigRotationEmotingPatch
{
    private const float      RotationSpeed  = 10f;
    public const  byte       EmoteEventCode = 189;
    public static Quaternion currentRotation;
    public static Transform  hips;
    public static Transform  lowerHips;

    private static void Postfix(VRRig __instance)
    {
        if (!__instance.isLocal || !Plugin.Emoting)
            return;

        //If you want less movement and the origin to be based from the chest then use this one here
        hips = AssetBundleLoader.KyleRobot
                                .transform.Find("ROOT/Hips/Spine1/Spine2");

        //Getting lower hip for more movement
        lowerHips = AssetBundleLoader.KyleRobot.transform.Find("ROOT/Hips");

        if (hips == null || lowerHips == null) 
            return;

        Quaternion zOffset        = Quaternion.Euler(0f, 0f, 90f);
        Quaternion targetRotation = lowerHips.rotation * zOffset;

        currentRotation = Quaternion.Slerp(
                currentRotation == default(Quaternion) ? targetRotation : currentRotation,
                targetRotation,
                Time.deltaTime * RotationSpeed
        );

        __instance.transform.rotation    = currentRotation;
        RigUtils.Instance.RigRotation    = currentRotation;
    }
}

// Old networking not needed - GTPlayerTransform.UseNetRotation = true;