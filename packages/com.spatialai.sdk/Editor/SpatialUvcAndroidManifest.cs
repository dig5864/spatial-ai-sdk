using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace SpatialAI.Editor
{
    public sealed class SpatialUvcAndroidManifest : IPostGenerateGradleAndroidProject
    {
        const string Android = "http://schemas.android.com/apk/res/android";
        const string Attached = "android.hardware.usb.action.USB_DEVICE_ATTACHED";
        const string Resource = "@xml/spatialai_uvc_devices";
        public int callbackOrder { get { return 995; } }
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var settings = SpatialUvcSettings.instance;
            Configure(path, settings.AssociateOnAttach, settings.Devices);
        }

        // Also used to validate the exact build transformation without generating an APK.
        public static void Configure(string library, bool associate, IReadOnlyList<SpatialUvcDeviceId> devices)
        {
            if (associate)
            {
                if (devices == null || devices.Count == 0)
                    throw new InvalidOperationException("Spatial AI UVC: add at least one VID/PID in Project Settings > Spatial AI > UVC.");
                foreach (var device in devices)
                    if (device == null || device.vendorId < 0 || device.vendorId > 65535 || device.productId < 0 || device.productId > 65535)
                        throw new InvalidOperationException("Spatial AI UVC: VID/PID must be decimal integers from 0 to 65535.");
            }
            var path = Path.Combine(library, "src/main/AndroidManifest.xml");
            var xml = new XmlDocument(); xml.Load(path);
            var ns = new XmlNamespaceManager(xml.NameTable); ns.AddNamespace("android", Android);
            var activity = xml.SelectSingleNode("/manifest/application/activity[meta-data[@android:name='unityplayer.UnityActivity' and @android:value='true']]", ns)
                ?? xml.SelectSingleNode("/manifest/application/activity[intent-filter[action[@android:name='android.intent.action.MAIN'] and category[@android:name='android.intent.category.LAUNCHER']]]", ns);
            if (activity == null) throw new InvalidOperationException("Spatial AI UVC: Unity main Activity missing from AndroidManifest.xml.");
            var metadata = activity.SelectSingleNode("meta-data[@android:name='" + Attached + "']", ns) as XmlElement;
            bool owned = metadata != null && metadata.GetAttribute("resource", Android) == Resource;
            if (associate && metadata != null && !owned)
                throw new InvalidOperationException("Spatial AI UVC: USB device metadata is already managed by another manifest/plugin. Disable SDK device association or consolidate the device list first.");
            if (xml.SelectSingleNode("/manifest/uses-feature[@android:name='android.hardware.usb.host']", ns) == null)
            {
                var feature = xml.CreateElement("uses-feature"); Set(feature, "name", "android.hardware.usb.host"); Set(feature, "required", "false");
                xml.DocumentElement.AppendChild(feature);
            }
            var resourcePath = Path.Combine(library, "src/main/res/xml/spatialai_uvc_devices.xml");
            if (associate)
            {
                if (activity.SelectSingleNode("intent-filter[action[@android:name='" + Attached + "']]", ns) == null)
                {
                    var filter = xml.CreateElement("intent-filter");
                    var action = xml.CreateElement("action"); Set(action, "name", Attached); filter.AppendChild(action);
                    var category = xml.CreateElement("category"); Set(category, "name", "android.intent.category.DEFAULT"); filter.AppendChild(category);
                    activity.AppendChild(filter);
                }
                if (metadata == null)
                {
                    metadata = xml.CreateElement("meta-data"); Set(metadata, "name", Attached); activity.AppendChild(metadata);
                }
                Set(metadata, "resource", Resource);
                var filters = new XmlDocument(); filters.AppendChild(filters.CreateElement("resources"));
                var seen = new HashSet<string>();
                foreach (var device in devices)
                {
                    if (!seen.Add(device.vendorId + ":" + device.productId)) continue;
                    var item = filters.CreateElement("usb-device");
                    item.SetAttribute("vendor-id", device.vendorId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    item.SetAttribute("product-id", device.productId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    filters.DocumentElement.AppendChild(item);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(resourcePath)); filters.Save(resourcePath);
            }
            else if (owned)
            {
                activity.RemoveChild(metadata);
                foreach (XmlNode filter in activity.SelectNodes("intent-filter[action[@android:name='" + Attached + "']]", ns))
                {
                    // Keep any other actions a consumer added to this filter.
                    foreach (XmlNode action in filter.SelectNodes("action[@android:name='" + Attached + "']", ns)) filter.RemoveChild(action);
                    if (filter.SelectNodes("action").Count == 0) activity.RemoveChild(filter);
                }
                if (File.Exists(resourcePath)) File.Delete(resourcePath);
            }
            xml.Save(path);
        }
        static void Set(XmlElement node, string name, string value)
        {
            var attribute = node.OwnerDocument.CreateAttribute("android", name, Android);
            attribute.Value = value; node.SetAttributeNode(attribute);
        }
    }
}
