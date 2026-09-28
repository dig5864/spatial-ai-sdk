#if SPATIALAI_HAS_CARDBOARD
using System;
using System.Runtime.InteropServices;
using Google.XR.Cardboard;
using SpatialAI.Core;
using SpatialAI.Unity;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace SpatialAI.Cardboard
{
    // Cardboard owns display submission and distortion. Spatial AI only observes its
    // XR session and selects our independent optical profile by device ID.
    public sealed class SpatialCardboardBridge : MonoBehaviour
    {
        [Tooltip("External headset ID from a future connection service. If empty, use the phone device code or model for diagnostics only.")]
        public string headsetDeviceId;
        public bool scanCardboardViewerOnStart;
        [Tooltip("Only for the sample: load generic Cardboard I/O 2015 viewer parameters when none are saved. Never use these parameters to validate RhinoX optics.")]
        public bool useGenericViewerForDiagnostics;
        [Tooltip("Use the historical RhinoX Pro Sharp dual-eye distortion mesh only on the known RhinoX Android device. Calibration still requires visual verification.")]
        public bool useRhinoXLegacyMeshForDiagnostics;
        public DeviceIdentity Identity { get; private set; }
        public OpticalProfileDescriptor MatchedProfile { get; private set; }
        public Result ProfileResult { get; private set; }
        public bool XrRunning { get; private set; }
        public bool ViewerParamsReady { get; private set; }
        public bool UsingGenericViewerParams { get; private set; }
        public bool TriggerPressed { get; private set; }
        public bool RhinoXLegacyMeshApplied { get; private set; }
        private bool rhinoXNativeApiAvailable;
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_setRhinoXLegacyMesh")]
        private static extern void SetRhinoXLegacyMesh(int enabled);
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_isRhinoXLegacyMeshApplied")]
        private static extern int IsRhinoXLegacyMeshApplied();
#endif
        private bool viewerParamsChecked;
        private bool viewerScanInProgress;

        private const string GenericViewerFlag = "SpatialAI.Samples.GenericViewer";
        // Google's public Cardboard I/O 2015 example URI. This is a render/input diagnostic only.
        private const string GenericViewerUri =
            "https://google.com/cardboard/cfg?p=CgZHb29nbGUSEkNhcmRib2FyZCBJL08gMjAxNR0rGBU9JQHegj0qEAAASEIAAEhCAABIQgAASEJYADUpXA89OggeZnc-Ej6aPlAAYAM";

        private void Start()
        {
            Identity = SpatialDeviceProfiles.ReadHostIdentity(headsetDeviceId);
            // The current RhinoX AIO reports QUALCOMM / RhinoX / kona. Do not
            // infer headset identity from the generic Qualcomm device code alone.
            bool knownRhinoX = string.Equals(Identity.Manufacturer, "QUALCOMM", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Identity.Model, "RhinoX", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Identity.DeviceCode, "kona", StringComparison.OrdinalIgnoreCase);
            if (knownRhinoX && string.IsNullOrEmpty(Identity.HeadsetId)) Identity.HeadsetId = "RhinoXPro";
            ProfileResult = SpatialDeviceProfiles.Select(Identity, out var profile);
            MatchedProfile = profile;
            Debug.Log("Spatial AI device: " + Identity.DisplayName + "; optics: " + ProfileResult.Message);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (knownRhinoX && useRhinoXLegacyMeshForDiagnostics && MatchedProfile != null &&
                MatchedProfile.profileId == "rhinox-pro-legacy-reference")
            {
                try
                {
                    SetRhinoXLegacyMesh(1);
                    rhinoXNativeApiAvailable = true;
                    Debug.Log("Spatial AI requested RhinoX Pro Sharp mesh; waiting for native render reload.");
                }
                catch (Exception e) { Debug.LogError("RhinoX native mesh API unavailable: " + e.Message); }
            }
#endif
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void Update()
        {
            TriggerPressed = false;
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            var loader = manager != null ? manager.activeLoader as Google.XR.Cardboard.XRLoader : null;
            var display = loader != null ? loader.GetLoadedSubsystem<XRDisplaySubsystem>() : null;
            XrRunning = display != null && display.running;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (rhinoXNativeApiAvailable) RhinoXLegacyMeshApplied = IsRhinoXLegacyMeshApplied() != 0;
#endif
            if (!XrRunning) return;
            if (!viewerParamsChecked)
            {
                viewerParamsChecked = true;
                ViewerParamsReady = Api.HasDeviceParams();
                UsingGenericViewerParams = ViewerParamsReady && PlayerPrefs.GetInt(GenericViewerFlag, 0) == 1;
                if (!ViewerParamsReady && useGenericViewerForDiagnostics && Application.platform == RuntimePlatform.Android)
                {
                    Api.SaveDeviceParams(GenericViewerUri);
                    Api.ReloadDeviceParams();
                    ViewerParamsReady = Api.HasDeviceParams();
                    UsingGenericViewerParams = ViewerParamsReady;
                    if (ViewerParamsReady)
                    {
                        PlayerPrefs.SetInt(GenericViewerFlag, 1);
                        Debug.LogWarning("Spatial XR loaded generic Cardboard viewer parameters for diagnostics. RhinoX optical calibration is NOT applied.");
                    }
                    else Debug.LogWarning("Spatial XR could not load diagnostic viewer parameters; scan a valid Cardboard viewer QR code.");
                }
            }
            if (scanCardboardViewerOnStart && !ViewerParamsReady)
            {
                scanCardboardViewerOnStart = false;
                viewerScanInProgress = true;
                Api.ScanDeviceParams();
            }
            if (Api.IsGearButtonPressed)
            {
                viewerScanInProgress = true;
                Api.ScanDeviceParams();
            }
            if (Api.HasNewDeviceParams())
            {
                Api.ReloadDeviceParams();
                ViewerParamsReady = Api.HasDeviceParams();
                if (viewerScanInProgress && ViewerParamsReady)
                {
                    UsingGenericViewerParams = false;
                    PlayerPrefs.DeleteKey(GenericViewerFlag);
                    viewerScanInProgress = false;
                }
            }
            Api.UpdateScreenParams();
            TriggerPressed = Api.IsTriggerPressed;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) { XrRunning = false; TriggerPressed = false; }
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (rhinoXNativeApiAvailable) SetRhinoXLegacyMesh(0);
#endif
        }
    }
}
#endif
