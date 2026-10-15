using System;
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
            var result = HostDeviceProfileSelector.Select(identity, catalog, out profile);
            if (identity == null) return result;
            // Unknown phone on an explicitly configured fixed rig. Preserve all
            // exact-match/invalid-profile errors; do not borrow Xiaomi identity.
            bool rhinoX = string.Equals(identity.Manufacturer, "QUALCOMM", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(identity.Model, "RhinoX", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(identity.DeviceCode, "kona", StringComparison.OrdinalIgnoreCase);
            var rig = profile == null && result.Code == ErrorCode.Unsupported && !rhinoX && string.IsNullOrEmpty(identity.HeadsetId)
                ? SpatialPhoneScreen.LoadRig() : null;
            if (rig != null)
            {
                profile = new HostDeviceProfile { schemaVersion = 1, profileId = rig.rigId + "-phone-development",
                    manufacturer = identity.Manufacturer, model = identity.Model, deviceCode = identity.DeviceCode,
                    nativePortraitWidth = Math.Min(Screen.width, Screen.height), nativePortraitHeight = Math.Max(Screen.width, Screen.height),
                    targetFrameRate = 60, displayMode = "phone-cardboard", trackingSource = "phone-cardboard", cameraSource = "external-uvc",
                    imu = rig.phoneImu, invertViewPitch = rig.invertViewPitch, invertViewRoll = rig.invertViewRoll,
                    reflectHandDisplayX = rig.reflectHandDisplayX, uvcSwapHandedness = rig.uvcSwapHandedness, reverseStereoBaseline = rig.reverseStereoBaseline };
                return profile.Validate();
            }
            return result;
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
