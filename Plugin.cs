using BepInEx;
using HarmonyLib;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace ThirdMonke
{
    [BepInPlugin("com.narezany.thirdmonke", "Third Monke", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static bool ThirdPersonActive = false;
        public static bool ShowHandDots = false; // Toggle to show white hand indicator dots
        public static float CameraDistance = 1.5f;
        public static float ShoulderOffset = 0.0f; // -1.0f (left) to 1.0f (right)

        private bool lastPrimaryState = false;
        private float lastDistanceLogTime = 0f;

        private GameObject leftDot;
        private GameObject rightDot;
        private static Material handDotMat;

        private void Awake()
        {
            try
            {
                var harmony = new Harmony("com.narezany.thirdmonke");
                harmony.PatchAll(typeof(Plugin).Assembly);

                Logger.LogInfo("Third Monke loaded successfully! Clean GUI, hand dots, and fixed culling mask physics.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error initializing Third Monke: {ex}");
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;

            if (leftDot != null) Destroy(leftDot);
            if (rightDot != null) Destroy(rightDot);
        }

        private void Update()
        {
            try
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.tKey.wasPressedThisFrame)
                    {
                        ThirdPersonActive = !ThirdPersonActive;
                        UnityEngine.Debug.Log($"[ThirdMonke] Keyboard Toggle. Third Person Mode: {ThirdPersonActive}");
                    }

                    if (ThirdPersonActive)
                    {
                        if (Keyboard.current.upArrowKey.isPressed)
                        {
                            AdjustDistance(Time.deltaTime * 1.5f);
                        }
                        if (Keyboard.current.downArrowKey.isPressed)
                        {
                            AdjustDistance(-Time.deltaTime * 1.5f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ThirdMonke] Keyboard Input Error: {ex}");
            }

            try
            {
                var leftDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
                if (leftDevice.isValid)
                {
                    bool primaryPressed = false;
                    leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out primaryPressed);

                    if (primaryPressed && !lastPrimaryState)
                    {
                        ThirdPersonActive = !ThirdPersonActive;
                        UnityEngine.Debug.Log($"[ThirdMonke] VR Controller Toggle. Third Person Mode: {ThirdPersonActive}");
                    }
                    lastPrimaryState = primaryPressed;
                }

                if (ThirdPersonActive)
                {
                    var rightDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
                    if (rightDevice.isValid)
                    {
                        Vector2 thumbstick = Vector2.zero;
                        rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out thumbstick);

                        if (Mathf.Abs(thumbstick.y) > 0.15f)
                        {
                            AdjustDistance(-thumbstick.y * Time.deltaTime * 1.5f);
                        }
                    }
                }
            }
            catch
            {
            }

            // Update hand indicator dots position and visibility
            UpdateDots();
        }

        private void AdjustDistance(float delta)
        {
            CameraDistance = Mathf.Clamp(CameraDistance + delta, 0.4f, 4.0f);
            
            if (Time.time - lastDistanceLogTime > 1.0f)
            {
                UnityEngine.Debug.Log($"[ThirdMonke] Camera Distance adjusted to: {CameraDistance:F2} meters");
                lastDistanceLogTime = Time.time;
            }
        }

        private void UpdateDots()
        {
            bool shouldBeActive = ThirdPersonActive && ShowHandDots;
            
            if (shouldBeActive)
            {
                try
                {
                    var tagger = GorillaTagger.Instance;
                    if (tagger != null && tagger.leftHandTransform != null && tagger.rightHandTransform != null)
                    {
                        if (leftDot == null)
                        {
                            leftDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            leftDot.name = "ThirdMonke_LeftHandDot";
                            var col = leftDot.GetComponent<Collider>();
                            if (col != null) Destroy(col);
                            leftDot.GetComponent<Renderer>().material = GetHandDotMaterial();
                            leftDot.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
                            GameObject.DontDestroyOnLoad(leftDot);
                        }
                        if (rightDot == null)
                        {
                            rightDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            rightDot.name = "ThirdMonke_RightHandDot";
                            var col = rightDot.GetComponent<Collider>();
                            if (col != null) Destroy(col);
                            rightDot.GetComponent<Renderer>().material = GetHandDotMaterial();
                            rightDot.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
                            GameObject.DontDestroyOnLoad(rightDot);
                        }

                        leftDot.SetActive(true);
                        rightDot.SetActive(true);
                        leftDot.transform.position = tagger.leftHandTransform.position;
                        rightDot.transform.position = tagger.rightHandTransform.position;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[ThirdMonke] UpdateDots Error: {ex}");
                }
            }
            else
            {
                if (leftDot != null && leftDot.activeSelf) leftDot.SetActive(false);
                if (rightDot != null && rightDot.activeSelf) rightDot.SetActive(false);
            }
        }

        public static Material GetHandDotMaterial()
        {
            if (handDotMat == null)
            {
                Shader shader = Shader.Find("GUI/Text Shader");
                if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
                
                handDotMat = new Material(shader);
                handDotMat.color = Color.white;
            }
            return handDotMat;
        }

        private Vector3 originalCamPos;
        private Quaternion originalCamRot;
        private int originalCullingMask;
        private bool hasOriginalPos = false;

        private void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!ThirdPersonActive) return;

            try
            {
                var tagger = GorillaTagger.Instance;
                if (tagger == null || tagger.mainCamera == null) return;

                if (camera.gameObject == tagger.mainCamera)
                {
                    originalCamPos = camera.transform.position;
                    originalCamRot = camera.transform.rotation;
                    originalCullingMask = camera.cullingMask;
                    hasOriginalPos = true;

                    // Set camera culling mask to render all layers (including player layers) during rendering pass
                    camera.cullingMask = -1;

                    Vector3 headPos = originalCamPos;
                    Quaternion headRot = originalCamRot;

                    // Behind Mode: offset backward, to the side, and slightly upward
                    Vector3 offsetDir = -Vector3.forward * CameraDistance + Vector3.right * ShoulderOffset + Vector3.up * (CameraDistance * 0.15f);
                    Vector3 targetPos = headPos + headRot * offsetDir;
                    Quaternion targetRot = headRot;

                    // Anti-Clip Collision Detection (RaycastAll to ignore player's own visual body and cosmetics)
                    Vector3 rayDir = targetPos - headPos;
                    float dist = rayDir.magnitude;
                    if (dist > 0.05f)
                    {
                        int layerMask = ~((1 << 8) | (1 << 9) | (1 << 18) | (1 << 20));
                        RaycastHit[] hits = Physics.RaycastAll(headPos, rayDir.normalized, dist, layerMask);
                        
                        RaycastHit closestObstacle = default;
                        float closestDist = float.MaxValue;
                        bool hitObstacle = false;

                        foreach (var hit in hits)
                        {
                            if (hit.transform.IsChildOf(tagger.transform))
                                continue;

                            if (tagger.offlineVRRig != null && hit.transform.IsChildOf(tagger.offlineVRRig.transform))
                                continue;

                            if (hit.collider.isTrigger)
                                continue;

                            if (hit.distance < closestDist)
                            {
                                closestDist = hit.distance;
                                closestObstacle = hit;
                                hitObstacle = true;
                            }
                        }

                        if (hitObstacle)
                        {
                            targetPos = closestObstacle.point - rayDir.normalized * 0.15f;
                        }
                    }

                    camera.transform.position = targetPos;
                    camera.transform.rotation = targetRot;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ThirdMonke] OnBeginCamera Error: {ex}");
            }
        }

        private void OnEndCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!ThirdPersonActive || !hasOriginalPos) return;

            try
            {
                var tagger = GorillaTagger.Instance;
                if (tagger == null || tagger.mainCamera == null) return;

                if (camera.gameObject == tagger.mainCamera)
                {
                    camera.transform.position = originalCamPos;
                    camera.transform.rotation = originalCamRot;
                    camera.cullingMask = originalCullingMask;
                    hasOriginalPos = false;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ThirdMonke] OnEndCamera Error: {ex}");
            }
        }

        #region Desktop GUI (IMGUI)
        private GUIStyle windowStyle;
        private GUIStyle titleStyle;
        private GUIStyle toggleStyle;
        private GUIStyle textStyle;
        private GUIStyle buttonStyle;

        private GUIStyle GetWindowStyle()
        {
            if (windowStyle == null)
            {
                Texture2D bg = new Texture2D(1, 1);
                bg.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.12f, 0.9f));
                bg.Apply();

                windowStyle = new GUIStyle();
                windowStyle.normal.background = bg;
            }
            return windowStyle;
        }

        private GUIStyle GetTitleStyle()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle();
                titleStyle.fontSize = 15;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.normal.textColor = new Color(0.2f, 0.8f, 1.0f); // Aqua Cyan
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.richText = true;
            }
            return titleStyle;
        }

        private GUIStyle GetToggleStyle()
        {
            if (toggleStyle == null)
            {
                toggleStyle = new GUIStyle(GUI.skin.toggle);
                toggleStyle.fontSize = 12;
                toggleStyle.fontStyle = FontStyle.Bold;
                toggleStyle.normal.textColor = Color.white;
                toggleStyle.onNormal.textColor = Color.green;
                toggleStyle.richText = true;
            }
            return toggleStyle;
        }

        private GUIStyle GetTextStyle()
        {
            if (textStyle == null)
            {
                textStyle = new GUIStyle();
                textStyle.fontSize = 12;
                textStyle.normal.textColor = Color.white;
                textStyle.richText = true;
            }
            return textStyle;
        }

        private GUIStyle GetButtonStyle()
        {
            if (buttonStyle == null)
            {
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontSize = 11;
                buttonStyle.fontStyle = FontStyle.Bold;
                buttonStyle.normal.textColor = Color.white;
                buttonStyle.richText = true;
            }
            return buttonStyle;
        }

        private void OnGUI()
        {
            int winWidth = 320;
            int winHeight = 245;
            Rect winRect = new Rect(20, Screen.height - winHeight - 20, winWidth, winHeight);

            GUI.Box(winRect, "", GetWindowStyle());

            GUILayout.BeginArea(new Rect(winRect.x + 15, winRect.y + 12, winWidth - 30, winHeight - 24));
            
            GUILayout.Label("<b>THIRD MONKE</b> v1.0.0", GetTitleStyle());
            GUILayout.Space(8);

            // Toggle Third Person
            ThirdPersonActive = GUILayout.Toggle(ThirdPersonActive, "  Enable Third Person Mode", GetToggleStyle());
            GUILayout.Space(8);

            if (ThirdPersonActive)
            {
                // Hand Indicators Toggle
                ShowHandDots = GUILayout.Toggle(ShowHandDots, "  Show Hand Indicators (Dots)", GetToggleStyle());
                GUILayout.Space(8);

                // Shoulder Offset Slider
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Shoulder Offset: <b>{ShoulderOffset:F2}</b>", GetTextStyle(), GUILayout.Width(130));
                ShoulderOffset = GUILayout.HorizontalSlider(ShoulderOffset, -1.0f, 1.0f, GUILayout.Width(150));
                GUILayout.EndHorizontal();
                GUILayout.Space(8);

                // Distance Slider
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Distance: <b>{CameraDistance:F2}m</b>", GetTextStyle(), GUILayout.Width(130));
                CameraDistance = GUILayout.HorizontalSlider(CameraDistance, 0.4f, 4.0f, GUILayout.Width(150));
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Enable Third Person Mode to configure settings.", GetTextStyle());
            }

            GUILayout.Space(10);

            // Discord Link Button
            GUI.backgroundColor = new Color(0.35f, 0.45f, 0.9f);
            if (GUILayout.Button("<b>JOIN DISCORD SERVER</b>", GetButtonStyle(), GUILayout.Height(28)))
            {
                Application.OpenURL("https://discord.gg/2myJxynQtX");
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndArea();
        }
        #endregion

        // Helper to check if a renderer belongs strictly to the visual avatar or cosmetics
        public static bool ShouldShowRenderer(Renderer r, VRRig rig)
        {
            if (r == null) return false;

            if (r == rig.mainSkin) return true;

            string name = r.name;

            // Skip gaze objects, eye indicators, phantom colliders, green tracking targets, etc.
            if (name.Contains("Phantom") || name.Contains("Collider") || name.Contains("Trigger") || 
                name.Contains("Bounds") || name.Contains("Gaze") || name.Contains("Eye") || name.Contains("Target"))
                return false;

            // Render visual mesh parts ending in _new (e.g., body_new, hand.L_new, etc.)
            if (name.EndsWith("_new")) return true;

            // Render cosmetics and accessories attached to rig anchors
            Transform p = r.transform;
            while (p != null && p != rig.transform)
            {
                string pName = p.name;
                if (pName.Contains("Cosmetics") || pName.Contains("Friendship") || 
                    pName.Contains("friendship") || pName.Contains("Anchor") || pName.Contains("body_new"))
                    return true;
                p = p.parent;
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(VRRig), "PostTick")]
    public static class VRRigPostTickPatch
    {
        [HarmonyPostfix]
        public static void Postfix(VRRig __instance)
        {
            try
            {
                if (Plugin.ThirdPersonActive && (__instance.isOfflineVRRig || __instance.isMyPlayer))
                {
                    AccessTools.Field(typeof(VRRig), "IsInvisibleToLocalPlayer").SetValue(__instance, false);

                    if (__instance.headMesh != null)
                    {
                        __instance.headMesh.SetActive(true);
                        
                        var headRends = __instance.headMesh.GetComponentsInChildren<Renderer>(true);
                        foreach (var hr in headRends)
                        {
                            hr.enabled = true;
                            // DO NOT change layer anymore, camera cullingMask handling resolves rendering safely
                        }
                    }

                    // Enable only visual mesh renderers, WITHOUT changing their physics layer
                    var renderers = __instance.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        if (Plugin.ShouldShowRenderer(r, __instance))
                        {
                            r.enabled = true;
                            // DO NOT change layer anymore, camera cullingMask handling resolves rendering safely
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ThirdMonke] PostTick Postfix Error: {ex}");
            }
        }
    }
}
