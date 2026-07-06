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
        public static float CameraDistance = 1.5f; // Adjustable distance
        private bool lastPrimaryState = false;
        private static bool loggedThirdPerson = false;
        private float lastDistanceLogTime = 0f;

        private void Awake()
        {
            try
            {
                var harmony = new Harmony("com.narezany.thirdmonke");
                harmony.PatchAll(typeof(Plugin).Assembly);

                Logger.LogInfo("Third Monke loaded successfully! Toggle: 'T' / Left Controller Primary. Distance: Up/Down Arrows / Right Controller Thumbstick.");
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
            // Keyboard toggle using the New Input System
            try
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.tKey.wasPressedThisFrame)
                    {
                        ThirdPersonActive = !ThirdPersonActive;
                        loggedThirdPerson = false;
                        UnityEngine.Debug.Log($"[ThirdMonke] Keyboard Toggle. Third Person Mode: {ThirdPersonActive}");
                    }

                    // Keyboard distance adjustment
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

            // VR Controller inputs
            try
            {
                // Left controller: toggle mode (Primary button X)
                var leftDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
                if (leftDevice.isValid)
                {
                    bool primaryPressed = false;
                    leftDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out primaryPressed);

                    if (primaryPressed && !lastPrimaryState)
                    {
                        ThirdPersonActive = !ThirdPersonActive;
                        loggedThirdPerson = false;
                        UnityEngine.Debug.Log($"[ThirdMonke] VR Controller Toggle. Third Person Mode: {ThirdPersonActive}");
                    }
                    lastPrimaryState = primaryPressed;
                }

                // Right controller: adjust distance (Thumbstick Y-axis)
                if (ThirdPersonActive)
                {
                    var rightDevice = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
                    if (rightDevice.isValid)
                    {
                        Vector2 thumbstick = Vector2.zero;
                        rightDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out thumbstick);

                        if (Mathf.Abs(thumbstick.y) > 0.15f)
                        {
                            // Push/pull camera distance based on thumbstick input
                            AdjustDistance(-thumbstick.y * Time.deltaTime * 1.5f);
                        }
                    }
                }
            }
            catch
            {
                // Silence XR exceptions if running in non-VR test modes
            }
        }

        private void AdjustDistance(float delta)
        {
            CameraDistance = Mathf.Clamp(CameraDistance + delta, 0.4f, 4.0f);
            
            // Log distance change at most once per second to prevent log flooding
            if (Time.time - lastDistanceLogTime > 1.0f)
            {
                UnityEngine.Debug.Log($"[ThirdMonke] Camera Distance adjusted to: {CameraDistance:F2} meters");
                lastDistanceLogTime = Time.time;
            }
        }

        private Vector3 originalCamPos;
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
                    if (!loggedThirdPerson)
                    {
                        UnityEngine.Debug.Log($"[ThirdMonke] OnBeginCamera caught main camera rendering! Offsetting.");
                        loggedThirdPerson = true;
                    }

                    originalCamPos = camera.transform.position;
                    hasOriginalPos = true;

                    // Offset camera based on current adjustable distance
                    Vector3 offset = -camera.transform.forward * CameraDistance + camera.transform.up * (CameraDistance * 0.15f);
                    camera.transform.position += offset;
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
                    hasOriginalPos = false;
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ThirdMonke] OnEndCamera Error: {ex}");
            }
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
                    // Ensure local player mesh is fully rendered in third person
                    AccessTools.Field(typeof(VRRig), "IsInvisibleToLocalPlayer").SetValue(__instance, false);

                    if (__instance.headMesh != null && !__instance.headMesh.activeSelf)
                    {
                        __instance.headMesh.SetActive(true);
                    }

                    // Force all skinned mesh renderers and normal renderers to be active and on Default layer (0)
                    var renderers = __instance.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in renderers)
                    {
                        r.enabled = true;
                        // 0 is the Default layer which is rendered by the Main Camera
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
