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
    // Cardboard owns display submission. A matched Spatial AI optical profile
    // supplies its display mesh and projection through the native boundary.
    public sealed class SpatialCardboardBridge : MonoBehaviour
    {
        [Tooltip("External headset ID from a future connection service. If empty, use the phone device code or model for diagnostics only.")]
        public string headsetDeviceId;
        public bool scanCardboardViewerOnStart;
        [Tooltip("Only for the sample: load generic Cardboard I/O 2015 viewer parameters when none are saved. Never use these parameters to validate RhinoX optics.")]
        public bool useGenericViewerForDiagnostics;
        public DeviceIdentity Identity { get; private set; }
        public OpticalProfileDescriptor MatchedProfile { get; private set; }
        public Result ProfileResult { get; private set; }
        public bool XrRunning { get; private set; }
        public bool ViewerParamsReady { get; private set; }
        public bool UsingGenericViewerParams { get; private set; }
        public bool TriggerPressed { get; private set; }
        public bool RhinoXLegacyMeshApplied { get; private set; }
        private bool spatialOpticsConfigured;
        private bool spatialTrackingConfigured;
        private bool opticsConfigurationRejected;
        private bool orientationMismatchLogged;
#if UNITY_ANDROID && !UNITY_EDITOR
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_configureSpatialOptics")]
        private static extern int ConfigureSpatialOptics(string meshId, string meshHash,
            int screenWidth, int screenHeight, float ipd, float nearPlane,
            float left, float right, float top, float bottom);
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_clearSpatialOptics")]
        private static extern void ClearSpatialOptics();
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_isSpatialOpticsApplied")]
        private static extern int IsSpatialOpticsApplied();
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_configureSpatialTracking")]
        private static extern int ConfigureSpatialTracking(float pitchDegrees, float yOffset,
            float imuX, float imuY, float imuZ, float frameX, float frameY,
            float frameZ, float frameW);
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_clearSpatialTracking")]
        private static extern void ClearSpatialTracking();
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
            opticsConfigurationRejected = knownRhinoX;
            Debug.Log("Spatial AI device: " + Identity.DisplayName + "; optics: " + ProfileResult.Message);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (knownRhinoX && MatchedProfile != null)
            {
                var displayResult = MatchedProfile.ValidateDisplayConfiguration();
                if (!displayResult.Success)
                {
                    Debug.LogError("Spatial AI optics rejected: " + displayResult.Message);
                }
                else try
                {
                    spatialOpticsConfigured = ConfigureSpatialOptics(
                        MatchedProfile.meshId, MatchedProfile.meshAssetHash,
                        MatchedProfile.screenWidth, MatchedProfile.screenHeight,
                        MatchedProfile.ipdMetres, MatchedProfile.frustumNearMetres,
                        MatchedProfile.frustumLeftMetres, MatchedProfile.frustumRightMetres,
                        MatchedProfile.frustumTopMetres, MatchedProfile.frustumBottomMetres) != 0;
                    var tracking = MatchedProfile.trackingCalibration;
                    var trackingResult = tracking != null ? tracking.ValidateDesignReference() :
                        new Result(ErrorCode.InvalidArgument, "IMU-to-eye design calibration is missing.");
                    if (spatialOpticsConfigured && trackingResult.Success)
                        spatialTrackingConfigured = ConfigureSpatialTracking(
                            tracking.eyeRenderToDesignDegreesX,
                            tracking.eyeRenderToDesignYOffsetMetres,
                            tracking.designEyeToImuXMetres,
                            tracking.designEyeToImuYMetres,
                            tracking.designEyeToImuZMetres,
                            tracking.designImuFromCardboardHeadX,
                            tracking.designImuFromCardboardHeadY,
                            tracking.designImuFromCardboardHeadZ,
                            tracking.designImuFromCardboardHeadW) != 0;
                    if (spatialOpticsConfigured && spatialTrackingConfigured)
                    {
                        opticsConfigurationRejected = false;
                        Debug.Log("Spatial AI optical profile configured: " + MatchedProfile.profileId);
                        Debug.Log("Spatial AI IMU-to-eye design reference configured: " +
                            tracking.calibrationVersion + "; Android axis mapping " +
                            (tracking.axisMappingVerified ? "verified" : "UNVERIFIED"));
                    }
                    else
                    {
                        if (spatialOpticsConfigured) { ClearSpatialOptics(); spatialOpticsConfigured = false; }
                        Debug.LogError("Spatial AI optics/tracking rejected: " +
                            (trackingResult.Success ? "native configuration failed." : trackingResult.Message));
                    }
                }
                catch (Exception e)
                {
                    if (spatialTrackingConfigured) { ClearSpatialTracking(); spatialTrackingConfigured = false; }
                    if (spatialOpticsConfigured) { ClearSpatialOptics(); spatialOpticsConfigured = false; }
                    Debug.LogError("Spatial AI native optics/tracking API unavailable: " + e.Message);
                }
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
            if (spatialTrackingConfigured && Screen.orientation != ScreenOrientation.LandscapeLeft)
            {
                if (!orientationMismatchLogged)
                {
                    Debug.LogError("RhinoX IMU axis mapping currently requires LandscapeLeft orientation.");
                    orientationMismatchLogged = true;
                }
                if (XrRunning) display.Stop();
                XrRunning = false;
                return;
            }
            if (opticsConfigurationRejected)
            {
                if (XrRunning) display.Stop();
                XrRunning = false;
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (spatialOpticsConfigured) RhinoXLegacyMeshApplied = IsSpatialOpticsApplied() != 0;
#endif
            if (!XrRunning) return;
            if (spatialOpticsConfigured)
            {
                ViewerParamsReady = RhinoXLegacyMeshApplied;
                UsingGenericViewerParams = false;
                Api.UpdateScreenParams();
                TriggerPressed = Api.IsTriggerPressed;
                return;
            }
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
            if (spatialTrackingConfigured) ClearSpatialTracking();
            if (spatialOpticsConfigured) ClearSpatialOptics();
#endif
        }
    }
}
#endif
