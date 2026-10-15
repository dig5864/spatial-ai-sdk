using System;
using System.Collections.Generic;
using SpatialAI.Core;
using SpatialAI.Unity;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;

namespace SpatialAI.XRHands
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // After the camera's main-thread polling.
    public sealed class SpatialUvcHands : MonoBehaviour
    {
        public SpatialHandTracking cameraTracking;
        public Transform trackingOrigin;
        [Tooltip("Physical head transform; never assign the reflected render camera. Empty means explicit head-local preview.")]
        public Transform physicalHead;
        public TextAsset calibrationJson;
        [Tooltip("Draw built-in camera landmarks using uncalibrated defaults. Does not create tracked XR input.")]
        public bool builtinSkeletonPreview;
        [Tooltip("Live estimated geometry only. 1.6 is strict; the far interaction sample uses 4 to tolerate palm compression.")]
        [Range(1.6f,4)] public float maximumPalmAxisRatio=1.6f;
        public long originRevision;
        public SpatialHandSubsystem Subsystem { get; private set; }
        public UvcHandsFrameSource Source { get; private set; }
        public string Status { get; private set; }="stopped";
        public float CaptureFps { get {return cameraTracking!=null&&cameraTracking.isActiveAndEnabled?cameraTracking.CaptureFps:0;} }
        public float InferenceFps { get {return Source!=null&&cameraTracking!=null&&Source.ResultAgeMs>=0&&Source.ResultAgeMs<=350&&cameraTracking.Latest!=null?cameraTracking.Latest.resultFps:0;} }
        public string InferenceDelegate { get {return cameraTracking!=null&&cameraTracking.Latest!=null?cameraTracking.Latest.@delegate:SpatialHandTracking.InferenceBackend.ToString();} }
        internal string DisplayState(HandSide side){return Source==null?"hidden":Source.Processor.DisplayState(side);}
        private bool ownsCamera,paused;
        private int dynamicFrame=-1,renderFrame=-1;
        private float nextLog,windowStart;
        private long windowCount;
        private int windowRenderFrames;
        private string model="unknown";
        private readonly List<XRDisplaySubsystem> displays=new List<XRDisplaySubsystem>();
        private readonly List<float> inference=new List<float>(),conversion=new List<float>(),ages=new List<float>();
        private readonly List<float> captureRates=new List<float>(),resultRates=new List<float>(),renderRates=new List<float>();
        private void OnEnable(){if(Application.isPlaying)StartTracking();}
        private void OnDisable(){StopTracking();}
        private void OnDestroy(){StopTracking();}
        private void OnApplicationPause(bool value)
        { paused=value;if(value)StopTracking();else if(isActiveAndEnabled)StartTracking(); }
        public bool StartTracking()
        {
            if(Subsystem!=null)return true;
            var all=new List<XRHandSubsystem>();SubsystemManager.GetSubsystems(all);
            foreach(var existing in all)if(existing is SpatialHandSubsystem)
            { Status="another Spatial hand subsystem owns input";Debug.LogWarning("SpatialXRHands: "+Status);return false; }
            UvcHandCalibration calibration=null;
            try
            {
                if(calibrationJson!=null)calibration=JsonUtility.FromJson<UvcHandCalibration>(calibrationJson.text);
                else if(SpatialDeviceProfiles.SelectHost(SpatialDeviceProfiles.ReadHostIdentity(),out var host).Success)
                    calibration=new UvcHandCalibration{swapHandedness=host.uvcSwapHandedness};
            }
            catch(Exception e){Status="invalid calibration JSON: "+e.Message;Debug.LogError("SpatialXRHands: "+Status);return false;}
            Source=new UvcHandsFrameSource(calibration,maximumPalmAxisRatio:maximumPalmAxisRatio,allowBuiltinPreview:builtinSkeletonPreview);
            if(cameraTracking==null)foreach(var candidate in FindObjectsOfType<SpatialHandTracking>())
                if(candidate.isActiveAndEnabled){cameraTracking=candidate;break;}
            if(cameraTracking==null){cameraTracking=gameObject.AddComponent<SpatialHandTracking>();ownsCamera=true;}
            if(ownsCamera)cameraTracking.enabled=true;
            cameraTracking.FrameUpdated+=OnFrame;cameraTracking.Invalidated+=Invalidate;
            Subsystem=SpatialHandSubsystem.Create(Source);Subsystem.Start();
            Subsystem.trackingAcquired+=Acquired;Subsystem.trackingLost+=Lost;
            dynamicFrame=renderFrame=-1;invalidInput=false;windowStart=Time.unscaledTime;nextLog=windowStart+5;
            windowCount=0;windowRenderFrames=0;inference.Clear();conversion.Clear();ages.Clear();
            captureRates.Clear();resultRates.Clear();renderRates.Clear();
            Application.onBeforeRender+=BeforeRender;
            Status=physicalHead==null?"UVC head-local estimated preview":"UVC estimated tracking-origin poses";
            Debug.Log("SpatialXRHands start id="+Subsystem.subsystemDescriptor.id+" ownsCamera="+ownsCamera+" maxPalmAxisRatio="+maximumPalmAxisRatio+" "+Status);
            return true;
        }
        public void StopTracking()
        {
            Application.onBeforeRender-=BeforeRender;
            if(cameraTracking!=null){cameraTracking.FrameUpdated-=OnFrame;cameraTracking.Invalidated-=Invalidate;}
            if(Source!=null)Source.Invalidate("driver stopped");
            if(Subsystem!=null){Subsystem.Stop();Subsystem.trackingAcquired-=Acquired;Subsystem.trackingLost-=Lost;Subsystem.Destroy();Subsystem=null;}
            if(ownsCamera && cameraTracking!=null)cameraTracking.enabled=false;
            Source=null;Status="stopped";
        }
        public void Invalidate(string reason)
        {
            if(Source==null)return;
            Source.Invalidate(reason);
            if(Subsystem!=null && Subsystem.running)Subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);
            Status=reason;Debug.Log("SpatialXRHands invalidated: "+reason);
        }
        private Pose PhysicalPose()
        {
            if(physicalHead==null)return Pose.identity;
            return trackingOrigin==null?new Pose(physicalHead.position,physicalHead.rotation):
                new Pose(trackingOrigin.InverseTransformPoint(physicalHead.position),Quaternion.Inverse(trackingOrigin.rotation)*physicalHead.rotation);
        }
        private void OnFrame(SpatialHandFrame image)
        {
            if(paused || Source==null || !SpatialHandTracking.GestureRecognitionEnabled)return;
            if(Source.Publish(image,PhysicalPose(),originRevision))
            {
                windowCount++;Add(inference,image.inferenceMs);Add(conversion,image.conversionMs);
                model="runningMode="+image.runningMode+" delegate="+image.@delegate+" mode="+image.gestureSource;
                Add(resultRates,image.resultFps);
                if(Source.IdentityEvents.Contains("label_change"))Debug.Log("SpatialXRHands identity seq="+image.sequence+" "+Source.IdentityEvents);
                Status=Source.Diagnostics;
            }
        }
        private static void Add(List<float> values,float value){if(value<0 || float.IsNaN(value) || float.IsInfinity(value))return;if(values.Count==240)values.RemoveAt(0);values.Add(value);}
        private void Acquired(XRHand hand){TrackingEvent(hand,"acquired");}
        private void Lost(XRHand hand){TrackingEvent(hand,"lost");}
        private void TrackingEvent(XRHand hand,string state)
        { Debug.Log("SpatialXRHands tracking="+state+" side="+hand.handedness+" seq="+(Subsystem.LastFrame==null?-1:Subsystem.LastFrame.Sequence)+" "+Status.Substring(0,Math.Min(400,Status.Length))); }
        private static string Distribution(List<float> values)
        {
            if(values.Count==0)return "n/a";
            var sorted=values.ToArray();Array.Sort(sorted);float sum=0;foreach(var value in sorted)sum+=value;
            return "mean/p50/p95="+(sum/sorted.Length).ToString("F1")+"/"+sorted[(int)((sorted.Length-1)*.5f)].ToString("F1")+"/"+sorted[(int)((sorted.Length-1)*.95f)].ToString("F1");
        }
        private bool invalidInput;
        private void Update()
        {
            if(Subsystem==null || paused)return;
            string state=cameraTracking==null?"camera missing":cameraTracking.State;
            bool invalid=!SpatialHandTracking.GestureRecognitionEnabled || cameraTracking==null || !cameraTracking.isActiveAndEnabled ||
                state.Contains("disconnected") || state.Contains("closed") || state.Contains("failed") || state.Contains("unavailable:") || state=="stopped";
            if(invalid && !invalidInput)Invalidate("input unavailable: "+state);
            invalidInput=invalid;
            if(dynamicFrame!=Time.frameCount)
            {
                dynamicFrame=Time.frameCount;windowRenderFrames++;Subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);Add(ages,(float)Source.ResultAgeMs);
                Add(captureRates,cameraTracking==null?0:cameraTracking.CaptureFps);
                if(Time.unscaledDeltaTime>0)Add(renderRates,1f/Time.unscaledDeltaTime);
            }
            if(Time.unscaledTime<nextLog)return;
            float seconds=Mathf.Max(.001f,Time.unscaledTime-windowStart);
            Debug.Log("SpatialXRHands stats captureFps="+(cameraTracking==null?0:cameraTracking.CaptureFps).ToString("F1")+
                " publishedFps="+(windowCount/seconds).ToString("F1")+" renderFps="+(windowRenderFrames/seconds).ToString("F1")+
                " left="+Subsystem.leftHand.isTracked+" right="+Subsystem.rightHand.isTracked+
                " ageMs="+Distribution(ages)+" inferMs="+Distribution(inference)+" convertMs="+Distribution(conversion)+
                " skippedCaptureSequences="+Source.SkippedCaptureSequences+" seq="+(Subsystem.LastFrame==null?-1:Subsystem.LastFrame.Sequence)+
                " sourceClock=Unknown receiveClock=HostStopwatch "+model);
            Debug.Log("SpatialXRHands fps capture "+Distribution(captureRates)+" result "+Distribution(resultRates)+" render "+Distribution(renderRates));
            windowStart=Time.unscaledTime;nextLog=windowStart+5;windowCount=0;windowRenderFrames=0;ages.Clear();inference.Clear();conversion.Clear();
            captureRates.Clear();resultRates.Clear();renderRates.Clear();
        }
        private void LateUpdate()
        {
            displays.Clear();SubsystemManager.GetSubsystems(displays);
            foreach(var display in displays)if(display.running)return;
            BeforeRender();
        }
        [BeforeRenderOrder(-100)] private void BeforeRender()
        {
            if(Subsystem==null || paused || renderFrame==Time.frameCount)return;
            renderFrame=Time.frameCount;Subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.BeforeRender);
        }
    }
}
