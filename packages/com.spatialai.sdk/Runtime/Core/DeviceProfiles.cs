using System;
using System.Collections.Generic;

namespace SpatialAI.Core
{
    // The phone host identity and an external headset identity are distinct.
    public sealed class DeviceIdentity
    {
        public string Manufacturer;
        public string Model;
        public string DeviceCode;
        public string HeadsetId;

        public string DisplayName { get { return (Manufacturer + " " + Model).Trim() + " [" + DeviceCode + "]"; } }
    }

    [Serializable]
    public sealed class OpticalProfileDescriptor
    {
        public int schemaVersion;
        public string profileId;
        public string deviceModel;
        public string[] deviceIds;
        public string calibrationVersion;
        public bool validatedForHardware;
        public int eyeWidth;
        public int eyeHeight;
        public int screenWidth;
        public int screenHeight;
        public float ipdMetres;
        public float frustumNearMetres;
        public float frustumLeftMetres;
        public float frustumRightMetres;
        public float frustumTopMetres;
        public float frustumBottomMetres;
        public string meshId;
        public string meshAssetHash;
        public TrackingCalibrationDescriptor trackingCalibration;

        // A historical reference may be usable for development without claiming
        // that its per-unit optical calibration has been verified for release.
        public Result ValidateDisplayConfiguration()
        {
            if (schemaVersion != 1 || string.IsNullOrEmpty(profileId) ||
                string.IsNullOrEmpty(deviceModel) || string.IsNullOrEmpty(meshId) ||
                string.IsNullOrEmpty(meshAssetHash) || screenWidth <= 0 ||
                screenHeight <= 0 || eyeWidth <= 0 || eyeHeight <= 0 ||
                eyeWidth * 2 != screenWidth || eyeHeight != screenHeight ||
                !FinitePositive(ipdMetres) || !FinitePositive(frustumNearMetres) ||
                !FinitePositive(frustumRightMetres) || !FinitePositive(frustumTopMetres) ||
                !FiniteNegative(frustumLeftMetres) || !FiniteNegative(frustumBottomMetres))
                return new Result(ErrorCode.InvalidArgument, "Incomplete optical display configuration.");
            return Result.Ok;
        }

        private static bool FinitePositive(float value) { return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0; }
        private static bool FiniteNegative(float value) { return !float.IsNaN(value) && !float.IsInfinity(value) && value < 0; }

        public OpticsProfile ToOpticsProfile()
        {
            return new OpticsProfile
            {
                SchemaVersion = schemaVersion, ProfileId = profileId, DeviceModel = deviceModel,
                CalibrationVersion = calibrationVersion, ValidatedForHardware = validatedForHardware,
                EyeWidth = eyeWidth, EyeHeight = eyeHeight, IpdMetres = ipdMetres,
                MeshAssetHash = meshAssetHash
            };
        }
    }

    [Serializable]
    public sealed class TrackingCalibrationDescriptor
    {
        public string source;
        public string calibrationVersion;
        public bool validatedForHardware;
        public float eyeRenderToDesignDegreesX;
        public float eyeRenderToDesignYOffsetMetres;
        public float designEyeToImuXMetres;
        public float designEyeToImuYMetres;
        public float designEyeToImuZMetres;
        public string poseFrame;
        public bool axisMappingVerified;
        public float designImuFromCardboardHeadX;
        public float designImuFromCardboardHeadY;
        public float designImuFromCardboardHeadZ;
        public float designImuFromCardboardHeadW;

        public Result ValidateDesignReference()
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(calibrationVersion) ||
                !Finite(eyeRenderToDesignDegreesX) || Math.Abs(eyeRenderToDesignDegreesX) > 45 ||
                !Bounded(eyeRenderToDesignYOffsetMetres) ||
                !Bounded(designEyeToImuXMetres) || !Bounded(designEyeToImuYMetres) ||
                !Bounded(designEyeToImuZMetres) ||
                poseFrame != "cardboard-unity-landscape-left" ||
                !Finite(designImuFromCardboardHeadX) ||
                !Finite(designImuFromCardboardHeadY) ||
                !Finite(designImuFromCardboardHeadZ) ||
                !Finite(designImuFromCardboardHeadW) ||
                Math.Abs(designImuFromCardboardHeadX * designImuFromCardboardHeadX +
                    designImuFromCardboardHeadY * designImuFromCardboardHeadY +
                    designImuFromCardboardHeadZ * designImuFromCardboardHeadZ +
                    designImuFromCardboardHeadW * designImuFromCardboardHeadW - 1) > 0.01f)
                return new Result(ErrorCode.InvalidArgument, "Incomplete IMU-to-eye design calibration.");
            return Result.Ok;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Bounded(float value) { return Finite(value) && Math.Abs(value) < 0.5f; }
    }

    public static class DeviceProfileSelector
    {
        // Exact, case-insensitive IDs only. An explicit headset ID takes precedence over
        // host device code / model; unknown and ambiguous devices fail closed.
        public static Result Select(DeviceIdentity identity, IEnumerable<OpticalProfileDescriptor> profiles,
            out OpticalProfileDescriptor selected)
        {
            selected = null;
            if (identity == null || profiles == null)
                return new Result(ErrorCode.InvalidArgument, "Device identity or profile catalog is missing.");
            var id = !string.IsNullOrWhiteSpace(identity.HeadsetId) ? identity.HeadsetId :
                !string.IsNullOrWhiteSpace(identity.DeviceCode) ? identity.DeviceCode : identity.Model;
            if (string.IsNullOrWhiteSpace(id))
                return new Result(ErrorCode.InvalidArgument, "No device ID is available.");
            foreach (var profile in profiles)
            {
                if (profile == null || profile.deviceIds == null) continue;
                bool match = false;
                foreach (var candidate in profile.deviceIds)
                    if (string.Equals(candidate == null ? "" : candidate.Trim(), id.Trim(), StringComparison.OrdinalIgnoreCase))
                    { match = true; break; }
                if (!match) continue;
                if (selected != null)
                {
                    selected = null;
                    return new Result(ErrorCode.InvalidArgument, "Multiple optical profiles match device ID " + id + ".");
                }
                selected = profile;
            }
            if (selected == null)
                return new Result(ErrorCode.Unsupported, "No optical profile matches device ID " + id + ".");
            var validation = selected.ToOpticsProfile().Validate();
            return validation.Success ? Result.Ok : new Result(validation.Code,
                "Profile " + selected.profileId + " matched " + id + " but cannot drive hardware: " + validation.Message);
        }
    }
}
