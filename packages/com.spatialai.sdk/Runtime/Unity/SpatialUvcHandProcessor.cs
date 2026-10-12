using System;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.Unity
{
    /// <summary>Shared UVC association, conversion and presentation compensation.
    /// Presentation-only positions never become tracked input or gesture events.</summary>
    public sealed class SpatialUvcHandProcessor
    {
        private sealed class Presentation
        {
            public Vector3[] Points;
            public bool[] Valid;
            public Pose Head;
            public string State="hidden";
            public int Track;
            public float Depth;
            public long GeometryNs=-1,DisplayNs=-1;
        }
        private readonly Func<long> clock;
        private readonly UvcHandCalibration calibration;
        private readonly float maximumPalmAxisRatio;
        private readonly SpatialUvcHandStabilizer stabilizer=new SpatialUvcHandStabilizer();
        private readonly Presentation[] presentation={new Presentation(),new Presentation()};
        private SpatialUvcHandsConverter converter;
        private string camera;
        private int width,height;
        private long sequence=-1,revision=-1,lastReceivedNs=-1;
        private float receivedAgeMs;
        public SpatialHandsFrame Latest { get; private set; }
        public SpatialHandFrame Associated { get; private set; }
        public string Diagnostics { get; private set; }="waiting for UVC";
        public string IdentityEvents { get; private set; }="";
        public string CalibrationWarning { get {return converter.CalibrationWarning;} }
        public SpatialHandCapabilities Capabilities { get {return converter.Capabilities;} }
        public string Reason(HandSide side){return side==HandSide.Left?converter.LeftDiagnostics:converter.RightDiagnostics;}
        public float CameraDepth(HandSide side){return presentation[(int)side].Depth;}
        public static long HostNowNs(){return (long)(System.Diagnostics.Stopwatch.GetTimestamp()*(1_000_000_000.0/System.Diagnostics.Stopwatch.Frequency));}
        public SpatialUvcHandProcessor(UvcHandCalibration calibration=null,float maximumPalmAxisRatio=1.6f,Func<long> clock=null)
        {
            this.calibration=calibration;this.maximumPalmAxisRatio=maximumPalmAxisRatio;this.clock=clock??HostNowNs;
            converter=new SpatialUvcHandsConverter(calibration,maximumPalmAxisRatio);
        }
        public void Reset(string reason="reset")
        {
            Latest=null;Associated=null;sequence=-1;lastReceivedNs=-1;stabilizer.Reset();
            converter=new SpatialUvcHandsConverter(calibration,maximumPalmAxisRatio);
            foreach(var p in presentation){p.Points=null;p.Valid=null;p.Track=0;p.Depth=0;p.GeometryNs=p.DisplayNs=-1;p.State="hidden";}
            Diagnostics=reason;IdentityEvents="reset: "+reason;
        }
        public bool Publish(SpatialHandFrame image,Pose head,long originRevision)
        {
            if(image==null||image.cameraId==null||!image.cameraId.StartsWith("uvc:")||image.sequence<0)
            {Reset("not a valid UVC frame");return false;}
            long received=image.receivedHostStopwatchNs>0?image.receivedHostStopwatchNs:clock();
            if(camera!=image.cameraId||width!=image.imageWidth||height!=image.imageHeight||revision!=originRevision||image.sequence<sequence||received<lastReceivedNs)
                Reset("camera/size/origin/sequence/clock changed");
            if(image.sequence==sequence)return false;
            camera=image.cameraId;width=image.imageWidth;height=image.imageHeight;revision=originRevision;sequence=image.sequence;lastReceivedNs=received;
            receivedAgeMs=Mathf.Max(0,image.resultAgeAtReceiveMs);
            Associated=stabilizer.Process(image,received,out var events);IdentityEvents=events;
            var result=converter.Convert(Associated,head,received,originRevision,out var frame,out var detail);
            Diagnostics=result.Success?detail:result.Message;Latest=result.Success?frame:null;
            if(!result.Success||receivedAgeMs>350){ClearPresentation();return result.Success;}
            for(int side=0;side<2;side++)
            {
                string previous=presentation[side].State;UpdatePresentation((HandSide)side,frame,head,received);
                if(previous!=presentation[side].State)Debug.Log("SpatialHandsComp side="+(HandSide)side+" state="+presentation[side].State+
                    " seq="+sequence+" track="+presentation[side].Track+" depth="+presentation[side].Depth.ToString("F3")+" reason="+Reason((HandSide)side));
            }
            return true;
        }
        private void ClearPresentation()
        {foreach(var p in presentation){p.Points=null;p.Valid=null;p.Track=0;p.GeometryNs=p.DisplayNs=-1;p.State="hidden";}}
        private SpatialHand FindHand(HandSide side)
        {
            var expected=(side==HandSide.Left)!=(calibration!=null&&calibration.swapHandedness)?"Left":"Right";
            SpatialHand found=null;
            foreach(var hand in Associated.hands??new SpatialHand[0])if(hand!=null&&hand.EffectiveHandedness==expected)
            {if(found!=null)return null;found=hand;}
            return found;
        }
        private void UpdatePresentation(HandSide side,SpatialHandsFrame frame,Pose head,long received)
        {
            var p=presentation[(int)side];var sample=side==HandSide.Left?frame.Left:frame.Right;var hand=FindHand(side);
            bool same=hand!=null&&hand.trackingId>0&&p.Track==hand.trackingId;
            if(sample.IsTracked&&hand!=null)
            {
                var points=new Vector3[26];var valid=new bool[26];
                for(int i=0;i<26;i++)
                {
                    var joint=sample.GetJoint((HandJointId)i);valid[i]=(joint.Valid&HandDataFields.Position)!=0;
                    var world=new Vector3(joint.Position.X,joint.Position.Y,joint.Position.Z);
                    points[i]=Quaternion.Inverse(head.rotation)*(world-head.position);
                }
                p.Points=points;p.Valid=valid;p.Head=head;p.Track=hand.trackingId;p.GeometryNs=p.DisplayNs=received;
                // PreviewCameraDepth expects head-local roots.
                var localRoot=Quaternion.Inverse(head.rotation)*(new Vector3(sample.Root.Position.X,sample.Root.Position.Y,sample.Root.Position.Z)-head.position);
                var extrinsic=calibration==null?HandRigidPose.Identity:calibration.HeadFromCamera;
                var q=extrinsic.Orientation;var t=extrinsic.Position;
                p.Depth=(Quaternion.Inverse(new Quaternion(q.X,q.Y,q.Z,q.W))*(localRoot-new Vector3(t.X,t.Y,t.Z))).z;
                p.State="fresh";return;
            }
            if(same&&p.GeometryNs>=0&&Reason(side).StartsWith("Palm axes disagree")&&
                converter.TryPreviewPositions(Associated,hand,p.Depth,out var projected))
            {
                var points=new Vector3[26];var valid=new bool[26];
                for(int i=0;i<26;i++){int landmark=MonocularHandConverter.LandmarkIndex((HandJointId)i);if(landmark>=0){points[i]=projected[landmark];valid[i]=true;}}
                // XR metacarpals and palm are reconstructed only for drawing; no orientations are invented.
                foreach(int i in new[]{6,11,16,21})if(valid[0]&&valid[i+1]){points[i]=(points[0]+points[i+1])*.5f;valid[i]=true;}
                if(valid[7]&&valid[12]&&valid[17]&&valid[22]){points[1]=(points[7]+points[12]+points[17]+points[22])*.25f;valid[1]=true;}
                p.Points=points;p.Valid=valid;p.Head=head;p.DisplayNs=received;p.State="depth-held";return;
            }
            bool missing=hand==null&&Reason(side)=="not detected"&&stabilizer.IsAlive(p.Track,received)&&Within(received,p.DisplayNs,120_000_000);
            bool held=same&&Within(received,p.GeometryNs,150_000_000);
            p.State=missing?"missing-held":held?"held":"hidden";
            if(p.State=="hidden"){p.Points=null;p.Valid=null;if(!same){p.Track=0;p.GeometryNs=p.DisplayNs=-1;}}
        }
        private static bool Within(long now,long previous,long limit){return previous>=0&&now>=previous&&now-previous<=limit;}
        public string DisplayState(HandSide side)
        {
            var p=presentation[(int)side];long now=clock();
            if(!Within(now,lastReceivedNs,350_000_000)||(now-lastReceivedNs)/1e6+receivedAgeMs>350||
                p.State=="missing-held"&&!Within(now,p.DisplayNs,120_000_000)||p.State=="held"&&!Within(now,p.GeometryNs,150_000_000))return "hidden";
            return p.State;
        }
        public bool TryPresentation(HandSide side,out Vector3[] positions,out bool[] valid)
        {
            var p=presentation[(int)side];positions=p.Points;valid=p.Valid;
            return DisplayState(side)!="hidden"&&positions!=null;
        }
        public bool TryPresentationJoint(HandSide side,HandJointId joint,out Vector3 position)
        {
            var p=presentation[(int)side];position=Vector3.zero;
            if(!TryPresentation(side,out var points,out var valid)||!valid[(int)joint])return false;
            position=p.Head.position+p.Head.rotation*points[(int)joint];return true;
        }
    }
}
