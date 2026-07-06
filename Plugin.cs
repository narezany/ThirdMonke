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
        public static bool CameraSideFront = false; // false = Behind, true = Front
        public static float CameraDistance = 1.5f;
        public static float ShoulderOffset = 0.0f; // -1.0f (left) to 1.0f (right)

        private bool lastPrimaryState = false;
        private float lastDistanceLogTime = 0f;

        private void Awake()
        {
            try
            {
                var harmony = new Harmony("com.narezany.thirdmonke");
                harmony.PatchAll(typeof(Plugin).Assembly);

                Logger.LogInfo("Third Monke loaded successfully! Desktop GUI, Front Mode, Shoulder Offset, and Anti-Clip Collision active.");
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

        private Vector3 originalCamPos;
        private Quaternion originalCamRot;
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
                    hasOriginalPos = true;

                    Vector3 headPos = originalCamPos;
                    Quaternion headRot = originalCamRot;

                    Vector3 targetPos;
                    Quaternion targetRot;

                    if (!CameraSideFront)
                    {
                        // Behind Mode: offset backward, to the side (shoulder offset), and slightly upward
                        Vector3 offsetDir = -Vector3.forward * CameraDistance + Vector3.right * ShoulderOffset + Vector3.up * (CameraDistance * 0.15f);
                        targetPos = headPos + headRot * offsetDir;
                        targetRot = headRot;
                    }
                    else
                    {
                        // Front Mode: offset forward, to the side, and slightly upward, pointing back at player's head
                        Vector3 offsetDir = Vector3.forward * CameraDistance - Vector3.right * ShoulderOffset + Vector3.up * (CameraDistance * 0.15f);
                        targetPos = headPos + headRot * offsetDir;
                        targetRot = Quaternion.LookRotation(headPos - targetPos, headRot * Vector3.up);
                    }

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
                            // Skip player's own root transform and rig visual components (otherwise camera snaps inside head)
                            if (hit.transform.IsChildOf(tagger.transform))
                                continue;

                            if (tagger.offlineVRRig != null && hit.transform.IsChildOf(tagger.offlineVRRig.transform))
                                continue;

                            // Skip triggers
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
                            // Position camera 0.15m in front of the hit point
                            targetPos = closestObstacle.point - rayDir.normalized * 0.15f;
                            
                            if (CameraSideFront)
                            {
                                targetRot = Quaternion.LookRotation(headPos - targetPos, headRot * Vector3.up);
                            }
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
                bg.SetPixel(0, 0, new Color(0.12f, 0.12f, 0.12f, 0.9f)); // Semi-transparent dark gray
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
                titleStyle.normal.textColor = new Color(1.0f, 0.3f, 0.3f); // Reddish accent
                titleStyle.alignment = TextAnchor.MiddleCenter;
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
            }
            return buttonStyle;
        }

        private void OnGUI()
        {
            int winWidth = 330;
            int winHeight = 240;
            Rect winRect = new Rect(20, Screen.height - winHeight - 20, winWidth, winHeight);

            // Draw window background
            GUI.Box(winRect, "", GetWindowStyle());

            GUILayout.BeginArea(new Rect(winRect.x + 15, winRect.y + 15, winWidth - 30, winHeight - 30));
            
            GUILayout.Label("<b>THIRD MONKE CONFIG</b>", GetTitleStyle());
            GUILayout.Space(12);

            // Toggle Third Person
            ThirdPersonActive = GUILayout.Toggle(ThirdPersonActive, " Enable Third Person Mode", GetToggleStyle());
            GUILayout.Space(12);

            if (ThirdPersonActive)
            {
                // Mode Selector
                GUILayout.BeginHorizontal();
                GUILayout.Label("View Direction:", GetTextStyle(), GUILayout.Width(110));
                if (GUILayout.Button(CameraSideFront ? "FRONT (Спереди)" : "BEHIND (Сзади)", GetButtonStyle(), GUILayout.Width(170)))
                {
                    CameraSideFront = !CameraSideFront;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(12);

                // Shoulder Offset Slider (Left / Right)
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Shoulder Offset: {ShoulderOffset:F2}", GetTextStyle(), GUILayout.Width(130));
                ShoulderOffset = GUILayout.HorizontalSlider(ShoulderOffset, -1.0f, 1.0f, GUILayout.Width(150));
                GUILayout.EndHorizontal();
                GUILayout.Space(12);

                // Distance Slider
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Distance: {CameraDistance:F2}m", GetTextStyle(), GUILayout.Width(130));
                CameraDistance = GUILayout.HorizontalSlider(CameraDistance, 0.4f, 4.0f, GUILayout.Width(150));
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Enable Third Person Mode to configure settings.", GetTextStyle());
            }

            GUILayout.EndArea();
        }
        #endregion
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
                    // Ensure local player mesh is fully rendered in third person
                    AccessTools.Field(typeof(VRRig), "IsInvisibleToLocalPlayer").SetValue(__instance, false);

                    if (__instance.headMesh != null)
                    {
                        __instance.headMesh.SetActive(true);
                        
                        var headRends = __instance.headMesh.GetComponentsInChildren<Renderer>(true);
                        foreach (var hr in headRends)
                        {
                            hr.enabled = true;
                            hr.gameObject.layer = 0;
                        }
                    }

                    // Enable only visual renderers, skipping debug physics shapes and active colliders
                    var renderers = __instance.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;

                        string name = r.name;
                        if (name.Contains("Phantom") || name.Contains("Collider") || name.Contains("Trigger") || name.Contains("Bounds"))
                            continue;

                        if (r.GetComponent<Collider>() != null)
                            continue;

                        r.enabled = true;
                        r.gameObject.layer = 0;
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
