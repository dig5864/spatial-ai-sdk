#if UNITY_EDITOR
using System;
using SpatialAI.Core;
using SpatialAI.Unity;
using SpatialAI.Cardboard;
using UnityEditor;
using UnityEngine;
public static class HxqPersistedAlignmentCheck
{
    static void Check(bool ok,string detail){if(!ok)throw new Exception(detail);}
    public static void Run()
    {
        const string path="Assets/SpatialAI/Calibrations/hxq-20261015-intrinsics-v1.json";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var c=JsonUtility.FromJson<UvcHandCalibration>(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text);
        Check(c.calibrationVersion=="hxq-20261015-manual-aligned-33deg-v3"&&c.Validate(true).Success&&!c.Validate(false).Success,"Expected unverified manual alignment profile");
        var q=c.headFromCameraRotationXyzw;
        Check(Math.Abs(q[0]-(float)Math.Sin(33*Math.PI/360))<1e-6f && Math.Abs(q[3]-(float)Math.Cos(33*Math.PI/360))<1e-6f && q[1]==0&&q[2]==0,"Expected exactly 33deg in asset; no doubled preview correction");
        Check(Math.Abs(c.headFromCameraTranslationMetres[1]-.0812132034f)<1e-6f && Math.Abs(c.headFromCameraTranslationMetres[2]-.0894974747f)<1e-6f,"Independent mounting translation changed");
        var host=JsonUtility.FromJson<HostDeviceProfile>(Resources.Load<TextAsset>("SpatialAI/HostProfiles/xiaomi-mayfly").text);
        Check(host.reverseStereoBaseline&&host.Validate().Success,"Reflected phone profile not persisted");
        var rig=new ScreenRigProfile{reverseStereoBaseline=true};
        Check(JsonUtility.FromJson<ScreenRigProfile>(JsonUtility.ToJson(rig)).reverseStereoBaseline,"Rig serialization lost stereo mapping");
        try {
            SpatialCardboardBridge.SetPhoneStereoDiagnosticMode(PhoneStereoDiagnosticMode.Profile);
            Check(Math.Abs(SpatialCardboardBridge.PhoneStereoDiagnosticBaseline(.062f,true)+.062f)<1e-6f,"Profile failed to reverse reflected holder");
            Check(Math.Abs(SpatialCardboardBridge.PhoneStereoDiagnosticBaseline(.062f,false)-.062f)<1e-6f,"Ordinary profile baseline changed");
            SpatialCardboardBridge.SetPhoneStereoDiagnosticMode(PhoneStereoDiagnosticMode.Normal);
            Check(SpatialCardboardBridge.PhoneStereoDiagnosticBaseline(.062f,true)>.061f,"Normal diagnostic cannot override profile");
            SpatialCardboardBridge.SetPhoneStereoDiagnosticMode(PhoneStereoDiagnosticMode.Zero);
            Check(SpatialCardboardBridge.PhoneStereoDiagnosticBaseline(.062f,true)==0,"Zero diagnostic cannot override profile");
        } finally { SpatialCardboardBridge.SetPhoneStereoDiagnosticMode(PhoneStereoDiagnosticMode.Profile); }
        Debug.Log("HXQ_PERSISTED_ALIGNMENT_PASS version="+c.calibrationVersion+" exact33deg=true profileReverse=true defaultNormalUnaffected=true");
    }
    public static void Build(){Run();UvcLiveCalibrationCheck.Run();RhinoXHandCalibrationCheck.RunAndBuild();}
}
#endif
