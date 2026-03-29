using System.Reflection;
using BepInEx;
using Colossal;
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
    private static GameObject gameObjections;

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
        if (gameObjections == null)
            gameObjections = new GameObject();

        gameObjections.name = "ColossalEmotes";
        gameObjections.AddComponent<Plugin>();
        DontDestroyOnLoad(gameObjections);
    }
}