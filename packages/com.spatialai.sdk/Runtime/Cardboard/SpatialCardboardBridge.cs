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
    public enum PhoneStereoDiagnosticMode { Profile, Normal, Zero, Reverse }
    public sealed class SpatialCardboardBridge : MonoBehaviour
    {
        [Tooltip("External headset ID from a future connection service. If empty, use the phone device code or model for diagnostics only.")]
        public string headsetDeviceId;
        public bool scanCardboardViewerOnStart;
        [Tooltip("Only for the sample: load generic Cardboard I/O 2015 viewer parameters when none are saved. Never use these parameters to validate RhinoX optics.")]
        public bool useGenericViewerForDiagnostics;
        [Tooltip("Comparison scenes only: render the original RhinoX mesh on a phone panel. This is not calibration for the phone's lenses.")]
        public bool useRhinoXReferenceMeshForDiagnostics;
        [Tooltip("Fixed rig physical screen regions. If empty, load SpatialAI/ScreenRig or the locally saved rig.json.")]
        public TextAsset phoneRigProfile;
        // Session-only A/B diagnostic. Never changes the measured physical IPD or camera calibration.
        public static PhoneStereoDiagnosticMode PhoneStereoMode { get; private set; }
        public static void SetPhoneStereoDiagnosticMode(PhoneStereoDiagnosticMode mode)
        {
            if(!Enum.IsDefined(typeof(PhoneStereoDiagnosticMode),mode))throw new ArgumentOutOfRangeException(nameof(mode));
            PhoneStereoMode=mode;
            Debug.Log("Spatial phone stereo diagnostic requested: "+mode+"; resets on app restart");
        }
        public static float PhoneStereoDiagnosticBaseline(float ipdMetres,bool reverseInProfile=false)
        {
            if(float.IsNaN(ipdMetres)||float.IsInfinity(ipdMetres)||ipdMetres<=0||ipdMetres>.1f)throw new ArgumentOutOfRangeException(nameof(ipdMetres));
            if(PhoneStereoMode==PhoneStereoDiagnosticMode.Zero)return 0;
            bool reverse=PhoneStereoMode==PhoneStereoDiagnosticMode.Reverse || PhoneStereoMode==PhoneStereoDiagnosticMode.Profile && reverseInProfile;
            return reverse?-ipdMetres:ipdMetres;
        }
        public string PhoneDisplayStatus { get; private set; } = "Not configured";
        public float[] PhoneEyeRects { get; private set; }
        private SpatialScreenCalibration screenCalibration;
        private string phoneConfigurationKey;
        private float nextScreenRead;
        private bool restartPhoneDisplay;
        private bool isPhoneReference;
        private int phoneDisplayRevision;
        public void RefreshPhoneDisplay() { phoneConfigurationKey = null; nextScreenRead = 0; }
        public DeviceIdentity Identity { get; private set; }
        public HostDeviceProfile MatchedHostProfile { get; private set; }
        public Result HostProfileResult { get; private set; }
        public OpticalProfileDescriptor MatchedProfile { get; private set; }
        public Result ProfileResult { get; private set; }
        public bool XrRunning { get; private set; }
        public bool ViewerParamsReady { get; private set; }
        public bool UsingGenericViewerParams { get; private set; }
        public bool TriggerPressed { get; private set; }
        public bool RhinoXLegacyMeshApplied { get; private set; }
        public bool HostTrackingConfigured { get; private set; }
        public string HostTrackingStatus { get; private set; } = "default Cardboard";
        private bool hostTrackingRejected;
        private readonly float[] hostPoseSample = new float[8];
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
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_configureSpatialEyeRects")]
        private static extern int ConfigureSpatialEyeRects(float lx, float ly, float lw, float lh,
            float rx, float ry, float rw, float rh, int rotateOutput180);
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_configureSpatialPhoneDisplay")]
        private static extern int ConfigureSpatialPhoneDisplay(string meshId, string meshHash,
            int width, int height, float ipd, float nearPlane, float left, float right, float top, float bottom,
            [In] float[] rects, int rotateOutput180, int revision);
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_getAppliedPhoneDisplayRevision")]
        private static extern int GetAppliedPhoneDisplayRevision();
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_isSpatialOpticsApplied")]
        private static extern int IsSpatialOpticsApplied();
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_configureSpatialTracking")]
        private static extern int ConfigureSpatialTracking(float pitchDegrees, float yOffset,
            float imuX, float imuY, float imuZ, float frameX, float frameY,
            float frameZ, float frameW);
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_clearSpatialTracking")]
        private static extern void ClearSpatialTracking();
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_configureHostTracking")]
        private static extern int ConfigureHostTracking(float bx, float by, float bz, float bw,
            float mx, float my, float mz, float mw, int preserveNeck, int invertInput);
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_clearHostTracking")]
        private static extern void ClearHostTracking();
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_setHostViewCompensation")]
        private static extern void SetHostViewCompensation(int invertPitch, int invertRoll);
        [DllImport("GfxPluginCardboard", EntryPoint = "CardboardUnity_getHostTrackingSample")]
        private static extern int GetHostTrackingSample([Out] float[] sample);
#endif
        private bool viewerParamsChecked;
        private bool viewerScanInProgress;

        private const string GenericViewerFlag = "SpatialAI.Samples.GenericViewer";
        // Google's public Cardboard I/O 2015 example URI. This is a render/input diagnostic only.
        private const string GenericViewerUri =
            "https://google.com/cardboard/cfg?p=CgZHb29nbGUSEkNhcmRib2FyZCBJL08gMjAxNR0rGBU9JQHegj0qEAAASEIAAEhCAABIQgAASEJYADUpXA89OggeZnc-Ej6aPlAAYAM";

        private void Awake()
        {
            if (useRhinoXReferenceMeshForDiagnostics) SpatialPhoneScreen.ConfiguredRig = phoneRigProfile;
        }

        private void Start()
        {
            if (useRhinoXReferenceMeshForDiagnostics)
            {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                SpatialPhoneScreen.ConfiguredRig = phoneRigProfile;
            }
            Identity = SpatialDeviceProfiles.ReadHostIdentity(headsetDeviceId);
            HostProfileResult = SpatialDeviceProfiles.SelectHost(Identity, out var hostProfile);
            if (!HostProfileResult.Success && hostProfile != null)
            {
                hostTrackingRejected = true;
                HostTrackingStatus = HostProfileResult.Message;
                Debug.LogError("Spatial host configuration rejected: " + HostTrackingStatus);
                return;
            }
            if (HostProfileResult.Success)
            {
                MatchedHostProfile = hostProfile;
                Application.targetFrameRate = hostProfile.targetFrameRate;
                Screen.orientation = ScreenOrientation.LandscapeLeft;
#if UNITY_ANDROID && !UNITY_EDITOR
                if (hostProfile.imu != null)
                {
                    var imu = hostProfile.imu;
                    try
                    {
                        HostTrackingConfigured = ConfigureHostTracking(imu.basisX, imu.basisY, imu.basisZ, imu.basisW,
                            imu.mountX, imu.mountY, imu.mountZ, imu.mountW, imu.preserveNeckModel ? 1 : 0,
                            imu.invertInputRotation ? 1 : 0) != 0;
                        hostTrackingRejected = !HostTrackingConfigured;
                        if (HostTrackingConfigured && (hostProfile.invertViewPitch || hostProfile.invertViewRoll))
                            SetHostViewCompensation(hostProfile.invertViewPitch ? 1 : 0, hostProfile.invertViewRoll ? 1 : 0);
                        HostTrackingStatus = imu.calibrationVersion + (imu.validatedForHardware ? " VERIFIED" : " UNVERIFIED");
                        Debug.Log("Spatial phone IMU: " + HostTrackingStatus + "; configured=" + HostTrackingConfigured +
                            "; B=(" + imu.basisX + "," + imu.basisY + "," + imu.basisZ + "," + imu.basisW + ")" +
                            "; M=(" + imu.mountX + "," + imu.mountY + "," + imu.mountZ + "," + imu.mountW + ")" +
                            "; invertInput=" + imu.invertInputRotation + "; invertViewPitch=" + hostProfile.invertViewPitch +
                            "; invertViewRoll=" + hostProfile.invertViewRoll);
                    }
                    catch (Exception e) { hostTrackingRejected = true; HostTrackingStatus = e.Message; Debug.LogError(e); }
                }
#endif
                // Apply the separately selected phone IMU profile after Cardboard conversion.
                // Generic viewer optics allow a first screen/IMU test; a real viewer
                // still needs its QR calibration before assessing optical accuracy.
                if (string.IsNullOrEmpty(headsetDeviceId)) useGenericViewerForDiagnostics = true;
                // Cardboard reads the active display dimensions. The native panel size
                // is diagnostic metadata, not a forced resolution for an external display.
                Debug.Log("Spatial AI host profile configured: " + hostProfile.profileId +
                    "; target FPS=" + hostProfile.targetFrameRate +
                    "; native portrait panel=" + hostProfile.nativePortraitWidth + "x" + hostProfile.nativePortraitHeight +
                    "; optical calibration selected separately.");
            }
            else Debug.Log("Spatial AI host profile: " + HostProfileResult.Message);
            // The current RhinoX AIO reports QUALCOMM / RhinoX / kona. Do not
            // infer headset identity from the generic Qualcomm device code alone.
            bool knownRhinoX = string.Equals(Identity.Manufacturer, "QUALCOMM", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Identity.Model, "RhinoX", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Identity.DeviceCode, "kona", StringComparison.OrdinalIgnoreCase);
            if (knownRhinoX && string.IsNullOrEmpty(Identity.HeadsetId)) Identity.HeadsetId = "RhinoXPro";
            ProfileResult = SpatialDeviceProfiles.Select(Identity, out var profile);
            MatchedProfile = profile;
            if (MatchedHostProfile != null && string.IsNullOrEmpty(Identity.HeadsetId))
                ProfileResult = new Result(ErrorCode.Unsupported, "Phone screen: Cardboard viewer parameters; optical calibration pending.");
            opticsConfigurationRejected = knownRhinoX;
            isPhoneReference = !knownRhinoX && useRhinoXReferenceMeshForDiagnostics;
            if (isPhoneReference)
            {
                ProfileResult = SpatialDeviceProfiles.Select(new DeviceIdentity { HeadsetId = "RhinoXPro" }, out var reference);
                MatchedProfile = reference;
                opticsConfigurationRejected = true;
                useGenericViewerForDiagnostics = false;
                screenCalibration = GetComponent<SpatialScreenCalibration>();
                if (screenCalibration == null) screenCalibration = gameObject.AddComponent<SpatialScreenCalibration>();
            }
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
#if UNITY_ANDROID && !UNITY_EDITOR
            if (isPhoneReference && (Time.unscaledTime >= nextScreenRead ||
                Screen.width + "x" + Screen.height != phoneOutputSize)) UpdatePhoneDisplay();
#endif
            TriggerPressed = false;
            var manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            var loader = manager != null ? manager.activeLoader as Google.XR.Cardboard.XRLoader : null;
            var display = loader != null ? loader.GetLoadedSubsystem<XRDisplaySubsystem>() : null;
            XrRunning = display != null && display.running;
            if (isPhoneReference && screenCalibration != null && screenCalibration.IsOpen)
            {
                if (XrRunning) { restartPhoneDisplay = true; display.Stop(); }
                XrRunning = false; TriggerPressed = false; return;
            }
            if ((spatialTrackingConfigured || HostTrackingConfigured) && Screen.orientation != ScreenOrientation.LandscapeLeft)
            {
                if (!orientationMismatchLogged)
                {
                    Debug.LogError("Configured IMU mapping requires LandscapeLeft orientation.");
                    orientationMismatchLogged = true;
                }
                if (XrRunning) display.Stop();
                XrRunning = false;
                return;
            }
            if (opticsConfigurationRejected || hostTrackingRejected)
            {
                if (XrRunning) { restartPhoneDisplay = isPhoneReference; display.Stop(); }
                XrRunning = false;
                return;
            }
            if (isPhoneReference && restartPhoneDisplay && display != null && !display.running)
            {
                display.Start(); restartPhoneDisplay = false; XrRunning = display.running;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (spatialOpticsConfigured)
            {
                bool applied = isPhoneReference ? GetAppliedPhoneDisplayRevision() == phoneDisplayRevision : IsSpatialOpticsApplied() != 0;
                if (isPhoneReference && applied && !RhinoXLegacyMeshApplied)
                    Debug.Log("Spatial phone display applied: revision=" + phoneDisplayRevision + "; " + PhoneDisplayStatus);
                RhinoXLegacyMeshApplied = applied;
            }
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
            else RefreshPhoneDisplay();
        }

        private string phoneOutputSize;
#if UNITY_ANDROID && !UNITY_EDITOR
        private void UpdatePhoneDisplay()
        {
            nextScreenRead = Time.unscaledTime + 1;
            phoneOutputSize = Screen.width + "x" + Screen.height;
            var metrics = SpatialPhoneScreen.Read();
            var screen = SpatialPhoneScreen.Resolve(metrics);
            var rig = SpatialPhoneScreen.LoadRig(phoneRigProfile);
            string key = phoneOutputSize + "/" + metrics.displayWidthPx + "x" + metrics.displayHeightPx + "/" +
                metrics.rotation + "/" + metrics.modeId + "/" + metrics.displayId + "/" + metrics.fullDisplayMapping + "/" + Screen.safeArea + "/" + SpatialPhoneScreen.Revision +
                "/" + PhoneStereoMode + "/" + (screen == null ? "missing" : JsonUtility.ToJson(screen)) + "/" + (rig == null ? "missing" : JsonUtility.ToJson(rig));
            if (key == phoneConfigurationKey) return;
            phoneConfigurationKey = key;
            Debug.Log("Spatial screen metrics: display=" + metrics.displayWidthPx + "x" + metrics.displayHeightPx +
                "; output=" + phoneOutputSize + "; dpi=" + metrics.xdpi + "," + metrics.ydpi +
                "; physicalMm=" + (screen == null ? "missing" : screen.widthMm + "x" + screen.heightMm) +
                "; measured=" + (screen != null && screen.measured) + "; " + metrics.diagnostic);
            PhoneEyeRects = null; RhinoXLegacyMeshApplied = false;
            if (!metrics.fullDisplayMapping || screen == null || !screen.IsValid)
            { RejectPhoneDisplay(screen == null ? "Needs screen measurement. Open the 50 mm ruler." : metrics.diagnostic); return; }
            // Until a reference rig is saved, only its own reference phone may
            // derive an estimated physical baseline. Never infer it on another phone.
            if (rig == null && MatchedHostProfile != null && ScreenLayout.ValidRects(MatchedHostProfile.referenceEyeRects))
            {
                var reference = ScreenLayout.FromReference(MatchedHostProfile.referenceEyeRects,
                    MatchedHostProfile.nativePortraitHeight, MatchedHostProfile.nativePortraitWidth,
                    screen, "unsaved-reference-rig", MatchedHostProfile.referenceOutputRotate180, out rig);
                if (!reference.Success) { RejectPhoneDisplay(reference.Message); return; }
                rig.reverseStereoBaseline = MatchedHostProfile.reverseStereoBaseline;
            }
            if (rig == null) { RejectPhoneDisplay("Needs reference rig. Import the rig.json measured on the reference phone."); return; }
            var result = ScreenLayout.Calculate(rig, screen, Screen.width, Screen.height, out var rects);
            if (!result.Success) { RejectPhoneDisplay(result.Message); return; }
            var safe = Screen.safeArea;
            for (int e = 0; e < 2; e++)
            {
                int i = e * 4;
                float bottom = Screen.height - rects[i + 1] - rects[i + 3];
                if (rects[i] < safe.xMin || rects[i] + rects[i + 2] > safe.xMax ||
                    bottom < safe.yMin || bottom + rects[i + 3] > safe.yMax)
                { RejectPhoneDisplay("Eye region intersects an unsafe/cutout area. Adjust mount; no automatic safe-area shift."); return; }
            }
            if (MatchedProfile == null || !MatchedProfile.ValidateDisplayConfiguration().Success)
            { RejectPhoneDisplay("Reference optical mesh configuration unavailable."); return; }
            try
            {
                phoneDisplayRevision = phoneDisplayRevision == int.MaxValue ? 1 : phoneDisplayRevision + 1;
                spatialOpticsConfigured = ConfigureSpatialPhoneDisplay(MatchedProfile.meshId, MatchedProfile.meshAssetHash,
                    Screen.width, Screen.height, PhoneStereoDiagnosticBaseline(MatchedProfile.ipdMetres,rig.reverseStereoBaseline), MatchedProfile.frustumNearMetres,
                    MatchedProfile.frustumLeftMetres, MatchedProfile.frustumRightMetres, MatchedProfile.frustumTopMetres,
                    MatchedProfile.frustumBottomMetres, rects, rig.rotateOutput180 ? 1 : 0, phoneDisplayRevision) != 0;
                if (!spatialOpticsConfigured) { RejectPhoneDisplay("Native display configuration rejected."); return; }
                PhoneEyeRects = rects; opticsConfigurationRejected = false;
                PhoneDisplayStatus = (screen.measured && rig.referenceScreenMeasured ? "MEASURED layout" : "ESTIMATED layout") +
                    "; reference optics UNVERIFIED; " + phoneOutputSize;
                Debug.Log("Spatial phone display: " + PhoneDisplayStatus + "; rects=" + string.Join(",", rects));
                Debug.Log("Spatial phone stereo diagnostic applied: mode="+PhoneStereoMode+" signedBaselineMetres="+PhoneStereoDiagnosticBaseline(MatchedProfile.ipdMetres,rig.reverseStereoBaseline)+" revision="+phoneDisplayRevision);
            }
            catch (Exception e) { RejectPhoneDisplay("Phone display API unavailable: " + e.Message); }
        }
        private void RejectPhoneDisplay(string message)
        {
            if (spatialOpticsConfigured) ClearSpatialOptics();
            spatialOpticsConfigured = false; opticsConfigurationRejected = true;
            PhoneDisplayStatus = message; Debug.LogWarning("Spatial phone display: " + message);
        }
#endif

        public bool TryGetHostTrackingRotations(out Quaternion raw, out Quaternion mapped)
        {
            raw = mapped = Quaternion.identity;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!HostTrackingConfigured || GetHostTrackingSample(hostPoseSample) == 0) return false;
            raw = new Quaternion(hostPoseSample[0], hostPoseSample[1], hostPoseSample[2], hostPoseSample[3]);
            mapped = new Quaternion(hostPoseSample[4], hostPoseSample[5], hostPoseSample[6], hostPoseSample[7]);
            return true;
#else
            return false;
#endif
        }

        private void OnDestroy()
        {
            if (SpatialPhoneScreen.ConfiguredRig == phoneRigProfile) SpatialPhoneScreen.ConfiguredRig = null;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (HostTrackingConfigured) ClearHostTracking();
            if (spatialTrackingConfigured) ClearSpatialTracking();
            if (spatialOpticsConfigured) ClearSpatialOptics();
#endif
        }
    }
}
#endif
