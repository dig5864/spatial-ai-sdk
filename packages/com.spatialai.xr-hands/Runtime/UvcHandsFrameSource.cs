using System;
using SpatialAI.Core;
using SpatialAI.Unity;
using UnityEngine;

namespace SpatialAI.XRHands
{
    // Main-thread immutable spatial snapshots. Repeated XR updates never create inference frames.
    public sealed class UvcHandsFrameSource : ISpatialHandsFrameSource
    {
        private readonly Func<long> now;
        internal SpatialUvcHandProcessor Processor { get; private set; }
        private SpatialHandsFrame latest;
        private string camera;
        private int width,height;
        private long sequence=-1,revision=-1;
        private float ageAtReceiveMs;
        public const long TimeoutNs=350_000_000;
        public string Id { get { return "SpatialAI-UVC"; } }
        public SpatialHandCapabilities Capabilities { get { return Processor.Capabilities; } }
        public string Diagnostics { get; private set; }="waiting for UVC";
        public string IdentityEvents { get; private set; }="";
        public long PublishedFrames { get; private set; }
        public long SkippedCaptureSequences { get; private set; }
        public double ResultAgeMs { get { return latest==null?-1:Math.Max(0,now()-latest.ReceivedTimestampNs)/1e6+ageAtReceiveMs; } }
        public UvcHandsFrameSource(UvcHandCalibration calibration=null,Func<long> clock=null,float maximumPalmAxisRatio=1.6f)
        {
            now=clock??(()=> (long)(System.Diagnostics.Stopwatch.GetTimestamp()*(1_000_000_000.0/System.Diagnostics.Stopwatch.Frequency)));
            Processor=new SpatialUvcHandProcessor(calibration,maximumPalmAxisRatio,now);
        }
        public void Invalidate(string reason)
        {
            latest=null;sequence=-1;Processor.Reset(reason);
            Diagnostics=reason;IdentityEvents="reset: "+reason;
        }
        public bool Publish(SpatialHandFrame image,Pose physicalHeadInOrigin,long originRevision)
        {
            if(image==null || image.cameraId==null || !image.cameraId.StartsWith("uvc:") || image.sequence<0)
            { Invalidate("not a valid UVC frame");return false; }
            if(camera!=image.cameraId || width!=image.imageWidth || height!=image.imageHeight ||
                revision!=originRevision || image.sequence<sequence)
                Invalidate("camera/size/origin/sequence changed");
            if(image.sequence==sequence)return false;
            if(sequence>=0)SkippedCaptureSequences+=Math.Max(0,image.sequence-sequence-1);
            camera=image.cameraId;width=image.imageWidth;height=image.imageHeight;revision=originRevision;sequence=image.sequence;
            bool published=Processor.Publish(image,physicalHeadInOrigin,originRevision);
            IdentityEvents=Processor.IdentityEvents;Diagnostics=Processor.Diagnostics;
            latest=published?Processor.Latest:null;
            ageAtReceiveMs=image.resultAgeAtReceiveMs>=0?image.resultAgeAtReceiveMs:0;
            if(published)PublishedFrames++;
            return published;
        }
        public bool TryGetLatestFrame(out SpatialHandsFrame frame)
        {
            frame=latest;
            if(frame==null)return false;
            long age=now()-frame.ReceivedTimestampNs;
            if(age<0 || age/1e6+ageAtReceiveMs>TimeoutNs/1e6){frame=null;return false;}
            return true;
        }
    }
}
