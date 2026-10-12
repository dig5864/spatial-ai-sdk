using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace SpatialAI.XRInteraction
{
    public static class SpatialHandInteractionRig
    {
        public static SpatialHandInteraction Create(Transform parent,XRInteractionManager manager)
        {
            var root=new GameObject("Spatial Hand Interaction Rig");root.SetActive(false);root.transform.SetParent(parent,false);
            var bridge=root.AddComponent<SpatialHandInteraction>();bridge.trackingOrigin=parent;bridge.manager=manager;
            bridge.leftRay=MakeRay(root.transform,manager,false);bridge.rightRay=MakeRay(root.transform,manager,true);
            root.SetActive(true);return bridge;
        }
        private static SpatialHandRayBinding MakeRay(Transform parent,XRInteractionManager manager,bool right)
        {
            var child=new GameObject(right?"Right Hand Ray":"Left Hand Ray");child.transform.SetParent(parent,false);
            var binding=child.AddComponent<SpatialHandRayBinding>();binding.rightHand=right;binding.manager=manager;return binding;
        }
    }
}
