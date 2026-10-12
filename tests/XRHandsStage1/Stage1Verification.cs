using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SpatialAI.Core;
using SpatialAI.XRHands;
using SpatialAI.XRHands.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Hands;

public static class Stage1Verification
{
    [InitializeOnLoadMethod]
    private static void RestorePlayCallback()
    {
        // Play-mode domain reload resets static delegates; SessionState survives.
        if (!UnityEditor.SessionState.GetBool("SpatialStage1.Pending", false)) return;
        EditorApplication.playModeStateChanged -= OnPlayState;
        EditorApplication.playModeStateChanged += OnPlayState;
    }
    private static int checks;
    private static void Check(bool condition, string name)
    { if (!condition) throw new Exception("FAIL: " + name); checks++; }
    private static SimulatedHandState State(Vector3 position)
    { return new SimulatedHandState { position = position, eulerAngles = Vector3.zero }; }
    public static void Run()
    {
        try
        {
            ContractTests();
            const string path = "Assets/SpatialHandsSimulation.unity";
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.spatialai.xr-hands/package.json");
            File.Copy(Path.Combine(package.resolvedPath, "Samples~", "HandsSimulation", "SpatialHandsSimulation.unity"), path);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(path);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(path, true) };
            var host = GameObject.Find("Spatial Hands Simulator").GetComponent<SpatialHandsSimulator>();
            host.animate = false;
            host.gameObject.AddComponent<Stage1PlayProbe>();
            UnityEditor.SessionState.SetBool("SpatialStage1.Pending", true);
            UnityEditor.SessionState.SetInt("SpatialStage1.PlaySessions", 0);
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= OnPlayState;
        if (!UnityEditor.SessionState.GetBool("SpatialStage1.PlayPassed", false)) { EditorApplication.Exit(1); return; }
        var subsystems = new List<XRHandSubsystem>(); SubsystemManager.GetSubsystems(subsystems);
        if (subsystems.FindAll(s => s is SpatialHandSubsystem).Count != 0) throw new Exception("Play exit left a hand subsystem.");
        int sessions = UnityEditor.SessionState.GetInt("SpatialStage1.PlaySessions", 0) + 1;
        UnityEditor.SessionState.SetInt("SpatialStage1.PlaySessions", sessions);
        if (sessions < 2)
        {
            UnityEditor.SessionState.SetBool("SpatialStage1.PlayPassed", false);
            var host = UnityEngine.Object.FindObjectOfType<SpatialHandsSimulator>();
            if (host.GetComponent<Stage1PlayProbe>() == null) host.gameObject.AddComponent<Stage1PlayProbe>();
            EditorApplication.playModeStateChanged += OnPlayState;
            EditorApplication.EnterPlaymode();
            return;
        }
        UnityEditor.SessionState.SetBool("SpatialStage1.Pending", false);
        Debug.Log("SPATIALAI_STAGE1_PASS contractChecks=" + UnityEditor.SessionState.GetInt("SpatialStage1.Checks", 0) + " playSessions=" + sessions);
        EditorApplication.Exit(0);
    }
    private static void ContractTests()
    {
        SpatialHandSubsystem.RegisterDescriptor(); SpatialHandSubsystem.RegisterDescriptor();
        var descriptors = new List<XRHandSubsystemDescriptor>(); SubsystemManager.GetSubsystemDescriptors(descriptors);
        Check(descriptors.FindAll(d => d.id == SpatialHandSubsystem.DescriptorId).Count == 1, "one descriptor");
        var source = new SimulatedHandsFrameSource();
        var left = State(new Vector3(-0.13f, 0.1f, 0.5f)); var right = State(new Vector3(0.13f, 0.1f, 0.5f));
        var initial = source.Publish(left, right, 100);
        var subsystem = SpatialHandSubsystem.Create(source);
        try
        {
            for (int i = 0; i < 26; i++)
                Check(subsystem.jointsInLayout[SpatialXRHandsContract.ToXRJointId((HandJointId)i).ToIndex()], "layout " + i);
            subsystem.Start();
            int updates = 0, acquired = 0, lost = 0;
            subsystem.updatedHands += (s, flags, type) => updates++;
            subsystem.trackingAcquired += hand => acquired++;
            subsystem.trackingLost += hand => lost++;
            Check(subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic) == XRHandSubsystem.UpdateSuccessFlags.All, "all update flags");
            Check(subsystem.leftHand.isTracked && subsystem.rightHand.isTracked && acquired == 2, "both hands acquired once");
            Pose p;
            for (int i = 0; i < 26; i++)
            {
                var id = SpatialXRHandsContract.ToXRJointId((HandJointId)i);
                var joint = subsystem.leftHand.GetJoint(id);
                Check(joint.id == id && joint.handedness == Handedness.Left && joint.TryGetPose(out p), "left standard joint " + i);
                joint = subsystem.rightHand.GetJoint(id);
                Check(joint.id == id && joint.handedness == Handedness.Right && joint.TryGetPose(out p), "right standard joint " + i);
                float radius; Vector3 velocity;
                Check(joint.TryGetRadius(out radius) && Mathf.Abs(radius - 0.006f) < 1e-6f, "metric radius " + i);
                Check(!joint.TryGetLinearVelocity(out velocity) && !joint.TryGetAngularVelocity(out velocity), "unsupported velocities " + i);
            }
            Check(Vector3.Distance(subsystem.leftHand.rootPose.position, left.position) < 1e-6f, "root in origin metres");
            Check(subsystem.leftHand.rootPose.position.x < 0 && subsystem.rightHand.rootPose.position.x > 0, "independent left/right roots");
            subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.BeforeRender);
            Check(updates == 2 && acquired == 2 && subsystem.LastFrame.Sequence == initial.Sequence, "two phases reuse snapshot without reacquire");

            var oldLeft = initial.Left;
            left.position += new Vector3(0.08f, 0.03f, -0.05f); left.eulerAngles = new Vector3(23, 42, -17);
            var moved = source.Publish(left, right, 200);
            subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            for (int i = 0; i < 26; i++)
            {
                var a = oldLeft.GetJoint((HandJointId)i); var b = moved.Left.GetJoint((HandJointId)i);
                var local = new Vector3(a.Position.X, a.Position.Y, a.Position.Z) - new Vector3(-0.13f, 0.1f, 0.5f);
                var expected = left.position + Quaternion.Euler(left.eulerAngles) * local;
                var actual = new Vector3(b.Position.X, b.Position.Y, b.Position.Z);
                Check(Vector3.Distance(expected, actual) <= 0.00001f, "combined rigid position " + i);
                subsystem.leftHand.GetJoint(SpatialXRHandsContract.ToXRJointId((HandJointId)i)).TryGetPose(out p);
                var q = b.Orientation;
                Check(Quaternion.Angle(p.rotation, new Quaternion(q.X, q.Y, q.Z, q.W)) <= 0.01f, "joint orientation " + i);
            }
            Check(Vector3.Distance(subsystem.rightHand.rootPose.position, right.position) < 1e-6f, "left transform does not move right");
            BoneChecks(oldLeft, moved.Left);

            left.missingJoint = HandJointId.Palm; right.missingRotation = HandJointId.IndexTip;
            source.Publish(left, right, 300); subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(!subsystem.leftHand.GetJoint(XRHandJointID.Palm).TryGetPose(out p), "missing joint clears old pose");
            Check(!subsystem.rightHand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out p), "missing orientation does not fake pose");
            Check(subsystem.leftHand.isTracked && subsystem.rightHand.isTracked, "partial loss preserves hands");
            left.tracked = false; source.Publish(left, right, 400);
            Check(subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic) ==
                (XRHandSubsystem.UpdateSuccessFlags.RightHandJoints | XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose), "per-hand loss flags");
            Check(!subsystem.leftHand.isTracked && lost == 1, "left tracking lost once");
            for (int i = 0; i < 26; i++) Check(!subsystem.leftHand.GetJoint(SpatialXRHandsContract.ToXRJointId((HandJointId)i)).TryGetPose(out p), "lost joint " + i);
            left.tracked = true; left.missingJoint = right.missingRotation = HandJointId.Count;
            source.Publish(left, right, 500); subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(acquired == 3 && subsystem.leftHand.isTracked, "hand recovers");
            subsystem.Stop();
            Check(!subsystem.running && !subsystem.leftHand.isTracked && !subsystem.rightHand.isTracked, "stop invalidates cached hands");
            int stoppedUpdates = updates;
            Check(subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.BeforeRender) == XRHandSubsystem.UpdateSuccessFlags.None && updates == stoppedUpdates, "stopped subsystem has no update callbacks");
            subsystem.Start(); subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(subsystem.leftHand.isTracked && acquired == 5, "stop/start reacquires without another subsystem");

            foreach (SimulatedHandPose kind in Enum.GetValues(typeof(SimulatedHandPose)))
            {
                left.pose = kind; right.pose = kind; left.eulerAngles = right.eulerAngles = Vector3.zero;
                var frame = source.Publish(left, right, 600);
                Check(source.Capabilities.Validate(frame.Left).Success && source.Capabilities.Validate(frame.Right).Success, "valid pose " + kind);
                BoneChecks(oldLeft, frame.Left);
                if (kind == SimulatedHandPose.Pinch)
                {
                    var thumb = frame.Left.GetJoint(HandJointId.ThumbTip).Position; var index = frame.Left.GetJoint(HandJointId.IndexTip).Position;
                    Check(Vector3.Distance(new Vector3(thumb.X, thumb.Y, thumb.Z), new Vector3(index.X, index.Y, index.Z)) < 0.003f, "pinch tips converge");
                }
            }
            var revision = moved.OriginRevision; source.Recenter(); SpatialHandsFrame absent;
            Check(!source.TryGetLatestFrame(out absent), "recenter clears old revision");
            Check(source.Publish(left, right, 700).OriginRevision == revision + 1, "recenter increments origin revision");
        }
        finally { subsystem.Stop(); subsystem.Destroy(); }
        var instances = new List<XRHandSubsystem>(); SubsystemManager.GetSubsystems(instances);
        Check(instances.FindAll(s => s is SpatialHandSubsystem).Count == 0, "destroy removes subsystem");
        UnityEditor.SessionState.SetInt("SpatialStage1.Checks", checks); UnityEditor.SessionState.SetBool("SpatialStage1.PlayPassed", false);
    }
    private static void BoneChecks(SpatialHandSample original, SpatialHandSample transformed)
    {
        // Every finger segment, including metacarpal/proximal, must retain length.
        for (int i = 2; i < 26; i++)
        {
            if (i == 5 || i >= 6 && (i - 6) % 5 == 4) continue;
            Check(Mathf.Abs(Distance(original, i, i + 1) - Distance(transformed, i, i + 1)) < 0.00001f, "bone length " + i);
        }
    }
    private static float Distance(SpatialHandSample sample, int a, int b)
    {
        var p = sample.GetJoint((HandJointId)a).Position; var q = sample.GetJoint((HandJointId)b).Position;
        return Vector3.Distance(new Vector3(p.X, p.Y, p.Z), new Vector3(q.X, q.Y, q.Z));
    }
}
