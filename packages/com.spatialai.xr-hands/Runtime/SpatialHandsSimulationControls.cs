using UnityEngine;
using UnityEngine.InputSystem;

namespace SpatialAI.XRHands
{
    public sealed class SpatialHandsSimulationControls : MonoBehaviour
    {
        public SpatialHandsSimulator simulator;
        private void Update()
        {
            if (simulator == null) return;
            if (Pressed(Key.Digit1, KeyCode.Alpha1)) SetPose(SimulatedHandPose.OpenPalm);
            if (Pressed(Key.Digit2, KeyCode.Alpha2)) SetPose(SimulatedHandPose.Fist);
            if (Pressed(Key.Digit3, KeyCode.Alpha3)) SetPose(SimulatedHandPose.Pinch);
            if (Pressed(Key.L, KeyCode.L)) simulator.left.tracked = !simulator.left.tracked;
            if (Pressed(Key.R, KeyCode.R)) simulator.right.tracked = !simulator.right.tracked;
            if (Pressed(Key.Space, KeyCode.Space)) simulator.animate = !simulator.animate;
        }
        private static bool Pressed(Key systemKey, KeyCode legacyKey)
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current[systemKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(legacyKey);
#else
            return false;
#endif
        }
        private void SetPose(SimulatedHandPose pose) { simulator.left.pose = simulator.right.pose = pose; }
    }
}
