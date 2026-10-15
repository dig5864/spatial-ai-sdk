#if UNITY_EDITOR
using System;
using System.IO;
using System.Xml;
using SpatialAI.Editor;
using UnityEngine;

public static class UvcManifestCheck
{
    const string Android = "http://schemas.android.com/apk/res/android";
    const string Attached = "android.hardware.usb.action.USB_DEVICE_ATTACHED";
    static int checks;
    static void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); checks++; }
    static XmlDocument Load(string path) { var xml = new XmlDocument(); xml.Load(path); return xml; }
    public static void Run()
    {
        string root = Path.GetFullPath("Temp/UvcManifestCheck-" + Guid.NewGuid().ToString("N"));
        string manifest = Path.Combine(root, "src/main/AndroidManifest.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(manifest));
        File.WriteAllText(manifest, "<manifest xmlns:android='" + Android + "'><application><activity android:name='com.unity3d.player.UnityPlayerActivity'><intent-filter><action android:name='android.intent.action.MAIN'/><category android:name='android.intent.category.LAUNCHER'/><category android:name='com.qti.intent.category.SNAPDRAGON_VR'/></intent-filter><meta-data android:name='unityplayer.UnityActivity' android:value='true'/></activity></application></manifest>");
        var devices = new[] { new SpatialUvcDeviceId(), new SpatialUvcDeviceId(), new SpatialUvcDeviceId { vendorId=1234, productId=5678 } };
        var callback = new SpatialUvcAndroidManifest();
        Check(SpatialUvcSettings.CreateSettingsProvider().settingsPath == "Project/Spatial AI/UVC", "Project Settings provider");
        Check(!SpatialUvcSettings.instance.AssociateOnAttach, "association disabled by default");
        callback.OnPostGenerateGradleAndroidProject(root);
        Check(!File.ReadAllText(manifest).Contains(Attached), "default callback does not add USB launcher");
        Check(File.ReadAllText(manifest).Contains("android.hardware.usb.host"), "optional USB host declared");
        SpatialUvcAndroidManifest.Configure(root, true, devices);
        SpatialUvcAndroidManifest.Configure(root, true, devices);
        var xml = Load(manifest); var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("android", Android);
        Check(xml.SelectNodes("//action[@android:name='"+Attached+"']", ns).Count == 1, "repeated build does not duplicate filter");
        Check(xml.SelectNodes("//meta-data[@android:name='"+Attached+"']", ns).Count == 1, "repeated build does not duplicate metadata");
        string resource = Path.Combine(root, "src/main/res/xml/spatialai_uvc_devices.xml");
        Check(Load(resource).SelectNodes("/resources/usb-device").Count == 2, "VID PID list deduplicated");
        string before = File.ReadAllText(manifest); bool rejected=false;
        try { SpatialUvcAndroidManifest.Configure(root, true, new[] { new SpatialUvcDeviceId { vendorId=65536 } }); }
        catch (InvalidOperationException) { rejected=true; }
        Check(rejected && File.ReadAllText(manifest)==before, "invalid device IDs fail without changing manifest");
        rejected=false;
        try { SpatialUvcAndroidManifest.Configure(root, true, new SpatialUvcDeviceId[0]); }
        catch (InvalidOperationException) { rejected=true; }
        Check(rejected, "empty enabled list rejected");
        SpatialUvcAndroidManifest.Configure(root, false, devices);
        Check(!File.ReadAllText(manifest).Contains(Attached) && !File.Exists(resource), "disabling removes SDK association from reused export");
        Check(File.ReadAllText(manifest).Contains("SNAPDRAGON_VR") && File.ReadAllText(manifest).Contains("android.intent.action.MAIN"), "launcher categories preserved");
        xml=Load(manifest); var activity=xml.SelectSingleNode("/manifest/application/activity");
        var foreign=xml.CreateElement("meta-data"); foreign.SetAttribute("name",Android,Attached); foreign.SetAttribute("resource",Android,"@xml/custom_devices"); activity.AppendChild(foreign); xml.Save(manifest);
        SpatialUvcAndroidManifest.Configure(root,false,devices);
        Check(File.ReadAllText(manifest).Contains("@xml/custom_devices"), "disabled SDK preserves consumer metadata");
        rejected=false;
        try { SpatialUvcAndroidManifest.Configure(root,true,devices); } catch(InvalidOperationException) { rejected=true; }
        Check(rejected, "conflicting consumer metadata rejected");
        Debug.Log("UvcManifestCheck PASS checks=" + checks + " fixture=" + root);
    }
}
#endif
