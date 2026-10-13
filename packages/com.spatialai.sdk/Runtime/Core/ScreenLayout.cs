using System;

namespace SpatialAI.Core
{
    [Serializable]
    public sealed class PhoneScreenCalibration
    {
        public int schemaVersion = 1;
        public string phoneKey;
        // LandscapeLeft active display dimensions, excluding the bezel.
        public float widthMm, heightMm;
        public float offsetXmm, offsetYmm;
        public bool measured;
        public string savedUtc;
        public bool IsValid { get { return schemaVersion == 1 && !string.IsNullOrEmpty(phoneKey) &&
            ScreenLayout.Positive(widthMm) && ScreenLayout.Positive(heightMm) &&
            ScreenLayout.Finite(offsetXmm) && ScreenLayout.Finite(offsetYmm); } }
    }

    [Serializable]
    public sealed class ScreenRigProfile
    {
        public int schemaVersion = 1;
        public string rigId;
        // Left then right x,y,width,height in mm, relative to the reference
        // active display centre. Axes: right/down. Requires the same screen plane.
        public float[] eyeRectsMm;
        public bool rotateOutput180;
        public bool referenceScreenMeasured;
        public string referencePhoneKey;
        public bool opticsValidated;
        // Shared installation candidate; never declares a new phone verified.
        public HostImuProfile phoneImu;
        public bool invertViewPitch, invertViewRoll, reflectHandDisplayX, uvcSwapHandedness;
        public bool IsValid { get { return schemaVersion == 1 && !string.IsNullOrEmpty(rigId) &&
            ScreenLayout.ValidRects(eyeRectsMm); } }
    }

    public static class ScreenLayout
    {
        public static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public static bool Positive(float value) { return Finite(value) && value > 0; }
        public static bool ValidRects(float[] rects)
        {
            if (rects == null || rects.Length != 8) return false;
            for (int i = 0; i < 8; i++) if (!Finite(rects[i])) return false;
            return Positive(rects[2]) && Positive(rects[3]) && Positive(rects[6]) && Positive(rects[7]);
        }

        public static Result FromReference(float[] rects, int widthPx, int heightPx,
            PhoneScreenCalibration screen, string rigId, bool rotate180, out ScreenRigProfile rig)
        {
            rig = null;
            if (screen == null || !screen.IsValid || widthPx <= 0 || heightPx <= 0 || !ValidRects(rects))
                return new Result(ErrorCode.InvalidArgument, "Invalid reference screen or rectangles.");
            var mm = new float[8];
            for (int e = 0; e < 2; e++)
            {
                int i = 4 * e;
                mm[i] = (rects[i] / widthPx - .5f) * screen.widthMm + screen.offsetXmm;
                mm[i + 1] = (rects[i + 1] / heightPx - .5f) * screen.heightMm + screen.offsetYmm;
                mm[i + 2] = rects[i + 2] / widthPx * screen.widthMm;
                mm[i + 3] = rects[i + 3] / heightPx * screen.heightMm;
            }
            var candidate = new ScreenRigProfile { rigId = rigId, eyeRectsMm = mm,
                rotateOutput180 = rotate180, referenceScreenMeasured = screen.measured,
                referencePhoneKey = screen.phoneKey, opticsValidated = false };
            var result = Calculate(candidate, screen, widthPx, heightPx, out var ignored);
            if (result.Success) rig = candidate;
            return result;
        }

        public static Result Calculate(ScreenRigProfile rig, PhoneScreenCalibration screen,
            int widthPx, int heightPx, out float[] rects)
        {
            rects = null;
            if (rig == null || !rig.IsValid || screen == null || !screen.IsValid ||
                widthPx <= 0 || heightPx <= 0)
                return new Result(ErrorCode.InvalidArgument, "Screen dimensions or rig profile are missing/invalid.");
            float kx = widthPx / screen.widthMm, ky = heightPx / screen.heightMm;
            var output = new float[8];
            for (int e = 0; e < 2; e++)
            {
                int i = e * 4;
                output[i] = (rig.eyeRectsMm[i] - screen.offsetXmm + screen.widthMm / 2) * kx;
                output[i + 1] = (rig.eyeRectsMm[i + 1] - screen.offsetYmm + screen.heightMm / 2) * ky;
                output[i + 2] = rig.eyeRectsMm[i + 2] * kx;
                output[i + 3] = rig.eyeRectsMm[i + 3] * ky;
                // The current native renderer requires each eye inside its half.
                // Allow floating point roundoff, never clamp real geometry errors.
                const float tolerance = .001f;
                if (!Finite(output[i]) || !Finite(output[i + 1]) || !Positive(output[i + 2]) ||
                    !Positive(output[i + 3]) || output[i] < e * widthPx * .5f - tolerance ||
                    output[i + 1] < -tolerance || output[i] + output[i + 2] > (e + 1) * widthPx * .5f + tolerance ||
                    output[i + 1] + output[i + 3] > heightPx + tolerance)
                    return new Result(ErrorCode.Unsupported, "Eye region exceeds the display or its native half-screen. Check size/mount; do not shrink the rig.");
                output[i] = Math.Max(e * widthPx * .5f, output[i]);
                output[i + 1] = Math.Max(0, output[i + 1]);
                output[i + 2] = Math.Min(output[i + 2], (e + 1) * widthPx * .5f - output[i]);
                output[i + 3] = Math.Min(output[i + 3], heightPx - output[i + 1]);
            }
            rects = output;
            return Result.Ok;
        }
    }
}
