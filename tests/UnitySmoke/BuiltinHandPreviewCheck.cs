#if UNITY_EDITOR
using System;
using System.Reflection;
using SpatialAI.Core;
using SpatialAI.Unity;
using SpatialAI.XRHands;
using UnityEngine;

public static class BuiltinHandPreviewCheck
{
    private static int checks;
    private static void Check(bool value,string name)
    { if(!value)throw new Exception("FAIL: "+name);checks++; }
    private static SpatialHandFrame Image(long sequence,long now,string camera="builtin-rear:0")
    {
        var joints=new SpatialHandJoint[21];
        for(int i=0;i<21;i++)joints[i]=new SpatialHandJoint{x=.5f+i*.003f,y=.6f-i*.01f,z=0};
        return new SpatialHandFrame{cameraId=camera,cameraSource="builtin-rear",imageWidth=640,imageHeight=480,
            inputWidth=216,inputHeight=384,sequence=sequence,receivedHostStopwatchNs=now,
            hands=new[]{new SpatialHand{rawHandedness="Left",rawGesture="Open_Palm",joints=joints}}};
    }
    public static void Run()
    {
        checks=0;long now=1_000_000_000;
        var wrongUvc=new UvcHandCalibration{cameraId="uvc:unrelated",swapHandedness=true};
        var strict=new SpatialUvcHandProcessor(wrongUvc,clock:()=>now);
        Check(!strict.Publish(Image(1,now),Pose.identity,0),"default SDK mode still rejects uncalibrated builtin input");
        var source=new UvcHandsFrameSource(wrongUvc,()=>now,allowBuiltinPreview:true);
        Check(source.Publish(Image(1,now),Pose.identity,0),"builtin preview does not reuse UVC calibration");
        SpatialHandsFrame frame;
        Check(source.TryGetLatestFrame(out frame)&&!frame.Left.IsTracked&&!frame.Right.IsTracked,"drawing does not create tracked XR input");
        var processor=(SpatialUvcHandProcessor)typeof(UvcHandsFrameSource).GetProperty("Processor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(source);
        Check(processor.CalibrationWarning.Contains("display only"),"preview assumptions labelled");
        Check(processor.Associated.inputWidth==216&&processor.Associated.inputHeight==384,"rotated input metadata retained");
        Vector3 wrist;
        Check(processor.TryPresentationJoint(HandSide.Left,HandJointId.Wrist,out wrist),"raw Left remains Left despite UVC handedness swap");
        Check(Mathf.Abs(wrist.z-.5f)<.0001f,"fixed depth used");
        Check(Mathf.Abs(wrist.y-(-.1f*384/(216*1.8660254f)*.5f))<.0001f,"projection uses rotated model dimensions");
        Vector3[] points;bool[] valid;
        Check(processor.TryPresentation(HandSide.Left,out points,out valid)&&Array.TrueForAll(valid,x=>x),"all 26 skeleton joints available for drawing");
        var subsystem=SpatialHandSubsystem.Create(source);subsystem.Start();subsystem.TryUpdateHands(UnityEngine.XR.Hands.XRHandSubsystem.UpdateType.Dynamic);
        var root=new GameObject("Builtin preview verification");var visual=root.AddComponent<SpatialXRHandsVisualizer>();
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        try
        {
            var markers=(Transform[,])typeof(SpatialXRHandsVisualizer).GetField("markers",flags).GetValue(visual);
            if(markers[0,0]==null)typeof(SpatialXRHandsVisualizer).GetMethod("Awake",flags).Invoke(visual,null);
            typeof(SpatialXRHandsVisualizer).GetField("subsystem",flags).SetValue(visual,subsystem);
            typeof(SpatialXRHandsVisualizer).GetMethod("Draw",flags).Invoke(visual,null);
            int count=0;for(int i=0;i<26;i++)if(markers[0,i].gameObject.activeSelf)count++;
            Check(count==26,"existing XR visualizer renders builtin skeleton with untracked input");
            Check(!subsystem.leftHand.isTracked,"preview cannot drive XR selection");
            now+=351_000_000;subsystem.TryUpdateHands(UnityEngine.XR.Hands.XRHandSubsystem.UpdateType.Dynamic);
            typeof(SpatialXRHandsVisualizer).GetMethod("Draw",flags).Invoke(visual,null);
            count=0;for(int i=0;i<26;i++)if(markers[0,i].gameObject.activeSelf)count++;
            Check(count==0,"stale skeleton hidden");
            now+=1_000_000;var empty=Image(2,now);empty.hands=new SpatialHand[0];source.Publish(empty,Pose.identity,0);
            Check(!processor.TryPresentationJoint(HandSide.Left,HandJointId.Wrist,out wrist),"missing hand clears drawing immediately");
            now+=1_000_000;Check(source.Publish(Image(3,now,"builtin-front:1"),Pose.identity,0),"front source switch accepted");
            Check(processor.TryPresentationJoint(HandSide.Left,HandJointId.Wrist,out wrist),"front skeleton rebuilt after switch");
            now+=1_000_000;var uv=Image(4,now,"uvc:unrelated");
            Check(source.Publish(uv,Pose.identity,0)&&!processor.BuiltinPreviewActive,"switching to UVC restores original converter");
        }
        finally { UnityEngine.Object.DestroyImmediate(root);subsystem.Stop();subsystem.Destroy(); }
        Debug.Log("SPATIALAI_BUILTIN_PREVIEW_PASS checks="+checks);
    }
    public static void RunAndBuild(){Run();SpatialXRSetup.BuildAndroidSample();}
}
#endif
