using System;
using SpatialAI.Core;
using SpatialAI.Unity;
using SpatialAI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Stage2Verification
{
    private static int checks;
    private static void Check(bool value,string name)
    { if(!value)throw new Exception("FAIL: "+name);checks++; }
    static SpatialHand ImageHand(UvcHandCalibration calibration,string label)
    {
        var input=UvcGeometryChecks.Project(calibration,UvcGeometryChecks.Skeleton());
        var joints=new SpatialHandJoint[21];
        for(int i=0;i<21;i++)joints[i]=new SpatialHandJoint{x=input[i].X,y=input[i].Y,z=input[i].Z};
        return new SpatialHand{rawHandedness=label,joints=joints};
    }
    public static void Run()
    {
        UvcGeometryChecks.Run(Check);
        var calibration=UvcGeometryChecks.Calibration();
        var json=JsonUtility.ToJson(calibration,true);
        var loaded=JsonUtility.FromJson<UvcHandCalibration>(json);
        Check(loaded.Validate(false).Success && loaded.distortion.Length==5 && !loaded.validatedForHardware,"JSON calibrated field roundtrip");
        Check(!JsonUtility.FromJson<UvcHandCalibration>("{}").Validate(false).Success,"empty calibration JSON cannot publish");
        var wrapper=new SpatialUvcHandsConverter(calibration);
        var raw=new SpatialHandFrame{cameraId=calibration.cameraId,sequence=4,imageWidth=640,imageHeight=480,
            timestampNs=999999999999,receivedAtNs=999999999999,hands=new[]{ImageHand(calibration,"Left"),ImageHand(calibration,"Right")}};
        SpatialHandsFrame output;string diagnostic;
        Check(wrapper.Convert(raw,Pose.identity,123456,7,out output,out diagnostic).Success,"legacy result conversion succeeds");
        Check(output.Left.IsTracked && output.Right.IsTracked,"both explicit sides tracked");
        Check(output.Source==HandFrameSource.MonocularEstimate && output.Sequence==4 && output.OriginRevision==7,"identity and estimated source preserved");
        long latency;
        Check(output.SourceTimestampNs==-1 && output.SourceClock==HandClockDomain.Unknown && output.ReceivedClock==HandClockDomain.HostStopwatch && output.ReceivedTimestampNs==123456 && !output.TryGetSourceToReceiveNs(out latency),"unknown legacy clock does not fabricate exposure latency");
        Check(wrapper.Capabilities.Validate(output.Left).Success && diagnostic.Contains("estimated"),"estimated capabilities and diagnostic");
        raw.hands=new[]{ImageHand(calibration,"Left"),ImageHand(calibration,"Left")};
        Check(wrapper.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && !output.Left.IsTracked && !output.Right.IsTracked && diagnostic.Contains("duplicate"),"duplicate side labels invalidated");
        raw.hands=new[]{ImageHand(calibration,"Unknown")};
        Check(wrapper.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && !output.Left.IsTracked && diagnostic.Contains("unknown"),"unknown handedness not guessed");
        calibration.swapHandedness=true;raw.hands=new[]{ImageHand(calibration,"Left")};
        Check(wrapper.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && output.Right.IsTracked && !output.Left.IsTracked,"verified explicit handedness correction independent of image mirror");
        raw.hands[0].joints=new SpatialHandJoint[20];
        Check(wrapper.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && !output.Right.IsTracked,"bad skeleton produces untracked side");
        raw.hands=null;
        Check(wrapper.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && !output.Left.IsTracked && !output.Right.IsTracked,"no hands invalidates both sides");
        raw.cameraId="wrong";Check(!wrapper.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && output==null,"camera mismatch rejects whole frame");raw.cameraId=calibration.cameraId;
        raw.imageWidth=384;Check(!wrapper.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success,"raw/preview size mismatch rejected");raw.imageWidth=640;
        Check(!wrapper.Convert(raw,Pose.identity,-1,0,out output,out diagnostic).Success,"bad host timestamp rejected");
        Check(!wrapper.Convert(raw,new Pose(Vector3.zero,new Quaternion(0,0,0,0)),1,0,out output,out diagnostic).Success,"invalid physical head pose rejected");
        calibration.handednessVerified=false;
        Check(wrapper.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && diagnostic.Contains("DEVELOPMENT") && wrapper.CalibrationWarning.Length>0,"unverified physical config only warns by default");
        var preview=new SpatialUvcHandsConverter();
        int fallbackWarnings=0;
        Application.LogCallback countWarning=(message,stack,type)=>{if(type==LogType.Warning && message.StartsWith("SpatialHands: Calibration"))fallbackWarnings++;};
        Application.logMessageReceived+=countWarning;
        raw.hands=new[]{ImageHand(UvcGeometryChecks.Calibration(),"Left"),ImageHand(UvcGeometryChecks.Calibration(),"Right")};
        Check(preview.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && diagnostic.Contains("DEVELOPMENT") && output.Left.IsTracked && output.Right.IsTracked,"missing config produces both tracked preview hands");
        Check(output.Source==HandFrameSource.MonocularEstimate && preview.Capabilities.Validate(output.Left).Success && output.SourceId.Contains("uncalibrated-preview"),"fallback source identity and estimated capabilities");
        Check(preview.Convert(raw,Pose.identity,2,0,out output,out diagnostic).Success && preview.CalibrationWarning.Length>0,"missing-config warning remains available on subsequent frames");
        Application.logMessageReceived-=countWarning;
        Check(fallbackWarnings==1,"fallback warning logs once rather than every frame");
        var emptyPreview=new SpatialUvcHandsConverter(JsonUtility.FromJson<UvcHandCalibration>("{}"));
        Check(emptyPreview.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && output.Left.IsTracked,"empty JSON calibration does not block preview");
        var template=JsonUtility.FromJson<UvcHandCalibration>("{\"cameraId\":\"uvc:11cb:281b\",\"imageWidth\":640,\"imageHeight\":480,\"distortion\":[0,0,0,0,0],\"headFromCameraTranslationMetres\":[0,0,0],\"headFromCameraRotationXyzw\":[0,0,0,1]}");
        Check(new SpatialUvcHandsConverter(template).Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success && output.Right.IsTracked,"zero-filled pending template continues with defaults");
        calibration.fx=float.NaN;Check(!wrapper.Convert(raw,Pose.identity,1,0,out output,out diagnostic).Success,"nonfinite geometry is not treated as missing calibration");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        SpatialUvcCalibrationScene.Populate(scene);
        Check(UnityEngine.Object.FindObjectsOfType<SpatialHandTracking>().Length==1 && UnityEngine.Object.FindObjectsOfType<SpatialUvcCalibrationRecorder>().Length==1,"capture scene has one camera owner and one recorder");
        Check(EditorSceneManager.SaveScene(scene,"Assets/Stage2CaptureVerified.unity"),"capture scene serializes");
        var metadata=new SpatialUvcCalibrationRecorder.CaptureMetadata{cameraId="uvc:11cb:281b",imageWidth=384,imageHeight=288,nativeWidth=640,nativeHeight=480};
        var restored=JsonUtility.FromJson<SpatialUvcCalibrationRecorder.CaptureMetadata>(JsonUtility.ToJson(metadata));
        Check(restored.fullFieldOfView && !restored.inputMirrored && restored.imageRotationClockwise==0 && restored.nativeWidth==640,"preview capture format explicitly records scale, mirror and crop");
        Debug.Log("SPATIALAI_STAGE2_PASS checks="+checks+" Unity="+Application.unityVersion);
        EditorApplication.Exit(0);
    }
}
