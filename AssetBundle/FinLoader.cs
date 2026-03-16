using System.IO;
using System.Reflection;
using GorillaNetworking;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Colossal;

internal class FinLoader : MonoBehaviour
{
    private GameObject      imageObj;
    private GameObject      stumpObj;
    private TextMeshProUGUI textObj;
    private Image           uiImage;

    public void Start() => GorillaTagger.OnPlayerSpawned(HurryUpAndSpawnThisBichTheGameHasLoaded);

    private void Update()
    {
        if (stumpObj != null && Camera.main != null)
        {
            stumpObj.transform.LookAt(Camera.main.transform.position);
            stumpObj.transform.Rotate(0f, 180f, 0f);
        }

        if (VRRig.LocalRig != null)
            uiImage.color = VRRig.LocalRig.playerColor;
    }

    public void HurryUpAndSpawnThisBichTheGameHasLoaded()
    {
        stumpObj = new GameObject("ZLOTHYDANCESPromoStump");
        Canvas canvas = stumpObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        CanvasScaler scaler = stumpObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        stumpObj.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = stumpObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta          = new Vector2(2f, 2f);
        stumpObj.transform.position   = new Vector3(-66.9419f, 12.35f, -82.6273f);
        stumpObj.transform.localScale = Vector3.one * 0.003f;
        stumpObj.transform.Rotate(0f, 180f, 0f);

        textObj = new GameObject("FinText").AddComponent<TextMeshProUGUI>();
        textObj.transform.SetParent(stumpObj.transform, false);
        textObj.font = LoadFont("comic");
        textObj.text =
                "<color=blue>ZlothY Dances</color>\n<size=50%>(Reworked Colossal Emotes)</size>\n<color=white>Made By</color> <color=purple>ZlothY</color>";

        textObj.fontSize  = 50f;
        textObj.color     = Color.paleVioletRed;
        textObj.alignment = TextAlignmentOptions.Center;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchoredPosition = new Vector2(0f,   -50f);
        textRect.sizeDelta        = new Vector2(400f, 200f);

        Texture2D tex = LoadEmbeddedImage("emote.png");
        if (tex != null)
        {
            imageObj = new GameObject("EmoteIcon");
            imageObj.transform.SetParent(stumpObj.transform, false);
            uiImage = imageObj.AddComponent<Image>();

            RectTransform imgRect = imageObj.GetComponent<RectTransform>();

            float targetHeight = 100f;
            float aspect       = (float)tex.width / tex.height;
            float targetWidth  = targetHeight     * aspect * 1f;

            imgRect.sizeDelta        = new Vector2(targetWidth, targetHeight);
            imgRect.anchoredPosition = new Vector2(0f,          80f);

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            uiImage.sprite = sprite;
        }

        GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fin.transform.localScale = new Vector3(0.8f,    0.9f, 0.0001f);
        fin.transform.position   = new Vector3(-64.72f, 12f,  -84.72f);
        fin.transform.rotation   = Quaternion.Euler(0f, 271.63f, 0f);

        if (!fin.TryGetComponent(out Renderer renderer))
            return;

        renderer.material.shader      = Shader.Find("GorillaTag/UberShader");
        renderer.material.mainTexture = LoadEmbeddedImage("fin.png");
        renderer.material.EnableKeyword("_USE_TEXTURE");
    }

    private Texture2D LoadEmbeddedImage(string name)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ZlothYDances.AssetBundle." + name);

        if (stream == null) return null;
        byte[] imageData = new byte[stream.Length];
        stream.Read(imageData, 0, imageData.Length);
        Texture2D texture = new(2, 2);
        texture.LoadImage(imageData);

        return texture;
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
}