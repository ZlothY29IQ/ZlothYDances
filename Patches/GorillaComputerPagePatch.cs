using System;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using Photon.Voice.Unity;
using UnityEngine;

namespace ZlothYDances.Patches;

[HarmonyPatch(typeof(GorillaComputer))]
internal static class GorillaComputerPagePatch
{
    private const string                        PageLabel = "DANCES";
    private const GorillaComputer.ComputerState PageState = (GorillaComputer.ComputerState)100;

    public static bool DisableMusic;
    public static bool UpsideDown;
    public static bool BassFiltered;

    private static GorillaComputer computer;

    private static float lastTimePressed;

    [HarmonyPatch(nameof(GorillaComputer.Awake))]
    [HarmonyPostfix]
    private static void AwakePostfix(GorillaComputer __instance)
    {
        DisableMusic = PlayerPrefs.GetInt(nameof(DisableMusic), 0) == 1;
        UpsideDown   = PlayerPrefs.GetInt(nameof(UpsideDown),   0) == 1;
        BassFiltered = PlayerPrefs.GetInt(nameof(BassFiltered), 0) == 1;

        AssetBundleLoader.LowPass?.enabled = BassFiltered;

        computer = __instance;

        if (__instance.OrderList.Exists(item => item.State == PageState))
            return;

        __instance.OrderList.Add(new GorillaComputer.StateOrderItem(PageState, PageLabel));
    }

    [HarmonyPatch(nameof(GorillaComputer.UpdateScreen))]
    [HarmonyPrefix]
    private static bool UpdateScreenPrefix(GorillaComputer __instance)
    {
        if (!IsCustomPage(__instance))
            return true;

        __instance.UpdateFunctionScreen();

        __instance.screenText.Set(GetCustomScreenText());

        return false;
    }

    [HarmonyPatch(nameof(GorillaComputer.PressButton))]
    [HarmonyPrefix]
    private static bool PressButtonPrefix(GorillaComputer __instance, GorillaKeyboardBindings buttonPressed)
    {
        if (!IsCustomPage(__instance))
            return true;

        if (buttonPressed is GorillaKeyboardBindings.up or GorillaKeyboardBindings.down)
            return true;

        if (Time.time - lastTimePressed < 0.1f)
            return false;

        lastTimePressed = Time.time;

        HandleInput(buttonPressed);
        __instance.UpdateScreen();

        return false;
    }

    private static void HandleInput(GorillaKeyboardBindings button)
    {
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (button)
        {
            case GorillaKeyboardBindings.option1:
                DisableMusic = !DisableMusic;
                PlayerPrefs.SetInt(nameof(DisableMusic), DisableMusic ? 1 : 0);

                break;

            case GorillaKeyboardBindings.option2:
                UpsideDown = !UpsideDown;
                PlayerPrefs.SetInt(nameof(UpsideDown), UpsideDown ? 1 : 0);

                break;

            case GorillaKeyboardBindings.option3:
                BassFiltered                      = !BassFiltered;
                AssetBundleLoader.LowPass.enabled = BassFiltered;
                PlayerPrefs.SetInt(nameof(BassFiltered), BassFiltered ? 1 : 0);

                break;

            case GorillaKeyboardBindings.enter:
                if (PhotonNetwork.InRoom)
                {
                    GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
                    GorillaTagger.Instance.myRecorder.RestartRecording();
                }

                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(button), button, null);
        }

        PlayerPrefs.Save();
    }

    private static bool IsCustomPage(GorillaComputer computer) =>
            ReferenceEquals(GorillaComputerPagePatch.computer, computer) &&
            computer.currentState == PageState;

    private static string GetCustomScreenText() =>
            // ReSharper disable once HeuristicUnreachableCode
            $"ZlothY Dances {(Constants.TrackingDebug ? "DEBUG" : "SETTINGS")}\n\n" +
            "OPTION 1 - DISABLE MUSIC: " + (DisableMusic ? "ON" : "OFF") + "\n\n" +
            "OPTION 2 - UPSIDE DOWN: " + (UpsideDown ? "ON" : "OFF") + "\n\n" +
            "OPTION 3 - BASS FILTER (LOCAL): " + (BassFiltered ? "ON" : "OFF");
}