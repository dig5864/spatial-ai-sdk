using System;
using System.Collections;
using System.IO;
using SpatialAI.Unity;
using SpatialAI.XRHands;
using UnityEngine;

namespace SpatialAI.Samples
{
    public sealed class Unity6SharedCalibrationDemo : MonoBehaviour
    {
        public Camera xrCamera;
        public Shader targetShader;

        const float Stroke = .002f;
        SpatialImageTracking tracker;
        SpatialUvcHands hands;
        Transform blueAnchor, detailAnchor;
        readonly Material[] materials = new Material[2];
        readonly Mesh[] frames = new Mesh[2];
        string status;
        bool retryAvailable = true, retrying;
        SpatialVuforiaTargetCatalog.Target primaryTarget, secondaryTarget;
        string primaryImagePath, secondaryImagePath;
        int primaryWidthMm;

        void Start()
        {
            try
            {
                SpatialHandTracking.CameraSource = SpatialHandCameraSource.Uvc;
                SpatialHandTracking.GestureRecognitionEnabled = true;
                var head = xrCamera.transform;
                var handRig = new GameObject("Calibrated UVC hands");
                handRig.SetActive(false);
                handRig.transform.SetParent(head, false);
                hands = handRig.AddComponent<SpatialUvcHands>();
                hands.requireSharedCalibration = true;
                hands.physicalHead = head;
                hands.trackingOrigin = head;
                handRig.AddComponent<SpatialXRHandsVisualizer>().showEstimatedPresentation = false;
                handRig.SetActive(true);

                var mount = new GameObject("SDK calibrated UVC camera").transform;
                mount.SetParent(head, false);
                tracker = gameObject.AddComponent<SpatialImageTracking>();
                tracker.CameraMount = mount;
                tracker.RequireSharedCalibration = true;
                var targets = SpatialVuforiaTargetCatalog.LoadBuiltIn();
                if (targets.Count < 2) throw new InvalidDataException("The validation scene requires two SDK Vuforia targets.");
                primaryTarget = targets[0];
                secondaryTarget = targets[1];
                primaryWidthMm = primaryTarget.ResolveWidthMm(ReadBlueWidthOverride());
                primaryImagePath = primaryTarget.MaterializeImage();
                secondaryImagePath = secondaryTarget.MaterializeImage();
                blueAnchor = tracker.TargetAnchor = CreateTarget(mount, primaryTarget, new Color(.1f, .75f, 1f), primaryWidthMm, 0);
                detailAnchor = tracker.SecondTargetAnchor = CreateTarget(mount, secondaryTarget, new Color(1f, .55f, .13f), secondaryTarget.DefaultWidthMm, 1);
                Debug.Log("Spatial Unity 6 SDK targets: " + DescribeTarget(primaryTarget, primaryWidthMm) +
                    ", " + DescribeTarget(secondaryTarget, secondaryTarget.DefaultWidthMm));
                StartTracking();
            }
            catch (Exception error)
            {
                status = error.Message;
                Debug.LogError("Spatial Unity 6 validation: " + status);
            }
        }

        Transform CreateTarget(Transform mount, SpatialVuforiaTargetCatalog.Target target, Color color, int widthMm, int index)
        {
            float width = widthMm * .001f;
            var anchor = new GameObject(target.Id).transform;
            anchor.SetParent(mount, false);
            var outerX = width * .5f;
            var outerY = outerX / target.AspectRatio;
            var stroke = Mathf.Min(Stroke, Mathf.Min(outerX, outerY) * .5f);
            var innerX = outerX - stroke;
            var innerY = outerY - stroke;
            frames[index] = new Mesh { name = target.Id + " metric outline" };
            frames[index].vertices = new[]
            {
                new Vector3(-outerX,-outerY,0), new Vector3(-outerX,outerY,0),
                new Vector3(outerX,outerY,0), new Vector3(outerX,-outerY,0),
                new Vector3(-innerX,-innerY,0), new Vector3(-innerX,innerY,0),
                new Vector3(innerX,innerY,0), new Vector3(innerX,-innerY,0)
            };
            frames[index].triangles = new[] { 0,1,5,0,5,4, 1,2,6,1,6,5, 2,3,7,2,7,6, 3,0,4,3,4,7 };
            frames[index].RecalculateBounds();
            materials[index] = new Material(targetShader) { color = color };
            anchor.gameObject.AddComponent<MeshFilter>().sharedMesh = frames[index];
            anchor.gameObject.AddComponent<MeshRenderer>().sharedMaterial = materials[index];

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Target centre cube";
            cube.transform.SetParent(anchor, false);
            cube.transform.localScale = Vector3.one * (width * .2f);
            cube.GetComponent<Renderer>().sharedMaterial = materials[index];
            Destroy(cube.GetComponent<Collider>());
            anchor.gameObject.SetActive(false);
            return anchor;
        }

        void StartTracking()
        {
            retryAvailable = true;
            if (tracker == null || blueAnchor == null || detailAnchor == null) return;
            try
            {
                var config = ReadVuforiaConfig();
                if (string.IsNullOrWhiteSpace(config?.license))
                    throw new InvalidDataException("Set the Vuforia license in vuforia-config.json in the application files directory.");
                tracker.StartTracking(config.license, primaryImagePath, primaryWidthMm * .001f,
                    secondImagePath: secondaryImagePath, secondWidthM: secondaryTarget.DefaultWidthMm * .001f);
                retryAvailable = false;
                status = null;
            }
            catch (Exception error) { status = error.Message; }
        }

        static VuforiaConfig ReadVuforiaConfig()
        {
            string path = Path.Combine(Application.persistentDataPath, "vuforia-config.json");
            return File.Exists(path) ? JsonUtility.FromJson<VuforiaConfig>(File.ReadAllText(path)) : null;
        }

        static int ReadBlueWidthOverride()
        {
            int width = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var intent = activity.Call<AndroidJavaObject>("getIntent"))
                width = intent.Call<int>("getIntExtra", "spatial_blue_card_width_mm", 0);
#endif
            return width == 0 ? ReadVuforiaConfig()?.blueCardWidthMm ?? 0 : width;
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused && tracker != null && tracker.State == "stopped") retryAvailable = true;
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24, 24, Mathf.Min(540, Screen.width - 48), 190), GUI.skin.box);
            if (primaryTarget != null && secondaryTarget != null)
                GUILayout.Label(DescribeTarget(primaryTarget, primaryWidthMm) + ": " + Seen(blueAnchor) +
                    " | " + DescribeTarget(secondaryTarget, secondaryTarget.DefaultWidthMm) + ": " + Seen(detailAnchor));
            GUILayout.Label("Vuforia: " + (status ?? tracker?.State ?? "starting"));
            GUILayout.Label("UVC hands: " + (hands?.Status ?? "starting"));
            var frame = hands?.cameraTracking?.Latest;
            if (frame?.hands != null && hands.Subsystem != null && hands.Subsystem.running)
                foreach (var hand in frame.hands)
                    if (hand != null) GUILayout.Label("Gesture: " + hand.Gesture);
            if (!retrying && (retryAvailable || tracker?.State.StartsWith("error:", StringComparison.Ordinal) == true))
                if (GUILayout.Button("Retry tracking"))
                    StartCoroutine(RetryTracking());
            GUILayout.EndArea();
        }

        static string Seen(Transform anchor) => anchor != null && anchor.gameObject.activeSelf ? "tracked" : "searching";
        static string DescribeTarget(SpatialVuforiaTargetCatalog.Target target, int widthMm) => target.Name + " " + widthMm + " mm";

        IEnumerator RetryTracking()
        {
            retrying = true;
            if (!retryAvailable && tracker != null) tracker.StopTracking();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (tracker != null && tracker.IsStopping && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (hands != null) hands.ReloadCalibration();
            StartTracking();
            retrying = false;
        }

        void OnDestroy()
        {
            foreach (var material in materials) if (material != null) Destroy(material);
            foreach (var frame in frames) if (frame != null) Destroy(frame);
        }

        [Serializable] sealed class VuforiaConfig { public string license; public int blueCardWidthMm; }
    }
}
