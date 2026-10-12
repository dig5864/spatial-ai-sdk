using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace SpatialAI.XRInteraction
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SpatialHandInputState : IInputStateTypeInfo
    {
        public FourCC format { get { return new FourCC('S','H','N','D'); } }
        [InputControl(name="isTracked",layout="Button",bit=0)]
        [InputControl(name="pinch",layout="Button",bit=1)]
        public uint buttons;
        [InputControl(layout="Vector3")] public Vector3 devicePosition;
        [InputControl(layout="Quaternion")] public Quaternion deviceRotation;
        [InputControl(layout="Integer")] public int trackingState;
        [InputControl(layout="Axis")] public float pinchStrength;
    }

    [InputControlLayout(stateType=typeof(SpatialHandInputState),displayName="Spatial Hand Ray")]
    public sealed class SpatialHandInputDevice : InputDevice
    {
        public ButtonControl isTracked { get; private set; }
        public ButtonControl pinch { get; private set; }
        public Vector3Control devicePosition { get; private set; }
        public QuaternionControl deviceRotation { get; private set; }
        public IntegerControl trackingState { get; private set; }
        public AxisControl pinchStrength { get; private set; }
        protected override void FinishSetup()
        {
            base.FinishSetup();isTracked=GetChildControl<ButtonControl>("isTracked");pinch=GetChildControl<ButtonControl>("pinch");
            devicePosition=GetChildControl<Vector3Control>("devicePosition");deviceRotation=GetChildControl<QuaternionControl>("deviceRotation");
            trackingState=GetChildControl<IntegerControl>("trackingState");pinchStrength=GetChildControl<AxisControl>("pinchStrength");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Register(){InputSystem.RegisterLayout<SpatialHandInputDevice>();}
    }
}
