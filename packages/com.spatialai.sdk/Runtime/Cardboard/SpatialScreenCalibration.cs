#if SPATIALAI_HAS_CARDBOARD
using System;
using System.Globalization;
using SpatialAI.Core;
using SpatialAI.Unity;
using UnityEngine;
using Google.XR.Cardboard;

namespace SpatialAI.Cardboard
{
    /// <summary>Unwarped screen ruler. XR submission is suspended by the bridge
    /// while this panel is open; use a real ruler on the bare phone screen.</summary>
    [RequireComponent(typeof(SpatialCardboardBridge))]
    public sealed class SpatialScreenCalibration : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        private SpatialCardboardBridge bridge;
        private PhoneScreenMetrics metrics;
        private string width = "", height = "", offsetX = "0", offsetY = "0", status = "";
        private float nextRead;
        private Camera calibrationCamera;
        private int previousCullingMask;
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject nativeButton;
        private ButtonListener nativeListener;
        private volatile bool openRequested, destroyed;
        private float nextGearLayout;
        private sealed class ButtonListener : AndroidJavaProxy
        {
            private readonly SpatialScreenCalibration owner;
            public ButtonListener(SpatialScreenCalibration owner) : base("android.view.View$OnClickListener") { this.owner = owner; }
            public void onClick(AndroidJavaObject view) { owner.openRequested = true; }
        }
        private void OnEnable()
        {
            OnAndroidUi(activity =>
            {
                if (destroyed || nativeButton != null) return;
                nativeButton = new AndroidJavaObject("android.widget.Button", activity);
                // Cardboard renders the gear. This transparent native hit target stays
                // above XR submission without adding another visible button.
                nativeButton.Call("setText", "");
                nativeButton.Call("setContentDescription", "Screen size / 50 mm ruler");
                nativeButton.Call("setBackgroundColor", 0);
                nativeButton.Call("setTextColor", -1);
                nativeButton.Call("setTextSize", 24f);
                nativeButton.Call("setPadding", 0, 0, 0, 0);
                nativeButton.Call("setMinimumWidth", 0);
                nativeButton.Call("setMinimumHeight", 0);
                nativeListener = new ButtonListener(this);
                nativeButton.Call("setOnClickListener", nativeListener);
                using (var parameters = new AndroidJavaObject("android.widget.FrameLayout$LayoutParams", 1, 1, 53))
                    activity.Call("addContentView", nativeButton, parameters);
            });
        }
        private void UpdateNativeGearLayout()
        {
            var rect = Widget.GearButtonRect;
            int outputWidth = Screen.width, outputHeight = Screen.height;
            bool renderFallbackIcon = bridge == null || !bridge.XrRunning;
            OnAndroidUi(activity =>
            {
                if (destroyed || nativeButton == null || outputWidth <= 0 || outputHeight <= 0) return;
                using (var parent = nativeButton.Call<AndroidJavaObject>("getParent"))
                {
                    if (parent == null) return;
                    int windowWidth = parent.Call<int>("getWidth"), windowHeight = parent.Call<int>("getHeight");
                    if (windowWidth <= 0 || windowHeight <= 0) return;
                    float sx = (float)windowWidth / outputWidth, sy = (float)windowHeight / outputHeight;
                    // Unity uses a bottom-left origin; Android margins use top-left.
                    using (var parameters = new AndroidJavaObject("android.widget.FrameLayout$LayoutParams",
                        Math.Max(1, (int)Math.Round(rect.width * sx)), Math.Max(1, (int)Math.Round(rect.height * sy)), 51))
                    {
                        parameters.Set("leftMargin", (int)Math.Round(rect.x * sx));
                        parameters.Set("topMargin", (int)Math.Round((outputHeight - rect.yMax) * sy));
                        nativeButton.Call("setLayoutParams", parameters);
                    }
                    // Keep calibration reachable when an invalid layout stops XR.
                    nativeButton.Call("setText", renderFallbackIcon ? "\u2699\uFE0E" : "");
                }
            });
        }
        private void OnDestroy()
        {
            destroyed = true;
            RestoreCamera();
            OnAndroidUi(activity =>
            {
                if (nativeButton == null) return;
                using (var parent = nativeButton.Call<AndroidJavaObject>("getParent"))
                    if (parent != null) parent.Call("removeView", nativeButton);
                nativeButton.Dispose(); nativeButton = null; nativeListener = null;
            });
        }
        private static void OnAndroidUi(Action<AndroidJavaObject> action)
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    using (activity)
                        try { action(activity); }
                        catch (Exception e) { Debug.LogWarning("Spatial screen button: " + e.Message); }
                }));
            }
        }
        private void SetNativeButtonVisible(bool visible)
        {
            OnAndroidUi(activity => { if (nativeButton != null) nativeButton.Call("setVisibility", visible ? 0 : 8); });
        }
#endif
        private void Awake() { bridge = GetComponent<SpatialCardboardBridge>(); }
        public void Open()
        {
            metrics = SpatialPhoneScreen.Read();
            var screen = SpatialPhoneScreen.Resolve(metrics);
            width = screen == null ? "" : Format(screen.widthMm); height = screen == null ? "" : Format(screen.heightMm);
            offsetX = screen == null ? "0" : Format(screen.offsetXmm); offsetY = screen == null ? "0" : Format(screen.offsetYmm);
            status = "Match BOTH 50 mm rulers on the bare screen. Same screen plane/tilt required.";
            if (!IsOpen)
            {
                calibrationCamera = Camera.main;
                if (calibrationCamera != null) { previousCullingMask = calibrationCamera.cullingMask; calibrationCamera.cullingMask = 0; }
            }
            IsOpen = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            SetNativeButtonVisible(false);
#endif
        }
        public void Close()
        {
            IsOpen = false; RestoreCamera(); bridge.RefreshPhoneDisplay();
#if UNITY_ANDROID && !UNITY_EDITOR
            SetNativeButtonVisible(true);
#endif
        }
        private void RestoreCamera()
        {
            if (calibrationCamera != null) { calibrationCamera.cullingMask = previousCullingMask; calibrationCamera = null; }
        }
        private static string Format(float value) { return value.ToString("F3", CultureInfo.InvariantCulture); }
        private static bool Parse(string value, out float parsed) { return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed); }
        private PhoneScreenCalibration Candidate()
        {
            if (metrics == null || !Parse(width, out float w) || !Parse(height, out float h) ||
                !Parse(offsetX, out float x) || !Parse(offsetY, out float y)) return null;
            return new PhoneScreenCalibration { phoneKey = metrics.phoneKey, widthMm = w, heightMm = h,
                offsetXmm = x, offsetYmm = y, measured = true };
        }
        private void Save(bool establishRig)
        {
            try
            {
                metrics = SpatialPhoneScreen.Read();
                var screen = Candidate();
                if (screen == null || !screen.IsValid || screen.widthMm <= screen.heightMm || !metrics.fullDisplayMapping)
                    throw new InvalidOperationException("Enter valid landscape dimensions; fullscreen mapping must be available.");
                ScreenRigProfile rig = null;
                if (establishRig)
                {
                    var host = bridge.MatchedHostProfile;
                    if (host == null || !ScreenLayout.ValidRects(host.referenceEyeRects))
                        throw new InvalidOperationException("Use the reference phone with known eye rectangles to establish the rig.");
                    var result = ScreenLayout.FromReference(host.referenceEyeRects, host.nativePortraitHeight,
                        host.nativePortraitWidth, screen, "fixed-reflected-rig-v1", host.referenceOutputRotate180, out rig);
                    if (!result.Success) throw new InvalidOperationException(result.Message);
                    rig.phoneImu = host.imu; rig.invertViewPitch = host.invertViewPitch; rig.invertViewRoll = host.invertViewRoll;
                    rig.reflectHandDisplayX = host.reflectHandDisplayX; rig.uvcSwapHandedness = host.uvcSwapHandedness;
                    rig.reverseStereoBaseline = host.reverseStereoBaseline;
                }
                else
                {
                    var existing = SpatialPhoneScreen.LoadRig(bridge.phoneRigProfile);
                    if (existing != null)
                    {
                        var result = ScreenLayout.Calculate(existing, screen, metrics.widthPx, metrics.heightPx, out var ignored);
                        if (!result.Success) throw new InvalidOperationException(result.Message);
                    }
                }
                SpatialPhoneScreen.Save(screen);
                if (rig != null) SpatialPhoneScreen.SaveRig(rig);
                status = rig == null ? "Screen saved. Close to apply." : "Screen + rig saved: " + SpatialPhoneScreen.RigPath;
                bridge.RefreshPhoneDisplay();
            }
            catch (Exception e) { status = "Save failed: " + e.Message; }
        }
        private void Update()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (openRequested) { openRequested = false; Open(); }
            if (Time.unscaledTime >= nextGearLayout)
            {
                nextGearLayout = Time.unscaledTime + .5f;
                UpdateNativeGearLayout();
            }
#endif
            if (IsOpen && Time.unscaledTime >= nextRead) { metrics = SpatialPhoneScreen.Read(); nextRead = Time.unscaledTime + 1; }
        }
        private void OnGUI()
        {
            var previous = GUI.matrix; GUI.matrix = Matrix4x4.identity;
            try
            {
                if (!IsOpen)
                {
#if !UNITY_ANDROID || UNITY_EDITOR
                    GUI.Box(new Rect(Screen.width - 620, Screen.height - 88, 610, 32), bridge.PhoneDisplayStatus);
                    if (GUI.Button(new Rect(Screen.width - 230, Screen.height - 48, 220, 38), "Screen size / 50 mm ruler")) Open();
#endif
                    return;
                }
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.blackTexture);
                DrawCalibrationPanel();
                var candidate = Candidate();
                if (candidate != null && candidate.IsValid && metrics != null && metrics.fullDisplayMapping)
                {
                    float horizontal = 50 * Screen.width / candidate.widthMm, vertical = 50 * Screen.height / candidate.heightMm;
                    GUI.DrawTexture(new Rect(60, 65, horizontal, 3), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(60, 58, 2, 18), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(60 + horizontal, 58, 2, 18), Texture2D.whiteTexture);
                    GUI.Label(new Rect(70, 30, 250, 25), "Horizontal: 50 mm (between ticks)");
                    float verticalTop = Mathf.Max(5, (Screen.height - vertical) / 2);
                    GUI.DrawTexture(new Rect(35, verticalTop, 3, vertical), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(28, verticalTop, 18, 2), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(28, verticalTop + vertical, 18, 2), Texture2D.whiteTexture);
                    GUI.Label(new Rect(60, verticalTop + 5, 250, 25), "Vertical: 50 mm (between ticks)");
                }
            }
            finally { GUI.matrix = previous; GUI.enabled = true; }
        }
        private void DrawCalibrationPanel()
        {
            // Scale only the control panel. The rulers below remain in output pixels.
            float scale = Mathf.Min(Screen.height / 1080f, Screen.width / 1800f);
            float panelWidth = 1000f, panelHeight = 900f;
            var previous = GUI.matrix;
            try
            {
                GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width - (panelWidth + 20) * scale,
                    (Screen.height - panelHeight * scale) / 2f, 0), Quaternion.identity, new Vector3(scale, scale, 1));
                var label = new GUIStyle(GUI.skin.label) { fontSize = 28, wordWrap = true };
                var field = new GUIStyle(GUI.skin.textField) { fontSize = 30, alignment = TextAnchor.MiddleLeft };
                var button = new GUIStyle(GUI.skin.button) { fontSize = 28, wordWrap = true };
                var title = new GUIStyle(label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
                GUI.Box(new Rect(0, 0, panelWidth, panelHeight), "");
                GUI.Label(new Rect(20, 20, 960, 50), "Screen calibration (bare screen)", title);
                GUI.Label(new Rect(20, 85, 960, 90), metrics == null ? "Reading..." : metrics.phoneKey + "\n" + metrics.diagnostic, label);
                width = Field(185, "Active width mm", width, label, field);
                height = Field(260, "Active height mm", height, label, field);
                offsetX = Field(335, "Screen centre X in rig mm", offsetX, label, field);
                offsetY = Field(410, "Screen centre Y in rig mm", offsetY, label, field);
                GUI.Label(new Rect(20, 490, 960, 100), "Adjust until BOTH lines measure 50 mm.\nOffsets: right/down positive; 0 = screen centred.", label);
                if (GUI.Button(new Rect(20, 610, 460, 70), "Save measured screen", button)) Save(false);
                bool canEstablish = bridge.MatchedHostProfile != null && ScreenLayout.ValidRects(bridge.MatchedHostProfile.referenceEyeRects) && bridge.phoneRigProfile == null;
                GUI.enabled = canEstablish;
                if (GUI.Button(new Rect(500, 610, 480, 70), "Save + establish reference rig", button)) Save(true);
                GUI.enabled = true;
                if (GUI.Button(new Rect(20, 700, 960, 70), "Close / resume display", button)) Close();
                GUI.Label(new Rect(20, 795, 960, 95), status, label);
            }
            finally { GUI.matrix = previous; GUI.enabled = true; }
        }
        private static string Field(float top, string text, string value, GUIStyle label, GUIStyle field)
        {
            GUI.Label(new Rect(20, top, 420, 60), text, label);
            return GUI.TextField(new Rect(460, top, 520, 60), value, field);
        }
    }
}
#endif
