using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
#if SPATIALAI_XRI_3_OR_NEWER
using UnityEngine.XR.Interaction.Toolkit.Interactables;
#endif

namespace SpatialAI.XRInteraction
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class SpatialInteractionTarget : MonoBehaviour
    {
        public int SelectCount { get; private set; }
        public int CancelCount { get; private set; }
        public bool Selected { get; private set; }
        private XRSimpleInteractable interactable;
        private Color original;
        private Renderer visual;
        private void Awake(){interactable=GetComponent<XRSimpleInteractable>();visual=GetComponent<Renderer>();if(visual!=null)original=visual.sharedMaterial.color;}
        private void OnEnable(){interactable.selectEntered.AddListener(Enter);interactable.selectExited.AddListener(Exit);interactable.hoverEntered.AddListener(HoverEnter);interactable.hoverExited.AddListener(HoverExit);}
        private void OnDisable(){interactable.selectEntered.RemoveListener(Enter);interactable.selectExited.RemoveListener(Exit);interactable.hoverEntered.RemoveListener(HoverEnter);interactable.hoverExited.RemoveListener(HoverExit);if(visual!=null)visual.sharedMaterial.color=original;}
        private void Refresh(){if(visual!=null)visual.sharedMaterial.color=Selected?Color.white:interactable.isHovered?Color.Lerp(original,Color.white,.35f):original;}
        private void HoverEnter(HoverEnterEventArgs args){Refresh();Debug.Log("SpatialXRI hover target="+name+" ray="+args.interactorObject.transform.name);}
        private void HoverExit(HoverExitEventArgs args){Refresh();}
        private void Enter(SelectEnterEventArgs args)
        {
            SelectCount++;Selected=true;Refresh();
            Debug.Log("SpatialXRI select target="+name+" ray="+args.interactorObject.transform.name+" count="+SelectCount);
        }
        private void Exit(SelectExitEventArgs args)
        {
            if(args.isCanceled)CancelCount++;Selected=interactable.interactorsSelecting.Count>0;
            Refresh();
            Debug.Log("SpatialXRI release target="+name+" canceled="+args.isCanceled+" remaining="+interactable.interactorsSelecting.Count);
        }
    }
}
