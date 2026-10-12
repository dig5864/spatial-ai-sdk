#if SPATIALAI_HAS_CARDBOARD
using SpatialAI.Unity;
using SpatialAI.Cardboard;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.Samples
{
    /// <summary>RGB hand landmarks projected into eye space with approximate per-hand depth.</summary>
    public sealed class HandTrackingDemo : MonoBehaviour
    {
        public Camera xrCamera;
        public TextAsset uvcCalibrationJson;
        [Tooltip("Reflect rendered UVC hand X for the mirror display; physical hand data and labels stay unchanged.")]
        public bool reflectUvcDisplayX;
        [Tooltip("Correct UVC anatomical labels when no calibration JSON is assigned. JSON swapHandedness takes precedence.")]
        public bool defaultUvcSwapHandedness;
        [Tooltip("Scale all hand-demo visuals away from the eye while retaining their angular size. Presentation only.")]
        [Range(1f, 10f)] public float presentationDistanceScale = 3f;
        public float assumedHandDepthMetres = 0.35f;
        public float palmWidthMetres = 0.075f;
        public float wristToMiddleMetres = 0.10f;
        private static readonly int[] Edges = {
            0,1, 1,2, 2,3, 3,4, 0,5, 5,6, 6,7, 7,8,
            0,9, 9,10, 10,11, 11,12, 0,13, 13,14, 14,15, 15,16,
            0,17, 17,18, 18,19, 19,20, 5,9, 9,13, 13,17
        };
        private static readonly Vector3[] TargetPositions = {
            new Vector3(-0.17f, -0.13f, 0.8f),
            new Vector3(0.17f, -0.13f, 0.8f)
        };
        private readonly LineRenderer[,] bones = new LineRenderer[2, Edges.Length / 2];
        private readonly GameObject[] cursors = new GameObject[2];
        private readonly Material[] targetMaterials = new Material[2];
        private readonly bool[] targetHovered = new bool[2];
        private readonly float[] targetFeedbackUntil = new float[2];
        private SpatialHandTracking tracking;
        private SpatialCardboardBridge xrBridge;
        private SpatialHandFrame latestFrame;
        private SpatialRgbCalibration calibration;
        private SpatialUvcHandProcessor uvcProcessor;
        private float renderWindowStart,renderFps;
        private int renderFrames;
        private bool swapUvcHandedness;
        private long receivedHostNs;
        private string geometryStatus = "Waiting for hands";
        private float nextGeometryLog;
        private readonly string[] displayState = { "hidden", "hidden" };
        private string identityEvents = "";
        private float nextSnapshotTime;
        private bool staleLogged;
        private TextMesh status;
        private long lastSequence = -1;
        private float lastFrameTime;
        private int pinchEvents;
        private int selectionEvents;
        private int frameEvents;
        private readonly bool[] wasPinching = new bool[2];
        private readonly float[] handDepth = new float[2];
        private readonly bool[] hasHandDepth = new bool[2];
        private readonly Vector2[] lastWrist = new Vector2[2];
        private readonly Vector3[,] eyeJoints = new Vector3[2, 21];
        private readonly bool[,] eyeJointValid = new bool[2, 21];
        private bool initialized;
        private static Mesh sphereMesh;

        private static GameObject CreateVisualSphere(string name, Material material)
        {
            if (sphereMesh == null)
            {
                const int longitude = 24;
                const int latitude = 12;
                var vertices = new Vector3[(longitude + 1) * (latitude + 1)];
                var triangles = new int[longitude * latitude * 6];
                for (var y = 0; y <= latitude; y++)
                {
                    var polar = Mathf.PI * y / latitude;
                    for (var x = 0; x <= longitude; x++)
                    {
                        var azimuth = 2f * Mathf.PI * x / longitude;
                        vertices[y * (longitude + 1) + x] = new Vector3(
                            0.5f * Mathf.Sin(polar) * Mathf.Cos(azimuth),
                            0.5f * Mathf.Cos(polar),
                            0.5f * Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    }
                }
                for (var y = 0; y < latitude; y++)
                    for (var x = 0; x < longitude; x++)
                    {
                        var a = y * (longitude + 1) + x;
                        var b = a + longitude + 1;
                        var t = (y * longitude + x) * 6;
                        triangles[t] = a;
                        triangles[t + 1] = a + 1;
                        triangles[t + 2] = b;
                        triangles[t + 3] = b;
                        triangles[t + 4] = a + 1;
                        triangles[t + 5] = b + 1;
                    }
                sphereMesh = new Mesh { name = "SpatialAI hand target sphere" };
                sphereMesh.vertices = vertices;
                sphereMesh.triangles = triangles;
                sphereMesh.RecalculateNormals();
            }
            var visual = new GameObject(name);
            visual.AddComponent<MeshFilter>().sharedMesh = sphereMesh;
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            return visual;
        }

        private void Start()
        {
            if (xrCamera == null) { enabled = false; return; }
            Debug.Log("SpatialAI hand demo init: calibration");
            calibration = SpatialRgbCalibration.LoadRhinoXReference();
            UvcHandCalibration uvcCalibration = new UvcHandCalibration
                { swapHandedness = defaultUvcSwapHandedness };
            if (uvcCalibrationJson != null)
            {
                try { uvcCalibration = JsonUtility.FromJson<UvcHandCalibration>(uvcCalibrationJson.text); }
                catch (System.Exception error)
                {
                    Debug.LogError("SpatialHands3D: invalid calibration JSON: " + error.Message);
                    enabled = false;
                    return;
                }
            }
            uvcProcessor = new SpatialUvcHandProcessor(uvcCalibration);
            renderWindowStart=Time.unscaledTime;
            swapUvcHandedness = uvcCalibration != null && uvcCalibration.swapHandedness;
            Debug.Log("SpatialAI hand demo init: tracker");
            tracking = GetComponent<SpatialHandTracking>();
            xrBridge = GetComponent<SpatialCardboardBridge>();
            if (tracking == null) tracking = gameObject.AddComponent<SpatialHandTracking>();
            tracking.FrameUpdated += OnHandFrame;
            tracking.Invalidated += OnInvalidated;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var targetShader = Shader.Find("Unlit/Color");
            if (targetShader == null) targetShader = shader;
            if (shader == null || targetShader == null)
            {
                Debug.LogError("SpatialAI hand demo requires an included unlit shader.");
                enabled = false;
                return;
            }
            var visualRoot = new GameObject("Hand demo distance root").transform;
            visualRoot.SetParent(xrCamera.transform, false);
            visualRoot.localScale = Vector3.one * Mathf.Clamp(presentationDistanceScale, 1f, 10f);
            for (var hand = 0; hand < 2; hand++)
            {
                Debug.Log("SpatialAI hand demo init: visuals " + hand);
                var material = new Material(shader);
                material.color = hand == 0 ? Color.green : Color.cyan;
                for (var edge = 0; edge < Edges.Length / 2; edge++)
                {
                    var segment = new GameObject("Hand " + hand + " bone " + edge);
                    segment.transform.SetParent(visualRoot, false);
                    var line = segment.AddComponent<LineRenderer>();
                    line.useWorldSpace = false;
                    line.positionCount = 2;
                    line.startWidth = line.endWidth = 0.006f;
                    line.sharedMaterial = material;
                    line.enabled = false;
                    bones[hand, edge] = line;
                }
                var cursor = CreateVisualSphere("Hand " + hand + " pinch pointer", material);
                cursor.transform.SetParent(visualRoot, false);
                cursor.transform.localScale = Vector3.one * 0.025f;
                cursor.SetActive(false);
                cursors[hand] = cursor;
            }
            for (var i = 0; i < TargetPositions.Length; i++)
            {
                Debug.Log("SpatialAI hand demo init: target " + i);
                var material = new Material(targetShader);
                material.color = Color.cyan;
                var target = CreateVisualSphere("Pinch target " + (i + 1), material);
                target.transform.SetParent(visualRoot, false);
                target.transform.localPosition = TargetPositions[i];
                target.transform.localScale = Vector3.one * 0.13f;
                targetMaterials[i] = material;
            }
            var label = new GameObject("Hand tracking readout");
            label.transform.SetParent(visualRoot, false);
            label.transform.localPosition = new Vector3(-0.34f, 0.34f, 1.8f);
            status = label.AddComponent<TextMesh>();
            status.anchor = TextAnchor.UpperLeft;
            status.fontSize = 48;
            status.characterSize = 0.0075f;
            status.color = Color.white;
            var backdrop = new GameObject("Hand status backdrop");
            backdrop.transform.SetParent(visualRoot, false);
            backdrop.transform.localPosition = new Vector3(-0.35f, 0.35f, 1.82f);
            var panelMesh = new Mesh { name = "SpatialAI status panel" };
            panelMesh.vertices = new[] {
                Vector3.zero, new Vector3(0.90f, 0f, 0f),
                new Vector3(0f, -0.45f, 0f), new Vector3(0.90f, -0.45f, 0f)
            };
            panelMesh.triangles = new[] { 0, 1, 2, 1, 3, 2 };
            panelMesh.RecalculateNormals();
            backdrop.AddComponent<MeshFilter>().sharedMesh = panelMesh;
            var panelMaterial = new Material(targetShader);
            panelMaterial.color = new Color(0.015f, 0.02f, 0.04f, 1f);
            backdrop.AddComponent<MeshRenderer>().sharedMaterial = panelMaterial;
            initialized = true;
            Debug.Log("SpatialAI hand interaction demo ready");
        }

        private void OnEnable()
        {
            lastSequence = -1;
            uvcProcessor?.Reset();
            for (var h = 0; h < 2; h++) displayState[h] = "hidden";
            if (tracking != null) { tracking.FrameUpdated += OnHandFrame; tracking.Invalidated += OnInvalidated; }
        }

        private void OnDisable()
        {
            if (tracking != null) { tracking.FrameUpdated -= OnHandFrame; tracking.Invalidated -= OnInvalidated; }
            uvcProcessor?.Reset("demo disabled");
            latestFrame = null;
            for (var h = 0; h < cursors.Length; h++)
            {
                if (cursors[h] != null) cursors[h].SetActive(false);
                wasPinching[h] = false;
                for (var e = 0; e < Edges.Length / 2; e++)
                    if (bones[h, e] != null) bones[h, e].enabled = false;
            }
        }

        private void OnHandFrame(SpatialHandFrame frame)
        {
            receivedHostNs=frame!=null&&frame.receivedHostStopwatchNs>0?frame.receivedHostStopwatchNs:SpatialUvcHandProcessor.HostNowNs();
            identityEvents="";
            if(frame!=null&&frame.cameraId!=null&&frame.cameraId.StartsWith("uvc:"))
            {
                uvcProcessor.Publish(frame,Pose.identity,0);
                latestFrame=uvcProcessor.Associated;identityEvents=uvcProcessor.IdentityEvents;
            }
            else latestFrame=frame;
            staleLogged = false;
            frameEvents++;
        }

        private void OnInvalidated(string reason)
        {
            uvcProcessor?.Reset(reason);latestFrame=null;lastSequence=-1;
            for(int h=0;h<2;h++)
            {
                displayState[h]="hidden";wasPinching[h]=false;
                if(cursors[h]!=null)cursors[h].SetActive(false);
                for(int e=0;e<Edges.Length/2;e++)if(bones[h,e]!=null)bones[h,e].enabled=false;
            }
            for(int i=0;i<targetHovered.Length;i++)targetHovered[i]=false;
        }

        private static SpatialHand FindHand(SpatialHandFrame frame, string side)
        {
            foreach (var hand in frame.hands ?? new SpatialHand[0])
                if (hand != null && hand.EffectiveHandedness == side) return hand;
            return null;
        }

        private string HandGestureText(SpatialHandFrame frame, string handedness)
        {
            if (frame == null || frame.hands == null) return handedness + ": --";
            var rawLabel = frame.cameraId != null && frame.cameraId.StartsWith("uvc:") && swapUvcHandedness
                ? (handedness == "Left" ? "Right" : "Left") : handedness;
            for (var i = 0; i < frame.hands.Length; i++)
            {
                var hand = frame.hands[i];
                if (hand == null || hand.EffectiveHandedness != rawLabel) continue;
                return handedness + ": " + hand.Gesture + " G=" + hand.gestureScore.ToString("F2") +
                    " rawH=" + (hand.hasHandednessScore ? hand.handednessScore.ToString("F2") : "--") +
                    (hand.rawHandedness != hand.EffectiveHandedness ? " LOCK" : "") +
                    (hand.Pinching ? " PINCH" : "");
            }
            return handedness + ": --";
        }

        private void LogHandDiagnostics(SpatialHandFrame frame, string events, bool snapshot)
        {
            Debug.Log("SpatialHandsDiag event=" + events + " seq=" + frame.sequence + " camera=" + frame.cameraId +
                " size=" + frame.imageWidth + "x" + frame.imageHeight + " receivedHostNs=" + receivedHostNs +
                " fps=" + frame.resultFps.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                " inferMs=" + frame.inferenceMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                " leftDisplay=" + displayState[0] + " rightDisplay=" + displayState[1] +
                " leftCameraDepth=" + handDepth[0].ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " rightCameraDepth=" + handDepth[1].ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                " leftReason=" + uvcProcessor.Reason(HandSide.Left) + " rightReason=" + uvcProcessor.Reason(HandSide.Right));
            foreach (var hand in frame.hands ?? new SpatialHand[0])
            {
                if (hand == null) continue;
                var side = hand.EffectiveHandedness != "Left" && hand.EffectiveHandedness != "Right" ? "Unknown" :
                    (hand.EffectiveHandedness == "Left") != swapUvcHandedness ? "Left" : "Right";
                Debug.Log("SpatialHandsDiag hand seq=" + frame.sequence + " track=" + hand.trackingId +
                    " rawSide=" + hand.rawHandedness + " stableSide=" + hand.EffectiveHandedness + " mappedSide=" + side +
                    " handednessScore=" + (hand.hasHandednessScore ? hand.handednessScore.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "unavailable") +
                    " gesture=" + hand.rawGesture + " gestureScore=" + hand.gestureScore.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            }
            if (!snapshot || Time.unscaledTime < nextSnapshotTime) return;
            nextSnapshotTime = Time.unscaledTime + .5f;
            foreach (var hand in frame.hands ?? new SpatialHand[0])
            {
                if (hand == null) continue;
                var json = JsonUtility.ToJson(hand);
                const int chunkSize = 600;
                for (var offset = 0; offset < json.Length; offset += chunkSize)
                    Debug.Log("SpatialHandsDiag snapshot seq=" + frame.sequence + " track=" + hand.trackingId +
                        " part=" + (offset / chunkSize + 1) + "/" + ((json.Length + chunkSize - 1) / chunkSize) + " " +
                        json.Substring(offset, Mathf.Min(chunkSize, json.Length - offset)));
            }
        }

        private void LateUpdate()
        {
            if (!initialized || tracking == null) return;
            renderFrames++;var elapsed=Time.unscaledTime-renderWindowStart;
            if(elapsed>=1){renderFps=renderFrames/elapsed;renderFrames=0;renderWindowStart=Time.unscaledTime;}
            var frame = latestFrame;
            if (frame != null && frame.sequence != lastSequence)
            {
                lastSequence = frame.sequence;
                lastFrameTime = Time.unscaledTime;
                var uvc = frame.cameraId != null && frame.cameraId.StartsWith("uvc:");
                SpatialHandsFrame converted = null;
                var stateEvents = identityEvents;
                var problem = identityEvents.Contains("label_change");
                if (uvc)
                {
                    converted=uvcProcessor.Latest;
                    geometryStatus=converted==null?"3D rejected: "+uvcProcessor.Diagnostics:
                        uvcProcessor.CalibrationWarning.Length>0?"3D ESTIMATED | calibration defaults/unverified":"3D ESTIMATED | calibration loaded";
                }
                for (var i = 0; i < targetHovered.Length; i++) targetHovered[i] = false;
                for (var h = 0; h < 2; h++)
                {
                    var hand = uvc ? FindHand(frame, (h == 0) != swapUvcHandedness ? "Left" : "Right") :
                        frame.hands != null && h < frame.hands.Length ? frame.hands[h] : null;
                    var sample = converted == null ? null : h == 0 ? converted.Left : converted.Right;
                    var valid = uvc ? sample != null && sample.IsTracked :
                        hand != null && hand.joints != null && hand.joints.Length >= 21 &&
                        hand.joints[0] != null &&
                        calibration != null && frame.cameraId == calibration.cameraId &&
                        frame.imageWidth == calibration.imageWidth &&
                        frame.imageHeight == calibration.imageHeight;
                    bool displayed=valid;
                    if(uvc)
                    {
                        var side=h==0?HandSide.Left:HandSide.Right;
                        displayed=uvcProcessor.TryPresentation(side,out var positions,out var positionsValid);
                        var state=uvcProcessor.DisplayState(side);
                        if(state!=displayState[h]){stateEvents+=(h==0?"Left=":"Right=")+state+"; ";problem|=state!="fresh";}
                        displayState[h]=state;handDepth[h]=uvcProcessor.CameraDepth(side);
                        for(int j=0;j<21;j++)eyeJointValid[h,j]=false;
                        if(displayed)for(int id=0;id<26;id++)
                        {
                            int landmark=MonocularHandConverter.LandmarkIndex((HandJointId)id);
                            if(landmark<0||!positionsValid[id])continue;
                            var point=positions[id];eyeJointValid[h,landmark]=true;
                            eyeJoints[h,landmark]=new Vector3(reflectUvcDisplayX?-point.x:point.x,point.y,point.z);
                        }
                    }
                    else if (!uvc && valid)
                    {
                        float measuredDepth;
                        var wrist = new Vector2(hand.joints[0].x, hand.joints[0].y);
                        if (calibration.TryEstimateApproximateHandDepth(hand.joints,
                            frame.imageWidth, frame.imageHeight, palmWidthMetres,
                            wristToMiddleMetres, out measuredDepth))
                        {
                            var sameHand = hasHandDepth[h] &&
                                (wrist - lastWrist[h]).sqrMagnitude < 0.09f;
                            handDepth[h] = sameHand ? Mathf.Lerp(handDepth[h], measuredDepth, 0.55f)
                                : measuredDepth;
                            hasHandDepth[h] = true;
                        }
                        else if (!hasHandDepth[h]) handDepth[h] = Mathf.Max(0.2f, assumedHandDepthMetres);
                        lastWrist[h] = wrist;
                        for (var j = 0; j < 21; j++)
                            eyeJointValid[h, j] = calibration.TryEyePoint(hand.joints[j],
                                frame.imageWidth, frame.imageHeight, handDepth[h], out eyeJoints[h, j]);
                    }
                    else hasHandDepth[h] = false;
                    for (var e = 0; e < Edges.Length / 2; e++)
                    {
                        var line = bones[h, e];
                        var first = Edges[e * 2];
                        var second = Edges[e * 2 + 1];
                        line.enabled = displayed && eyeJointValid[h, first] && eyeJointValid[h, second];
                        if (!line.enabled) continue;
                        line.SetPosition(0, eyeJoints[h, first]);
                        line.SetPosition(1, eyeJoints[h, second]);
                    }
                    var hovered = -1;
                    cursors[h].SetActive(false);
                    if (displayed && eyeJointValid[h, 4] && eyeJointValid[h, 8])
                    {
                        var pinchPoint = (eyeJoints[h, 4] + eyeJoints[h, 8]) * 0.5f;
                        if (pinchPoint.z > 0.05f)
                        {
                            var scale = TargetPositions[0].z / pinchPoint.z;
                            var pointer = new Vector3(pinchPoint.x * scale,
                                pinchPoint.y * scale, TargetPositions[0].z - 0.08f);
                            cursors[h].transform.localPosition = pointer;
                            cursors[h].SetActive(true);
                            for (var i = 0; valid && i < TargetPositions.Length; i++)
                            {
                                var difference = pointer - TargetPositions[i];
                                if (difference.x * difference.x + difference.y * difference.y < 0.08f * 0.08f)
                                {
                                    hovered = i;
                                    targetHovered[i] = true;
                                    break;
                                }
                            }
                        }
                    }
                    var pinching = valid && hand != null && hand.Pinching;
                    if (pinching && !wasPinching[h])
                    {
                        pinchEvents++;
                        if (hovered >= 0)
                        {
                            selectionEvents++;
                            targetFeedbackUntil[hovered] = Time.unscaledTime + 0.6f;
                        }
                        Debug.Log("SpatialAI HAND_PINCH hand=" + h + " target=" + hovered +
                            " pinch=" + pinchEvents + " select=" + selectionEvents +
                            " depth=" + handDepth[h].ToString("F2") + "m");
                    }
                    wasPinching[h] = pinching;
                }
                if (uvc && (stateEvents.Length > 0 || Time.unscaledTime >= nextGeometryLog))
                {
                    nextGeometryLog = Time.unscaledTime + 2f;
                    LogHandDiagnostics(frame, stateEvents.Length > 0 ? stateEvents : "periodic",
                        problem || frame.hands != null && frame.hands.Length > 0 &&
                        (displayState[0] != "fresh" && displayState[1] != "fresh"));
                    Debug.Log("SpatialHands3D: sequence=" + frame.sequence +
                        " left=" + (converted != null && converted.Left.IsTracked) +
                        " right=" + (converted != null && converted.Right.IsTracked) +
                        " displayMirrorX=" + reflectUvcDisplayX +
                        " handednessSwap=" + swapUvcHandedness + " " + geometryStatus);
                }
            }
            // The SDK owns all UVC hold and expiry decisions, including render-only expiry.
            if(frame!=null&&frame.cameraId!=null&&frame.cameraId.StartsWith("uvc:"))for(int h=0;h<2;h++)
            {
                var state=uvcProcessor.DisplayState(h==0?HandSide.Left:HandSide.Right);
                if(state=="hidden"&&displayState[h]!="hidden")
                {
                    Debug.Log("SpatialHandsDiag event="+(displayState[h]=="missing-held"?"missing_hold_expired":"presentation_expired")+" side="+(h==0?"Left":"Right")+" seq="+lastSequence);
                    displayState[h]=state;cursors[h].SetActive(false);wasPinching[h]=false;
                    for(int e=0;e<Edges.Length/2;e++)bones[h,e].enabled=false;
                }
            }
            if (displayState[0] == "hidden" && displayState[1] == "hidden")
                for (var i = 0; i < targetHovered.Length; i++) targetHovered[i] = false;
            if ((frame==null || frame.cameraId==null || !frame.cameraId.StartsWith("uvc:")) && Time.unscaledTime - lastFrameTime > 0.35f)
            {
                if (frame != null && !staleLogged)
                { staleLogged = true; Debug.Log("SpatialHandsDiag event=stale seq=" + frame.sequence +
                    " ageSeconds=" + (Time.unscaledTime - lastFrameTime).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)); }
                for (var h = 0; h < 2; h++)
                {
                    displayState[h] = "hidden";
                    if (cursors[h] != null) cursors[h].SetActive(false);
                    for (var e = 0; e < Edges.Length / 2; e++) bones[h, e].enabled = false;
                    wasPinching[h] = false;
                }
                for (var i = 0; i < targetHovered.Length; i++) targetHovered[i] = false;
            }
            for (var i = 0; i < targetMaterials.Length; i++)
                if (targetMaterials[i] != null)
                    targetMaterials[i].color = Time.unscaledTime < targetFeedbackUntil[i] ? Color.green :
                        targetHovered[i] ? Color.yellow : Color.cyan;
            if (status != null)
            {
                var count = frame != null && frame.hands != null && Time.unscaledTime - lastFrameTime < 0.35f
                    ? frame.hands.Length : 0;
                var resultIsFresh = frame != null && Time.unscaledTime - lastFrameTime < 0.35f;
                var freshFrame = resultIsFresh ? frame : null;
                status.text = "XR " + (xrBridge != null && xrBridge.XrRunning ? "ON" : "WAIT") +
                    "  Mesh " + (xrBridge != null && xrBridge.RhinoXLegacyMeshApplied ? "ON" : "OFF") + "\n" +
                    (frame != null && frame.gestureSource == "landmarks-fallback"
                        ? "HANDS ONLY | " : "GESTURES | ") +
                    (!resultIsFresh ? tracking.State : frame.@delegate+" "+frame.resultFps.ToString("F1") + " FPS") + "\n" +
                    "Hands " + count + "  Pinch " + pinchEvents + "  Select " + selectionEvents + "\n" +
                    geometryStatus + "\n" +
                    "3D Left " + displayState[0] + " | Right " + displayState[1] + "\n" +
                    HandGestureText(freshFrame, "Left") + "\n" +
                    HandGestureText(freshFrame, "Right") + "\n" +
                    "AI " + (frame == null ? 0f : frame.inferenceMs).ToString("F0") +
                    "ms Age " + (frame == null ? -1f : frame.captureToResultMs).ToString("F0") + "ms\n" +
                    "AI " + (freshFrame==null?0:freshFrame.resultFps).ToString("F1") + " UVC " + tracking.CaptureFps.ToString("F1") + " Render " + renderFps.ToString("F1") + " FPS\n" +
                    "Point at orb, then pinch";
            }
        }
    }
}
#endif
