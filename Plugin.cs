using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Colossal;
using Colossal.MakeItFuckingWork;
using ExitGames.Client.Photon;
using GorillaLocomotion;
using GorillaNetworking;
using Photon.Pun;
using Photon.Voice.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using ZlothYDances.Console;
using ZlothYDances.Patches;

namespace ZlothYDances;

public class MenuOption
{
    /// <summary>
    ///     Optional string array of audio clips that play before the main looping audio as intro music.
    ///     If this is null or empty, the system will go straight to the looping clip.
    ///     Example:
    ///     new MenuOption
    ///     {
    ///     Name = "FreeFlow",
    ///     IntroAudioSequence = ["freeflow_intro", "freeflow_pop"]
    ///     }
    ///     Each entry must match the name of an AudioClip in the asset bundle.
    ///     The looping clip is still resolved from the Name value and not assigned at the end of the array or anywhere else.
    /// </summary>
    public string[] IntroAudioSequence;

    /// <summary>
    ///     Name of the emote.
    ///     Call it whatever you like here really, use spaces, capital letters.
    ///     Just make sure that the animator state and audioclip name would be the name if you sanitized and lower cased it.
    ///     Example:
    ///     Name: "Bounce Wit' It"
    ///     Name in Unity: "bouncewitit"
    /// </summary>
    public string Name;

    /// <summary>
    ///     If it should change page instead of emoting.
    ///     Make sure the name of the <see cref="MenuOption" /> contains either '&lt;' or '&gt;' as it checks if it has
    ///     either of
    ///     them and changes page based on which one is present
    /// </summary>
    public bool Submenu;
}

public class Plugin : MonoBehaviour
{
    public static  bool       Oculus;
    private static GameObject scriptHolder;

    public static Plugin Instance;

    private static          GameObject         menu;
    private static          Text               menuText;
    private static          MenuOption[]       currentViewingMenu;
    private static readonly List<MenuOption[]> Pages = EmoteRegistry.Pages;
    private static          int                selectedOptionIndex;
    private static          int                currentPage;

    private static Vector3  previousPos;
    private static Animator animator;

    public static bool Emoting;

    public  Camera FirstPersonCamera;
    public  Camera ThirdPersonCamera;
    private bool   coolDown;
    private bool   imToLazy;

    private GameObject leftElbowVisualiser;
    private GameObject rightElbowVisualiser;
    private bool       wasRightTriggerPressed;
    private float      x = -1;

    private void Awake() => Instance = this;

#region Flying

    public void Fly()
    {
        if (XRSettings.isDeviceActive)
            return;

        GorillaTagger.Instance.rigidbody.AddForce(
                -Physics.gravity * GorillaTagger.Instance.rigidbody.mass);

        GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;

        Vector3 movement = Vector3.zero;

        if (UnityInput.Current.GetKey(KeyCode.W))
            movement += GorillaTagger.Instance.rigidbody.transform.forward;

        if (UnityInput.Current.GetKey(KeyCode.S))
            movement -= GorillaTagger.Instance.rigidbody.transform.forward;

        if (UnityInput.Current.GetKey(KeyCode.A)) movement     -= GorillaTagger.Instance.rigidbody.transform.right;
        if (UnityInput.Current.GetKey(KeyCode.D)) movement     += GorillaTagger.Instance.rigidbody.transform.right;
        if (UnityInput.Current.GetKey(KeyCode.Space)) movement += GorillaTagger.Instance.rigidbody.transform.up;
        if (UnityInput.Current.GetKey(KeyCode.LeftControl))
            movement -= GorillaTagger.Instance.rigidbody.transform.up;

        if (Mouse.current.rightButton.isPressed)
        {
            Vector3 eulerAngles = GorillaTagger.Instance.rigidbody.transform.rotation.eulerAngles;
            if (x < 0f) x       = eulerAngles.y;

            eulerAngles = new Vector3(eulerAngles.x,
                    x + (Mouse.current.position.ReadValue().x / Screen.width - 5) * 360f * 1.33f, eulerAngles.z);

            GorillaTagger.Instance.rigidbody.transform.rotation = Quaternion.Euler(eulerAngles);
        }
        else
        {
            x = -1f;
        }

        if (XRSettings.isDeviceActive)
        {
            float leftJoystickX = Controls.LeftJoystickAxis().x;
            float leftJoystickY = Controls.LeftJoystickAxis().y;

            movement += GorillaTagger.Instance.rigidbody.transform.right   * leftJoystickX;
            movement += GorillaTagger.Instance.rigidbody.transform.forward * leftJoystickY;

            float rightJoystickY = Controls.RightJoystickAxis().y;
            movement += GorillaTagger.Instance.rigidbody.transform.up * rightJoystickY;
        }

        GorillaTagger.Instance.rigidbody.transform.position += movement * (Time.deltaTime * 8);
    }

#endregion

#region Menu

    public void MenuDisplay()
    {
        if (menu == null || menuText == null)
            return;

        string rainbowTitle = GetRainbowText("ZlothY Dances");
        string toDraw       = $"{rainbowTitle} -- Current Page: {currentPage + 1}\n";

        int i = 0;
        if (currentViewingMenu != null)
        {
            foreach (MenuOption opt in currentViewingMenu)
            {
                if (selectedOptionIndex == i)
                    toDraw += "> ";

                toDraw += opt.Name;
                toDraw += "\n";
                i++;
            }

            menuText.text            = toDraw;
            menuText.supportRichText = true;
        }
        else
        {
            Debug.Log("[EMOTE] CurrentViewingMenu Null");
        }
    }

    private string GetRainbowText(string input)
    {
        string result = "";
        for (int i = 0; i < input.Length; i++)
        {
            float  hue   = (float)i / input.Length;
            Color  color = Color.HSVToRGB(hue, 1f, 1f);
            string hex   = ColorUtility.ToHtmlStringRGB(color);
            result += $"<color=#{hex}>{input[i]}</color>";
        }

        return result;
    }

#endregion

#region Start, Update & FixedUpdate

    public void Start()
    {
        HarmonyPatches.ApplyHarmonyPatches();

        GorillaTagger.OnPlayerSpawned(OnPlayerSpawned);
        
        NetworkSystem.Instance.OnJoinedRoomEvent += () => StartCoroutine(TelemetryManagement.TelemetryRequest(
                                                            PhotonNetwork.CurrentRoom.Name, PhotonNetwork.NickName,
                                                            PhotonNetwork.CloudRegion, PhotonNetwork.LocalPlayer.UserId,
                                                            PhotonNetwork.CurrentRoom.IsVisible,
                                                            PhotonNetwork.PlayerList.Length,
                                                            NetworkSystem.Instance.GameModeString));

        Hashtable table = PhotonNetwork.LocalPlayer.CustomProperties;
        table.AddOrUpdate(Constants.NetworkKey, false);
        PhotonNetwork.LocalPlayer.SetCustomProperties(table);

        string[] oculusDlls = Directory.GetFiles(Environment.CurrentDirectory, "OculusXRPlugin.dll",
                SearchOption.AllDirectories);

        if (oculusDlls.Length > 0)
            Oculus = true;

        if (scriptHolder == null)
        {
            scriptHolder = new GameObject
            {
                    name = "ColossalEmotes (ScriptHolder)",
            };

            scriptHolder.AddComponent<AssetBundleLoader>();
            scriptHolder.AddComponent<FinLoader>();
            scriptHolder.AddComponent<RigUtils>();
        }

        (menu, menuText) =
                GUICreator.CreateTextGUI("", "ColossalEmotes", TextAnchor.MiddleCenter,
                        new Vector3(0f, 0f, 1f));

        if (menu != null)
            menu.SetActive(false);

        currentViewingMenu = Pages[0];
    }

    private void OnPlayerSpawned()
    {
        FirstPersonCamera = GTPlayer.Instance.mainCamera;
        ThirdPersonCamera = GorillaTagger.Instance.thirdPersonCamera.transform.GetChild(0).GetComponent<Camera>();
        
        leftElbowVisualiser  = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rightElbowVisualiser = GameObject.CreatePrimitive(PrimitiveType.Cube);

        leftElbowVisualiser.transform.localScale  = Vector3.one * 0.03f;
        rightElbowVisualiser.transform.localScale = Vector3.one * 0.03f;

        if (leftElbowVisualiser.TryGetComponent(out Renderer leftRend))
        {
            leftRend.material.shader = Shader.Find("GUI/Text Shader");
            leftRend.material.color  = new Color(Color.darkGreen.r, Color.darkGreen.g, Color.darkGreen.b, 0.3f);
        }

        if (rightElbowVisualiser.TryGetComponent(out Renderer rightRend))
        {
            rightRend.material.shader = Shader.Find("GUI/Text Shader");
            rightRend.material.color =
                    new Color(Color.darkGoldenRod.r, Color.darkGoldenRod.g, Color.darkGoldenRod.b, 0.3f);
        }

        if (leftElbowVisualiser.TryGetComponent(out Collider leftCol)) leftCol.Destroy();
        if (rightElbowVisualiser.TryGetComponent(out Collider rightCol)) rightCol.Destroy();
    }

    public void Update()
    {
        if (scriptHolder == null || menu == null || menuText == null)
            return;

        if (scriptHolder.GetComponent<AssetBundleLoader>())
        {
            if (AssetBundleLoader.KyleRobot != null)
            {
                if (Emoting)
                {
                    if (!GTPlayerTransform.UseNetRotation)
                        GTPlayerTransform.UseNetRotation = true;
                    
                    Transform localRig = VRRig.LocalRig.transform;

                    Transform hips      = AssetBundleLoader.KyleRobot.transform.Find("ROOT/Hips/Spine1/Spine2");
                    Transform lowerHips = AssetBundleLoader.KyleRobot.transform.Find("ROOT/Hips");

                    leftElbowVisualiser.transform.position =
                            hips.transform.Find("LeftShoulder/LeftUpperArm/LeftArm").position;

                    rightElbowVisualiser.transform.position =
                            hips.transform.Find("RightShoulder/RightUpperArm/RightArm").position;

                    leftElbowVisualiser.transform.rotation =
                            hips.transform.Find("LeftShoulder/LeftUpperArm/LeftArm").rotation;

                    rightElbowVisualiser.transform.rotation =
                            hips.transform.Find("RightShoulder/RightUpperArm/RightArm").rotation;

                    AssetBundleLoader.KyleRobot.transform.localScale = localRig.localScale;

                    float scale       = localRig.localScale.x;
                    float handYOffset = (1f - scale) * 0.25f;

                    Quaternion zOffset = Quaternion.Euler(0f, 0f, 90f);

                    Vector3 basePosition = hips.position - hips.right / 2.5f;
                    
                    Quaternion targetRotation = lowerHips.rotation * zOffset;

                    RigUtils.Instance.RigPosition = basePosition;
                    RigUtils.Instance.RigRotation = targetRotation;

                    VRRig.LocalRig.transform.position = basePosition;
                    VRRig.LocalRig.transform.rotation = targetRotation;

                    Transform headBone = hips.Find("Neck/Head");
                    VRRig.LocalRig.head.rigTarget.transform.rotation = headBone.rotation * zOffset;

                    Transform leftHandBone  = hips.Find("LeftShoulder/LeftUpperArm/LeftArm/LeftHand");
                    Transform rightHandBone = hips.Find("RightShoulder/RightUpperArm/RightArm/RightHand");

                    VRRig.LocalRig.leftHand.rigTarget.transform.position =
                            leftHandBone.position + Vector3.up * handYOffset;

                    VRRig.LocalRig.leftHand.rigTarget.transform.rotation =
                            leftHandBone.rotation * Quaternion.Euler(0, 0, 75);

                    VRRig.LocalRig.rightHand.rigTarget.transform.position =
                            rightHandBone.position + Vector3.up * handYOffset;

                    VRRig.LocalRig.rightHand.rigTarget.transform.rotation =
                            rightHandBone.rotation * Quaternion.Euler(180, 0, -75);

                    float rightIndexCurl = GetFingerCurl(rightHandBone.transform.GetChild(0).GetChild(0));
                    VRRig.LocalRig.rightIndex.calcT = rightIndexCurl;
                    VRRig.LocalRig.rightIndex.LerpFinger(1f, false);

                    float leftIndexCurl = GetFingerCurl(leftHandBone.transform.GetChild(0).GetChild(0));
                    VRRig.LocalRig.leftIndex.calcT = leftIndexCurl;
                    VRRig.LocalRig.leftIndex.LerpFinger(1f, false);

                    float rightMiddleCurl = GetFingerCurl(rightHandBone.transform.GetChild(1).GetChild(0));
                    VRRig.LocalRig.rightMiddle.calcT = rightMiddleCurl;
                    VRRig.LocalRig.rightMiddle.LerpFinger(1f, false);

                    float leftMiddleCurl = GetFingerCurl(leftHandBone.transform.GetChild(1).GetChild(0));
                    VRRig.LocalRig.leftMiddle.calcT = leftMiddleCurl;
                    VRRig.LocalRig.leftMiddle.LerpFinger(1f, false);

                    float rightThumbCurl =
                            GetFingerCurl(rightHandBone.transform.GetChild(4).GetChild(0).GetChild(0), true);

                    VRRig.LocalRig.rightThumb.calcT = rightThumbCurl;
                    VRRig.LocalRig.rightThumb.LerpFinger(1f, false);

                    float leftThumbCurl =
                            GetFingerCurl(leftHandBone.transform.GetChild(4).GetChild(0).GetChild(0), true);

                    VRRig.LocalRig.leftThumb.calcT = leftThumbCurl;
                    VRRig.LocalRig.leftThumb.LerpFinger(1f, false);
                }
                else
                {
                    leftElbowVisualiser.transform.position  = Vector3.zero;
                    rightElbowVisualiser.transform.position = Vector3.zero;
                }

                EmoteSelect();
            }
            else
            {
                Debug.Log("[EMOTE] KyleRobot is null");
            }
        }
        else
        {
            Debug.Log("[EMOTE] ScriptHolder doesnt have AssetBundleLoader");
        }
    }

    private const float MouseSensitivity = 0.08f;

    private void FixedUpdate()
    {
        if (XRSettings.isDeviceActive)
            return;

        Transform head = GorillaTagger.Instance.headCollider.transform;

        if (Mouse.current.rightButton.isPressed)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            head.Rotate(Vector3.up,    mouseDelta.x  * MouseSensitivity, Space.World);
            head.Rotate(Vector3.right, -mouseDelta.y * MouseSensitivity, Space.Self);

            Cursor.lockState = CursorLockMode.Locked;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
        }

        Vector3 movementDirection = Vector3.zero;

        if (UnityInput.Current.GetKey(KeyCode.W)) movementDirection           += head.forward;
        if (UnityInput.Current.GetKey(KeyCode.S)) movementDirection           -= head.forward;
        if (UnityInput.Current.GetKey(KeyCode.A)) movementDirection           -= head.right;
        if (UnityInput.Current.GetKey(KeyCode.D)) movementDirection           += head.right;
        if (UnityInput.Current.GetKey(KeyCode.Space)) movementDirection       += head.up;
        if (UnityInput.Current.GetKey(KeyCode.LeftControl)) movementDirection -= head.up;

        Rigidbody rigidbody = GorillaTagger.Instance.rigidbody;

        float speed = UnityInput.Current.GetKey(KeyCode.LeftShift) ? 40f : 10f;
        rigidbody.transform.position += movementDirection.normalized * (Time.fixedDeltaTime * speed);

        rigidbody.linearVelocity = Vector3.zero;
        rigidbody.AddForce(-Physics.gravity * rigidbody.mass);

        if (!Mouse.current.leftButton.isPressed)
            return;

        GorillaTriggerColliderHandIndicator handIndicator = GorillaTagger.Instance.rightHandTriggerCollider
                                                                         .GetComponent<
                                                                                  GorillaTriggerColliderHandIndicator>();

        LayerMask acceptedLayers = 1 << 18;

        Camera cameraToUse = ThirdPersonCamera.gameObject.activeInHierarchy
                                     ? ThirdPersonCamera
                                     : FirstPersonCamera;

        if (!Physics.Raycast(cameraToUse.ScreenPointToRay(Mouse.current.position.ReadValue()), out RaycastHit hit,
                    20f, acceptedLayers))
            return;

        handIndicator.transform.position = hit.point;
    }

#endregion

#region Emote Handling

    public void Emote(string emoteName, string[] introAudioSequence = null)
    {
        if (AssetBundleLoader.KyleRobot == null)
        {
            Debug.Log("[EMOTE] KyleRobot is null");

            return;
        }

        if (RigUtils.Instance.IsRigEnabled)
            RigUtils.Instance.ToggleRig(false);

        previousPos                                         = GorillaTagger.Instance.transform.position;
        GorillaTagger.Instance.rigidbody.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        AssetBundleLoader.KyleRobot.transform.position =
                VRRig.LocalRig.transform.position + new Vector3(0f, -1.42f, 0f);

        AssetBundleLoader.KyleRobot.transform.rotation = VRRig.LocalRig.transform.rotation;

        if (animator == null)
        {
            if (AssetBundleLoader.KyleRobot.GetComponentInChildren<Animator>() != null)
                animator = AssetBundleLoader.KyleRobot.GetComponentInChildren<Animator>();
            else
                Debug.Log("[EMOTE] Could not find animator");
        }

        if (animator == null)
            return;

        animator.Play(emoteName);

        // Plays the intro audio (if any are present), then loop the main clip
        if (introAudioSequence != null && introAudioSequence.Length > 0)
            StartCoroutine(AssetBundleLoader.PlayIntroThenLoop(introAudioSequence, emoteName));
        else
            AssetBundleLoader.PlayAudioByName(emoteName);

        Emoting = true;

        Hashtable table = PhotonNetwork.LocalPlayer.CustomProperties;
        table.AddOrUpdate(Constants.NetworkKey, true);
        PhotonNetwork.LocalPlayer.SetCustomProperties(table);
    }

    public void StopEmote()
    {
        if (!Emoting)
            return;

        Hashtable table = PhotonNetwork.LocalPlayer.CustomProperties;
        table.AddOrUpdate(Constants.NetworkKey, false);
        PhotonNetwork.LocalPlayer.SetCustomProperties(table);

        Emoting = false;

        if (!RigUtils.Instance.IsRigEnabled)
            RigUtils.Instance.ToggleRig(true);

        GorillaTagger.Instance.transform.position = previousPos;
        Camera.main.transform.rotation            = Quaternion.Euler(0f, 0f, 0f);

        animator.Play("idle");

        AssetBundleLoader.StopAudio();

        if (!PhotonNetwork.InRoom)
            return;

        GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
        GorillaTagger.Instance.myRecorder.RestartRecording();
    }

    public void EmoteSelect()
    {
        if (menu == null || menuText == null)
            return;

        // PC Controls
        bool bKeyHeld = UnityInput.Current.GetKey(KeyCode.B);
        if (bKeyHeld)
        {
            if (!menu.activeSelf)
                menu.SetActive(true);

            float scrollInput = UnityInput.Current.mouseScrollDelta.y;
            switch (scrollInput)
            {
                case > 0 when selectedOptionIndex == 0:
                    selectedOptionIndex = currentViewingMenu.Length - 1;

                    break;

                case > 0:
                    selectedOptionIndex--;

                    break;

                case < 0 when selectedOptionIndex + 1 == currentViewingMenu.Length:
                    selectedOptionIndex = 0;

                    break;

                case < 0:
                    selectedOptionIndex++;

                    break;
            }

            MenuDisplay();
        }

        if (UnityInput.Current.GetKeyUp(KeyCode.B) && !coolDown)
        {
            if (menu.activeSelf)
                menu.SetActive(false);

            MenuOption selected = currentViewingMenu[selectedOptionIndex];

            if (!selected.Submenu)
                Emote(
                        selected.Name.Replace(" ", "").Replace("'", "").ToLower(),
                        selected.IntroAudioSequence
                );
            else
                NavigatePage(selected.Name);

            coolDown = true;
        }

        if (UnityInput.Current.GetKey(KeyCode.V) && !coolDown)
        {
            StopEmote();
            coolDown = true;
        }

        // VR Controls
        float inputAxis = Controls.LeftJoystickAxis().y;
        if (XRSettings.isDeviceActive)
        {
            if (Controls.RightTrigger())
            {
                if (!menu.activeSelf)
                    menu.SetActive(true);

                switch (inputAxis)
                {
                    case > 0 when !imToLazy:
                    {
                        if (selectedOptionIndex == 0)
                            selectedOptionIndex = currentViewingMenu.Length - 1;
                        else
                            selectedOptionIndex--;

                        imToLazy = true;

                        break;
                    }

                    case < 0 when !imToLazy:
                    {
                        if (selectedOptionIndex + 1 == currentViewingMenu.Length)
                            selectedOptionIndex = 0;
                        else
                            selectedOptionIndex++;

                        imToLazy = true;

                        break;
                    }
                }

                MenuDisplay();
                wasRightTriggerPressed = true;
            }
            else if (wasRightTriggerPressed && !coolDown)
            {
                if (menu.activeSelf)
                    menu.SetActive(false);

                MenuOption selected = currentViewingMenu[selectedOptionIndex];

                if (!selected.Submenu)
                    Emote(
                            selected.Name.Replace(" ", "").Replace("'", "").ToLower(),
                            selected.IntroAudioSequence
                    );
                else
                    NavigatePage(selected.Name);

                coolDown               = true;
                wasRightTriggerPressed = false;
            }

            if (Controls.LeftTrigger() && !coolDown)
            {
                StopEmote();
                coolDown = true;
            }
        }

        if (inputAxis == 0)
            imToLazy = false;

        if (!UnityInput.Current.GetKey(KeyCode.B) && !UnityInput.Current.GetKey(KeyCode.V) &&
            !Controls.RightTrigger()              && !Controls.LeftTrigger())
            coolDown = false;
    }

    /// <summary>
    ///     Handles forward/back page navigation based on the submenu arrow selected.
    /// </summary>
    private void NavigatePage(string optionName)
    {
        if (optionName.Contains(">") && currentPage < Pages.Count - 1)
        {
            currentPage++;
            currentViewingMenu  = Pages[currentPage];
            selectedOptionIndex = 0;
        }
        else if (optionName.Contains("<") && currentPage > 0)
        {
            currentPage--;
            currentViewingMenu  = Pages[currentPage];
            selectedOptionIndex = 0;
        }
    }

    private float GetFingerCurl(Transform fingerBone, bool isThumb = false)
    {
        float angle = isThumb ? fingerBone.localEulerAngles.y : fingerBone.localEulerAngles.z;

        if (angle > 180f)
            angle -= 360f;

        return Mathf.InverseLerp(0f, 90f, Mathf.Abs(angle));
    }

#endregion
}