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
        // Render-eye baseline direction for the holder's reflected optical path; physical IPD remains positive.
        public bool reverseStereoBaseline;
        // Candidate optical view response; separate from the physical IMU mount.
        public bool invertViewPitch;
        public bool invertViewRoll;
        // Presentation of head-local hand visuals through the current mirror/output path.
        // Independent of camera input mirroring and anatomical handedness.
        public bool reflectHandDisplayX;
        // Anatomical labels for this external UVC input, independent of optical reflection.
        // An explicitly loaded hand calibration takes precedence over this preview default.
        public bool uvcSwapHandedness;
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

    public static class ReflectedViewRotation
    {
        public static Rotation Apply(Rotation pose, bool invertPitch, bool invertRoll)
        {
            // Vertical image reflection maps axial rotation as (-X, Y, -Z).
            // This conjugation preserves composition, including combined turns.
            var view = invertRoll ? new Rotation(-pose.X, pose.Y, -pose.Z, pose.W) : pose;
            return invertPitch != invertRoll ? InvertPitch(view) : view;
        }

        // Reflect only elevation of the view's forward ray. Pre-multiplying a
        // correction around its horizontal right axis preserves heading and roll.
        // This is an optical view adjustment, not a change of sensor coordinates.
        public static Rotation InvertPitch(Rotation pose)
        {
            float fx = 2 * (pose.X * pose.Z + pose.W * pose.Y);
            float fy = 2 * (pose.Y * pose.Z - pose.W * pose.X);
            float fz = 1 - 2 * (pose.X * pose.X + pose.Y * pose.Y);
            double horizontal = Math.Sqrt(fx * fx + fz * fz);
            double heading = horizontal > 0.000001 ? Math.Atan2(fx, fz) :
                Math.Atan2(-2 * (pose.X * pose.Z - pose.W * pose.Y),
                    1 - 2 * (pose.Y * pose.Y + pose.Z * pose.Z));
            double elevation = Math.Atan2(-fy, horizontal);
            var correction = new Rotation(
                (float)(-Math.Sin(elevation) * Math.Cos(heading)), 0,
                (float)(Math.Sin(elevation) * Math.Sin(heading)), (float)Math.Cos(elevation));
            return correction * pose;
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
