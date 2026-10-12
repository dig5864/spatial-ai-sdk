#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using SpatialAI.Core;
using SpatialAI.Unity;
using SpatialAI.XRHands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Hands;

public static class Stage3Verification
{
    private static int checks;
    private static void Check(bool value,string reason){if(!value)throw new Exception(reason);checks++;}
    public static UvcHandCalibration Calibration()
    {
        return new UvcHandCalibration{cameraId="uvc:11cb:281b",calibrationVersion="stage3-synthetic",imageWidth=640,imageHeight=480,
            fx=554.256f,fy=554.256f,cx=320,cy=240,distortion=new float[5],headFromCameraTranslationMetres=new float[3],
            headFromCameraRotationXyzw=new[]{0f,0f,0f,1f},palmWidthMetres=.07f,wristToMiddleMetres=.08f,minDepthMetres=.2f,maxDepthMetres=1.2f};
    }
    public static SpatialHand Hand(string label)
    {
        var c=Calibration();var points=new SpatialHandJoint[21];
        for(int i=0;i<21;i++)points[i]=new SpatialHandJoint{x=.5f,y=.5f,z=0};
        float[] x={-.055f,-.035f,0,.018f,.035f};
        for(int finger=0;finger<5;finger++)for(int j=0;j<4;j++)
        {
            int index=finger==0?1+j:5+(finger-1)*4+j;
            float y=(finger==0?.035f:.08f)+j*.025f;
            points[index]=new SpatialHandJoint{x=.5f+x[finger]*c.fx/(640*.5f),y=.5f-y*c.fy/(480*.5f)};
        }
        return new SpatialHand{rawHandedness=label,joints=points,hasHandednessScore=true,handednessScore=.9f};
    }
    public static SpatialHandFrame Image(long sequence,params SpatialHand[] hands)
    {return new SpatialHandFrame{cameraId="uvc:11cb:281b",imageWidth=640,imageHeight=480,sequence=sequence,hands=hands,runningMode="VIDEO",@delegate="GPU",gestureSource="test-synthetic"};}
    private static void CheckCompensationClocks()
    {
        long time=100_000_000;var processor=new SpatialUvcHandProcessor(Calibration(),clock:()=>time);
        var head=new Pose(new Vector3(1,2,3),Quaternion.Euler(15,20,5));
        processor.Publish(Image(1,Hand("Left")),head,0);
        Check(processor.Associated.runningMode=="VIDEO"&&processor.Associated.@delegate=="GPU","association preserves backend and timing metadata");
        Check(processor.TryPresentationJoint(HandSide.Left,HandJointId.Wrist,out var wrist)&&Vector3.Distance(wrist,head.position+head.rotation*new Vector3(0,0,.5f))<1e-4,"presentation applies the physical head transform once");
        var malformed=Hand("Right");malformed.joints[5]=null;time+=50_000_000;
        processor.Publish(Image(2,malformed),head,0);
        Check(processor.DisplayState(HandSide.Left)=="held"&&!processor.Latest.Left.IsTracked&&processor.TryPresentationJoint(HandSide.Left,HandJointId.Wrist,out var held)&&Vector3.Distance(held,wrist)<1e-4,"general geometric rejection holds only the previous visualization");
        time+=101_000_000;
        Check(processor.DisplayState(HandSide.Left)=="hidden","150ms geometry hold expires without another result");
        processor.Publish(Image(3,Hand("Left")),head,0);time+=351_000_000;
        Check(processor.DisplayState(HandSide.Left)=="hidden","350ms frame timeout hides fresh presentation");
        processor.Reset("backend changed");
        Check(processor.Latest==null&&processor.Associated==null&&!processor.TryPresentationJoint(HandSide.Left,HandJointId.Wrist,out held),"active invalidation clears identity and retained positions");
    }
    public static void Run()
    {
        try
        {
            CheckCompensationClocks();
            long now=100_000_000;
            var source=new UvcHandsFrameSource(Calibration(),()=>now);
            var processor=(SpatialUvcHandProcessor)typeof(UvcHandsFrameSource).GetProperty("Processor",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(source);
            var initialRight=Hand("Right");foreach(var point in initialRight.joints)point.x+=.1f;
            Check(source.Publish(Image(1,Hand("Left"),initialRight),Pose.identity,0),"publish UVC frame");
            Check(source.TryGetLatestFrame(out var initial) && initial.Left.IsTracked && initial.Right.IsTracked,"both strict hands tracked");
            var subsystem=SpatialHandSubsystem.Create(source);subsystem.Start();
            Check(subsystem.subsystemDescriptor.id==SpatialHandSubsystem.UvcDescriptorId,"distinct UVC descriptor");
            int acquired=0,lost=0;subsystem.trackingAcquired+=h=>acquired++;subsystem.trackingLost+=h=>lost++;
            subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(acquired==2 && subsystem.leftHand.isTracked && subsystem.rightHand.isTracked,"standard acquired events");
            for(int i=0;i<26;i++)
            {
                var id=SpatialXRHandsContract.ToXRJointId((HandJointId)i);
                var expected=initial.Left.GetJoint((HandJointId)i);
                var joint=subsystem.leftHand.GetJoint(id);Pose pose;
                bool valid=(expected.Valid&(HandDataFields.Position|HandDataFields.Rotation))==(HandDataFields.Position|HandDataFields.Rotation);
                Check(joint.TryGetPose(out pose)==valid,"standard joint validity "+id);
                if(valid)Check(Vector3.Distance(pose.position,new Vector3(expected.Position.X,expected.Position.Y,expected.Position.Z))<1e-5,"metric pose unaffected by mirror/display scale "+id);
                Check(!joint.TryGetRadius(out var radius) && !joint.TryGetLinearVelocity(out var v),"unsupported fields absent "+id);
            }
            subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.BeforeRender);
            Check(subsystem.LastFrame==initial && acquired==2,"render phase reuses immutable snapshot");
            Check(!source.Publish(Image(1,Hand("Left")),Pose.identity,0) && source.PublishedFrames==1,"duplicate input not counted as inference");
            var flipped=Hand("Right");now+=50_000_000;
            source.Publish(Image(2,flipped),Pose.identity,0);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(subsystem.leftHand.isTracked && !subsystem.rightHand.isTracked && flipped.stabilizedHandedness==null,"stable identity without modifying raw DTO");
            now+=351_000_000;subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.BeforeRender);
            Check(!subsystem.leftHand.isTracked && lost==2,"350 ms timeout clears standard tracking");
            now+=1;source.Publish(Image(3,Hand("Left")),Pose.identity,0);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(subsystem.leftHand.isTracked,"timeout recovery");
            source.Publish(Image(4),Pose.identity,0);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(!subsystem.leftHand.isTracked,"empty detection immediately untracks standard API");
            source.Publish(Image(5,Hand("Left")),Pose.identity,0);
            var bad=Hand("Left");bad.joints[17].x=bad.joints[5].x+.01f;bad.joints[17].y=bad.joints[5].y;
            source.Publish(Image(6,bad),Pose.identity,0);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(!subsystem.leftHand.isTracked && source.Diagnostics.Contains("Palm axes disagree"),"display depth-held is not promoted to tracked");
            Vector3 heldTip=Vector3.zero;
            Check(processor.DisplayState(HandSide.Left)=="depth-held"&&processor.TryPresentationJoint(HandSide.Left,HandJointId.IndexTip,out heldTip),"SDK shares the legacy side-palm presentation fallback");
            var previousPoint=heldTip;now+=200_000_000;foreach(var point in bad.joints)point.x+=.01f;
            source.Publish(Image(7,bad),Pose.identity,0);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(processor.TryPresentationJoint(HandSide.Left,HandJointId.IndexTip,out heldTip)&&heldTip.x>previousPoint.x&&!subsystem.leftHand.isTracked,"side-palm fallback follows current points beyond 150ms without interaction input");
            source.Publish(Image(8),Pose.identity,0);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(processor.DisplayState(HandSide.Left)=="missing-held"&&!subsystem.leftHand.isTracked,"missing detection is presentation-only");
            now+=121_000_000;
            Check(!processor.TryPresentationJoint(HandSide.Left,HandJointId.IndexTip,out heldTip),"missing presentation expires without another camera result");
            source.Publish(Image(9,bad),Pose.identity,0);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(processor.TryPresentationJoint(HandSide.Left,HandJointId.IndexTip,out heldTip)&&!subsystem.leftHand.isTracked,"presentation recovers after brief dropout without tracked input");
            source.Invalidate("inference backend changed");subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(processor.DisplayState(HandSide.Left)=="hidden","backend changes clear compensation");
            source.Publish(Image(10,bad),Pose.identity,0);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(processor.DisplayState(HandSide.Left)=="hidden","new identity cannot inherit previous depth");
            source.Invalidate("USB disconnect");subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(subsystem.LastFrame==null && !source.TryGetLatestFrame(out var missing),"USB invalidation clears snapshot");
            var head=new Pose(new Vector3(1,2,3),Quaternion.Euler(20,35,10));
            source.Publish(Image(0,Hand("Left")),head,1);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(Vector3.Distance(subsystem.leftHand.rootPose.position,head.position+head.rotation*new Vector3(0,0,.5f))<1e-4,"physical head transformed into tracking origin");
            var badSize=Image(1,Hand("Left"));badSize.imageWidth=320;
            Check(!source.Publish(badSize,head,1) && !source.TryGetLatestFrame(out missing),"camera metadata mismatch rejects frame");
            source.Publish(Image(2,Hand("Left")),head,2);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(subsystem.LastFrame.OriginRevision==2 && subsystem.leftHand.isTracked,"origin revision recovery");
            var late=Image(3,Hand("Left"));late.resultAgeAtReceiveMs=400;
            source.Publish(late,head,2);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Check(!subsystem.leftHand.isTracked,"already old Android result rejected before host timeout");
            subsystem.Stop();subsystem.Destroy();
            SpatialAI.XRHands.Editor.SpatialUvcHandsScene.Ensure();
            EditorSceneManager.OpenScene(SpatialAI.XRHands.Editor.SpatialUvcHandsScene.ScenePath);
            Check(UnityEngine.Object.FindObjectOfType<SpatialUvcHandsSample>()!=null,"standard UVC scene factory creates consumer");
            Check(Camera.main!=null && Camera.main.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>()!=null,"scene camera has XR rotation driver");
            Debug.Log("SPATIALAI_STAGE3_CONTRACT_PASS checks="+checks);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            new GameObject("Stage 3 lifecycle probe").AddComponent<Stage3PlayProbe>();
            EditorApplication.EnterPlaymode();
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
#endif
