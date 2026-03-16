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
    public static GameObject  Promo;

    public static Dictionary<string, AudioClip> audioPool = new();

    // Quad object for PNG
    public static GameObject finQuad;

    private Vector3    playerPosition;
    private Quaternion playerRotation;

    public void Start()
    {
        Debug.Log("[EMOTE] Asset Bundle Loader Start");

        // Load the asset bundle
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

                Promo = assetBundleParent.transform.GetChild(1).gameObject;

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

        GUIStyle style = new(GUI.skin.label);
        style.fontSize         = 16;
        style.normal.textColor = Color.white;
        style.alignment        = TextAnchor.UpperRight;

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

    public static void PlayAudioByName(string audioClipName)
    {
        if (audioSource == null)
        {
            Debug.LogError("[EMOTE] AudioSource is not assigned.");

            return;
        }

        if (audioPool.ContainsKey(audioClipName))
        {
            AudioClip clip = audioPool[audioClipName];
            audioSource.clip = clip;
            audioSource.Play();

            if (PhotonNetwork.InRoom)
            {
                GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
                GorillaTagger.Instance.myRecorder.AudioClip  = clip;
                GorillaTagger.Instance.myRecorder.RestartRecording();
            }

            Debug.Log("[EMOTE] Playing AudioClip: " + audioClipName);
        }
        else
        {
            Debug.LogError("[EMOTE] AudioClip not found: " + audioClipName);
        }
    }
}