using BepInEx;
using HarmonyLib;
using System;
using UnityEngine;

namespace ThirdMonke
{
    [BepInPlugin("com.narezany.thirdmonke", "Third Monke", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static bool ThirdPersonActive = false;
        private bool lastPrimaryState = false;

        private void Awake()
        {
            try
            {
                var harmony = new Harmony("com.narezany.thirdmonke");
                harmony.PatchAll(typeof(Plugin).Assembly);

                // Spawn our camera manager component
                var go = new GameObject("ThirdMonke_Manager");
                GameObject.DontDestroyOnLoad(go);
                go.AddComponent<ThirdPersonCamera>();

                Logger.LogInfo("Third Monke loaded successfully! Press 'T' or Left Controller Primary Button to toggle Third Person.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error initializing Third Monke: {ex}");
            }
        }

        private void Update()
        {
            // Keyboard toggle
            if (Input.GetKeyDown(KeyCode.T))
            {
                ThirdPersonActive = !ThirdPersonActive;
                UnityEngine.Debug.Log($"[ThirdMonke] Keyboard Toggle. Third Person Mode: {ThirdPersonActive}");
            }

            // VR Controller toggle (Left hand primary button - X)
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
            }
            catch
            {
                // Silence XR exceptions if running in non-VR test modes
            }
        }
    }

    public class ThirdPersonCamera : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Plugin.ThirdPersonActive)
            {
                try
                {
                    var tagger = GorillaTagger.Instance;
                    if (tagger != null && tagger.mainCamera != null)
                    {
                        Transform cam = tagger.mainCamera.transform;
                        // Move camera 1.7 meters backward and 0.3 meters upward relative to headset orientation
                        Vector3 offset = -cam.forward * 1.7f + cam.up * 0.3f;
                        cam.position += offset;
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[ThirdMonke] Camera Offset Error: {ex}");
                }
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
