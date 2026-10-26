using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
#if SPATIALAI_XRI_3_OR_NEWER
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
#endif

namespace SpatialAI.XRInteraction
{
    // Explicit bindings avoid the OpenXR controller layouts and XRI starter assets.
    public sealed class SpatialHandRayBinding : MonoBehaviour
    {
        public bool rightHand;
        public XRInteractionManager manager;
        public InputActionAsset inputActions;
        public bool cursorOnly;
        public XRRayInteractor Ray { get; private set; }
        public ActionBasedController Controller { get; private set; }
        public XRInteractorLineVisual Visual { get; private set; }
        private LineRenderer lineRenderer;
        private Transform directionTip,movingArrow;
        private Mesh arrowMesh;
        private Material arrowMaterial;
        private Transform fingerCursor;
        private Mesh cursorMesh;
        private Material cursorMaterial;
        private bool cursorPinched;
        private Vector3 cursorWorldPosition;
        private Quaternion cursorWorldRotation;
        private float nextAimLog;
        private readonly List<InputAction> actions=new List<InputAction>();
        private InputActionProperty Bind(string title,string control,string layout,InputActionType type=InputActionType.Value)
        {
            var action=inputActions!=null?inputActions.FindAction((rightHand?"RightHand":"LeftHand")+"/"+control,true):
                new InputAction(title,type,"<SpatialHandInputDevice>{"+(rightHand?"RightHand":"LeftHand")+"}/"+control,expectedControlType:layout);
            if(!actions.Contains(action))actions.Add(action);return new InputActionProperty(action);
        }
        private void Awake()
        {
            Controller=gameObject.AddComponent<ActionBasedController>();
            Controller.enabled=false;
            Controller.uiScrollAction=default(InputActionProperty);Controller.activateAction=default(InputActionProperty);
            Controller.activateActionValue=default(InputActionProperty);
            Controller.positionAction=Bind("Spatial position","devicePosition","Vector3");
            Controller.rotationAction=Bind("Spatial rotation","deviceRotation","Quaternion");
            Controller.isTrackedAction=Bind("Spatial tracked","isTracked","Button",InputActionType.Button);
            Controller.trackingStateAction=Bind("Spatial tracking state","trackingState","Integer");
            Controller.selectAction=Bind("Spatial select","pinch","Button",InputActionType.Button);
            Controller.uiPressAction=Bind("Spatial UI press","pinch","Button",InputActionType.Button);
            // Select and UI press use the debounced button; strength is a diagnostic analog control.
            Ray=gameObject.AddComponent<XRRayInteractor>();Ray.interactionManager=manager;
            Ray.lineType=XRRayInteractor.LineType.StraightLine;Ray.maxRaycastDistance=12;Ray.enableUIInteraction=true;
#if SPATIALAI_XRI_3_OR_NEWER
            Ray.selectActionTrigger=XRBaseInputInteractor.InputTriggerType.StateChange;
            Ray.inputCompatibilityMode=XRBaseInputInteractor.InputCompatibilityMode.ForceInputReaders;
            // XRI 3 reads interactor inputs directly instead of reading the controller.
            Ray.selectInput=new XRInputButtonReader
            {
                inputSourceMode=XRInputButtonReader.InputSourceMode.InputAction,
                inputActionPerformed=Controller.selectAction.action,
                inputActionValue=Controller.selectAction.action
            };
            Ray.uiPressInput=new XRInputButtonReader
            {
                inputSourceMode=XRInputButtonReader.InputSourceMode.InputAction,
                inputActionPerformed=Controller.uiPressAction.action,
                inputActionValue=Controller.uiPressAction.action
            };
#else
            Ray.selectActionTrigger=XRBaseControllerInteractor.InputTriggerType.StateChange;
#endif
            Ray.allowAnchorControl=false;Ray.enabled=false;
            Ray.rayOriginTransform=transform;
            var line=gameObject.AddComponent<LineRenderer>();line.positionCount=2;line.startWidth=line.endWidth=.007f;
            line.useWorldSpace=true;
            line.material=SpatialAI.Unity.SpatialSampleMaterial.Create(rightHand?new Color(1,.65f,.2f):new Color(.2f,.8f,1));
            lineRenderer=line;Visual=gameObject.AddComponent<XRInteractorLineVisual>();
            Visual.overrideInteractorLineOrigin=false;Visual.enabled=false;line.enabled=false;
            arrowMesh=CreateArrowMesh();arrowMaterial=SpatialAI.Unity.SpatialSampleMaterial.Create(Color.white);
            directionTip=CreateArrow("Ray destination arrow");movingArrow=CreateArrow("Ray forward motion arrow");
            // The stationary destination must not look like an enlarged copy of the moving white marker.
            directionTip.GetComponent<MeshRenderer>().sharedMaterial=line.sharedMaterial;
            var cursor=new GameObject("Index fingertip cursor");cursor.transform.SetParent(transform,false);fingerCursor=cursor.transform;
            cursorMesh=new Mesh{name="XR UI fingertip ring"};
            const int segments=48;var vertices=new Vector3[segments*2];var triangles=new int[segments*6];
            for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;var radial=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                vertices[i*2]=radial*.045f;vertices[i*2+1]=radial*.034f;
                int next=(i+1)%segments,offset=i*6;
                triangles[offset]=i*2;triangles[offset+1]=i*2+1;triangles[offset+2]=next*2;
                triangles[offset+3]=i*2+1;triangles[offset+4]=next*2+1;triangles[offset+5]=next*2;
            }
            cursorMesh.vertices=vertices;cursorMesh.triangles=triangles;cursorMesh.RecalculateNormals();
            cursorMaterial=SpatialAI.Unity.SpatialSampleMaterial.Create(Color.white);cursor.AddComponent<MeshFilter>().sharedMesh=cursorMesh;
            cursor.AddComponent<MeshRenderer>().sharedMaterial=cursorMaterial;cursor.SetActive(false);
        }
        private void OnEnable(){foreach(var action in actions)action.Enable();if(Controller!=null)Controller.enabled=true;}
        private void OnDisable()
        {
            Cancel();if(Controller!=null)Controller.enabled=false;foreach(var action in actions)action.Disable();
            GetComponentInParent<SpatialHandInteraction>()?.ResetHand(rightHand,"ray binding disabled");
        }
        public void Cancel()
        {
            if(fingerCursor!=null)fingerCursor.gameObject.SetActive(false);
            if(Ray==null)return;
            var interactionManager=Ray.interactionManager;
            if(interactionManager!=null){interactionManager.CancelInteractorSelection((IXRSelectInteractor)Ray);interactionManager.CancelInteractorHover((IXRHoverInteractor)Ray);}
            SetRayEnabled(false);
        }
        public void SetRayEnabled(bool value)
        {
            if(Ray!=null)Ray.enabled=value;
            if(Visual!=null)Visual.enabled=value&&!cursorOnly;
            if((!value||cursorOnly) && lineRenderer!=null)lineRenderer.enabled=false;
            if(!value||cursorOnly){if(directionTip!=null)directionTip.gameObject.SetActive(false);if(movingArrow!=null)movingArrow.gameObject.SetActive(false);}
        }
        internal void UpdateCursor(Vector3 position,Quaternion rotation,bool pinched)
        {cursorWorldPosition=position;cursorWorldRotation=rotation;fingerCursor.SetPositionAndRotation(position,rotation);fingerCursor.gameObject.SetActive(true);cursorPinched=pinched;}
        private static Mesh CreateArrowMesh()
        {
            const int segments=12;var vertices=new Vector3[segments+2];var triangles=new int[segments*6];
            vertices[0]=Vector3.forward;vertices[1]=Vector3.zero;
            for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;vertices[i+2]=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0);
                int next=(i+1)%segments+2,offset=i*6;
                triangles[offset]=0;triangles[offset+1]=i+2;triangles[offset+2]=next;
                triangles[offset+3]=1;triangles[offset+4]=next;triangles[offset+5]=i+2;
            }
            var mesh=new Mesh{name="Spatial ray forward arrow"};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();return mesh;
        }
        private Transform CreateArrow(string title)
        {
            var arrow=new GameObject(title);arrow.transform.SetParent(transform,false);
            arrow.AddComponent<MeshFilter>().sharedMesh=arrowMesh;arrow.AddComponent<MeshRenderer>().sharedMaterial=arrowMaterial;
            arrow.SetActive(false);return arrow.transform;
        }
        private void UpdateDirectionMarkers()
        {
            bool visible=Ray!=null&&Ray.enabled&&lineRenderer!=null&&lineRenderer.enabled&&lineRenderer.positionCount>=2;
            if(!visible){directionTip.gameObject.SetActive(false);movingArrow.gameObject.SetActive(false);return;}
            var start=lineRenderer.GetPosition(0);var end=lineRenderer.GetPosition(lineRenderer.positionCount-1);
            if(!lineRenderer.useWorldSpace){start=transform.TransformPoint(start);end=transform.TransformPoint(end);}
            var delta=end-start;float length=delta.magnitude;
            if(length<.1f){directionTip.gameObject.SetActive(false);movingArrow.gameObject.SetActive(false);return;}
            var rotation=Quaternion.LookRotation(delta/length,transform.up);
            const float size=.045f;
            directionTip.gameObject.SetActive(true);directionTip.SetPositionAndRotation(end-delta/length*size*2,rotation);
            directionTip.localScale=new Vector3(size,size,size*2);
            float progress=Mathf.Repeat(Time.unscaledTime*.6f,1);
            var movingPosition=Vector3.Lerp(start,end,progress);
            // Fixed dimensions preserve perspective: moving away from the viewer becomes smaller on screen.
            const float movingSize=.045f;
            movingArrow.gameObject.SetActive(true);movingArrow.SetPositionAndRotation(movingPosition,rotation);
            movingArrow.localScale=new Vector3(movingSize,movingSize,movingSize*2.5f);
        }
        private void LateUpdate()
        {
            if(cursorOnly)
            {
                // The standard controller may move the parent after the provider callback.
                fingerCursor.SetPositionAndRotation(cursorWorldPosition,cursorWorldRotation);
                Visual.enabled=false;lineRenderer.enabled=false;directionTip.gameObject.SetActive(false);movingArrow.gameObject.SetActive(false);
                cursorMaterial.color=cursorPinched?Color.green:Ray.enabled&&(Ray.hasHover||Ray.TryGetCurrentUIRaycastResult(out _))?Color.white:rightHand?new Color(1,.65f,.2f):new Color(.2f,.8f,1);
                return;
            }
            UpdateDirectionMarkers();
            if(Ray==null || !Ray.enabled || Time.unscaledTime<nextAimLog)return;
            nextAimLog=Time.unscaledTime+2;
            var bridge=GetComponentInParent<SpatialHandInteraction>();
            var reference=bridge==null?null:bridge.trackingOrigin;
            var origin=reference==null?transform.position:reference.InverseTransformPoint(transform.position);
            var direction=reference==null?transform.forward:reference.InverseTransformDirection(transform.forward);
            var camera=Camera.main;
            float cameraForward= camera==null?float.NaN:Vector3.Dot(transform.forward,camera.transform.forward);
            string rendered="hidden";
            if(lineRenderer!=null&&lineRenderer.enabled&&lineRenderer.positionCount>=2)
            {
                var start=lineRenderer.GetPosition(0);var end=lineRenderer.GetPosition(lineRenderer.positionCount-1);
                if(!lineRenderer.useWorldSpace){start=transform.TransformPoint(start);end=transform.TransformPoint(end);}
                float forward=camera==null?float.NaN:Vector3.Dot((end-start).normalized,camera.transform.forward);
                if(reference!=null){start=reference.InverseTransformPoint(start);end=reference.InverseTransformPoint(end);}
                rendered="start="+start.ToString("F3")+" end="+end.ToString("F3")+" forwardDot="+forward.ToString("F3");
            }
            Debug.Log("SpatialXRI aim side="+(rightHand?"Right":"Left")+" origin="+origin.ToString("F3")+
                " aimMode="+(bridge==null?"unknown":bridge.aimMode.ToString())+" aimHeld="+(bridge!=null&&bridge.IsAimHeld(rightHand))+
                " direction="+direction.ToString("F3")+" cameraForwardDot="+cameraForward.ToString("F3")+
                " visualMatchesRay="+(Visual!=null&&!Visual.overrideInteractorLineOrigin)+" selected="+Ray.hasSelection+
                " renderedLine="+rendered);
        }
        private void OnDestroy()
        {
            if(inputActions==null)foreach(var action in actions)action.Dispose();
            var line=GetComponent<LineRenderer>();if(line!=null && line.sharedMaterial!=null)Destroy(line.sharedMaterial);
            if(arrowMesh!=null)Destroy(arrowMesh);if(arrowMaterial!=null)Destroy(arrowMaterial);
            if(cursorMesh!=null)Destroy(cursorMesh);if(cursorMaterial!=null)Destroy(cursorMaterial);
        }
    }
}
