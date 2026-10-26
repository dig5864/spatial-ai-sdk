using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using SpatialAI.Core;

namespace SpatialAI.XRHands
{
    // Standard XR Hands poses for tracked input; SDK-internal fallback is drawing only.
    // Joint poses are tracking-origin poses, so visuals are parented to that origin.
    public sealed class SpatialXRHandsVisualizer : MonoBehaviour
    {
        public Transform trackingOrigin;
        public bool reflectDisplayX;
        [Range(1f,10f)] public float presentationDistanceScale=1f;
        private XRHandSubsystem subsystem;
        private readonly List<XRHandSubsystem> candidates = new List<XRHandSubsystem>();
        private readonly Transform[,] markers = new Transform[2, 26];
        private readonly LineRenderer[,] bones = new LineRenderer[2, 26];
        private readonly int[] parents = { -1, 0, 0, 2, 3, 4, 0, 6, 7, 8, 9,
            0, 11, 12, 13, 14, 0, 16, 17, 18, 19, 0, 21, 22, 23, 24 };
        private Material leftMaterial, rightMaterial;
        private Mesh markerMesh;
        private void Awake()
        {
            leftMaterial = SpatialAI.Unity.SpatialSampleMaterial.Create(new Color(0.2f, 0.8f, 1));
            rightMaterial = SpatialAI.Unity.SpatialSampleMaterial.Create(new Color(1, 0.65f, 0.2f));
            markerMesh = new Mesh { name = "Spatial XR joint marker" };
            markerMesh.vertices = new[] { Vector3.up * .5f, Vector3.down * .5f, Vector3.left * .5f,
                Vector3.right * .5f, Vector3.forward * .5f, Vector3.back * .5f };
            markerMesh.triangles = new[] { 0,4,3, 0,2,4, 0,5,2, 0,3,5, 1,3,4, 1,4,2, 1,2,5, 1,5,3 };
            markerMesh.RecalculateNormals();
            for (int side = 0; side < 2; side++) for (int i = 0; i < 26; i++)
            {
                var sphere = new GameObject();
                sphere.AddComponent<MeshFilter>().sharedMesh = markerMesh;
                sphere.AddComponent<MeshRenderer>();
                sphere.name = (side == 0 ? "Left " : "Right ") + ((SpatialAI.Core.HandJointId)i);
                sphere.transform.SetParent(trackingOrigin != null ? trackingOrigin : transform, false);
                sphere.transform.localScale = Vector3.one * 0.012f;
                sphere.GetComponent<Renderer>().sharedMaterial = side == 0 ? leftMaterial : rightMaterial;
                markers[side, i] = sphere.transform;
                var lineObject = new GameObject(sphere.name + " bone");
                lineObject.transform.SetParent(sphere.transform.parent, false);
                var line = lineObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false; line.positionCount = 2; line.startWidth = line.endWidth = 0.004f;
                line.sharedMaterial = side == 0 ? leftMaterial : rightMaterial;
                bones[side, i] = line;
                sphere.SetActive(false); lineObject.SetActive(false);
            }
        }
        private void Update()
        {
            if (subsystem != null && subsystem.running) return;
            if (subsystem != null) subsystem.updatedHands -= OnHandsUpdated;
            subsystem = null; candidates.Clear(); SubsystemManager.GetSubsystems(candidates);
            foreach (var candidate in candidates)
                if (candidate.running && (candidate.subsystemDescriptor.id == SpatialHandSubsystem.DescriptorId || candidate.subsystemDescriptor.id == SpatialHandSubsystem.UvcDescriptorId))
                { subsystem = candidate; subsystem.updatedHands += OnHandsUpdated; break; }
            if (subsystem != null) Draw(); else Hide();
        }
        private void OnHandsUpdated(XRHandSubsystem sender, XRHandSubsystem.UpdateSuccessFlags flags, XRHandSubsystem.UpdateType type)
        { Draw(); }
        private void Draw()
        {
            DrawHand(subsystem.leftHand, 0); DrawHand(subsystem.rightHand, 1);
        }
        private void DrawHand(XRHand hand, int side)
        {
            var source=(subsystem as SpatialHandSubsystem)?.FrameSource as UvcHandsFrameSource;
            for (int i = 0; i < 26; i++)
            {
                Pose pose = Pose.identity;
                bool valid = hand.isTracked && hand.GetJoint(SpatialXRHandsContract.ToXRJointId((SpatialAI.Core.HandJointId)i)).TryGetPose(out pose);
                if(!valid&&source!=null&&source.Processor.DisplayState((HandSide)side)!="fresh"&&
                    source.Processor.TryPresentationJoint((HandSide)side,(HandJointId)i,out var heldPosition))
                {pose=new Pose(heldPosition,Quaternion.identity);valid=true;}
                markers[side, i].gameObject.SetActive(valid);
                if (valid)
                {
                    float scale=Mathf.Clamp(presentationDistanceScale,1f,10f);
                    markers[side,i].localPosition=new Vector3(reflectDisplayX?-pose.position.x:pose.position.x,pose.position.y,pose.position.z)*scale;
                    var q=pose.rotation;
                    markers[side,i].localRotation=reflectDisplayX?new Quaternion(q.x,-q.y,-q.z,q.w):q;
                    markers[side,i].localScale=Vector3.one*.012f*scale;
                    bones[side,i].startWidth=bones[side,i].endWidth=.004f*scale;
                }
            }
            for (int i = 0; i < 26; i++)
            {
                int parent = parents[i];
                bool valid = parent >= 0 && markers[side, i].gameObject.activeSelf && markers[side, parent].gameObject.activeSelf;
                bones[side, i].gameObject.SetActive(valid);
                if (valid) { bones[side, i].SetPosition(0, markers[side, parent].localPosition); bones[side, i].SetPosition(1, markers[side, i].localPosition); }
            }
        }
        private void Hide()
        {
            for (int side = 0; side < 2; side++) for (int i = 0; i < 26; i++)
            { if (markers[side, i] != null) markers[side, i].gameObject.SetActive(false); if (bones[side, i] != null) bones[side, i].gameObject.SetActive(false); }
        }
        private void OnDisable() { if (subsystem != null) subsystem.updatedHands -= OnHandsUpdated; subsystem = null; Hide(); }
        private void OnDestroy()
        {
            for (int side = 0; side < 2; side++) for (int i = 0; i < 26; i++)
            { if (markers[side, i] != null) Destroy(markers[side, i].gameObject); if (bones[side, i] != null) Destroy(bones[side, i].gameObject); }
            if (leftMaterial != null) Destroy(leftMaterial); if (rightMaterial != null) Destroy(rightMaterial);
            if (markerMesh != null) Destroy(markerMesh);
        }
    }
}
