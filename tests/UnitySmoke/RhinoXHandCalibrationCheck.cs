#if UNITY_EDITOR
using System;
using SpatialAI.Core;
using SpatialAI.Unity;
using SpatialAI.XRHands;
using UnityEngine;
public static class RhinoXHandCalibrationCheck
{
    static void Check(bool value,string detail){if(!value)throw new Exception(detail);}
    public static void Run()
    {
        var asset=Resources.Load<TextAsset>("SpatialAI/Calibrations/rhinox-pro-03P11D14202L-hands");
        Check(asset!=null,"RhinoX calibration resource missing");
        var c=JsonUtility.FromJson<UvcHandCalibration>(asset.text);
        Check(c.Validate(true).Success && !c.Validate(false).Success,"Initial installation must remain unverified");
        var reference=JsonUtility.FromJson<SpatialRgbCalibration>(asset.text);
        int rays=0;
        foreach(float u in new[]{.05f,.2f,.5f,.8f,.95f})foreach(float v in new[]{.05f,.25f,.5f,.75f,.95f})
        {
            HandVector3 a;Vector3 b=Vector3.zero;
            Check(c.TryCameraRay(u,v,out a) && reference.TryCameraRay(new SpatialHandJoint{x=u,y=v},1440,1080,out b),"Rational ray solve failed");
            Check((new Vector3(a.X,a.Y,a.Z)-b).magnitude<.0001f,"Rational ray disagrees with independent analytic solver");rays++;
        }
        var points=UvcGeometryChecks.Skeleton();
        points[5]=new HandVector3(-.032f,.09f,.5f);points[9]=new HandVector3(0,.09f,.5f);points[17]=new HandVector3(.032f,.09f,.5f);
        var joints=new SpatialHandJoint[21];
        for(int i=0;i<21;i++){float u,v;Check(c.TryProject(points[i],out u,out v),"Projection failed");joints[i]=new SpatialHandJoint{x=u,y=v,z=0};}
        long now=1000000000;
        var image=new SpatialHandFrame{cameraId="2",cameraSource="rhinox-rgb",imageWidth=1440,imageHeight=1080,inputWidth=384,inputHeight=288,sequence=1,receivedHostStopwatchNs=now,hands=new[]{new SpatialHand{rawHandedness="Left",joints=joints}}};
        var source=new UvcHandsFrameSource(c,()=>now,maximumPalmAxisRatio:4);
        Check(source.Publish(image,Pose.identity,0),"Calibrated RhinoX frame rejected");
        SpatialHandsFrame frame;Check(source.TryGetLatestFrame(out frame) && frame.Right.IsTracked,"RhinoX hand not converted with shared measured palm scale");
        var expected=c.HeadFromCamera.Transform(points[0]);var actual=frame.Right.Root.Position;
        Check(Math.Abs(actual.X-expected.X)<.0001f && Math.Abs(actual.Y-expected.Y)<.0001f && Math.Abs(actual.Z-expected.Z)<.0001f,"Extrinsic not applied to hand root");
        image.sequence++;image.cameraId="1";Check(!source.Publish(image,Pose.identity,0),"Wrong native camera accepted");
        image.cameraId="2";image.imageWidth=1920;Check(!source.Publish(image,Pose.identity,0),"Uncalibrated crop accepted");
        Debug.Log("RHINOX_HAND_CALIBRATION_PASS rays="+rays+" extrinsic-applied=true mismatch-rejected=true");
    }
    public static void RunAndBuild(){Run();BuildCalibratedXri.Build();}
}
#endif
