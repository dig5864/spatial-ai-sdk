#if UNITY_EDITOR
using System;
using SpatialAI.Core;
using SpatialAI.Unity;
using SpatialAI.XRHands;
using UnityEngine;
public static class UvcLiveCalibrationCheck
{
    static void Check(bool ok,string detail){if(!ok)throw new Exception(detail);}
    public static void Run()
    {
        var c=UvcGeometryChecks.Calibration();c.headFromCameraTranslationMetres=new[]{0f,.0812132f,.0894975f};
        var xyz=UvcGeometryChecks.Skeleton();var uv=UvcGeometryChecks.Project(c,xyz);
        var joints=new SpatialHandJoint[21];for(int i=0;i<21;i++)joints[i]=new SpatialHandJoint{x=uv[i].X,y=uv[i].Y,z=0};
        long now=1000000000;
        var image=new SpatialHandFrame{cameraId=c.cameraId,cameraSource="external-uvc",imageWidth=640,imageHeight=480,sequence=1,receivedHostStopwatchNs=now,hands=new[]{new SpatialHand{rawHandedness="Left",joints=joints}}};
        var source=new UvcHandsFrameSource(c,()=>now,maximumPalmAxisRatio:4);
        Check(source.Publish(image,Pose.identity,0),"Initial calibration did not publish");
        SpatialHandsFrame original;Check(source.TryGetLatestFrame(out original)&&original.Left.IsTracked,"Initial hand missing");
        var candidate=JsonUtility.FromJson<UvcHandCalibration>(JsonUtility.ToJson(c));
        candidate.cameraId="uvc:wrong";Check(!source.TryReconfigureForPreview(candidate),"Wrong camera accepted");
        SpatialHandsFrame unchanged;Check(source.TryGetLatestFrame(out unchanged)&&ReferenceEquals(original,unchanged),"Failed reconfiguration cleared valid input");
        candidate.cameraId=c.cameraId;var q=Quaternion.AngleAxis(15,Vector3.right);
        candidate.headFromCameraRotationXyzw=new[]{q.x,q.y,q.z,q.w};candidate.calibrationVersion="live-pitch-fixture";
        Check(source.TryReconfigureForPreview(candidate),"Valid preview replacement rejected");
        Check(!source.TryGetLatestFrame(out unchanged),"Old pose survived calibration change");
        candidate.headFromCameraTranslationMetres[1]=99; // Caller mutation cannot change the owned snapshot.
        now+=1000000;image.sequence++;image.receivedHostStopwatchNs=now;
        Check(source.Publish(image,Pose.identity,0),"New frame did not use changed calibration");
        SpatialHandsFrame frame;Check(source.TryGetLatestFrame(out frame)&&frame.Left.IsTracked,"Changed frame missing");
        var expected=new Vector3(0,.0812132f,.0894975f)+q*new Vector3(0,0,.5f);
        var actual=frame.Left.Root.Position;
        Check((new Vector3(actual.X,actual.Y,actual.Z)-expected).magnitude<.0001f,"Pitch/translation composition wrong or clone mutated");
        Check(actual.Y<original.Left.Root.Position.Y,"Positive pitch failed to move rays down");
        var id=HandJointId.IndexTip;
        var oa=original.Left.GetJoint(id).Position;var na=frame.Left.GetJoint(id).Position;
        var oroot=original.Left.Root.Position;var nroot=frame.Left.Root.Position;
        float od=(new Vector3(oa.X-oroot.X,oa.Y-oroot.Y,oa.Z-oroot.Z)).magnitude;
        float nd=(new Vector3(na.X-nroot.X,na.Y-nroot.Y,na.Z-nroot.Z)).magnitude;
        Check(Math.Abs(od-nd)<.00001f,"Rigid pitch changed physical hand scale");
        now+=351000000;Check(!source.TryGetLatestFrame(out frame),"Source clock timeout lost after replacement");
        Debug.Log("UVC_LIVE_CALIBRATION_PASS wrong-camera-rejected=true old-frame-cleared=true pitch-down=true scale-preserved=true clock-preserved=true");
    }
}
#endif
