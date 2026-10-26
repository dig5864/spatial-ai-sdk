using SpatialAI.Unity;
using UnityEngine;

namespace SpatialAI.XRHands
{
    // Standard XR Hands consumer scene; head-local preview until a physical head rig is assigned.
    public sealed class SpatialUvcHandsSample : MonoBehaviour
    {
        public Camera xrCamera;
        public SpatialUvcHands driver;
        public TextAsset calibrationJson;
        public Transform physicalHead,trackingOrigin;
        private TextMesh readout;
        private GameObject visualRoot,label,backdrop;
        private Mesh panelMesh;
        private Material panelMaterial;
        private int renderFrames;
        private float renderFps,renderWindowStart;
        private void Start()
        {
            if(xrCamera==null)xrCamera=Camera.main;
            if(xrCamera==null){Debug.LogError("SpatialXRHands sample needs an XR camera");enabled=false;return;}
            visualRoot=new GameObject("Standard XR Hands display");
            visualRoot.transform.SetParent(physicalHead==null?xrCamera.transform:trackingOrigin,false);
            var visualizer=visualRoot.AddComponent<SpatialXRHandsVisualizer>();
            visualizer.trackingOrigin=visualRoot.transform;visualizer.presentationDistanceScale=3;
            if(SpatialDeviceProfiles.SelectHost(SpatialDeviceProfiles.ReadHostIdentity(),out var host).Success)
                visualizer.reflectDisplayX=host.reflectHandDisplayX;
            var inputRoot=new GameObject("UVC XR Hands input");inputRoot.transform.SetParent(transform,false);inputRoot.SetActive(false);
            driver=inputRoot.AddComponent<SpatialUvcHands>();driver.calibrationJson=calibrationJson;
            driver.physicalHead=physicalHead;driver.trackingOrigin=trackingOrigin;
            inputRoot.SetActive(true);
            label=new GameObject("Standard XR Hands readout");label.transform.SetParent(xrCamera.transform,false);
            label.transform.localPosition=new Vector3(-1.02f,1.02f,5.4f);
            readout=label.AddComponent<TextMesh>();readout.anchor=TextAnchor.UpperLeft;
            readout.fontSize=48;readout.characterSize=.0225f;readout.color=Color.white;
            backdrop=new GameObject("XR Hands readout background");backdrop.transform.SetParent(xrCamera.transform,false);
            panelMesh=new Mesh { name="Spatial XR readout panel" };
            panelMesh.vertices=new[] { new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0) };
            panelMesh.triangles=new[] { 0,1,2,0,2,3 };panelMesh.RecalculateNormals();
            backdrop.AddComponent<MeshFilter>().sharedMesh=panelMesh;
            backdrop.transform.localPosition=new Vector3(.24f,.345f,5.46f);backdrop.transform.localScale=new Vector3(2.7f,1.5f,1);
            panelMaterial=SpatialSampleMaterial.Create(new Color(.015f,.02f,.04f));
            backdrop.AddComponent<MeshRenderer>().sharedMaterial=panelMaterial;
            Debug.Log("SpatialXRHands sample ready: standard XRHandSubsystem, head-local preview, display scale=3");
        }
        private void Update()
        {
            if(readout==null || driver==null)return;
            renderFrames++;float seconds=Time.unscaledTime-renderWindowStart;
            if(seconds>=1){renderFps=renderFrames/seconds;renderFrames=0;renderWindowStart=Time.unscaledTime;}
            var subsystem=driver.Subsystem;
            readout.text="REAL UVC -> UNITY XR HANDS\n"+
                "Left cyan / Right orange\n"+
                "Left "+(subsystem!=null && subsystem.leftHand.isTracked)+" / Right "+(subsystem!=null && subsystem.rightHand.isTracked)+"\n"+
                "Head-local estimated preview\n"+
                "Strict poses; no depth-held tracking\n"+
                "Result age "+(driver.Source==null?-1:driver.Source.ResultAgeMs).ToString("F0")+" ms\n"+
                driver.InferenceDelegate+" | AI "+driver.InferenceFps.ToString("F1")+" UVC "+driver.CaptureFps.ToString("F1")+" Render "+renderFps.ToString("F1")+" FPS";
        }
        private void OnEnable(){if(driver!=null)driver.enabled=true;SetVisible(true);}
        private void OnDisable(){if(driver!=null)driver.enabled=false;SetVisible(false);}
        private void SetVisible(bool visible)
        {
            if(visualRoot!=null)visualRoot.SetActive(visible);
            if(label!=null)label.SetActive(visible);
            if(backdrop!=null)backdrop.SetActive(visible);
        }
        private void OnDestroy()
        {
            if(visualRoot!=null)Destroy(visualRoot);
            if(label!=null)Destroy(label);
            if(backdrop!=null)Destroy(backdrop);
            if(panelMesh!=null)Destroy(panelMesh);
            if(panelMaterial!=null)Destroy(panelMaterial);
        }
    }
}
