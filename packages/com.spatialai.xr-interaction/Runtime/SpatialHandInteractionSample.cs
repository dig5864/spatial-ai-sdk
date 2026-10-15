using SpatialAI.Unity;
using SpatialAI.XRHands;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SpatialAI.XRInteraction
{
    public sealed class SpatialHandInteractionSample : MonoBehaviour
    {
        public Camera xrCamera;
        public bool useSimulation;
        public TextAsset calibrationJson;
        public GameObject interactionRigPrefab;
        [SerializeField] private GameObject standardGesturesPrefab;
        public SpatialHandInteraction Bridge { get; private set; }
        public SpatialHandsSimulator Simulator { get; private set; }
        public Transform WorldTargetsRoot { get; private set; }
        public int LeftClicks { get; private set; }
        public int RightClicks { get; private set; }
        private GameObject displayRoot,inputRoot,eventRoot;
        private Text hud;
        private SpatialUvcHands handsInput;
        private float renderFps,renderWindowStart;
        private int renderFrames;
        private Transform gestureFeedback;
        private Mesh cubeMesh;
        private readonly Material[] cubeMaterials=new Material[2];
        private readonly SpatialInteractionTarget[] cubeTargets=new SpatialInteractionTarget[2];
        private void Start()
        {
            if(xrCamera==null)xrCamera=Camera.main;
            if(xrCamera==null){Debug.LogError("SpatialXRI sample needs an XR camera");enabled=false;return;}
            // 3DOF fingertip cursors and standard UI; gesture events come from the official XR Hands sample.
            displayRoot=new GameObject("Spatial XRI sample display");displayRoot.transform.SetParent(xrCamera.transform,false);
            WorldTargetsRoot=new GameObject("Spatial XRI world targets").transform;
            WorldTargetsRoot.SetParent(displayRoot.transform,false);
            var manager=displayRoot.AddComponent<XRInteractionManager>();
            if(EventSystem.current==null)
            {
                eventRoot=new GameObject("Spatial XRI EventSystem");eventRoot.transform.SetParent(transform,false);eventRoot.AddComponent<EventSystem>();
                var module=eventRoot.AddComponent<XRUIInputModule>();module.enableMouseInput=false;module.enableTouchInput=false;
                module.enableGamepadInput=false;module.enableJoystickInput=false;
            }
            else if(EventSystem.current.GetComponent<XRUIInputModule>()==null)Debug.LogError("SpatialXRI: existing EventSystem needs XRUIInputModule; use one active UI input module.");
            if(interactionRigPrefab==null)Bridge=SpatialHandInteractionRig.Create(displayRoot.transform,manager);
            else
            {
                var rig=Instantiate(interactionRigPrefab,displayRoot.transform);Bridge=rig.GetComponent<SpatialHandInteraction>();
                Bridge.manager=manager;Bridge.trackingOrigin=displayRoot.transform;
                Bridge.leftRay.Ray.interactionManager=manager;Bridge.rightRay.Ray.interactionManager=manager;
            }
            bool reflection=false;
            if(!useSimulation && SpatialDeviceProfiles.SelectHost(SpatialDeviceProfiles.ReadHostIdentity(),out var host).Success)reflection=host.reflectHandDisplayX;
            Bridge.reflectDisplayX=reflection;Bridge.presentationDistanceScale=3;
            Bridge.uiCursorOnly=true;Bridge.rayOrigin=SpatialHandRayOrigin.IndexTip;Bridge.holdAimDuringPinch=false;
            Bridge.aimPitchOffsetDegrees=0;Bridge.maximumAimElevationDegrees=90;
            Bridge.leftRay.cursorOnly=Bridge.rightRay.cursorOnly=true;
            var handVisualObject=new GameObject("XR Hands skeleton");handVisualObject.SetActive(false);handVisualObject.transform.SetParent(displayRoot.transform,false);
            var handVisual=handVisualObject.AddComponent<SpatialXRHandsVisualizer>();handVisual.trackingOrigin=displayRoot.transform;
            handVisual.reflectDisplayX=reflection;handVisual.presentationDistanceScale=3;handVisualObject.SetActive(true);
            if(standardGesturesPrefab!=null)
            {
                gestureFeedback=Instantiate(standardGesturesPrefab,displayRoot.transform).transform;
                // Keep the official gesture events and feedback flags; present their state in the HUD.
                foreach(var graphic in gestureFeedback.GetComponentsInChildren<Graphic>(true))graphic.enabled=false;
            }
            inputRoot=new GameObject("Spatial XRI sample input");inputRoot.SetActive(false);inputRoot.transform.SetParent(transform,false);
            if(useSimulation)
            {
                Simulator=inputRoot.AddComponent<SpatialHandsSimulator>();
                inputRoot.AddComponent<SpatialHandsSimulationControls>().simulator=Simulator;
            }
            else
            {
                var input=inputRoot.AddComponent<SpatialUvcHands>();input.calibrationJson=calibrationJson;
                input.builtinSkeletonPreview=true;
                input.maximumPalmAxisRatio=4;
                handsInput=input;
            }
            inputRoot.SetActive(true);
            cubeMesh=CreateCubeMesh();
            for(int side=0;side<2;side++)
            {
                var target=new GameObject(side==0?"Left cube":"Right cube");target.transform.SetParent(WorldTargetsRoot,false);
                target.transform.localPosition=new Vector3(side==0?-.65f:.65f,.1f,4.2f);target.transform.localScale=Vector3.one*.45f;
                target.AddComponent<MeshFilter>().sharedMesh=cubeMesh;
                cubeMaterials[side]=new Material(Shader.Find("Unlit/Color")){color=side==0?new Color(.2f,.8f,1):new Color(1,.65f,.2f)};
                target.AddComponent<MeshRenderer>().sharedMaterial=cubeMaterials[side];target.AddComponent<BoxCollider>();
                var interactable=target.AddComponent<XRSimpleInteractable>();interactable.interactionManager=manager;
                interactable.selectMode=InteractableSelectMode.Multiple;cubeTargets[side]=target.AddComponent<SpatialInteractionTarget>();
            }
            var canvasObject=new GameObject("XRI World UI",typeof(RectTransform));canvasObject.transform.SetParent(WorldTargetsRoot,false);
            canvasObject.transform.localPosition=new Vector3(0,-1.15f,4.2f);canvasObject.transform.localScale=Vector3.one*.003f;
            var canvas=canvasObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=xrCamera;
            canvasObject.GetComponent<RectTransform>().sizeDelta=new Vector2(800,240);canvasObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            for(int side=0;side<2;side++)
            {
                var buttonObject=new GameObject(side==0?"Left UI button":"Right UI button",typeof(RectTransform));buttonObject.transform.SetParent(canvasObject.transform,false);
                var rect=buttonObject.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(320,110);rect.anchoredPosition=new Vector2(side==0?-190:190,0);
                var image=buttonObject.AddComponent<Image>();image.color=new Color(.12f,.18f,.28f);
                var button=buttonObject.AddComponent<Button>();button.targetGraphic=image;
                if(side==0)button.onClick.AddListener(()=>{LeftClicks++;Debug.Log("SpatialXRI UI click target=Left count="+LeftClicks);});
                else button.onClick.AddListener(()=>{RightClicks++;Debug.Log("SpatialXRI UI click target=Right count="+RightClicks);});
                var label=TextChild(buttonObject.transform,side==0?"LEFT UI":"RIGHT UI",36);label.alignment=TextAnchor.MiddleCenter;
                label.rectTransform.sizeDelta=rect.sizeDelta;
            }
            var hudObject=new GameObject("XR Hands debug UI",typeof(RectTransform));hudObject.transform.SetParent(displayRoot.transform,false);
            // Below the cubes, near the centre of the reflected view. The closer
            // depth keeps cube geometry from occluding either text or background.
            hudObject.transform.localPosition=new Vector3(0,-.12f,2.8f);hudObject.transform.localScale=Vector3.one*.002f;
            var hudCanvas=hudObject.AddComponent<Canvas>();hudCanvas.renderMode=RenderMode.WorldSpace;hudCanvas.worldCamera=xrCamera;
            var panel=new GameObject("Debug background",typeof(RectTransform));panel.transform.SetParent(hudObject.transform,false);
            var panelRect=panel.GetComponent<RectTransform>();panelRect.pivot=new Vector2(.5f,1);panelRect.anchoredPosition=Vector2.zero;panelRect.sizeDelta=new Vector2(1000,250);
            var panelImage=panel.AddComponent<Image>();panelImage.color=new Color(.015f,.025f,.045f,.88f);panelImage.raycastTarget=false;
            hud=TextChild(panel.transform,"",30);hud.alignment=TextAnchor.UpperLeft;
            hud.rectTransform.anchorMin=hud.rectTransform.anchorMax=new Vector2(0,1);hud.rectTransform.pivot=new Vector2(0,1);
            hud.rectTransform.anchoredPosition=new Vector2(14,-10);hud.rectTransform.sizeDelta=new Vector2(972,230);
            renderWindowStart=Time.unscaledTime;
            Debug.Log("SpatialXRI sample ready mode="+(useSimulation?"simulation":"uvc")+" interaction=thumbIndexCursor skeleton=true cubes=headLocked visibleRays=false cursorPinchHold=false gestures=UnityXRHands HUD=centralBackground UI=Button.onClick; open hand to arm");
        }
        private static Mesh CreateCubeMesh()
        {
            var mesh=new Mesh{name="XRI sample cube"};
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)};
            mesh.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};mesh.RecalculateNormals();return mesh;
        }
        private static Text TextChild(Transform parent,string text,int size)
        {
            var obj=new GameObject("Label",typeof(RectTransform));obj.transform.SetParent(parent,false);var label=obj.AddComponent<Text>();
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.fontSize=size;label.color=Color.white;label.raycastTarget=false;return label;
        }
        private void Update()
        {
            if(hud==null||Bridge==null)return;
            renderFrames++;float elapsed=Time.unscaledTime-renderWindowStart;
            if(elapsed>=1){renderFps=renderFrames/elapsed;renderFrames=0;renderWindowStart=Time.unscaledTime;}
            bool preview=SpatialHandTracking.CameraSource==SpatialHandCameraSource.Rear || SpatialHandTracking.CameraSource==SpatialHandCameraSource.Front;
            var frame=handsInput!=null && handsInput.cameraTracking!=null?handsInput.cameraTracking.Latest:null;
            string inputSize=frame!=null?frame.inputWidth+"x"+frame.inputHeight:"--";
            string captureSize=frame!=null?frame.imageWidth+"x"+frame.imageHeight:"--";
            hud.text="XR HANDS | "+SpatialHandTracking.CameraSource+" Capture "+captureSize+" Input "+inputSize+(preview?" PREVIEW":"")+" | Max "+SpatialHandTracking.MaxHands+"\nL: "+Bridge.LeftStatus+" | "+GestureText("Left")+"\nR: "+Bridge.RightStatus+" | "+GestureText("Right")+
                "\nUI L="+LeftClicks+" R="+RightClicks+" | Cube L="+(cubeTargets[0]==null?0:cubeTargets[0].SelectCount)+" R="+(cubeTargets[1]==null?0:cubeTargets[1].SelectCount)+
                "\n"+(handsInput==null?"SIM":handsInput.InferenceDelegate)+" | AI "+(handsInput==null?0:handsInput.InferenceFps).ToString("F1")+
                " Capture "+(handsInput==null?0:handsInput.CaptureFps).ToString("F1")+" Render "+renderFps.ToString("F1")+" FPS"+
                "\nConvert "+(frame==null?"--":frame.conversionMs.ToString("F1"))+" ms | Inference "+(frame==null?"--":frame.inferenceMs.ToString("F1"))+" ms";
        }
        private string GestureText(string side)
        {
            var frame=handsInput!=null && handsInput.cameraTracking!=null?handsInput.cameraTracking.Latest:null;
            if(frame!=null && frame.cameraSource!=null && frame.cameraSource.StartsWith("builtin-"))
            {
                long now=(long)(System.Diagnostics.Stopwatch.GetTimestamp()*(1_000_000_000.0/System.Diagnostics.Stopwatch.Frequency));
                if(now-frame.receivedHostStopwatchNs>350_000_000L) return "RAW stale";
                if(frame.hands!=null) foreach(var hand in frame.hands)
                    if(hand!=null && hand.rawHandedness==side) return "RAW "+hand.Gesture;
                return "RAW none";
            }
            if(gestureFeedback==null)return "unavailable";
            string text="";foreach(var name in new[]{"OPEN","FIST","PINCH"})
            {var flag=gestureFeedback.Find(side+" "+name+"/Detected");if(flag!=null&&flag.gameObject.activeInHierarchy)text+=(text.Length==0?"":"/")+name;}
            return text.Length==0?"NONE":text;
        }
        private void OnEnable(){if(displayRoot!=null)displayRoot.SetActive(true);if(inputRoot!=null)inputRoot.SetActive(true);if(eventRoot!=null)eventRoot.SetActive(true);if(WorldTargetsRoot!=null)WorldTargetsRoot.gameObject.SetActive(true);}
        private void OnDisable(){if(displayRoot!=null)displayRoot.SetActive(false);if(inputRoot!=null)inputRoot.SetActive(false);if(eventRoot!=null)eventRoot.SetActive(false);if(WorldTargetsRoot!=null)WorldTargetsRoot.gameObject.SetActive(false);}
        private void OnDestroy(){if(displayRoot!=null)Destroy(displayRoot);if(WorldTargetsRoot!=null)Destroy(WorldTargetsRoot.gameObject);if(cubeMesh!=null)Destroy(cubeMesh);foreach(var material in cubeMaterials)if(material!=null)Destroy(material);}
    }
}
