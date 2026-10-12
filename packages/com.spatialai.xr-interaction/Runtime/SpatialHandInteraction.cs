using System.Collections.Generic;
using SpatialAI.XRHands;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;

namespace SpatialAI.XRInteraction
{
    public enum SpatialHandAimMode { ShoulderToPalm, HeadToPalm, IndexExtension, HeadForward }
    public enum SpatialHandRayOrigin { Palm, IndexTip }
    [DisallowMultipleComponent]
    public sealed class SpatialHandInteraction : MonoBehaviour
    {
        public Transform trackingOrigin,physicalHead;
        public XRInteractionManager manager;
        public SpatialHandRayBinding leftRay,rightRay;
        [Tooltip("Sample presentation only; raw XR Hands joints remain unchanged.")]
        public bool reflectDisplayX;
        public SpatialHandAimMode aimMode=SpatialHandAimMode.ShoulderToPalm;
        [Tooltip("Ray start only; the aim mode controls direction separately.")]
        public SpatialHandRayOrigin rayOrigin=SpatialHandRayOrigin.Palm;
        [Tooltip("Estimated screen-plane extension of wrist to index knuckle. Depth still points forward.")]
        [Range(.5f,5)] public float indexAimExtension=2.5f;
        [Tooltip("Head-relative pitch adjustment in degrees; negative lowers the ray.")]
        [Range(-60,60)] public float aimPitchOffsetDegrees;
        [Tooltip("Maximum head-relative elevation. Set to zero to prevent upward rays.")]
        [Range(-60,90)] public float maximumAimElevationDegrees=90;
        [Tooltip("Keep the last open-hand direction through pinch press and release debounce.")]
        public bool holdAimDuringPinch;
        [Tooltip("3DOF presentation: a thumb/index midpoint marker instead of a visible ray.")]
        public bool uiCursorOnly;
        [Range(1,10)] public float presentationDistanceScale=1;
        public float pressRatio=.32f,releaseRatio=.48f,debounceMs=50;
        public string LeftStatus { get; private set; }="untracked";
        public string RightStatus { get; private set; }="untracked";
        public SpatialHandInputDevice LeftDevice { get; private set; }
        public SpatialHandInputDevice RightDevice { get; private set; }
        private readonly SpatialPinchGate[] gates={new SpatialPinchGate(),new SpatialPinchGate()};
        private readonly bool[] tracked=new bool[2];
        private readonly Vector3[] openAim=new Vector3[2];
        private readonly bool[] hasOpenAim=new bool[2],aimHeld=new bool[2];
        public bool IsAimHeld(bool right){return aimHeld[right?1:0];}
        private XRHandSubsystem subsystem;
        private long sequence=-1,revision=-1;
        private string source;
        private bool paused;
        private static SpatialHandInteraction owner;
        private void OnEnable()
        {
            if(owner!=null && owner!=this){Debug.LogWarning("SpatialXRI: another bridge owns hand input");enabled=false;return;}
            owner=this;SpatialHandInputDevice.Register();
            LeftDevice=InputSystem.AddDevice<SpatialHandInputDevice>();InputSystem.SetDeviceUsage(LeftDevice,"LeftHand");
            RightDevice=InputSystem.AddDevice<SpatialHandInputDevice>();InputSystem.SetDeviceUsage(RightDevice,"RightHand");
            sequence=-1;source=null;
        }
        private void Update()
        {
            if(owner!=this || paused)return;
            if(subsystem!=null && subsystem.running)return;
            Detach();var candidates=new List<XRHandSubsystem>();SubsystemManager.GetSubsystems(candidates);
            foreach(var candidate in candidates)if(candidate.running && candidate is SpatialHandSubsystem)
            {subsystem=candidate;subsystem.updatedHands+=OnHands;break;}
        }
        private void OnHands(XRHandSubsystem sender,XRHandSubsystem.UpdateSuccessFlags flags,XRHandSubsystem.UpdateType type)
        {
            if(paused || type!=XRHandSubsystem.UpdateType.Dynamic)return;
            var frame=((SpatialHandSubsystem)sender).LastFrame;
            if(frame==null){Lose(0,"frame invalid");Lose(1,"frame invalid");sequence=-1;return;}
            bool changed=source!=frame.SourceId || revision!=frame.OriginRevision || frame.Sequence<sequence;
            if(changed){Lose(0,"source/origin changed");Lose(1,"source/origin changed");sequence=-1;}
            source=frame.SourceId;revision=frame.OriginRevision;
            // Even reused snapshots can become untracked due to timeout.
            if(!sender.leftHand.isTracked)Lose(0,"tracking lost");
            if(!sender.rightHand.isTracked)Lose(1,"tracking lost");
            if(frame.Sequence==sequence)return;sequence=frame.Sequence;
            Publish(sender.leftHand,0);Publish(sender.rightHand,1);
        }
        private bool Point(XRHand hand,XRHandJointID id,out Vector3 point)
        {bool valid=hand.GetJoint(id).TryGetPose(out var pose);point=pose.position;return valid&&Finite(point);}
        private static bool Finite(Vector3 p)
        {return !float.IsNaN(p.x)&&!float.IsInfinity(p.x)&&!float.IsNaN(p.y)&&!float.IsInfinity(p.y)&&!float.IsNaN(p.z)&&!float.IsInfinity(p.z);}
        public void ResetHand(bool right,string reason){Lose(right?1:0,reason);}
        private void Publish(XRHand hand,int side)
        {
            var binding=side==0?leftRay:rightRay;
            if(binding!=null&&!binding.isActiveAndEnabled){Lose(side,"ray input disabled");return;}
            if(!hand.isTracked || !Point(hand,XRHandJointID.ThumbTip,out var thumb) || !Point(hand,XRHandJointID.IndexTip,out var index) ||
                !Point(hand,XRHandJointID.IndexProximal,out var a) || !Point(hand,XRHandJointID.LittleProximal,out var b))
            {Lose(side,"required joint missing");return;}
            float width=Vector3.Distance(a,b);
            // Pinching can compress the MCP span. Wrist-to-middle is an independent live scale reference.
            bool hasWrist=Point(hand,XRHandJointID.Wrist,out var wrist);
            if(aimMode==SpatialHandAimMode.IndexExtension&&!hasWrist){Lose(side,"index aim wrist missing");return;}
            if(hasWrist&&Point(hand,XRHandJointID.MiddleProximal,out var middle))
                width=Mathf.Max(width,Vector3.Distance(wrist,middle)*.875f);
            float ratio=width>.005f?Vector3.Distance(thumb,index)/width:float.NaN;
            if(float.IsNaN(ratio)||float.IsInfinity(ratio)){Lose(side,"palm width invalid");return;}
            // MCP palm anchor avoids moving the ray just because thumb/index tips close during a pinch.
            var headPosition=physicalHead==null?Vector3.zero:trackingOrigin==null?physicalHead.position:trackingOrigin.InverseTransformPoint(physicalHead.position);
            var headRotation=physicalHead==null?Quaternion.identity:trackingOrigin==null?physicalHead.rotation:Quaternion.Inverse(trackingOrigin.rotation)*physicalHead.rotation;
            var palm=(a+b)*.5f;
            var origin=rayOrigin==SpatialHandRayOrigin.IndexTip?index:palm;
            // Match the original hand demo's pointer between thumb and index fingertips.
            if(uiCursorOnly)origin=(thumb+index)*.5f;
            var indexAxis=a-wrist;
            // Determine the anatomical shoulder in the same presentation frame as the hand.
            // Reflecting an already chosen shoulder would put the left shoulder on the right.
            if(reflectDisplayX)
            {
                origin.x=-origin.x;palm.x=-palm.x;headPosition.x=-headPosition.x;
                indexAxis.x=-indexAxis.x;
                headRotation=new Quaternion(headRotation.x,-headRotation.y,-headRotation.z,headRotation.w);
            }
            var shoulder=headPosition+headRotation*new Vector3(side==0?-.16f:.16f,-.18f,-.04f);
            var direction=palm-(aimMode==SpatialHandAimMode.HeadToPalm?headPosition:shoulder);
            if(aimMode==SpatialHandAimMode.IndexExtension)
            {
                // Extrapolate outwards in the view plane, rather than back towards the palm's vanishing point.
                // Monocular joint depth is estimated; do not treat a curled fingertip's 3D tangent as a reliable ray.
                direction=origin-headPosition+Vector3.ProjectOnPlane(indexAxis,headRotation*Vector3.forward)*Mathf.Clamp(indexAimExtension,.5f,5);
            }
            else if(aimMode==SpatialHandAimMode.HeadForward)direction=headRotation*Vector3.forward;
            if(uiCursorOnly)direction=origin-headPosition;
            if(!Finite(direction)||direction.sqrMagnitude<.0025f || Vector3.Dot(direction,headRotation*Vector3.forward)<=0)
            {Lose(side,"ray direction invalid");return;}
            if(!uiCursorOnly&&(aimPitchOffsetDegrees!=0||maximumAimElevationDegrees<90))
            {
                var local=Quaternion.Inverse(headRotation)*direction.normalized;
                float horizontal=Mathf.Sqrt(local.x*local.x+local.z*local.z);
                float elevation=Mathf.Atan2(local.y,horizontal)*Mathf.Rad2Deg;
                float adjusted=Mathf.Clamp(Mathf.Min(elevation+aimPitchOffsetDegrees,maximumAimElevationDegrees),-80,80)*Mathf.Deg2Rad;
                direction=headRotation*new Vector3(local.x/horizontal*Mathf.Cos(adjusted),Mathf.Sin(adjusted),local.z/horizontal*Mathf.Cos(adjusted));
            }
            var gate=gates[side];gate.pressRatio=pressRatio;gate.releaseRatio=releaseRatio;gate.debounceSeconds=debounceMs/1000.0;
            aimHeld[side]=false;
            if(!uiCursorOnly&&(aimMode==SpatialHandAimMode.IndexExtension||holdAimDuringPinch))
            {
                // Save the last open-hand aim before tips start closing, including the press/release debounce.
                if(gate.Armed&&(ratio<releaseRatio||gate.Pressed)&&hasOpenAim[side])
                {direction=openAim[side];aimHeld[side]=true;}
                else if(ratio>=releaseRatio){openAim[side]=direction.normalized;hasOpenAim[side]=true;}
            }
            bool previous=gate.Pressed;bool pinch=gate.Advance(ratio,Time.realtimeSinceStartupAsDouble);
            var rotation=Quaternion.LookRotation(direction.normalized,headRotation*Vector3.up);
            float scale=Mathf.Clamp(presentationDistanceScale,1,10);
            origin*=scale;
            if(uiCursorOnly)
            {
                if(binding!=null)
                {
                    binding.cursorOnly=true;
                    // The reference demo projects its pointer onto z = (0.8 - 0.08),
                    // scaled with the hand presentation. Use the same screen position
                    // for the circle and the standard XRI hit-test direction.
                    float forwardDepth=Vector3.Dot(direction,headRotation*Vector3.forward);
                    var cursorPosition=headPosition+direction*(.72f*scale/forwardDepth);
                    binding.UpdateCursor(trackingOrigin==null?cursorPosition:trackingOrigin.TransformPoint(cursorPosition),
                        trackingOrigin==null?headRotation:trackingOrigin.rotation*headRotation,pinch);
                }
                // Standard XR UI hit testing begins at the view origin, independent of estimated hand depth.
                origin=headPosition;
            }
            var device=side==0?LeftDevice:RightDevice;
            InputSystem.QueueStateEvent(device,new SpatialHandInputState{buttons=(uint)(1|(pinch?2:0)),trackingState=3,
                devicePosition=origin,deviceRotation=rotation,pinchStrength=Mathf.Clamp01(1-ratio/releaseRatio)});
            // Keep UI unregistered until rearmed; queued neutral input must replace any old controller press first.
            tracked[side]=true;var ray=side==0?leftRay:rightRay;if(ray!=null)ray.SetRayEnabled(gate.Armed);
            string status=(gate.Armed?(pinch?"pinch":"ready"):"open to arm")+" ratio="+ratio.ToString("F2");
            if(side==0)LeftStatus=status;else RightStatus=status;
            if(previous!=pinch)Debug.Log("SpatialXRI pinch="+pinch+" side="+hand.handedness+" seq="+sequence+" ratio="+ratio.ToString("F2"));
        }
        private void Lose(int side,string reason)
        {
            gates[side].Reset();hasOpenAim[side]=aimHeld[side]=false;var ray=side==0?leftRay:rightRay;if(ray!=null)ray.Cancel();
            var device=side==0?LeftDevice:RightDevice;
            if(device!=null && device.added)InputSystem.QueueStateEvent(device,new SpatialHandInputState{deviceRotation=Quaternion.identity});
            if(tracked[side])Debug.Log("SpatialXRI cancel side="+(side==0?"Left":"Right")+" reason="+reason);
            tracked[side]=false;if(side==0)LeftStatus="untracked: "+reason;else RightStatus="untracked: "+reason;
        }
        private void Detach()
        {
            if(subsystem!=null)subsystem.updatedHands-=OnHands;subsystem=null;
            Lose(0,"input detached");Lose(1,"input detached");sequence=-1;source=null;
        }
        private void OnDisable()
        {
            if(owner!=this)return;
            Detach();if(LeftDevice!=null && LeftDevice.added)InputSystem.RemoveDevice(LeftDevice);
            if(RightDevice!=null && RightDevice.added)InputSystem.RemoveDevice(RightDevice);
            LeftDevice=RightDevice=null;owner=null;
        }
        private void OnApplicationPause(bool value){paused=value;if(value)Detach();}
    }
}
