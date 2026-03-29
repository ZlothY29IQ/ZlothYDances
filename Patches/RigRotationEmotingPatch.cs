using System.Linq;
using Colossal;
using Colossal.MakeItFuckingWork;
using ExitGames.Client.Photon;
using GorillaExtensions;
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

        __instance.transform.rotation = currentRotation;
        RigUtils.Instance.RigRotation = lowerHips.rotation;
    }
}

//Gorilla Track networking, will be replaced with the built-in body tracking once it comes to pcvr
[HarmonyPatch(typeof(VRRig), nameof(VRRig.SerializeWriteShared))]
public class VRRigSerializeWriteSharedPatches
{
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Local
    // ReSharper disable once UnusedParameter.Local
    private static void Postfix(VRRig __instance)
    {
        if (!Plugin.Emoting)
            return;

        int packedHips = BitPackUtils.PackQuaternionForNetwork(RigRotationEmotingPatch.currentRotation);

        PhotonNetwork.RaiseEvent(
                RigRotationEmotingPatch.EmoteEventCode,
                packedHips,
                new RaiseEventOptions
                {
                        TargetActors = PhotonNetwork.PlayerList
                                                    .Where(p => !p.IsLocal)
                                                    .Select(p => p.ActorNumber)
                                                    .ToArray(),
                },
                SendOptions.SendUnreliable
        );
    }
}

[HarmonyPatch(typeof(VRRig), nameof(VRRig.SerializeReadShared))]
public class VRRigSerializeReadSharedPatches
{
    public static void EventReceived(EventData data)
    {
        if (data.Code != RigRotationEmotingPatch.EmoteEventCode)
            return;

        Player sender = PhotonNetwork.NetworkingClient.CurrentRoom.GetPlayer(data.Sender);
        VRRig  rig    = GorillaGameManager.instance.FindPlayerVRRig(sender);
        rig.syncRotation = BitPackUtils.UnpackQuaternionFromNetwork((int)data.CustomData);
    }

    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Local
    private static bool Prefix(VRRig __instance, InputStruct data)
    {
        Hashtable props = __instance.creator.GetPlayerRef().CustomProperties;

        if (!props.ContainsKey(Constants.NetworkKey) || !(bool)props[Constants.NetworkKey])
            return true;

        __instance.head.syncRotation.SetValueSafe(BitPackUtils.UnpackQuaternionFromNetwork(data.headRotation));
        BitPackUtils.UnpackHandPosRotFromNetwork(data.rightHandLong, out __instance.tempVec,
                out __instance.tempQuat);

        __instance.rightHand.syncPos = __instance.tempVec;
        __instance.rightHand.syncRotation.SetValueSafe(in __instance.tempQuat);
        BitPackUtils.UnpackHandPosRotFromNetwork(data.leftHandLong, out __instance.tempVec,
                out __instance.tempQuat);

        __instance.leftHand.syncPos = __instance.tempVec;
        __instance.leftHand.syncRotation.SetValueSafe(in __instance.tempQuat);
        __instance.syncPos  = BitPackUtils.UnpackWorldPosFromNetwork(data.position);
        __instance.handSync = data.handPosition;
        int packedFields = data.packedFields;
        __instance.remoteUseReplacementVoice = (packedFields & 512 /*0x0200*/) != 0;
        __instance.SpeakingLoudness          = (float)(packedFields >> 24 & byte.MaxValue) / byte.MaxValue;
        __instance.UpdateReplacementVoice();
        __instance.UnpackCompetitiveData(data.packedCompetitiveData);
        __instance.taggedById = data.taggedById;
        int num1 = (packedFields & 1024 /*0x0400*/) != 0 ? 1 : 0;
        __instance.grabbedRopeIsPhotonView = (packedFields & 2048 /*0x0800*/) != 0;
        if (num1 != 0)
        {
            __instance.grabbedRopeIndex     = data.grabbedRopeIndex;
            __instance.grabbedRopeBoneIndex = data.ropeBoneIndex;
            __instance.grabbedRopeIsLeft    = data.ropeGrabIsLeft;
            __instance.grabbedRopeIsBody    = data.ropeGrabIsBody;
            __instance.grabbedRopeOffset.SetValueSafe(in data.ropeGrabOffset);
        }
        else
        {
            __instance.grabbedRopeIndex = -1;
        }

        if (num1 == 0 & (packedFields & 32768 /*0x8000*/) != 0)
        {
            __instance.mountedMovingSurfaceId     = data.grabbedRopeIndex;
            __instance.mountedMovingSurfaceIsLeft = data.ropeGrabIsLeft;
            __instance.mountedMovingSurfaceIsBody = data.ropeGrabIsBody;
            __instance.mountedMonkeBlockOffset.SetValueSafe(in data.ropeGrabOffset);
            __instance.movingSurfaceIsMonkeBlock = data.movingSurfaceIsMonkeBlock;
        }
        else
        {
            __instance.mountedMovingSurfaceId = -1;
        }

        int  num2             = (packedFields & 8192 /*0x2000*/) != 0 ? 1 : 0;
        bool isHeldLeftHanded = (packedFields & 16384 /*0x4000*/) != 0;
        if (num2 != 0)
        {
            Quaternion q;
            BitPackUtils.UnpackHandPosRotFromNetwork(data.hoverboardPosRot, out Vector3 localPos, out q);
            Color boardColor = BitPackUtils.UnpackColorFromNetwork(data.hoverboardColor);
            if (q.IsValid())
                __instance.hoverboardVisual.SetIsHeld(isHeldLeftHanded, localPos.ClampMagnitudeSafe(1f), q,
                        boardColor);
        }
        else if (__instance.hoverboardVisual.gameObject.activeSelf)
        {
            __instance.hoverboardVisual.SetNotHeld();
        }

        if ((packedFields & 65536 /*0x010000*/) != 0)
        {
            bool isLeftHand = (packedFields & 131072 /*0x020000*/) != 0;
            BitPackUtils.UnpackHandPosRotFromNetwork(data.propHuntPosRot, out Vector3 localPos, out Quaternion handRot);
            __instance.propHuntHandFollower.SetProp(isLeftHand, localPos, handRot);
        }

        if (__instance.grabbedRopeIsPhotonView)
            __instance.localGrabOverrideBlend = -1f;

        Vector3 position = __instance.transform.position;
        __instance.leftHandLink.Read(__instance.leftHand.syncPos, __instance.syncRotation, position,
                data.isGroundedHand, data.isGroundedButt, (packedFields & 262144 /*0x040000*/) != 0,
                (packedFields & 1048576 /*0x100000*/) != 0, data.leftHandGrabbedActorNumber,
                data.leftGrabbedHandIsLeft);

        __instance.rightHandLink.Read(__instance.rightHand.syncPos, __instance.syncRotation, position,
                data.isGroundedHand, data.isGroundedButt, (packedFields & 524288 /*0x080000*/) != 0,
                (packedFields & 2097152 /*0x200000*/) != 0, data.rightHandGrabbedActorNumber,
                data.rightGrabbedHandIsLeft);

        __instance.LastTouchedGroundAtNetworkTime     = data.lastTouchedGroundAtTime;
        __instance.LastHandTouchedGroundAtNetworkTime = data.lastHandTouchedGroundAtTime;
        __instance.UpdateRopeData();
        __instance.UpdateMovingMonkeBlockData();
        __instance.AddVelocityToQueue(__instance.syncPos, data.serverTimeStamp);

        return false;
    }

    //Not yet implemented
    /*
    [HarmonyPatch(typeof(VRRig), nameof(VRRig.ShouldUseNewIKMethod))]
    private class ForceEmoteIK
    {
        private static bool Prefix(VRRig __instance, ref bool __result)
        {
            Hashtable props = __instance.creator.GetPlayerRef().CustomProperties;

            if (!props.ContainsKey(Constants.NetworkKey) || !(bool)props[Constants.NetworkKey])
                return true;

            __result = true;

            return false;
        }
    }
    */
}