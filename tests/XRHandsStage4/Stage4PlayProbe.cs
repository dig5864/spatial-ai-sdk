#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using SpatialAI.Core;
using SpatialAI.XRHands;
using SpatialAI.XRInteraction;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

public sealed class Stage4PlayProbe : MonoBehaviour
{
    private int checks,clicks,runtimeErrors;
    private SpatialHandSubsystem subsystem;
    private SimulatedHandsFrameSource source;
    private SpatialHandInteraction bridge;
    private readonly SimulatedHandState left=new SimulatedHandState(),right=new SimulatedHandState();
    private void Check(bool value,string text){checks++;if(!value)throw new Exception("Stage4: "+text);}
    private static Vector3 V(HandVector3 p){return new Vector3(p.X,p.Y,p.Z);}
    private static float ProjectedMarkerSpan(Camera camera,Transform marker,Vector3 position)
    {
        var matrix=Matrix4x4.TRS(position,marker.rotation,marker.lossyScale);
        var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
        foreach(var vertex in marker.GetComponent<MeshFilter>().sharedMesh.vertices)
        {
            var projected=camera.WorldToViewportPoint(matrix.MultiplyPoint3x4(vertex));
            var p=new Vector2(projected.x,projected.y);min=Vector2.Min(min,p);max=Vector2.Max(max,p);
        }
        return (max-min).magnitude;
    }
    private void Aim(SimulatedHandState state,bool isRight,Vector3 target,SimulatedHandPose pose,bool tracked=true)
    {
        state.position=Vector3.zero;state.eulerAngles=Vector3.zero;state.pose=pose;state.tracked=tracked;
        var fixture=new SimulatedHandsFrameSource().Publish(state,state,1);var hand=isRight?fixture.Right:fixture.Left;
        var center=(V(hand.GetJoint(HandJointId.IndexProximal).Position)+V(hand.GetJoint(HandJointId.LittleProximal).Position))*.5f;
        var shoulder=new Vector3(isRight?.16f:-.16f,-.18f,-.04f);
        state.position=Vector3.Lerp(shoulder,target,.35f)-center;
    }
    private void Publish(){source.Publish(left,right,1);subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.Dynamic);}
    private void PlaceIndex(SimulatedHandState state,bool isRight,SimulatedHandPose pose)
    {
        state.position=Vector3.zero;state.eulerAngles=Vector3.zero;state.pose=pose;state.tracked=true;
        var frame=new SimulatedHandsFrameSource().Publish(state,state,1);var hand=isRight?frame.Right:frame.Left;
        var midpoint=(V(hand.GetJoint(HandJointId.ThumbTip).Position)+V(hand.GetJoint(HandJointId.IndexTip).Position))*.5f;
        state.position=new Vector3(0,0,2)-midpoint;
    }
    private void PlacePalm(SimulatedHandState state,bool isRight,Vector3 rawAnchor,SimulatedHandPose pose=SimulatedHandPose.OpenPalm,Vector3? rotation=null)
    {
        state.position=Vector3.zero;state.eulerAngles=rotation??Vector3.zero;state.pose=pose;state.tracked=true;
        var frame=new SimulatedHandsFrameSource().Publish(state,state,1);var hand=isRight?frame.Right:frame.Left;
        var center=(V(hand.GetJoint(HandJointId.IndexProximal).Position)+V(hand.GetJoint(HandJointId.LittleProximal).Position))*.5f;
        state.position=rawAnchor-center;
    }
    private IEnumerator Pose(SimulatedHandPose pose,bool both=false)
    {
        Aim(left,false,new Vector3(0,0,2),pose);Aim(right,true,new Vector3(0,0,2),pose,both);Publish();
        yield return new WaitForSecondsRealtime(.075f);Publish();
        for(int i=0;i<4;i++)yield return null;
    }
    private IEnumerator Run()
    {
        var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=Vector3.zero;
        var manager=new GameObject("Interaction manager").AddComponent<XRInteractionManager>();
        var eventSystem=new GameObject("EventSystem").AddComponent<EventSystem>();var module=eventSystem.gameObject.AddComponent<XRUIInputModule>();
        module.enableMouseInput=module.enableTouchInput=module.enableGamepadInput=module.enableJoystickInput=false;
        bridge=SpatialHandInteractionRig.Create(null,manager);
        source=new SimulatedHandsFrameSource();subsystem=SpatialHandSubsystem.Create(source);subsystem.Start();
        var target=new GameObject("standard target");target.transform.position=new Vector3(0,0,2);target.transform.localScale=Vector3.one*.6f;target.AddComponent<BoxCollider>();
        var selectable=target.AddComponent<XRSimpleInteractable>();selectable.selectMode=InteractableSelectMode.Multiple;selectable.interactionManager=manager;
        var result=target.AddComponent<SpatialInteractionTarget>();
        yield return null;yield return null;Physics.SyncTransforms();
        yield return Pose(SimulatedHandPose.Pinch);Check(result.SelectCount==0,"acquired closed hand does not select");
        yield return Pose(SimulatedHandPose.OpenPalm);Check(bridge.LeftDevice.isTracked.isPressed,"tracked Input System action");
        Check(bridge.leftRay.Ray.rayOriginTransform==bridge.leftRay.transform&&!bridge.leftRay.Visual.overrideInteractorLineOrigin,"visual and collision share the explicit aim pose");
        Check(bridge.leftRay.Visual.enabled&&Vector3.Dot(bridge.leftRay.transform.forward,Vector3.forward)>0,"armed visible ray points forward");
        Check(bridge.leftRay.Controller.isTrackedAction.action.IsPressed(),"action binding reads device");
        yield return Pose(SimulatedHandPose.Pinch);Check(result.SelectCount==1 && selectable.isSelected,"standard XRI selected once");
        for(int i=0;i<12;i++){subsystem.TryUpdateHands(XRHandSubsystem.UpdateType.BeforeRender);yield return null;}
        Check(result.SelectCount==1,"reused render snapshots do not reselect");
        left.tracked=false;Publish();Check(!selectable.isSelected&&result.CancelCount==1,"tracking loss cancels immediately");
        Check(!bridge.leftRay.Visual.enabled&&!bridge.leftRay.GetComponent<LineRenderer>().enabled,"tracking loss immediately hides stale line");
        yield return Pose(SimulatedHandPose.Pinch);Check(result.SelectCount==1,"restored closed hand cannot reselect");
        yield return Pose(SimulatedHandPose.OpenPalm);yield return Pose(SimulatedHandPose.Pinch);
        Check(result.SelectCount==2,"rearmed selection");
        yield return Pose(SimulatedHandPose.OpenPalm);Check(!selectable.isSelected&&result.CancelCount==1,"release is normal exit");
        yield return Pose(SimulatedHandPose.Pinch);left.missingJoint=HandJointId.ThumbTip;Publish();
        Check(!selectable.isSelected,"missing required joint cancels");left.missingJoint=HandJointId.Count;
        yield return Pose(SimulatedHandPose.Pinch);Check(!selectable.isSelected,"missing-joint recovery requires open");
        yield return Pose(SimulatedHandPose.OpenPalm);yield return Pose(SimulatedHandPose.Pinch);
        bridge.leftRay.enabled=false;Check(!selectable.isSelected,"binding disable cancels immediately");
        bridge.leftRay.enabled=true;yield return Pose(SimulatedHandPose.Pinch);Check(!selectable.isSelected,"binding mode switch requires open");
        yield return Pose(SimulatedHandPose.OpenPalm);yield return Pose(SimulatedHandPose.Pinch);
        source.Recenter();Publish();Check(!selectable.isSelected,"origin revision cancels selection");
        yield return Pose(SimulatedHandPose.Pinch);Check(!selectable.isSelected,"recenter with closed hand cannot reselect");
        yield return Pose(SimulatedHandPose.OpenPalm,true);yield return Pose(SimulatedHandPose.Pinch,true);
        Check(selectable.interactorsSelecting.Count==2,"independent simultaneous hands select");
        bridge.enabled=false;Check(!selectable.isSelected&&InputSystem.devices.OfType<SpatialHandInputDevice>().Count()==0,"disable cancels and removes devices");
        bridge.enabled=true;yield return null;yield return null;
        yield return Pose(SimulatedHandPose.Pinch,true);Check(!selectable.isSelected,"reenable requires opening");
        target.SetActive(false);
        var canvasObject=new GameObject("World canvas",typeof(RectTransform));var canvas=canvasObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
        canvasObject.transform.position=new Vector3(0,0,2);canvasObject.transform.localScale=Vector3.one*.005f;
        canvasObject.GetComponent<RectTransform>().sizeDelta=new Vector2(200,100);canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        var buttonObject=new GameObject("UI button",typeof(RectTransform));buttonObject.transform.SetParent(canvasObject.transform,false);buttonObject.GetComponent<RectTransform>().sizeDelta=new Vector2(200,100);
        var image=buttonObject.AddComponent<Image>();var button=buttonObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>clicks++);
        yield return Pose(SimulatedHandPose.OpenPalm);yield return Pose(SimulatedHandPose.Pinch);
        Check(clicks==0,"UI click occurs on release");yield return Pose(SimulatedHandPose.OpenPalm);Check(clicks==1,"real XR UI module clicks once");
        yield return Pose(SimulatedHandPose.Pinch);left.tracked=false;Publish();for(int i=0;i<5;i++)yield return null;
        Check(clicks==1,"loss cancels UI press without click");
        yield return Pose(SimulatedHandPose.Pinch);Check(clicks==1,"restored held pinch does not click");
        yield return Pose(SimulatedHandPose.OpenPalm);yield return Pose(SimulatedHandPose.Pinch);
        source.Recenter();Publish();for(int i=0;i<5;i++)yield return null;
        Check(clicks==1&&!bridge.leftRay.Ray.enabled,"recenter cancels UI press without synthetic release click");
        yield return Pose(SimulatedHandPose.OpenPalm,true);yield return Pose(SimulatedHandPose.Pinch,true);yield return Pose(SimulatedHandPose.OpenPalm,true);
        Check(clicks==3,"both independent UI pointers click once");
        bridge.reflectDisplayX=true;bridge.presentationDistanceScale=3;
        PlacePalm(left,false,new Vector3(.16f,-.18f,.7f));PlacePalm(right,true,new Vector3(-.16f,-.18f,.7f));
        Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(Vector3.Distance(bridge.leftRay.transform.localPosition,new Vector3(-.48f,-.54f,2.1f))<.001f,"reflected left palm keeps the displayed position");
        Check(Vector3.Distance(bridge.rightRay.transform.localPosition,new Vector3(.48f,-.54f,2.1f))<.001f,"reflected right palm keeps the displayed position");
        Check(Vector3.Dot(bridge.leftRay.transform.forward,Vector3.forward)>.999f,"reflected left shoulder stays left without outward ray bias");
        Check(Vector3.Dot(bridge.rightRay.transform.forward,Vector3.forward)>.999f,"reflected right shoulder stays right without outward ray bias");
        Check(bridge.leftRay.GetComponent<LineRenderer>().useWorldSpace&&bridge.rightRay.GetComponent<LineRenderer>().useWorldSpace,"XRI world endpoints are rendered in world space");
        foreach(var binding in new[]{bridge.leftRay,bridge.rightRay})
        {
            var points=new Unity.Collections.NativeArray<Vector3>();
            try
            {
                Check(binding.Ray.GetLinePoints(ref points,out var count)&&count>=2,"standard ray provides visible endpoints");
                var renderer=binding.GetComponent<LineRenderer>();renderer.positionCount=count;renderer.SetPositions(points);renderer.enabled=true;
                binding.SendMessage("LateUpdate");
                var tip=binding.transform.Find("Ray destination arrow");var moving=binding.transform.Find("Ray forward motion arrow");
                var direction=(points[count-1]-points[0]).normalized;
                Check(tip.gameObject.activeSelf&&Vector3.Dot(tip.forward,direction)>.999f,"destination arrow points along the actual visible ray");
                Check(moving.gameObject.activeSelf&&Vector3.Dot(moving.forward,direction)>.999f,"motion arrow points from hand toward destination");
                binding.Cancel();Check(!tip.gameObject.activeSelf&&!moving.gameObject.activeSelf,"cancel hides direction markers immediately");
            }
            finally{if(points.IsCreated)points.Dispose();}
        }
        canvasObject.SetActive(false);bridge.aimMode=SpatialHandAimMode.HeadToPalm;
        target.SetActive(true);target.transform.position=new Vector3(-.65f,.12f,4.2f);Physics.SyncTransforms();
        var palmAnchor=new Vector3(.65f/4.2f*.7f,.12f/4.2f*.7f,.7f);
        PlacePalm(left,false,palmAnchor);right.tracked=false;Publish();yield return new WaitForSecondsRealtime(.075f);Publish();
        for(int i=0;i<4;i++)yield return null;
        Check(Vector3.Dot(bridge.leftRay.transform.forward,bridge.leftRay.transform.position.normalized)>.999f,"head-to-palm ray aligns with displayed palm position");
        Check(selectable.isHovered,"palm positioned over target provides ordinary XRI hover");
        int previousSelections=result.SelectCount;
        PlacePalm(left,false,palmAnchor,SimulatedHandPose.Pinch);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(selectable.isSelected&&result.SelectCount==previousSelections+1,"head-to-palm mirrored aiming selects the far target");
        PlacePalm(left,false,palmAnchor);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(!selectable.isSelected&&selectable.isHovered,"head-to-palm release retains aim hover without selecting again");
        bridge.rayOrigin=SpatialHandRayOrigin.IndexTip;
        PlacePalm(left,false,palmAnchor);PlacePalm(right,true,new Vector3(-palmAnchor.x,palmAnchor.y,palmAnchor.z));Publish();
        yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        var palmDirection=new Vector3(-palmAnchor.x,palmAnchor.y,palmAnchor.z).normalized;
        foreach(var pair in new[]{(hand:subsystem.leftHand,binding:bridge.leftRay),(hand:subsystem.rightHand,binding:bridge.rightRay)})
        {
            Check(pair.hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out var tipPose),"index ray has a current fingertip pose");
            var tip=tipPose.position;tip.x=-tip.x;
            Check(Vector3.Distance(pair.binding.transform.position,tip*3)<.001f,"index ray starts at the reflected scaled fingertip for each hand");
        }
        Check(Vector3.Dot(bridge.leftRay.transform.forward,palmDirection)>.999f,"index origin retains palm-based forward aim");
        target.transform.position=bridge.leftRay.transform.position+bridge.leftRay.transform.forward*2;Physics.SyncTransforms();
        for(int i=0;i<4;i++)yield return null;
        Check(selectable.isHovered,"fingertip ray hover uses the actual XRI collision origin");
        PlacePalm(left,false,palmAnchor,SimulatedHandPose.Pinch);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(Vector3.Dot(bridge.leftRay.transform.forward,palmDirection)>.999f,"pinching does not redirect aim along the bending index finger");
        Check(selectable.isSelected,"fingertip ray can select while pinching");
        left.missingJoint=HandJointId.IndexTip;Publish();
        Check(!selectable.isSelected&&!bridge.leftRay.Visual.enabled,"missing fingertip cancels selection and hides the fingertip ray");
        left.missingJoint=HandJointId.Count;bridge.aimMode=SpatialHandAimMode.IndexExtension;
        PlacePalm(left,false,palmAnchor);PlacePalm(right,true,new Vector3(-palmAnchor.x,palmAnchor.y,palmAnchor.z));Publish();
        yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        foreach(var pair in new[]{(hand:subsystem.leftHand,binding:bridge.leftRay),(hand:subsystem.rightHand,binding:bridge.rightRay)})
        {
            pair.hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out var wristPose);pair.hand.GetJoint(XRHandJointID.IndexProximal).TryGetPose(out var knucklePose);
            var wrist=wristPose.position;var knuckle=knucklePose.position;wrist.x=-wrist.x;knuckle.x=-knuckle.x;
            var axis=camera.WorldToViewportPoint(knuckle*3)-camera.WorldToViewportPoint(wrist*3);
            var points=new Unity.Collections.NativeArray<Vector3>();
            try
            {
                Check(pair.binding.Ray.GetLinePoints(ref points,out var count)&&count>=2,"index extension supplies actual collision endpoints");
                var projected=camera.WorldToViewportPoint(points[count-1])-camera.WorldToViewportPoint(points[0]);
                Check(Vector2.Dot(new Vector2(axis.x,axis.y),new Vector2(projected.x,projected.y))>0,"both reflected rays extend outwards past the finger in the rendered view");
                Check(Vector3.Dot(points[count-1]-points[0],camera.transform.forward)>0,"outwards screen motion also increases forward depth");
                var renderer=pair.binding.GetComponent<LineRenderer>();renderer.positionCount=count;renderer.SetPositions(points);renderer.enabled=true;
                pair.binding.SendMessage("LateUpdate");var moving=pair.binding.transform.Find("Ray forward motion arrow");
                var forwardScreen=camera.WorldToViewportPoint(moving.position+moving.forward*.2f)-camera.WorldToViewportPoint(moving.position);
                Check(Vector2.Dot(new Vector2(axis.x,axis.y),new Vector2(forwardScreen.x,forwardScreen.y))>0,"moving white arrow travels outward in screen projection");
                var nearSpan=ProjectedMarkerSpan(camera,moving,Vector3.Lerp(points[0],points[count-1],.1f));
                var farSpan=ProjectedMarkerSpan(camera,moving,Vector3.Lerp(points[0],points[count-1],.9f));
                Check(farSpan<nearSpan*.8f,"actual moving marker mesh shrinks in perspective as it travels away");
                var initialSize=moving.lossyScale;
                renderer.SetPosition(count-1,Vector3.Lerp(points[0],points[count-1],.5f));pair.binding.SendMessage("LateUpdate");
                Check(Vector3.Distance(initialSize,moving.lossyScale)<.00001f,"moving marker does not grow to compensate for changing distance");
                renderer.SetPositions(points);pair.binding.SendMessage("LateUpdate");
                var terminal=pair.binding.transform.Find("Ray destination arrow");
                Check(moving.GetComponent<MeshRenderer>().sharedMaterial.color==Color.white&&terminal.GetComponent<MeshRenderer>().sharedMaterial.color!=Color.white,
                    "stationary destination has ray color so it cannot masquerade as an enlarged moving white marker");
            }
            finally{if(points.IsCreated)points.Dispose();}
        }
        var openDirection=bridge.leftRay.transform.forward;right.tracked=false;
        target.transform.position=bridge.leftRay.transform.position+openDirection*2;Physics.SyncTransforms();for(int i=0;i<4;i++)yield return null;
        Check(selectable.isHovered,"index extension aims ordinary XRI hover at a forward object");
        PlacePalm(left,false,palmAnchor,SimulatedHandPose.Pinch,new Vector3(0,0,30));Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(bridge.IsAimHeld(false)&&Vector3.Dot(openDirection,bridge.leftRay.transform.forward)>.9999f,"closing and rotating the hand keeps the last open aim direction");
        Check(selectable.isSelected,"held finger aim still selects a forward target");
        left.missingJoint=HandJointId.Wrist;Publish();
        Check(!selectable.isSelected&&!bridge.IsAimHeld(false)&&!bridge.leftRay.Visual.enabled,"missing wrist cancels selection and clears the held aim");
        left.missingJoint=HandJointId.Count;Publish();for(int i=0;i<4;i++)yield return null;
        Check(!selectable.isSelected&&!bridge.leftRay.Ray.enabled,"restoring a closed hand cannot reuse a held finger aim");
        PlacePalm(left,false,palmAnchor,SimulatedHandPose.OpenPalm,new Vector3(0,0,30));Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(!bridge.IsAimHeld(false)&&Vector3.Dot(openDirection,bridge.leftRay.transform.forward)<.999f,"opening after recovery recomputes the changed finger direction");
        target.SetActive(false);canvasObject.SetActive(true);canvasObject.transform.position=bridge.leftRay.transform.position+bridge.leftRay.transform.forward*2;
        for(int i=0;i<4;i++)yield return null;
        PlacePalm(left,false,palmAnchor,SimulatedHandPose.Pinch,new Vector3(0,0,30));Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(clicks==3,"index extension UI press waits for release");
        PlacePalm(left,false,palmAnchor,SimulatedHandPose.OpenPalm,new Vector3(0,0,30));Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(clicks==4,"index extension UI release clicks once with a held aim");
        bridge.rayOrigin=SpatialHandRayOrigin.Palm;
        bridge.aimMode=SpatialHandAimMode.HeadToPalm;bridge.holdAimDuringPinch=true;
        bridge.aimPitchOffsetDegrees=-15;bridge.maximumAimElevationDegrees=0;
        canvasObject.SetActive(false);target.SetActive(true);
        PlacePalm(left,false,new Vector3(.1f,.2f,.7f));Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(Vector3.Distance(bridge.leftRay.transform.position,new Vector3(-.3f,.6f,2.1f))<.001f,"lowered ray starts at the reflected displayed palm rather than fingertip");
        Check(Vector3.Dot(bridge.leftRay.transform.forward,Vector3.up)<=.00001f&&Vector3.Dot(bridge.leftRay.transform.forward,Vector3.forward)>0,"upward hand pose is capped at head-relative horizontal while still aiming forward");
        PlacePalm(left,false,new Vector3(.1f,-.25f,.7f));Publish();for(int i=0;i<4;i++)yield return null;
        var loweredDirection=bridge.leftRay.transform.forward;
        Check(Vector3.Dot(loweredDirection,Vector3.up)<-.05f,"pitch adjustment lowers a below-horizontal palm aim");
        var projectedStart=camera.WorldToViewportPoint(bridge.leftRay.transform.position);
        var projectedEnd=camera.WorldToViewportPoint(bridge.leftRay.transform.position+loweredDirection*2);
        Check(projectedEnd.y<projectedStart.y,"palm ray extends lower in screen projection instead of rising toward the horizon");
        target.transform.position=bridge.leftRay.transform.position+loweredDirection*2;Physics.SyncTransforms();for(int i=0;i<4;i++)yield return null;
        PlacePalm(left,false,new Vector3(.1f,-.25f,.7f),SimulatedHandPose.Pinch);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(selectable.isSelected&&bridge.IsAimHeld(false)&&Vector3.Dot(loweredDirection,bridge.leftRay.transform.forward)>.9999f,"lowered palm aim retains stable direction and selects during a pinch");
        PlacePalm(left,false,new Vector3(.1f,-.25f,.7f));Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(!selectable.isSelected,"lowered palm ray releases normally");
        bridge.aimMode=SpatialHandAimMode.HeadForward;bridge.aimPitchOffsetDegrees=3;bridge.maximumAimElevationDegrees=5;
        PlacePalm(left,false,new Vector3(.1f,.2f,.7f));PlacePalm(right,true,new Vector3(-.1f,-.25f,.7f));
        Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        var forwardAim=Quaternion.AngleAxis(-3,Vector3.right)*Vector3.forward;
        Check(Vector3.Dot(bridge.leftRay.transform.forward,forwardAim)>.99999f&&Vector3.Dot(bridge.rightRay.transform.forward,forwardAim)>.99999f,
            "both palm rays stay parallel and rise three degrees despite different palm heights");
        Check(Vector3.Distance(bridge.leftRay.transform.position,new Vector3(-.3f,.6f,2.1f))<.001f,"parallel forward ray retains mirrored palm origin");
        right.tracked=false;target.transform.position=bridge.leftRay.transform.position+forwardAim*2;Physics.SyncTransforms();for(int i=0;i<4;i++)yield return null;
        PlacePalm(left,false,new Vector3(.1f,.2f,.7f),SimulatedHandPose.Pinch);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(selectable.isSelected&&bridge.IsAimHeld(false),"parallel palm ray selects a scene target with pinch aim hold");
        PlacePalm(left,false,new Vector3(.1f,.2f,.7f));Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(!selectable.isSelected,"parallel palm ray releases a selected scene target");
        bridge.uiCursorOnly=true;bridge.leftRay.cursorOnly=bridge.rightRay.cursorOnly=true;
        target.SetActive(false);canvasObject.SetActive(true);canvasObject.transform.position=new Vector3(0,0,4.2f);
        right.tracked=false;PlaceIndex(left,false,SimulatedHandPose.OpenPalm);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        var cursor=bridge.leftRay.transform.Find("Index fingertip cursor");
        Check(cursor.gameObject.activeSelf&&!bridge.leftRay.Visual.enabled&&!bridge.leftRay.GetComponent<LineRenderer>().enabled,"UI cursor is visible without a rendered ray");
        Check(!bridge.leftRay.transform.Find("Ray destination arrow").gameObject.activeSelf&&!bridge.leftRay.transform.Find("Ray forward motion arrow").gameObject.activeSelf,"UI cursor removes terminal and moving ray markers");
        Check(Vector3.Distance(cursor.position,new Vector3(0,0,2.16f))<.001f&&cursor.GetComponent<MeshFilter>().sharedMesh.normals[0].z<0,"circle uses the reference demo's pointer plane and faces the viewer");
        var cursorVertices=cursor.GetComponent<MeshFilter>().sharedMesh.vertices;
        Check(Mathf.Abs(cursorVertices.Max(v=>v.magnitude)-.045f)<.00001f&&Mathf.Abs(cursorVertices.Min(v=>v.magnitude)-.034f)<.00001f,"circle keeps its original annulus size and thickness");
        Check(bridge.LeftDevice.devicePosition.ReadValue().sqrMagnitude<.000001f,"standard UI pointer starts at view origin even when estimated fingertip is beyond the panel");
        int beforeCursorClicks=clicks;
        PlaceIndex(left,false,SimulatedHandPose.Pinch);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(clicks==beforeCursorClicks&&!bridge.IsAimHeld(false)&&Vector3.Distance(cursor.position,new Vector3(0,0,2.16f))<.001f,
            "matching thumb/index midpoint retains circle position without aim lock and UI waits for release");
        PlaceIndex(left,false,SimulatedHandPose.OpenPalm);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(clicks==beforeCursorClicks+1,"fingertip cursor activates ordinary Button.onClick exactly once");
        PlaceIndex(left,false,SimulatedHandPose.Pinch);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        left.tracked=false;Publish();for(int i=0;i<4;i++)yield return null;
        Check(!cursor.gameObject.activeSelf&&clicks==beforeCursorClicks+1,"loss hides circle and cancels UI press without a click");
        PlaceIndex(left,false,SimulatedHandPose.Pinch);Publish();for(int i=0;i<4;i++)yield return null;
        Check(!bridge.leftRay.Ray.enabled&&clicks==beforeCursorClicks+1,"closed-hand recovery cannot click again");
        PlaceIndex(left,false,SimulatedHandPose.OpenPalm);PlaceIndex(right,true,SimulatedHandPose.OpenPalm);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        PlaceIndex(left,false,SimulatedHandPose.Pinch);PlaceIndex(right,true,SimulatedHandPose.Pinch);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        PlaceIndex(left,false,SimulatedHandPose.OpenPalm);PlaceIndex(right,true,SimulatedHandPose.OpenPalm);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(clicks==beforeCursorClicks+3,"two fingertip circles drive independent standard UI clicks");
        left.position+=new Vector3(.15f,.1f,0);Publish();for(int i=0;i<4;i++)yield return null;
        Check(Vector3.Distance(cursor.position,new Vector3(-.15f,.1f,2)*1.08f)<.001f&&Quaternion.Angle(cursor.rotation,Quaternion.identity)<.01f,
            "circle projects the mirrored thumb/index midpoint after standard controller parent rotation");
        left.position+=Vector3.forward;Publish();for(int i=0;i<4;i++)yield return null;
        Check(Vector3.Distance(cursor.position,new Vector3(-.15f,.1f,3)*.72f)<.001f,"changing estimated hand depth preserves the reference pointer plane and perspective");
        canvasObject.SetActive(false);target.SetActive(true);target.transform.position=new Vector3(0,0,4.2f);Physics.SyncTransforms();
        PlaceIndex(left,false,SimulatedHandPose.OpenPalm);right.tracked=false;Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(selectable.isHovered&&!bridge.leftRay.Visual.enabled,"circle-only presentation still uses standard XRI cube hover without a visible line");
        PlaceIndex(left,false,SimulatedHandPose.Pinch);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(selectable.isSelected&&Vector3.Distance(cursor.position,new Vector3(0,0,2.16f))<.001f,"circle selects a cube at the thumb/index midpoint");
        left.position+=new Vector3(.06f,.04f,0);Publish();for(int i=0;i<4;i++)yield return null;
        Check(Vector3.Distance(cursor.position,new Vector3(-.0648f,.0432f,2.16f))<.001f&&!bridge.IsAimHeld(false),"whole-hand motion remains live during a held pinch");
        PlaceIndex(left,false,SimulatedHandPose.OpenPalm);Publish();yield return new WaitForSecondsRealtime(.075f);Publish();for(int i=0;i<4;i++)yield return null;
        Check(!selectable.isSelected,"circle releases the standard cube on opening");
        left.missingJoint=HandJointId.IndexTip;Publish();Check(!cursor.gameObject.activeSelf,"missing fingertip immediately hides the UI marker");left.missingJoint=HandJointId.Count;
        bridge.uiCursorOnly=false;bridge.leftRay.cursorOnly=bridge.rightRay.cursorOnly=false;
        bridge.aimPitchOffsetDegrees=0;bridge.maximumAimElevationDegrees=90;
        bridge.holdAimDuringPinch=false;
        bridge.aimMode=SpatialHandAimMode.ShoulderToPalm;bridge.reflectDisplayX=false;bridge.presentationDistanceScale=1;
        for(int i=0;i<20;i++){bridge.enabled=false;bridge.enabled=true;yield return null;Check(InputSystem.devices.OfType<SpatialHandInputDevice>().Count()==2,"no device leak on restart");}
        bridge.SendMessage("OnApplicationPause",true);Check(!bridge.leftRay.Ray.enabled&&!bridge.rightRay.Ray.enabled,"pause cancels rays");
        yield return null;Check(!bridge.leftRay.Ray.enabled,"paused bridge cannot reacquire");bridge.SendMessage("OnApplicationPause",false);
        yield return null;yield return Pose(SimulatedHandPose.Pinch,true);Check(!bridge.LeftDevice.pinch.isPressed,"resume held pinch blocked");
        bridge.enabled=false;subsystem.Stop();subsystem.Destroy();
        Check(InputSystem.devices.OfType<SpatialHandInputDevice>().Count()==0,"final device cleanup");
        Destroy(bridge.gameObject);Destroy(canvasObject);Destroy(target);Destroy(manager.gameObject);yield return null;yield return null;
        var sample=new GameObject("Sample prefab smoke").AddComponent<SpatialHandInteractionSample>();sample.useSimulation=true;sample.xrCamera=camera;
        sample.interactionRigPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ImportedStage4/SpatialHandInteractionRig.prefab");
        typeof(SpatialHandInteractionSample).GetField("standardGesturesPrefab",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .SetValue(sample,AssetDatabase.LoadAssetAtPath<GameObject>(SpatialAI.XRInteraction.Editor.SpatialHandInteractionScene.GesturesPrefabPath));
        for(int i=0;i<10;i++)yield return null;
        Check(sample.Bridge!=null&&sample.Simulator.Subsystem.running,"sample prefab starts standard simulation");
        Check(sample.Bridge.leftRay.Controller.selectAction.action.actionMap!=null,"sample uses action asset bindings");
        var worldTargets=sample.WorldTargetsRoot;
        var worldLeft=worldTargets.Find("XRI World UI/Left UI button");var worldRight=worldTargets.Find("XRI World UI/Right UI button");
        Check(worldTargets.GetComponentsInChildren<XRSimpleInteractable>().Length==2&&sample.Bridge.uiCursorOnly,"sample restores both standard selectable cubes with circle presentation");
        Check(camera.GetComponentsInChildren<SpatialXRHandsVisualizer>().Length==1,"sample restores standard XR Hands skeleton visualization");
        var panel=camera.transform.Find("Spatial XRI sample display/XR Hands debug UI/Debug background");
        var panelRect=panel.GetComponent<RectTransform>();
        var panelPosition=camera.transform.InverseTransformPoint(panel.position);
        Check(panelRect.anchoredPosition.y==0&&panelPosition.y<0&&panel.GetComponent<Image>().color.a>.8f,"debug panel sits below the centre with a readable dark background");
        Check(panelPosition.z<4.2f-.225f&&!panel.GetComponent<Image>().raycastTarget,"debug panel renders closer than the cubes without intercepting UI input");
        Check(panel.GetComponentInChildren<Text>().fontSize>=30,"lowered debug text keeps its readable font size");
        Check(panel.GetComponentInChildren<Text>().text.Contains("AI ")&&panel.GetComponentInChildren<Text>().text.Contains("UVC ")&&panel.GetComponentInChildren<Text>().text.Contains("Render "),"debug HUD includes inference, capture and render rates");
        var leftPosition=worldLeft.position;var rightPosition=worldRight.position;var leftRotation=worldLeft.rotation;
        var viewportBefore=camera.WorldToViewportPoint(leftPosition);
        camera.transform.SetPositionAndRotation(new Vector3(.2f,.1f,0),Quaternion.Euler(0,20,0));yield return null;yield return null;
        Check(Vector3.Distance(leftPosition,worldLeft.position)>.05f&&Vector3.Distance(rightPosition,worldRight.position)>.05f&&Quaternion.Angle(leftRotation,worldLeft.rotation)>10,
            "both UI buttons move and turn with the XR camera");
        Check(Vector3.Distance(viewportBefore,camera.WorldToViewportPoint(worldLeft.position))<.00001f&&worldTargets.IsChildOf(camera.transform),"camera-relative targets retain their screen positions when turning");
        camera.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        // Gesture prefab is under the camera-local display root, not the sample object's hierarchy.
        var trackingEvents=camera.GetComponentsInChildren<XRHandTrackingEvents>();
        Check(trackingEvents.Length==2,"sample exposes standard XRHandTrackingEvents for both hands");
        Type gestureType=null;foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies())gestureType=gestureType??assembly.GetType("UnityEngine.XR.Hands.Samples.GestureSample.StaticHandGesture");
        var gestureComponents=camera.GetComponentsInChildren(gestureType);
        Check(gestureComponents.Length==6,"sample uses six unchanged official StaticHandGesture components");
        foreach(var component in gestureComponents)foreach(var graphic in component.GetComponentsInChildren<Graphic>(true))Check(!graphic.enabled,"gesture detectors do not render button-like graphics");
        int openPerformed=0,fistPerformed=0,pinchPerformed=0,ended=0,lost=0;
        foreach(var component in gestureComponents)
        {
            if(!component.name.StartsWith("Left "))continue;
            var performed=(UnityEvent)gestureType.GetProperty("gesturePerformed").GetValue(component);
            if(component.name.EndsWith("OPEN"))performed.AddListener(()=>openPerformed++);
            if(component.name.EndsWith("FIST"))performed.AddListener(()=>fistPerformed++);
            if(component.name.EndsWith("PINCH"))performed.AddListener(()=>pinchPerformed++);
            ((UnityEvent)gestureType.GetProperty("gestureEnded").GetValue(component)).AddListener(()=>ended++);
        }
        foreach(var events in trackingEvents)if(events.handedness==Handedness.Left)events.trackingLost.AddListener(()=>lost++);
        sample.Simulator.right.tracked=false;sample.Simulator.left.pose=SimulatedHandPose.Fist;yield return new WaitForSeconds(.5f);
        Check(fistPerformed==1,"standard XRHandShape and official gesturePerformed detect the fist fixture");
        Check(camera.GetComponentsInChildren<Text>().Any(t=>t.enabled&&t.text.Contains("FIST")&&t.text.Contains("Cube L=")),"official gesture status is displayed in the debug text");
        yield return new WaitForSeconds(.3f);Check(fistPerformed==1,"held official gesture does not repeatedly perform");
        sample.Simulator.left.pose=SimulatedHandPose.OpenPalm;yield return new WaitForSeconds(.5f);
        Check(openPerformed==1&&ended>0,"standard open shape performs and prior gesture ends");
        sample.Simulator.left.pose=SimulatedHandPose.Pinch;yield return new WaitForSeconds(.5f);
        Check(pinchPerformed>=1,"standard finger Pinch condition performs through the official gesture event");
        sample.Simulator.left.tracked=false;yield return new WaitForSeconds(.2f);
        Check(lost==1,"standard XRHandTrackingEvents.trackingLost reports gesture input loss");
        foreach(var component in gestureComponents)if(component.name.StartsWith("Left "))Check(!component.transform.Find("Detected").gameObject.activeSelf,"standard trackingLost clears gesture UI feedback");
        sample.enabled=false;Check(InputSystem.devices.OfType<SpatialHandInputDevice>().Count()==0,"sample disable removes input devices");
        Check(!worldTargets.gameObject.activeSelf,"sample disable hides camera-relative targets");
        sample.enabled=true;yield return null;Check(InputSystem.devices.OfType<SpatialHandInputDevice>().Count()==2,"sample resume recreates input devices");
        Check(worldTargets.gameObject.activeSelf&&Vector3.Distance(worldLeft.position,leftPosition)<.00001f,"resume restores scene targets without moving their positions");
        Destroy(sample.gameObject);yield return null;yield return null;
        Check(InputSystem.devices.OfType<SpatialHandInputDevice>().Count()==0,"sample destroy clears devices");
        Check(worldTargets==null,"sample destroy removes camera-relative targets");
        Check(runtimeErrors==0,"no runtime errors/exceptions during integration and sample lifecycle");
        Debug.Log("SPATIALAI_STAGE4_PASS integrationChecks="+checks+" lifecycleCycles=20 uiClicks="+clicks+" syntheticOnly=true");
        EditorApplication.Exit(0);
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)runtimeErrors++;};
        var execution=Run();
        while(true)
        {
            object next;
            try{if(!execution.MoveNext())yield break;next=execution.Current;}
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);yield break;}
            yield return next;
        }
    }
}
#endif
