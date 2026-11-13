#if UNITY_EDITOR
using System;
using System.IO;
using SpatialAI.Unity;
using UnityEngine;
public static class CalibrationStoreCheck
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var json = File.ReadAllText(Environment.GetEnvironmentVariable("VISORX_CALIBRATION_FIXTURE"));
        var packaged = new TextAsset(json); string source;
        Check(!SpatialUvcCalibrationStore.HasSelected && !File.Exists(SpatialUvcCalibrationStore.DefaultPath), "Test store must start empty");
        SpatialUvcCalibrationStore.Load(packaged, out source); Check(source == "packaged", "Packaged fallback");
        Directory.CreateDirectory(SpatialUvcCalibrationStore.DirectoryPath);
        File.WriteAllText(SpatialUvcCalibrationStore.DefaultPath, json);
        SpatialHandTracking.UvcMountRotationDegrees=90; SpatialHandTracking.UvcMirrorOutputX=true;
        var active = SpatialUvcCalibrationStore.Load(packaged, out source);
        Check(source == "default file" && active.imageRotationClockwise==90 && active.inputMirrored && active.MatchesDeliveredSize(480,640), "Default precedence and frame orientation");
        SpatialHandTracking.UvcMountRotationDegrees=0; SpatialHandTracking.UvcMirrorOutputX=false;
        int changed=0; Action handler=()=>changed++; SpatialUvcCalibrationStore.Changed+=handler;
        SpatialUvcCalibrationStore.SaveSelected(json);
        SpatialUvcCalibrationStore.Load(packaged, out source); Check(source == "selected" && changed==1, "Selected precedence/event");
        bool rejected=false; try { SpatialUvcCalibrationStore.SaveSelected("{}"); } catch (ArgumentException) { rejected=true; }
        Check(rejected && changed==1 && SpatialUvcCalibrationStore.HasSelected, "Invalid selection replaced valid file");
        SpatialUvcCalibrationStore.SaveSelected(json); Check(changed==2, "Replacing selection failed");
        SpatialUvcCalibrationStore.ClearSelected(); SpatialUvcCalibrationStore.Load(packaged, out source);
        Check(source == "default file" && changed==3, "Clear did not restore default");
        File.WriteAllText(SpatialUvcCalibrationStore.DefaultPath, "{}"); rejected=false;
        try { SpatialUvcCalibrationStore.Load(packaged, out source); } catch (ArgumentException) { rejected=true; }
        Check(rejected, "Invalid default silently fell back to packaged");
        File.Delete(SpatialUvcCalibrationStore.DefaultPath);
        SpatialUvcCalibrationStore.Load(packaged, out source); Check(source == "packaged", "Packaged fallback after clearing");
        SpatialUvcCalibrationStore.Changed-=handler;
        Debug.Log("CALIBRATION_STORE_CHECK_PASS");
    }
}
#endif
