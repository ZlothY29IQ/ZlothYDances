using System;
using HarmonyLib;
using Photon.Voice;
using Photon.Voice.Unity;
using ZlothYDances.MakeItFuckingWork;

namespace ZlothYDances.Patches;

[HarmonyPatch(typeof(Recorder))]
public class RecorderPatches
{
    [HarmonyPatch(nameof(Recorder.SourceType), MethodType.Getter)]
    public static bool Prefix(ref Recorder.InputSourceType __result)
    {
        __result = Recorder.InputSourceType.Factory;

        return false;
    }

    [HarmonyPatch(nameof(Recorder.InputFactory), MethodType.Getter)]
    public static bool Prefix(ref Func<IAudioDesc> __result)
    {
        __result = () => EmoteAudioReader.Get();

        return false;
    }

    [HarmonyPatch(nameof(Recorder.CreateLocalVoiceAudioAndSource))]
    public static bool Prefix(Recorder __instance)
    {
        __instance.SourceType   = Recorder.InputSourceType.Factory;
        __instance.InputFactory = () => EmoteAudioReader.Get();

        return true;
    }
}