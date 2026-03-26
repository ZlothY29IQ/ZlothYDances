using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using ZlothYDances.Console;

namespace ZlothYDances.MakeItFuckingWork;

[HarmonyPatch(typeof(GorillaTagger), "Awake")]
internal class OnGameInit
{
    public static void Prefix()
    {
        BepInPatch.CreateBepInPatch();
    }
}

[BepInIncompatibility("hansolo1000falcon.zlothy.hamburbur")]
[BepInPlugin(Constants.Guid, Constants.Name, Constants.Version)]
public class BepInPatch : BaseUnityPlugin
{
    private static GameObject gameob;

    private BepInPatch()
    {
        new Harmony(Constants.Guid).PatchAll(Assembly.GetExecutingAssembly());
    }

    private void Start() => GorillaTagger.OnPlayerSpawned(() =>
                                                          {
                                                              GameObject hamburburDataHolder =
                                                                      new("ZlothYDancesHamburburDataComponentHolder");

                                                              hamburburDataHolder.AddComponent<HamburburData>();
                                                          });

    public static void CreateBepInPatch()
    {
        if (gameob == null)
            gameob = new GameObject();

        gameob.name = "ColossalEmotes";
        gameob.AddComponent<Plugin>();
        DontDestroyOnLoad(gameob);
    }
}