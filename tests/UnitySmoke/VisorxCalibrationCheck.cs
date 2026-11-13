#if UNITY_EDITOR
using System;
using System.IO;
using SpatialAI.Core;
using SpatialAI.Unity;
using UnityEngine;

public static class VisorxCalibrationCheck
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Run()
    {
        var path = Environment.GetEnvironmentVariable("VISORX_CALIBRATION_FIXTURE");
        var json = File.ReadAllText(path);
        var c = SpatialUvcCalibrationLoader.Load(json);
        Check(c.imageWidth == 640 && c.imageHeight == 480 && Math.Abs(c.fx - 332.34266f) < .001f &&
            Math.Abs(c.cx - 314.49696f) < .001f && c.distortion.Length == 5, "Intrinsics did not match export");
        Check(c.imageRotationClockwise == 0 && c.intrinsicsCalibrated && !c.extrinsicsMeasured && !c.handednessVerified,
            "Installation angle or verification status was misinterpreted");
        UvcHandCalibration resolved; string warning;
        Check(UvcHandCalibration.ResolveForPreview(c,"uvc:test",640,480,out resolved,out warning).Success && warning.Length > 0,
            "Export failed preview resolution or omitted unverified warning");
        var p = resolved.HeadFromCamera.Transform(new HandVector3(0,0,1));
        Check(Math.Abs(p.X-.0212132f) < .0001f && Math.Abs(p.Y+.4935355f) < .0001f && Math.Abs(p.Z-.9902513f) < .0001f,
            "Camera-to-head and alignment conversion failed");
        var legacy = new UvcHandCalibration { cameraId="uvc:legacy", fx=123, swapHandedness=true };
        var loaded = SpatialUvcCalibrationLoader.Load(JsonUtility.ToJson(legacy));
        Check(loaded.cameraId == legacy.cameraId && loaded.fx == legacy.fx && loaded.swapHandedness, "Legacy JSON changed");
        bool rejected=false;
        try { SpatialUvcCalibrationLoader.Load(json.Replace("\"version\": 1", "\"version\": 2")); }
        catch (ArgumentException) { rejected=true; }
        Check(rejected,"Unknown bundle version accepted");
        Debug.Log("VISORX_CALIBRATION_CHECK_PASS");
    }
}
#endif
