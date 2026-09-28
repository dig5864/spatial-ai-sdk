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
        public float ipdMetres;
        public string meshAssetHash;

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
