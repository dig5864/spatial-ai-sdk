#if UNITY_EDITOR
using System;
using System.Reflection;
using SpatialAI.Core;
using SpatialAI.Samples;
using SpatialAI.Unity;
using UnityEditor;
using UnityEngine;

// Run in the maintained Cardboard sample project with UvcGeometryChecks.cs.
public static class UvcHandDemoCheck
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }
    private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Call(object target, string method, params object[] args)
    { target.GetType().GetMethod(method, Private).Invoke(target, args); }
    private static T Field<T>(object target, string name)
    { return (T)target.GetType().GetField(name, Private).GetValue(target); }
    private static SpatialHand Hand(string side)
    {
        var points = UvcGeometryChecks.Project(UvcGeometryChecks.Calibration(), UvcGeometryChecks.Skeleton());
        var joints = new SpatialHandJoint[21];
        for (var i = 0; i < joints.Length; i++)
            joints[i] = new SpatialHandJoint { x = points[i].X, y = points[i].Y, z = points[i].Z };
        return new SpatialHand { rawHandedness = side, rawGesture = "Open_Palm", gestureScore = .95f, joints = joints };
    }
    private static void Send(HandTrackingDemo demo, long sequence, params SpatialHand[] hands)
    {
        Call(demo, "OnHandFrame", new SpatialHandFrame { cameraId = "uvc:11cb:281b", imageWidth = 640,
            imageHeight = 480, sequence = sequence, hands = hands, resultFps = 24 });
        Call(demo, "LateUpdate");
    }
    private static SpatialHand OffsetHand(string side, float x)
    {
        var hand = Hand(side);
        foreach (var point in hand.joints) point.x += x;
        hand.hasHandednessScore = true; hand.handednessScore = .97f;
        return hand;
    }
    private static SpatialHandFrame ImageFrame(long sequence, params SpatialHand[] hands)
    { return new SpatialHandFrame { cameraId = "uvc:11cb:281b", imageWidth = 640, imageHeight = 480,
        sequence = sequence, hands = hands }; }
    private static void CheckTemporalAssociation()
    {
        var stable = new SpatialUvcHandStabilizer(); string events;
        var original = OffsetHand("Right", -.15f);
        var first = stable.Process(ImageFrame(1, original), 0, out events);
        int id = first.hands[0].trackingId;
        var flipped = OffsetHand("Left", -.14f);
        var second = stable.Process(ImageFrame(2, flipped), 50_000_000, out events);
        Check(second.hands[0].trackingId == id && second.hands[0].EffectiveHandedness == "Right" &&
            second.hands[0].rawHandedness == "Left" && events.Contains("label_change"), "Same wrist holds identity during label flip");
        Check(flipped.stabilizedHandedness == null && flipped.rawHandedness == "Left", "Source DTO not rewritten");
        var empty = stable.Process(ImageFrame(3), 80_000_000, out events);
        Check(empty.hands.Length == 0, "No hand yields no stabilized skeleton");
        var expired = stable.Process(ImageFrame(4, flipped), 400_000_000, out events);
        Check(expired.hands[0].trackingId != id && expired.hands[0].EffectiveHandedness == "Left", "Lost track expires and reacquires label");
        stable.Reset();
        var both = stable.Process(ImageFrame(1, OffsetHand("Left", -.15f), OffsetHand("Right", .15f)), 0, out events);
        var reordered = stable.Process(ImageFrame(2, OffsetHand("Right", .14f), OffsetHand("Right", -.14f)), 40_000_000, out events);
        Check(reordered.hands[0].trackingId == both.hands[1].trackingId &&
            reordered.hands[1].trackingId == both.hands[0].trackingId && reordered.hands[1].EffectiveHandedness == "Left",
            "Two hands survive array reorder and duplicate raw labels");
        var restarted = stable.Process(ImageFrame(1, OffsetHand("Right", -.14f)), 80_000_000, out events);
        Check(restarted.hands[0].EffectiveHandedness == "Right" && events.Contains("reset"), "Source sequence restart resets association");
        var jump = stable.Process(ImageFrame(2, OffsetHand("Left", .2f)), 90_000_000, out events);
        Check(jump.hands[0].EffectiveHandedness == "Left" && jump.hands[0].trackingId != restarted.hands[0].trackingId,
            "Distant hand is a new identity");
        var decoded = JsonUtility.FromJson<SpatialHand>("{\"rawHandedness\":\"Right\",\"handednessScore\":0.91,\"hasHandednessScore\":true,\"gestureScore\":0.23}");
        Check(decoded.hasHandednessScore && Mathf.Abs(decoded.handednessScore - .91f) < .0001f &&
            Mathf.Abs(decoded.gestureScore - .23f) < .0001f, "Separate model confidence scores roundtrip");
    }
    private static void CheckDiagnosticHold()
    {
        var logs = new System.Collections.Generic.List<string>();
        Application.LogCallback recorder = (message, stack, type) => { if (message.StartsWith("SpatialHandsDiag")) logs.Add(message); };
        Application.logMessageReceived += recorder;
        var root = new GameObject("Degenerate hand verification");
        var camera = new GameObject("Degenerate hand camera");
        var demo = root.AddComponent<HandTrackingDemo>(); demo.xrCamera = camera.AddComponent<Camera>();
        Call(demo, "Start");
        long now=SpatialUvcHandProcessor.HostNowNs();
        var processor=new SpatialUvcHandProcessor(clock:()=>now);
        demo.GetType().GetField("uvcProcessor",Private).SetValue(demo,processor);
        var good = Hand("Left");
        good.joints[4].x = .317f; good.joints[8].x = .367f;
        good.joints[4].y = good.joints[8].y = .642f;
        Send(demo, 1, good);
        Check(!good.Pinching && Field<bool[]>(demo, "targetHovered")[0], "Valid unpinched fixture points at target before degeneration");
        var bad = Hand("Right");
        bad.hasHandednessScore = true; bad.handednessScore = .87f; bad.rawGesture = "Victory"; bad.gestureScore = .13f;
        bad.joints[17].x = bad.joints[5].x + .015f; bad.joints[17].y = bad.joints[5].y;
        bad.joints[4].x = bad.joints[8].x = .342f; bad.joints[4].y = bad.joints[8].y = .642f;
        Check(bad.Pinching, "Rejected geometry fixture still has a current pinch signal");
        Send(demo, 2, bad);
        var bones = Field<LineRenderer[,]>(demo, "bones");
        Check(bones[0, 0].enabled && Field<string[]>(demo, "displayState")[0] == "depth-held", "Side-on hand projects current points at previously valid depth");
        Check(processor.Latest!=null&&!processor.Latest.Left.IsTracked, "Held presentation never marks rejected SDK geometry tracked");
        Check(Field<int>(demo, "selectionEvents") == 0 && bad.Pinching, "Held geometry cannot trigger target selection");
        now+=200_000_000;
        Send(demo, 3, bad);
        Check(bones[0, 0].enabled && Field<string[]>(demo, "displayState")[0] == "depth-held", "Continuous side-on preview survives beyond 150 ms");
        var beforeMove = bones[0, 0].GetPosition(0);
        foreach (var point in bad.joints) point.x += .01f;
        Send(demo, 31, bad);
        Check(bones[0, 0].GetPosition(0).x > beforeMove.x && Field<int>(demo, "selectionEvents") == 0,
            "Fallback follows current landmarks and cannot select");
        Check(processor.Reason(HandSide.Left).Contains("widthDepth=") &&
            processor.Reason(HandSide.Left).Contains("ratio="), "Depth rejection includes numeric metrics");
        Send(demo, 32, Hand("Right"));
        Check(bones[0, 0].enabled && Field<string[]>(demo, "displayState")[0] == "fresh", "Continuous track recovers on valid geometry");
        Send(demo, 33);
        Check(Field<TextMesh>(demo, "status").text.Contains("AI " + 24f.ToString("F1") + " UVC "),
            "Completed inference FPS is shown even with zero detected hands");
        Check(bones[0, 0].enabled && Field<string[]>(demo, "displayState")[0] == "missing-held", "Brief detector dropout holds display");
        now+=130_000_000;
        Call(demo, "LateUpdate");
        Check(!bones[0, 0].enabled, "Missing hold expires without another camera frame");
        Send(demo, 34, bad);
        Check(bones[0, 0].enabled, "Same live identity recovers after brief dropout");
        processor.Reset();
        Send(demo, 35, bad);
        Check(!bones[0, 0].enabled, "New identity cannot inherit old depth");
        Check(logs.Exists(s => s.Contains("label_change")) && logs.Exists(s => s.Contains("Left=depth-held")) &&
            logs.Exists(s => s.Contains("missing_hold_expired")), "Events cover identity changes, fallback and dropout expiry");
        Check(logs.Exists(s => s.Contains("gesture=Victory") && s.Contains("handednessScore=0.870") && s.Contains("gestureScore=0.130")),
            "Diagnostics use actual independent values, not assumed gesture examples");
        Check(logs.Exists(s => s.Contains("snapshot seq=2") && s.Contains("part=")), "Problem frame landmark snapshot available");
        var json = "";
        foreach(var message in logs)
            if(message.Contains("snapshot seq=2 "))
            {
                Check(message.Length < 900, "Snapshot messages fit Unity Android line limit");
                json += message.Substring(message.IndexOf(" ",message.IndexOf("part="))+1);
            }
        Check(JsonUtility.FromJson<SpatialHand>(json).joints.Length == 21, "All snapshot fragments reconstruct full skeleton JSON");
        Send(demo, 36, good);
        Check(Field<bool[]>(demo, "targetHovered")[0], "Fresh hand restores hover before stream stall");
        now += 400_000_000;
        Call(demo, "LateUpdate");
        Check(!bones[0, 0].enabled && !Field<bool[]>(demo, "targetHovered")[0],
            "Stream stall expires skeleton and stale target hover without new inference");
        Application.logMessageReceived -= recorder;
        UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(camera);
    }
    public static void Run()
    {
        CheckTemporalAssociation();
        CheckDiagnosticHold();
        var cameraObject = new GameObject("Verification camera");
        var camera = cameraObject.AddComponent<Camera>();
        var root = new GameObject("Verification hands");
        var demo = root.AddComponent<HandTrackingDemo>();
        demo.xrCamera = camera;
        Call(demo, "Start");
        long demoNow=SpatialUvcHandProcessor.HostNowNs();
        demo.GetType().GetField("uvcProcessor",Private).SetValue(demo,new SpatialUvcHandProcessor(clock:()=>demoNow));
        Check(root.GetComponents<SpatialHandTracking>().Length == 1, "Exactly one camera owner");
        Send(demo, 1, Hand("Right"), Hand("Left"));
        var bones = Field<LineRenderer[,]>(demo, "bones");
        for (var side = 0; side < 2; side++) for (var edge = 0; edge < bones.GetLength(1); edge++)
        {
            Check(bones[side, edge].enabled, "Both 3D hands visible without calibration, regardless of input ordering");
            Check(bones[side, edge].GetPosition(0).z > .05f, "Bone has metre-space depth");
        }
        Check(Field<string>(demo, "geometryStatus").Contains("defaults"), "Missing calibration visible in HUD");
        Check(Field<TextMesh>(demo, "status").text.Contains("Left: OpenPalm"), "Gesture category in HUD");
        var beforeReflection = (Vector3[,])Field<Vector3[,]>(demo, "eyeJoints").Clone();
        demo.reflectUvcDisplayX = true;
        var rawLeft = Hand("Left");
        var originalInputX = rawLeft.joints[1].x;
        Send(demo, 10, Hand("Right"), rawLeft);
        var reflected = Field<Vector3[,]>(demo, "eyeJoints");
        for (var side = 0; side < 2; side++) for (var joint = 0; joint < 21; joint++)
            Check(Vector3.Distance(reflected[side, joint], new Vector3(-beforeReflection[side, joint].x,
                beforeReflection[side, joint].y, beforeReflection[side, joint].z)) < .000001f,
                "Display reflection reverses X only for every joint");
        Check(rawLeft.joints[1].x == originalInputX, "Display correction preserves input landmarks");
        Check(Field<TextMesh>(demo, "status").text.Contains("Left: OpenPalm"), "Display reflection preserves anatomical labels");
        var cursors = Field<GameObject[]>(demo, "cursors");
        Check(Mathf.Abs(cursors[0].transform.localPosition.x -
            (reflected[0, 4].x + reflected[0, 8].x) * .5f * .8f /
            ((reflected[0, 4].z + reflected[0, 8].z) * .5f)) < .000001f,
            "Pinch pointer follows reflected joints");
        var profileAsset = Resources.Load<TextAsset>("SpatialAI/HostProfiles/xiaomi-mayfly");
        Check(profileAsset != null && JsonUtility.FromJson<HostDeviceProfile>(profileAsset.text).reflectHandDisplayX,
            "Xiaomi host enables confirmed horizontal display compensation");
        demo.reflectUvcDisplayX = false;
        Send(demo, 2, Hand("Right"));
        Check(!bones[0, 0].enabled && bones[1, 0].enabled, "Stable left/right slot on hand disappearance");
        var bad = Hand("Left"); bad.joints[5] = null;
        Send(demo, 3, bad, Hand("Right"));
        Check(!bones[0, 0].enabled && bones[1, 0].enabled, "Degenerate hand does not suppress other side");
        demoNow+=351_000_000;
        Call(demo, "LateUpdate");
        Check(!bones[0, 0].enabled && !bones[1, 0].enabled, "Stale bones hidden");
        Send(demo, 4, Hand("Left"));
        Call(demo, "OnDisable");
        Check(!bones[0, 0].enabled, "Disable hides bones immediately");
        Call(demo, "OnEnable");
        Send(demo, 4, Hand("Left"));
        Check(bones[0, 0].enabled, "Re-enabled demo accepts restarted sequence");
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        var calibratedRoot = new GameObject("Loaded calibration hands");
        var calibratedCamera = new GameObject("Loaded calibration camera");
        var calibratedDemo = calibratedRoot.AddComponent<HandTrackingDemo>();
        calibratedDemo.xrCamera = calibratedCamera.AddComponent<Camera>();
        var calibration = UvcGeometryChecks.Calibration();
        calibration.swapHandedness = true;
        var asset = new TextAsset(JsonUtility.ToJson(calibration));
        calibratedDemo.uvcCalibrationJson = asset;
        Call(calibratedDemo, "Start");
        Send(calibratedDemo, 1, Hand("Left"));
        var calibratedBones = Field<LineRenderer[,]>(calibratedDemo, "bones");
        Check(!calibratedBones[0, 0].enabled && calibratedBones[1, 0].enabled, "Loaded calibration corrects visual handedness");
        Check(Field<TextMesh>(calibratedDemo, "status").text.Contains("Right: OpenPalm"), "Gesture HUD follows configured handedness correction");
        Check(Field<string>(calibratedDemo, "geometryStatus").Contains("loaded"), "Loaded measured parameters take precedence");
        UnityEngine.Object.DestroyImmediate(calibratedRoot);
        UnityEngine.Object.DestroyImmediate(calibratedCamera);
        UnityEngine.Object.DestroyImmediate(asset);
        var correctedRoot = new GameObject("Host default corrected hands");
        var correctedCamera = new GameObject("Host default camera");
        var correctedDemo = correctedRoot.AddComponent<HandTrackingDemo>();
        correctedDemo.xrCamera = correctedCamera.AddComponent<Camera>();
        correctedDemo.defaultUvcSwapHandedness = true;
        correctedDemo.reflectUvcDisplayX = true;
        Call(correctedDemo, "Start");
        var correctedBones = Field<LineRenderer[,]>(correctedDemo, "bones");
        var physicalLeft = Hand("Right");
        Send(correctedDemo, 1, physicalLeft);
        Check(correctedBones[0, 0].enabled && !correctedBones[1, 0].enabled,
            "Actual left uses left slot when raw label is Right");
        Check(correctedBones[0, 0].sharedMaterial.color == Color.green, "Actual left is green");
        Check(Field<TextMesh>(correctedDemo, "status").text.Contains("Left: OpenPalm"), "Actual left HUD label corrected");
        Check(physicalLeft.rawHandedness == "Right", "Raw source label retained");
        Check(Vector3.Distance(Field<Vector3[,]>(correctedDemo, "eyeJoints")[0, 1],
            new Vector3(-beforeReflection[0, 1].x, beforeReflection[0, 1].y, beforeReflection[0, 1].z)) < .000001f,
            "Anatomical swap preserves previously corrected display geometry");
        var physicalRight = Hand("Left"); physicalRight.rawGesture = "Closed_Fist";
        // These fixtures share a wrist position; declare the actual hand replacement.
        Field<SpatialUvcHandProcessor>(correctedDemo, "uvcProcessor").Reset();
        Send(correctedDemo, 2, physicalRight);
        Check(!correctedBones[0, 0].enabled && correctedBones[1, 0].enabled,
            "Actual right uses right slot when raw label is Left");
        Check(correctedBones[1, 0].sharedMaterial.color == Color.cyan, "Actual right is cyan");
        Check(Field<TextMesh>(correctedDemo, "status").text.Contains("Right: ClosedFist"), "Actual right HUD label corrected");
        Send(correctedDemo, 3, physicalRight, physicalLeft);
        var correctedText = Field<TextMesh>(correctedDemo, "status").text;
        Check(correctedBones[0, 0].enabled && correctedBones[1, 0].enabled && correctedText.Contains("Left: OpenPalm") &&
            correctedText.Contains("Right: ClosedFist"), "Distinct gestures remain on correct sides with both hands");
        Check(JsonUtility.FromJson<HostDeviceProfile>(profileAsset.text).uvcSwapHandedness, "Xiaomi enables anatomical correction");
        UnityEngine.Object.DestroyImmediate(correctedRoot);
        UnityEngine.Object.DestroyImmediate(correctedCamera);
        var overrideRoot = new GameObject("Calibration override hands");
        var overrideCamera = new GameObject("Calibration override camera");
        var overrideDemo = overrideRoot.AddComponent<HandTrackingDemo>();
        overrideDemo.xrCamera = overrideCamera.AddComponent<Camera>();
        overrideDemo.defaultUvcSwapHandedness = true;
        calibration.swapHandedness = false;
        var overrideAsset = new TextAsset(JsonUtility.ToJson(calibration));
        overrideDemo.uvcCalibrationJson = overrideAsset;
        Call(overrideDemo, "Start");
        Send(overrideDemo, 1, Hand("Left"));
        Check(Field<LineRenderer[,]>(overrideDemo, "bones")[0, 0].enabled &&
            !Field<bool>(overrideDemo, "swapUvcHandedness"), "Explicit JSON false overrides host default true");
        UnityEngine.Object.DestroyImmediate(overrideRoot);
        UnityEngine.Object.DestroyImmediate(overrideCamera);
        UnityEngine.Object.DestroyImmediate(overrideAsset);
        var bootRoot = new GameObject("Sample bootstrap");
        var boot = bootRoot.AddComponent<HelloSpatialXR>();
        var bootCamera = new GameObject("Bootstrap camera");
        boot.xrCamera = bootCamera.AddComponent<Camera>();
        Call(boot, "Start");
        Check(bootRoot.GetComponents<HandTrackingDemo>().Length == 1, "Sample bootstrap installs hand demo");
        UnityEngine.Object.DestroyImmediate(bootRoot);
        UnityEngine.Object.DestroyImmediate(bootCamera);
        Debug.Log("SPATIALAI_UVC_HAND_DEMO_PASS checks=" + checks);
        EditorApplication.Exit(0);
    }
}
#endif
