using System;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.Unity
{
    // Stage 2 conversion only: no camera subscription or XR subsystem ownership.
    // Callers must supply physical head pose, not a reflected rendering camera pose.
    public sealed class SpatialUvcHandsConverter
    {
        private readonly UvcHandCalibration calibration;
        private readonly MonocularHandConverter converter;
        private string lastLoggedWarning="";
        private readonly string session=Guid.NewGuid().ToString("N");
        // Presentation-only projection at an already acquired camera depth.
        // Does not manufacture joint orientations or a tracked SDK sample.
        public bool TryPreviewPositions(SpatialHandFrame frame, SpatialHand hand, float cameraDepth, out Vector3[] positions)
        {
            positions=null;
            if(frame==null || hand==null || hand.joints==null || hand.joints.Length!=21)return false;
            UvcHandCalibration active;string warning;
            if(!UvcHandCalibration.ResolveForPreview(calibration,frame.cameraId,frame.imageWidth,frame.imageHeight,out active,out warning).Success ||
                float.IsNaN(cameraDepth) || float.IsInfinity(cameraDepth) || cameraDepth<active.minDepthMetres || cameraDepth>active.maxDepthMetres)return false;
            var points=new Vector3[21];
            for(int i=0;i<21;i++)
            {
                var point=hand.joints[i];HandVector3 ray;
                if(point==null || float.IsNaN(point.z) || float.IsInfinity(point.z) || !active.TryCameraRay(point.x,point.y,out ray))return false;
                float depth=cameraDepth+(point.z-hand.joints[0].z)*frame.imageWidth/(float)Math.Sqrt(active.fx*active.fy)*cameraDepth;
                if(depth<=.05f || depth>active.maxDepthMetres*1.5f)return false;
                var p=active.HeadFromCamera.Transform(new HandVector3(ray.X*depth,ray.Y*depth,ray.Z*depth));
                points[i]=new Vector3(p.X,p.Y,p.Z);
            }
            positions=points;return true;
        }
        public float PreviewCameraDepth(SpatialHandsFrame frame, HandSide side)
        {
            // Root is head-local; recover camera Z through the configured rigid transform.
            var root=(side==HandSide.Left?frame.Left:frame.Right).Root.Position;
            var pose=calibration==null?HandRigidPose.Identity:calibration.HeadFromCamera;
            var rotation=pose.Orientation;
            var q=new Quaternion(rotation.X,rotation.Y,rotation.Z,rotation.W);
            var p=pose.Position;
            return (Quaternion.Inverse(q)*(new Vector3(root.X-p.X,root.Y-p.Y,root.Z-p.Z))).z;
        }
        public SpatialHandCapabilities Capabilities { get { return converter.Capabilities; } }
        public string CalibrationWarning { get; private set; } = "";
        public string LeftDiagnostics { get; private set; } = "not detected";
        public string RightDiagnostics { get; private set; } = "not detected";
        public SpatialUvcHandsConverter(UvcHandCalibration calibration=null,float maximumPalmAxisRatio=1.6f)
        { this.calibration=calibration;converter=new MonocularHandConverter(calibration,maximumPalmAxisRatio); }
        [Obsolete("Calibration status is advisory. Use the single-argument constructor; missing parameters use preview defaults.")]
        public SpatialUvcHandsConverter(UvcHandCalibration calibration,bool allowDevelopmentPreview):this(calibration) { }
        public Result Convert(SpatialHandFrame input,Pose physicalHeadInOrigin,long receivedHostNs,long originRevision,
            out SpatialHandsFrame output,out string diagnostics)
        {
            output=null;diagnostics="";
            LeftDiagnostics=RightDiagnostics="not detected";
            if(input==null || string.IsNullOrWhiteSpace(input.cameraId) || input.sequence<0 || receivedHostNs<0 || originRevision<0)
                return new Result(ErrorCode.InvalidArgument,"Camera identity/calibration/frame metadata mismatch.");
            UvcHandCalibration activeCalibration;string warning;
            var configured=UvcHandCalibration.ResolveForPreview(calibration,input.cameraId,input.imageWidth,input.imageHeight,
                out activeCalibration,out warning);
            CalibrationWarning=warning;
            if(!configured.Success)return configured;
            diagnostics=warning.Length==0?"monocular estimated; ":warning+" ";
            if(warning!=lastLoggedWarning)
            {
                lastLoggedWarning=warning;
                if(warning.Length>0)Debug.LogWarning("SpatialHands: "+warning);
            }
            var q=physicalHeadInOrigin.rotation;var p=physicalHeadInOrigin.position;
            var head=new HandRigidPose(new HandVector3(p.x,p.y,p.z),new Rotation(q.x,q.y,q.z,q.w));
            if(!head.IsValid)return new Result(ErrorCode.InvalidArgument,"Physical head pose is invalid.");
            var left=SpatialHandSample.Untracked(HandSide.Left);var right=SpatialHandSample.Untracked(HandSide.Right);
            var seen=new int[2];
            foreach(var imageHand in input.hands??new SpatialHand[0])
            {
                if(imageHand==null)continue;
                string label=imageHand.EffectiveHandedness;
                if(label!="Left" && label!="Right") { diagnostics+="unknown handedness; ";continue; }
                HandSide side=(label=="Left")!=activeCalibration.swapHandedness?HandSide.Left:HandSide.Right;
                seen[(int)side]++;
                var points=new NormalizedHandPoint[21];
                if(imageHand.joints==null || imageHand.joints.Length!=21){
                    if(side==HandSide.Left)LeftDiagnostics="invalid image skeleton";else RightDiagnostics="invalid image skeleton";
                    diagnostics+="invalid image skeleton; ";continue; }
                for(int i=0;i<21;i++)if(imageHand.joints[i]!=null)
                    points[i]=new NormalizedHandPoint(imageHand.joints[i].x,imageHand.joints[i].y,imageHand.joints[i].z);
                SpatialHandSample hand;float depth;
                var result=converter.Convert(side,points,input.imageWidth,input.imageHeight,head,out hand,out depth);
                var detail=result.Success?"estimated depth="+depth.ToString("F3",System.Globalization.CultureInfo.InvariantCulture):result.Message;
                if(result.Success&&converter.GeometryDiagnostics.Length>0)detail+=" "+converter.GeometryDiagnostics;
                if(side==HandSide.Left)LeftDiagnostics=detail;else RightDiagnostics=detail;
                diagnostics+=side+":"+detail+"; ";
                if(side==HandSide.Left)left=hand;else right=hand;
            }
            if(seen[0]>1){left=SpatialHandSample.Untracked(HandSide.Left);LeftDiagnostics="duplicate Left labels";diagnostics+="duplicate Left labels; ";}
            if(seen[1]>1){right=SpatialHandSample.Untracked(HandSide.Right);RightDiagnostics="duplicate Right labels";diagnostics+="duplicate Right labels; ";}
            // The legacy DTO does not identify its source clock. Preserve unknown
            // instead of fabricating capture-to-host latency from Android nanoTime.
            output=new SpatialHandsFrame(input.cameraId+":"+activeCalibration.calibrationVersion+":"+session,
                HandFrameSource.MonocularEstimate,input.sequence,originRevision,-1,HandClockDomain.Unknown,
                receivedHostNs,HandClockDomain.HostStopwatch,left,right);
            return Result.Ok;
        }
    }
}
