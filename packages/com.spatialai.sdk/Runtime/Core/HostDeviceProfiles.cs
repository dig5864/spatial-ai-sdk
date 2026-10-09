using System;
using System.Collections.Generic;

namespace SpatialAI.Core
{
    [Serializable]
    public sealed class HostImuProfile
    {
        public string calibrationVersion;
        // Already converted by Cardboard to Unity coordinates; not raw Android sensor axes.
        public string sourceFrame = "cardboard-unity";
        public string screenOrientation = "landscape-left";
        public float basisX, basisY, basisZ, basisW = 1;
        public float mountX, mountY, mountZ, mountW = 1;
        public bool preserveNeckModel;
        public bool invertInputRotation;
        public bool validatedForHardware;
        public Rotation Basis { get { return new Rotation(basisX, basisY, basisZ, basisW); } }
        public Rotation Mount { get { return new Rotation(mountX, mountY, mountZ, mountW); } }
        public Result Validate()
        {
            if (string.IsNullOrWhiteSpace(calibrationVersion) || sourceFrame != "cardboard-unity" ||
                screenOrientation != "landscape-left" || !Basis.IsUnit || !Mount.IsUnit)
                return new Result(ErrorCode.InvalidArgument, "Invalid host IMU frame, orientation or unit quaternion.");
            return Result.Ok;
        }
        // B changes the coordinate basis; M is head/eye mounting in the mapped frame.
        public Rotation Map(Rotation raw) { return Basis * (invertInputRotation ? raw.Inverse() : raw) * Basis.Inverse() * Mount; }
    }

    // Host display/performance settings are separate from headset optical calibration.
    [Serializable]
    public sealed class HostDeviceProfile
    {
        public int schemaVersion;
        public string profileId;
        public string manufacturer;
        public string model;
        public string deviceCode;
        public int nativePortraitWidth;
        public int nativePortraitHeight;
        public int targetFrameRate;
        public string displayMode;
        public string trackingSource;
        public string cameraSource;
        public bool validatedForHardware;
        // Left then right x,y,width,height in full-display Android pixels.
        public float[] referenceEyeRects;
        public bool referenceOutputRotate180;
        public HostImuProfile imu;

        public Result Validate()
        {
            if (schemaVersion != 1 || string.IsNullOrWhiteSpace(profileId) ||
                string.IsNullOrWhiteSpace(manufacturer) || string.IsNullOrWhiteSpace(model) ||
                string.IsNullOrWhiteSpace(deviceCode) || nativePortraitWidth <= 0 ||
                nativePortraitHeight <= 0 || targetFrameRate < 30 || targetFrameRate > 120 ||
                displayMode != "phone-cardboard" || trackingSource != "phone-cardboard" ||
                cameraSource != "external-uvc")
                return new Result(ErrorCode.InvalidArgument, "Invalid host device configuration.");
            return imu != null ? imu.Validate() : Result.Ok;
        }
    }

    public static class HostDeviceProfileSelector
    {
        public static Result Select(DeviceIdentity identity, IEnumerable<HostDeviceProfile> profiles,
            out HostDeviceProfile selected)
        {
            selected = null;
            if (identity == null || profiles == null)
                return new Result(ErrorCode.InvalidArgument, "Host identity or profile catalog is missing.");
            foreach (var profile in profiles)
            {
                if (profile == null || !Same(profile.manufacturer, identity.Manufacturer) ||
                    !Same(profile.model, identity.Model) || !Same(profile.deviceCode, identity.DeviceCode)) continue;
                if (selected != null)
                {
                    selected = null;
                    return new Result(ErrorCode.InvalidArgument, "Multiple host device profiles match.");
                }
                selected = profile;
            }
            return selected == null
                ? new Result(ErrorCode.Unsupported, "No host device profile matches.")
                : selected.Validate();
        }

        private static bool Same(string expected, string actual)
        {
            return !string.IsNullOrWhiteSpace(expected) && !string.IsNullOrWhiteSpace(actual) &&
                string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
