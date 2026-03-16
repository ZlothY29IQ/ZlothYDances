using UnityEngine;
using UnityEngine.UI;

namespace Colossal;

internal class GUICreator : MonoBehaviour
{
    public static (GameObject, Text) CreateTextGUI(string text, string name, TextAnchor alignment, Vector3 loctrans)
    {
        GameObject HUDObj = new();
        HUDObj.name = name;

        Canvas canvas = HUDObj.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;

        HUDObj.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        HUDObj.AddComponent<GraphicRaycaster>();

        RectTransform rectTransform = HUDObj.GetComponent<RectTransform>();
        rectTransform.sizeDelta     = new Vector2(5, 5);
        HUDObj.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);

        GameObject menuTextObj = new();
        menuTextObj.transform.SetParent(HUDObj.transform);
        Text MenuText = menuTextObj.AddComponent<Text>();
        MenuText.text            = text;
        MenuText.fontSize        = 10;
        MenuText.font            = Resources.GetBuiltinResource<Font>("Arial.ttf");
        MenuText.color           = Color.dodgerBlue;
        MenuText.supportRichText = true;
        //MenuText.font = AssetBundleLoader.LoadFont("comic").sourceFontFile;

        MenuText.rectTransform.sizeDelta     = new Vector2(260, 180);
        MenuText.rectTransform.localScale    = new Vector3(0.01f, 0.01f, 1f);
        MenuText.rectTransform.localPosition = loctrans;
        MenuText.material                    = new Material(Shader.Find("GUI/Text Shader"));
        MenuText.alignment                   = alignment;

        // Set the parent and adjust for camera position
        HUDObj.transform.SetParent(Camera.main.transform, false);
        HUDObj.transform.localPosition = new Vector3(0f, 0f, 1f);
        HUDObj.transform.localRotation = Quaternion.identity;

        return (HUDObj, MenuText);
    }
}