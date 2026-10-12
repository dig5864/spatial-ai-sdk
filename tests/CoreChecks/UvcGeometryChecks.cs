using System;
using SpatialAI.Core;

public static class UvcGeometryChecks
{
    public static UvcHandCalibration Calibration()
    {
        return new UvcHandCalibration { cameraId="uvc:11cb:281b",calibrationVersion="synthetic-test-only",source="synthetic",
            imageWidth=640,imageHeight=480,fx=500,fy=510,cx=320,cy=240,
            palmWidthMetres=.06f,wristToMiddleMetres=(float)Math.Sqrt(.08*.08+.01*.01),
            intrinsicsCalibrated=true,extrinsicsMeasured=true,handednessVerified=true,palmDimensionsMeasured=true };
    }
    public static HandVector3[] Skeleton(float depth=.5f)
    {
        var points=new HandVector3[21];points[0]=new HandVector3(0,0,depth);
        for(int i=1;i<=4;i++)points[i]=new HandVector3(-.035f-.01f*i,.012f*i,depth);
        for(int finger=0;finger<4;finger++)for(int joint=0;joint<4;joint++)
            points[5+finger*4+joint]=new HandVector3(-.03f+.02f*finger,.08f+.015f*joint,depth);
        return points;
    }
    public static NormalizedHandPoint[] Project(UvcHandCalibration c,HandVector3[] skeleton)
    {
        var input=new NormalizedHandPoint[21];
        for(int i=0;i<21;i++) { float u,v;if(!c.TryProject(skeleton[i],out u,out v))throw new Exception("Fixture projection failed");input[i]=new NormalizedHandPoint(u,v,0); }
        return input;
    }
    static float Distance(HandVector3 a,HandVector3 b)
    { return (float)Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z)); }
    public static void Run(Action<bool,string> check)
    {
        var c=Calibration();check(c.Validate(false).Success,"measured synthetic calibration accepted");
        check(!c.validatedForHardware,"synthetic profile does not claim hardware acceptance");
        c.extrinsicsMeasured=false;check(!c.Validate(false).Success && c.Validate(true).Success,"strict measurement assessment remains available");c.extrinsicsMeasured=true;
        c.fx=0;check(!c.Validate(true).Success,"raw geometry validation requires focal length before default resolution");c.fx=500;
        c.distortion=new[]{-.28f,.07f,.001f,-.002f,.005f};
        foreach(int rotation in new[]{0,90,180,270})foreach(bool mirror in new[]{false,true})
        {
            c.imageRotationClockwise=rotation;c.inputMirrored=mirror;
            foreach(float x in new[]{-.45f,-.1f,0,.1f,.45f})foreach(float y in new[]{-.3f,0,.3f})
            {
                var point=new HandVector3(x,y,1);float u,v;HandVector3 ray;
                check(c.TryProject(point,out u,out v) && c.TryCameraRay(u,v,out ray) && Distance(point,ray)<.00004,
                    "distorted projection/unprojection across image rotations and mirrors");
            }
            check(c.MatchesDeliveredSize(rotation%180==0?640:480,rotation%180==0?480:640),"rotated image size");
        }
        c.distortion=new float[5];
        foreach(int rotation in new[]{0,90,180,270})foreach(bool mirror in new[]{false,true})
        {
            c.imageRotationClockwise=rotation;c.inputMirrored=mirror;float u=.65f,v=.4f;
            float a=rotation==90?1-v:rotation==180?1-u:rotation==270?v:u;
            float b=rotation==90?u:rotation==180?1-v:rotation==270?1-u:v;
            if(mirror)a=1-a;HandVector3 ray;
            check(c.TryCameraRay(a,b,out ray) && Math.Abs(ray.X-.192f)<.00001 && Math.Abs(ray.Y-48f/510)<.00001,
                "independent known pixel axes/rotation/mirror expectation");
        }
        c.imageRotationClockwise=0;c.inputMirrored=false;HandVector3 unused;
        check(!c.TryCameraRay(-.1f,.5f,out unused) && !c.TryCameraRay(float.NaN,.5f,out unused),"invalid image coordinates rejected");
        c.distortion[0]=float.NaN;check(!c.TryCameraRay(.5f,.5f,out unused),"nonfinite lens rejected");c.distortion[0]=0;
        float pu,pv;check(!c.TryProject(new HandVector3(0,0,0),out pu,out pv),"point behind projection rejected");
        int[] mapping={0,-1,1,2,3,4,-1,5,6,7,8,-1,9,10,11,12,-1,13,14,15,16,-1,17,18,19,20};
        for(int i=0;i<26;i++)check(MonocularHandConverter.LandmarkIndex((HandJointId)i)==mapping[i],"independent 21-to-26 mapping");
        foreach(var side in new[]{HandSide.Left,HandSide.Right})foreach(float depth in new[]{.3f,.5f,.7f})
        {
            var skeleton=Skeleton(depth);var input=Project(c,skeleton);var converter=new MonocularHandConverter(c);
            SpatialHandSample hand;float estimated;
            check(converter.Convert(side,input,640,480,HandRigidPose.Identity,out hand,out estimated).Success && hand.IsTracked && Math.Abs(estimated-depth)<.00001,"metric depth at measured front-facing palm scale");
            check(converter.Capabilities.Validate(hand).Success,"conversion obeys estimated capabilities");
            for(int i=0;i<26;i++)
            {
                var joint=hand.GetJoint((HandJointId)i);
                check(joint.Valid==(HandDataFields.Position|HandDataFields.Rotation) && joint.Estimated==joint.Valid && joint.Orientation.IsUnit,
                    "all output poses explicitly estimated; radius and velocities unsupported");
                if(mapping[i]>=0)check(Distance(joint.Position,skeleton[mapping[i]])<.00002,"21 measured correspondences in metres");
            }
            check(Distance(hand.GetJoint(HandJointId.IndexMetacarpal).Position,new HandVector3(-.015f,.04f,depth))<.00002,"missing metacarpal halfway estimate");
            check(Distance(hand.GetJoint(HandJointId.Palm).Position,new HandVector3(0,.08f,depth))<.00002,"palm centre estimate");
            var mount=Rotation.FromYawPitch(14,-25);c.headFromCameraRotationXyzw=new[]{mount.X,mount.Y,mount.Z,mount.W};
            c.headFromCameraTranslationMetres=new[]{.025f,-.03f,.02f};
            var roll=new Rotation(0,0,(float)Math.Sin(.2),(float)Math.Cos(.2));
            var head=new HandRigidPose(new HandVector3(.1f,.2f,.3f),Rotation.FromYawPitch(65,20)*roll);
            check(converter.Convert(side,input,640,480,head,out hand,out estimated).Success,"combined installation and physical head rotations");
            check(Distance(hand.Root.Position,head.Transform(c.HeadFromCamera.Transform(skeleton[0])))<.00002,"camera to physical head to origin with translation");
            var root=hand.Root.Orientation;
            check(root.IsUnit,"composed quaternion stays unit");
            c.headFromCameraRotationXyzw=new[]{0f,0f,0f,1f};c.headFromCameraTranslationMetres=new float[3];
        }
        var conv=new MonocularHandConverter(c);var points=Project(c,Skeleton());SpatialHandSample h;float d;
        points[8]=default(NormalizedHandPoint);
        check(conv.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success && h.GetJoint(HandJointId.IndexTip).Valid==HandDataFields.None && h.GetJoint(HandJointId.IndexDistal).Valid==HandDataFields.Position,"occluded tip invalidates tip and predecessor orientation");
        points[5]=default(NormalizedHandPoint);
        check(!conv.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success && !h.IsTracked,"missing palm anchor invalidates hand");
        points=Project(c,Skeleton());points[17]=points[5];
        check(!conv.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success,"collapsed palm rejected");
        points=Project(c,Skeleton());points[17]=new NormalizedHandPoint(points[5].X+.005f,points[5].Y,0);
        check(!conv.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success,"severe foreshortening rejected");
        var compressed=Skeleton();
        foreach(int landmark in new[]{5,6,7,8,17,18,19,20})
            compressed[landmark]=new HandVector3(compressed[landmark].X*.4f,compressed[landmark].Y,compressed[landmark].Z);
        var relaxed=new MonocularHandConverter(c,4);
        points=Project(c,compressed);
        check(!conv.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success,"default strict policy still rejects compressed palm");
        check(relaxed.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success&&h.IsTracked&&Math.Abs(d-.5f)<.0001,
            "opt-in far interaction uses the current less foreshortened depth axis");
        check(relaxed.GeometryDiagnostics.Contains("foreshortened accepted")&&h.Root.Estimated==h.Root.Valid,"relaxed geometry is explicitly estimated and diagnosed");
        points[17]=points[5];check(!relaxed.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success,"relaxed policy still rejects collapsed palm");
        points=Project(c,compressed);points[5]=default(NormalizedHandPoint);
        check(!relaxed.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success,"relaxed policy never holds a missing live anchor");
        points=Project(c,Skeleton());points[17]=new NormalizedHandPoint(points[5].X+.005f,points[5].Y,0);
        check(!relaxed.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success,"relaxed policy still bounds severe foreshortening");
        points=Project(c,Skeleton(.1f));check(!conv.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success,"out-of-range depth rejected without clamping");
        points=Project(c,Skeleton());check(!conv.Convert(HandSide.Left,points,384,288,HandRigidPose.Identity,out h,out d).Success,"incorrect delivered dimensions rejected");
        check(!conv.Convert(HandSide.Left,points,640,480,default(HandRigidPose),out h,out d).Success,"invalid physical head quaternion rejected");
        check(!conv.Convert(HandSide.Left,new NormalizedHandPoint[20],640,480,HandRigidPose.Identity,out h,out d).Success,"malformed skeleton rejected");
        c.palmDimensionsMeasured=false;check(conv.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success && conv.CalibrationWarning.Contains("DEVELOPMENT"),"unmeasured scale warns and continues");
        var uncalibrated=new MonocularHandConverter();
        check(uncalibrated.Convert(HandSide.Left,points,640,480,HandRigidPose.Identity,out h,out d).Success && h.IsTracked &&
            uncalibrated.CalibrationWarning.Contains("Calibration missing"),"no calibration gives tracked preview and advisory warning");
        check(uncalibrated.Capabilities.Validate(h).Success && h.Root.Estimated==h.Root.Valid,"fallback still advertises estimated data only");
        UvcHandCalibration fallback;string warning;
        check(UvcHandCalibration.ResolveForPreview(null,"uvc:11cb:281b",640,480,out fallback,out warning).Success && fallback.Validate(true).Success && !fallback.Validate(false).Success,"null config resolves valid unmeasured defaults");
        check(fallback.fx>0 && fallback.cx==320 && fallback.cy==240 && !fallback.validatedForHardware,"fallback centred intrinsics do not claim hardware validation");
        check(fallback.HeadFromCamera.IsValid && fallback.headFromCameraTranslationMetres[0]==0 && fallback.headFromCameraRotationXyzw[3]==1,"fallback installation is explicit identity");
        check(fallback.palmWidthMetres==.07f && fallback.wristToMiddleMetres==.08f && warning.Contains("70/80 mm"),"fallback palm assumptions disclosed");
        var empty=new UvcHandCalibration();
        check(UvcHandCalibration.ResolveForPreview(empty,"uvc:11cb:281b",384,288,out fallback,out warning).Success && fallback.imageWidth==384 && fallback.imageHeight==288,"empty config follows delivered dimensions");
        check(empty.fx==0 && empty.cameraId==null && empty.palmWidthMetres==0,"resolution does not mutate input configuration");
        var partial=Calibration();partial.palmWidthMetres=partial.wristToMiddleMetres=0;
        check(UvcHandCalibration.ResolveForPreview(partial,partial.cameraId,640,480,out fallback,out warning).Success && fallback.fx==500 && fallback.fy==510 && fallback.intrinsicsCalibrated && !fallback.palmDimensionsMeasured,"partial calibration keeps measured intrinsics and defaults only missing palm dimensions");
        partial.fx=0;
        check(UvcHandCalibration.ResolveForPreview(partial,partial.cameraId,640,480,out fallback,out warning).Success && !fallback.intrinsicsCalibrated && fallback.fy==510,"missing focal length filled without pretending intrinsics measured");
        partial.fx=float.NaN;
        check(!UvcHandCalibration.ResolveForPreview(partial,partial.cameraId,640,480,out fallback,out warning).Success,"nonfinite configured geometry still rejected");
        partial=Calibration();partial.headFromCameraRotationXyzw=new[]{0f,0f,0f,.5f};
        check(!UvcHandCalibration.ResolveForPreview(partial,partial.cameraId,640,480,out fallback,out warning).Success,"nonunit configured rotation still rejected");
        partial=Calibration();partial.imageRotationClockwise=90;partial.imageWidth=partial.imageHeight=0;partial.fx=partial.fy=0;
        check(UvcHandCalibration.ResolveForPreview(partial,partial.cameraId,480,640,out fallback,out warning).Success && fallback.imageWidth==640 && fallback.imageHeight==480 && fallback.MatchesDeliveredSize(480,640),"fallback dimensions respect configured image rotation");
        check(!UvcHandCalibration.ResolveForPreview(null,"uvc:11cb:281b",0,480,out fallback,out warning).Success,"missing frame dimensions cannot generate valid geometry");
        partial=Calibration();
        check(!UvcHandCalibration.ResolveForPreview(partial,"wrong",640,480,out fallback,out warning).Success,"known camera mismatch still rejected");
        check(UvcHandCalibration.ResolveForPreview(partial,partial.cameraId,640,480,out fallback,out warning).Success && warning=="" && fallback.calibrationVersion==partial.calibrationVersion,"complete measured calibration is preferred without fallback warning");
    }
}
