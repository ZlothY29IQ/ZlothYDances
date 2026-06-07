using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using ZlothYDances.MakeItFuckingWork;
using ZlothYDances.Patches;

namespace ZlothYDances;

internal class AssetBundleLoader : MonoBehaviour
{
    public static AssetBundle bundle;
    public static GameObject  assetBundleParent;
    public static string      parentName = "ColossalEmotes";

    public static  GameObject         KyleRobot;
    private static AudioSource        audioSource;
    public static  AudioLowPassFilter LowPass;

    private static readonly Dictionary<string, AudioClip> AudioPool = new();

    public static GameObject FinQuad;

    private static Coroutine introCoroutine;

    private static AssetBundleLoader instance;

    private Vector3    playerPosition;
    private Quaternion playerRotation;

    public void Awake() => instance = this;

    public void Start()
    {
        Debug.Log("[EMOTE] Asset Bundle Loader Start");

        bundle = LoadAssetBundle("ZlothYDances.AssetBundle.colossalemotes");
        if (bundle != null)
        {
            assetBundleParent = Instantiate(bundle.LoadAsset<GameObject>(parentName));

            if (assetBundleParent != null)
            {
                assetBundleParent.transform.position = new Vector3(0, 0, 0);

                KyleRobot = assetBundleParent.transform.GetChild(0).gameObject;
                if (KyleRobot != null)
                    audioSource = KyleRobot.GetComponent<AudioSource>();

                LowPass                   = KyleRobot.AddComponent<AudioLowPassFilter>();
                LowPass.cutoffFrequency   = 180f;
                LowPass.lowpassResonanceQ = 1f;
                LowPass.enabled           = GorillaComputerPagePatch.BassFiltered;

                LoadAudioClips();

                if (Constants.TrackingDebug)
                    ApplyDebugObjectRecursive(KyleRobot);
            }
            else
            {
                Debug.Log("[EMOTE] assetBundleParent is null");
            }
        }
        else
        {
            Debug.Log("[EMOTE] bundle is null");
        }
    }

    private void Update()
    {
        playerPosition = VRRig.LocalRig.transform.position;
        playerRotation = VRRig.LocalRig.transform.rotation;
    }

    private void OnGUI()
    {
        if (!Constants.TrackingDebug)
            return;

        string info = $"Pos: {playerPosition.x:F2}, {playerPosition.y:F2}, {playerPosition.z:F2}\n" +
                      $"Rot: {playerRotation.eulerAngles.x:F2}, {playerRotation.eulerAngles.y:F2}, {playerRotation.eulerAngles.z:F2}";

        GUIStyle style = new(GUI.skin.label)
        {
                fontSize = 16,
                normal =
                {
                        textColor = Color.white,
                },
                alignment = TextAnchor.UpperRight,
        };

        GUI.Label(new Rect(Screen.width - 220, 10, 210, 50), info, style);
    }

    private void ApplyDebugObjectRecursive(GameObject target)
    {
        foreach (Transform child in target.transform)
            ApplyDebugObjectRecursive(child.gameObject);

        string n = target.name.ToLower();

        if (n is "kylerobot" or "robotkile" or "root")
            return;

        Plugin.BodyPartType type = Plugin.GetBodyPartType(target.transform);
        Color               col  = GetBodyPartColor(type);

        GameObject debugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        debugCube.name = "ZlothY Dances Rig Debug Cube";

        float scale = type == Plugin.BodyPartType.Finger
                              ? 0.01f
                              : 0.03f;

        debugCube.transform.localScale = Vector3.one * scale;
        debugCube.transform.SetParent(target.transform, false);

        if (debugCube.TryGetComponent(out Renderer renderer))
        {
            renderer.material.shader = Shader.Find("GUI/Text Shader");
            renderer.material.color  = new Color(col.r, col.g, col.b, 0.3f);
        }

        Destroy(debugCube.GetComponent<Collider>());

        debugCube.SetActive(false);
        Plugin.trackerDebugObjects[target] = debugCube;
    }

    private static Color GetBodyPartColor(Plugin.BodyPartType type)
    {
        return type switch
               {
                       Plugin.BodyPartType.Head     => new Color(1f,   0.1f, 0.1f),
                       Plugin.BodyPartType.Spine    => new Color(1f,   0.5f, 0f),
                       Plugin.BodyPartType.Hip      => new Color(1f,   1f,   0f),
                       Plugin.BodyPartType.Shoulder => new Color(0f,   1f,   0f),
                       Plugin.BodyPartType.Elbow    => new Color(0f,   1f,   1f),
                       Plugin.BodyPartType.Hand     => new Color(0.2f, 0.4f, 1f),
                       Plugin.BodyPartType.Finger   => new Color(0.7f, 0.2f, 1f),
                       Plugin.BodyPartType.Knee     => new Color(1f,   0f,   1f),
                       Plugin.BodyPartType.Foot     => new Color(1f,   0.6f, 0.8f),
                       var _                        => new Color(0.6f, 0.6f, 0.6f),
               };
    }

    public AssetBundle LoadAssetBundle(string path)
    {
        Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(path);
        if (stream == null)
        {
            Debug.Log("[Emote] Could not find resource at path: " + path);

            return null;
        }

        AssetBundle bundle = AssetBundle.LoadFromStream(stream);
        stream.Close();

        return bundle;
    }

    public void LoadAudioClips()
    {
        if (bundle == null)
        {
            Debug.LogError("[EMOTE] AssetBundle is null.");

            return;
        }

        AudioClip[] audioClips = bundle.LoadAllAssets<AudioClip>();
        foreach (AudioClip clip in audioClips)
            if (!AudioPool.ContainsKey(clip.name))
            {
                AudioPool.Add(clip.name, clip);
                Debug.Log("[EMOTE] Loaded AudioClip: " + clip.name);
            }
    }

    public static TMP_FontAsset LoadFont(string name)
    {
        if (bundle != null)
            return bundle.LoadAsset<TMP_FontAsset>(name);

        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        return TMP_FontAsset.CreateFontAsset(font);
    }

    public static void PlayAudioByName(string audioClipName, bool loop = true)
    {
        if (GorillaComputerPagePatch.DisableMusic)
            return;

        if (audioSource == null)
        {
            Debug.LogError("[EMOTE] AudioSource is not assigned.");

            return;
        }

        if (!AudioPool.TryGetValue(audioClipName, out AudioClip clip))
        {
            Debug.LogError("[EMOTE] AudioClip not found: " + audioClipName);

            return;
        }

        audioSource.loop = loop;
        audioSource.clip = clip;
        audioSource.Play();

        Debug.Log(
                $"[AssetBundleLoader] Calling PlayClip, EmoteAudioReader.Instance null: {EmoteAudioReader.Instance == null}");

        EmoteAudioReader.Instance?.PlayClip(clip, loop);
    }

    public static IEnumerator PlayIntroThenLoop(string[] introSequence, string mainClipName)
    {
        if (GorillaComputerPagePatch.DisableMusic)
            yield break;

        if (instance == null)
        {
            Debug.LogError("[EMOTE] AssetBundleLoader instance is null, cannot start coroutine.");

            yield break;
        }

        if (introCoroutine != null)
        {
            instance.StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        introCoroutine = instance.StartCoroutine(RunIntroThenLoop(introSequence, mainClipName));
    }

    private static IEnumerator RunIntroThenLoop(string[] introSequence, string mainClipName)
    {
        if (audioSource == null)
        {
            Debug.LogError("[EMOTE] AudioSource is not assigned.");

            yield break;
        }

        audioSource.loop = false;

        foreach (string clipName in introSequence)
        {
            if (!AudioPool.TryGetValue(clipName, out AudioClip clip))
            {
                Debug.LogWarning($"[EMOTE] Intro AudioClip not found, skipping: {clipName}");

                continue;
            }

            audioSource.clip = clip;
            audioSource.Play();

            EmoteAudioReader.Instance?.PlayClip(clip, false);

            Debug.Log($"[EMOTE] Playing intro clip: {clipName}");

            yield return new WaitWhile(() => audioSource.isPlaying);
        }

        PlayAudioByName(mainClipName);

        introCoroutine = null;
    }

    public static void StopAudio()
    {
        if (instance != null && introCoroutine != null)
        {
            instance.StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        if (audioSource != null)
            audioSource.Stop();

        EmoteAudioReader.Instance?.StopClip();
    }
}