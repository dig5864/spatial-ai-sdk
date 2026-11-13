#if UNITY_EDITOR
using System;
using System.IO;
using SpatialAI.Core;
using SpatialAI.Unity;
using UnityEngine;
public static class VisorxAlignmentCheck
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Point(UvcHandCalibration c, HandVector3 input, float x, float y, float z)
    {
        var p = c.HeadFromCamera.Transform(input);
        Check(Math.Abs(p.X-x)<.0001f && Math.Abs(p.Y-y)<.0001f && Math.Abs(p.Z-z)<.0001f, "Web-to-Unity point mismatch");
    }
    public static void Run()
    {
        var json=File.ReadAllText(Environment.GetEnvironmentVariable("VISORX_CALIBRATION_B_FIXTURE"));
        var c=SpatialUvcCalibrationLoader.Load(json);
        // Independently calculated from H * (Rx*Ry*Rz, t) * cameraToHead * C.
        Point(c,new HandVector3(0,0,1),.026213174f,-.370535481f,.964251347f);
        Point(c,new HandVector3(.2f,.1f,.7f),.120435512f,-.395330974f,.602993358f);
        Point(c,new HandVector3(-.2f,-.1f,.4f),-.085286863f,.098725766f,.543108113f);
        Check(!c.extrinsicsMeasured && !c.validatedForHardware,"Imported correction cannot claim hardware validation");
        bool rejected=false;
        try { SpatialUvcCalibrationLoader.Load(json.Replace("\"pitch\": -29.6", "\"pitch\": -90")); }
        catch(ArgumentException){rejected=true;}
        Check(rejected,"Out-of-range alignment must be rejected");
        Debug.Log("VISORX_ALIGNMENT_CHECK_PASS");
    }
}
#endif
