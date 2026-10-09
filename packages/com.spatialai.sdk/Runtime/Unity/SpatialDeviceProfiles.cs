using System.Collections.Generic;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.Unity
{
    public static class SpatialDeviceProfiles
    {
        public static Result SelectHost(DeviceIdentity identity, out HostDeviceProfile profile)
        {
            var assets = Resources.LoadAll<TextAsset>("SpatialAI/HostProfiles");
            var catalog = new List<HostDeviceProfile>(assets.Length);
            foreach (var asset in assets)
            {
                try { catalog.Add(JsonUtility.FromJson<HostDeviceProfile>(asset.text)); }
                catch (System.Exception e)
                { profile = null; return new Result(ErrorCode.InvalidArgument, "Invalid host profile " + asset.name + ": " + e.Message); }
            }
            return HostDeviceProfileSelector.Select(identity, catalog, out profile);
        }

        public static DeviceIdentity ReadHostIdentity(string externalHeadsetId = null)
        {
            var identity = new DeviceIdentity { Model = SystemInfo.deviceModel, HeadsetId = externalHeadsetId };
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var build = new AndroidJavaClass("android.os.Build"))
            {
                identity.Manufacturer = build.GetStatic<string>("MANUFACTURER");
                identity.Model = build.GetStatic<string>("MODEL");
                identity.DeviceCode = build.GetStatic<string>("DEVICE");
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS exposes a phone model, not an external glasses identity.
            // The headset connection service must supply HeadsetId separately.
            identity.Manufacturer = "Apple";
#endif
            return identity;
        }

        public static Result Select(DeviceIdentity identity, out OpticalProfileDescriptor profile)
        {
            var assets = Resources.LoadAll<TextAsset>("SpatialAI/Profiles");
            var catalog = new List<OpticalProfileDescriptor>(assets.Length);
            foreach (var asset in assets)
            {
                try { catalog.Add(JsonUtility.FromJson<OpticalProfileDescriptor>(asset.text)); }
                catch (System.Exception e)
                { profile = null; return new Result(ErrorCode.InvalidArgument, "Invalid profile " + asset.name + ": " + e.Message); }
            }
            return DeviceProfileSelector.Select(identity, catalog, out profile);
        }
    }
}
