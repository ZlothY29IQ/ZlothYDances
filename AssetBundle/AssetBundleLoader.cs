using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using Photon.Voice.Unity;
using TMPro;
using UnityEngine;

namespace Colossal;

internal class AssetBundleLoader : MonoBehaviour
{
    public static AssetBundle bundle;
    public static GameObject  assetBundleParent;
    public static string      parentName = "ColossalEmotes";

    public static GameObject  KyleRobot;
    public static AudioSource audioSource;

    public static Dictionary<string, AudioClip> audioPool = new();

    public static GameObject finQuad;

    private Vector3    playerPosition;
    private Quaternion playerRotation;

    private static Coroutine introCoroutine;

    private static AssetBundleLoader _instance;

    public void Awake() => _instance = this;

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
                
                LoadAudioClips();
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
        string info = $"Pos: {playerPosition.x:F2}, {playerPosition.y:F2}, {playerPosition.z:F2}\n" +
                      $"Rot: {playerRotation.eulerAngles.x:F2}, {playerRotation.eulerAngles.y:F2}, {playerRotation.eulerAngles.z:F2}";

        GUIStyle style = new(GUI.skin.label)
        {
                fontSize = 16,
                normal   =
                {
                        textColor = Color.white,
                },
                alignment = TextAnchor.UpperRight,
        };

        GUI.Label(new Rect(Screen.width - 220, 10, 210, 50), info, style);
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
            if (!audioPool.ContainsKey(clip.name))
            {
                audioPool.Add(clip.name, clip);
                Debug.Log("[EMOTE] Loaded AudioClip: " + clip.name);
            }
    }

    public static TMP_FontAsset LoadFont(string name)
    {
        Stream manifestResourceStream = Assembly.GetExecutingAssembly()
                                                .GetManifestResourceStream("ZlothYDances.AssetBundle." + name + ".ttf");

        byte[] array = new byte[manifestResourceStream.Length];
        manifestResourceStream.Read(array, 0, array.Length);
        string text = Path.Combine(Application.temporaryCachePath, "TempFont.ttf");
        File.WriteAllBytes(text, array);
        TMP_FontAsset result = TMP_FontAsset.CreateFontAsset(new Font(text));
        manifestResourceStream?.Dispose();

        return result;
    }
    
    public static void PlayAudioByName(string audioClipName, bool loop = true)
    {
        if (audioSource == null)
        {
            Debug.LogError("[EMOTE] AudioSource is not assigned.");
            return;
        }

        if (!audioPool.TryGetValue(audioClipName, out AudioClip clip))
        {
            Debug.LogError("[EMOTE] AudioClip not found: " + audioClipName);
            return;
        }

        audioSource.loop = loop;
        audioSource.clip = clip;
        audioSource.Play();

        if (PhotonNetwork.InRoom)
        {
            GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
            GorillaTagger.Instance.myRecorder.AudioClip  = clip;
            GorillaTagger.Instance.myRecorder.RestartRecording();
        }

        Debug.Log($"[EMOTE] Playing AudioClip: {audioClipName} (loop: {loop})");
    }
    
    public static IEnumerator PlayIntroThenLoop(string[] introSequence, string mainClipName)
    {
        if (_instance == null)
        {
            Debug.LogError("[EMOTE] AssetBundleLoader instance is null, cannot start coroutine.");
            yield break;
        }

        if (introCoroutine != null)
        {
            _instance.StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        introCoroutine = _instance.StartCoroutine(RunIntroThenLoop(introSequence, mainClipName));
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
            if (!audioPool.TryGetValue(clipName, out AudioClip clip))
            {
                Debug.LogWarning($"[EMOTE] Intro AudioClip not found, skipping: {clipName}");
                continue;
            }

            audioSource.clip = clip;
            audioSource.Play();

            if (PhotonNetwork.InRoom)
            {
                GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
                GorillaTagger.Instance.myRecorder.AudioClip  = clip;
                GorillaTagger.Instance.myRecorder.RestartRecording();
            }

            Debug.Log($"[EMOTE] Playing intro clip: {clipName}");

            yield return new WaitWhile(() => audioSource.isPlaying);
        }

        PlayAudioByName(mainClipName, loop: true);

        introCoroutine = null;
    }
    public static void StopAudio()
    {
        if (_instance != null && introCoroutine != null)
        {
            _instance.StopCoroutine(introCoroutine);
            introCoroutine = null;
        }

        if (audioSource != null)
            audioSource.Stop();
    }
}