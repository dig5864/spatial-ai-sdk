#if SPATIALAI_HAS_CARDBOARD
using SpatialAI.Cardboard;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

namespace SpatialAI.Samples
{
    // One Unity unit is one metre. Anchor once after XR pose becomes available.
    public sealed class PhysicalScaleValidation : MonoBehaviour
    {
        public const float CubeSizeMetres = .2f;
        public const float FrontDistanceMetres = .5f;
        public const float TargetDepressionDegrees = 15f;
        public Camera xrCamera;
        public Transform targetRoot;
        private SpatialCardboardBridge bridge;
        private bool pendingPlacement = true;
        private bool previousTrigger;
        private int placements;
        private float displayReadySince = -1f;

        private void Start()
        {
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            bridge = FindObjectOfType<SpatialCardboardBridge>();
            targetRoot.gameObject.SetActive(false);
        }

        private void Update()
        {
            var trigger = bridge != null && bridge.TriggerPressed;
            var touch = Touchscreen.current;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if ((touch != null && touch.primaryTouch.press.wasPressedThisFrame) ||
                (keyboard != null && keyboard.rKey.wasPressedThisFrame) ||
                (mouse != null && mouse.leftButton.wasPressedThisFrame) ||
                Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.R) ||
                (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began) ||
                (trigger && !previousTrigger)) pendingPlacement = true;
            previousTrigger = trigger;
        }

        private void LateUpdate()
        {
            if (bridge == null || !bridge.XrRunning || !bridge.RhinoXLegacyMeshApplied)
            {
                displayReadySince = -1f;
                return;
            }
            if (displayReadySince < 0f) displayReadySince = Time.unscaledTime;
            if (!pendingPlacement || xrCamera == null || Time.unscaledTime - displayReadySince < 1f) return;
            var head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            var valid = head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyeRotation, out var rotation) ||
                head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rotation);
            if (!valid)
            {
                var hmd = InputSystem.GetDevice<XRHMD>();
                valid = hmd != null && hmd.centerEyeRotation != null &&
                    Quaternion.Dot(hmd.centerEyeRotation.ReadValue(), hmd.centerEyeRotation.ReadValue()) > .9f;
            }
            if (!valid) return;
            targetRoot.SetPositionAndRotation(xrCamera.transform.position, xrCamera.transform.rotation);
            targetRoot.gameObject.SetActive(true);
            pendingPlacement = false;
            placements++;
            Debug.Log("SpatialAI SCALE placement=" + placements +
                " width=0.200m height=0.200m depth=0.200m frontPlane=0.500m center=0.600m depression=15deg" +
                " anchor=" + targetRoot.position.ToString("F4") + " rotation=" + targetRoot.rotation.ToString("F4") +
                " layout=" + bridge.PhoneDisplayStatus + " optics=UNVERIFIED");
        }
    }
}
#endif
