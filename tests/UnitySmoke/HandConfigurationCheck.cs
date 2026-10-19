#if UNITY_EDITOR
using System;
using System.Reflection;
using SpatialAI.Unity;
using UnityEditor;
using UnityEngine;

public static class HandConfigurationCheck
{
    static int checks;
    static readonly BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    static void Check(bool value, string name)
    { if (!value) throw new Exception("FAIL: " + name); checks++; }
    static void Reset()
    { typeof(SpatialHandTracking).GetMethod("ResetInferenceConfiguration", PrivateStatic).Invoke(null, null); }
    static void Startup(SpatialHandTracking tracker)
    { typeof(SpatialHandTracking).GetMethod("Awake", PrivateInstance).Invoke(tracker, null); }
    static SpatialHandTracking Create(SpatialHandInferenceBackend backend, SpatialHandMode mode)
    {
        var tracker = new GameObject("Hand configuration check").AddComponent<SpatialHandTracking>();
        var serialized = new SerializedObject(tracker);
        serialized.FindProperty("initialInferenceBackend").enumValueIndex = (int)backend;
        serialized.FindProperty("initialHandMode").enumValueIndex = (int)mode;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return tracker;
    }
    public static void Run()
    {
        checks = 0;
        SpatialHandTracking first = null, later = null;
        try
        {
            Reset();
            Check(SpatialHandTracking.ModelVariant == SpatialHandModelVariant.FP16, "official FP16 model default");
            SpatialHandTracking.ModelVariant = SpatialHandModelVariant.INT8;
            Check(SpatialHandTracking.ModelVariant == SpatialHandModelVariant.INT8, "INT8 model selectable independently");
            bool invalidModel = false;
            try { SpatialHandTracking.ModelVariant = (SpatialHandModelVariant)99; }
            catch (ArgumentOutOfRangeException) { invalidModel = true; }
            Check(invalidModel && SpatialHandTracking.ModelVariant == SpatialHandModelVariant.INT8, "invalid model rejected without mutation");
            Reset();
            Check(SpatialHandTracking.ModelVariant == SpatialHandModelVariant.FP16, "new session resets model to official FP16");
            Check(SpatialHandTracking.InferenceBackend == SpatialHandInferenceBackend.CPU &&
                SpatialHandTracking.HandMode == SpatialHandMode.Single && SpatialHandTracking.MaxHands == 1 &&
                SpatialHandTracking.SchedulingMode == SpatialHandSchedulingMode.Fixed, "CPU fixed single defaults");
            first = Create(SpatialHandInferenceBackend.GPU, SpatialHandMode.Dual);
            Reset(); Startup(first);
            Check(SpatialHandTracking.InferenceBackend == SpatialHandInferenceBackend.GPU &&
                SpatialHandTracking.HandMode == SpatialHandMode.Dual && SpatialHandTracking.MaxHands == 2,
                "serialized Inspector configuration applied at startup");
            later = Create(SpatialHandInferenceBackend.CPU, SpatialHandMode.Single);
            Startup(later);
            Check(SpatialHandTracking.InferenceBackend == SpatialHandInferenceBackend.GPU &&
                SpatialHandTracking.HandMode == SpatialHandMode.Dual, "later scene defaults do not overwrite settings");
            Reset();
            SpatialHandTracking.InferenceBackend = SpatialHandInferenceBackend.CPU;
            SpatialHandTracking.HandMode = SpatialHandMode.Auto;
            Startup(first);
            Check(SpatialHandTracking.InferenceBackend == SpatialHandInferenceBackend.CPU &&
                SpatialHandTracking.HandMode == SpatialHandMode.Auto, "explicit API overrides both Inspector options");
            Reset(); SpatialHandTracking.HandMode = SpatialHandMode.Single; Startup(first);
            Check(SpatialHandTracking.HandMode == SpatialHandMode.Single &&
                SpatialHandTracking.InferenceBackend == SpatialHandInferenceBackend.GPU, "overrides are per option");
            SpatialHandTracking.HandMode = SpatialHandMode.Auto;
            Check(SpatialHandTracking.SchedulingMode == SpatialHandSchedulingMode.Auto &&
                SpatialHandTracking.MaxHands == 2, "Auto maps to dynamic scheduler");
            SpatialHandTracking.HandMode = SpatialHandMode.Single;
            Check(SpatialHandTracking.SchedulingMode == SpatialHandSchedulingMode.Fixed &&
                SpatialHandTracking.MaxHands == 1, "Auto to Single switches to fixed single");
            SpatialHandTracking.MaxHands = 2;
            Check(SpatialHandTracking.HandMode == SpatialHandMode.Dual, "legacy hand count reflected by new API");
            SpatialHandTracking.SchedulingMode = SpatialHandSchedulingMode.Auto;
            Check(SpatialHandTracking.HandMode == SpatialHandMode.Auto, "legacy scheduler reflected by new API");
            bool rejected = false;
            try { SpatialHandTracking.HandMode = (SpatialHandMode)99; }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected && SpatialHandTracking.HandMode == SpatialHandMode.Auto, "invalid mode rejected without mutation");
            var editor = UnityEditor.Editor.CreateEditor(first);
            Check(editor.GetType().Name == "SpatialHandTrackingEditor", "SDK custom Inspector is registered");
            UnityEngine.Object.DestroyImmediate(editor);
            Debug.Log("HandConfigurationCheck PASS checks=" + checks);
        }
        finally
        {
            if (first != null) UnityEngine.Object.DestroyImmediate(first.gameObject);
            if (later != null) UnityEngine.Object.DestroyImmediate(later.gameObject);
            Reset();
        }
    }
}
#endif
