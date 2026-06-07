using HarmonyLib;
using Photon.Pun;
using Photon.Voice.Unity;
using POpusCodec.Enums;

namespace ZlothYDances.Patches;

[HarmonyPatch(typeof(NetworkSystemPUN), nameof(NetworkSystemPUN.SetupVoice))]
public class VoiceRecorderPatch
{
    private static void Postfix()
    {
        if (!PhotonNetwork.InRoom || !NetworkSystem.Instance.LocalRecorder)
            return;

        Recorder primaryRecorder = NetworkSystem.Instance.VoiceConnection.PrimaryRecorder;

        if (primaryRecorder.SamplingRate == SamplingRate.Sampling24000 && primaryRecorder.Bitrate == 30000)
            return;

        primaryRecorder.SamplingRate = SamplingRate.Sampling24000;
        primaryRecorder.Bitrate      = 30000;
    }
}