using System;
using UnityEngine;

namespace SpatialAI.Unity
{
    /// <summary>RhinoX RGB intrinsics and RGB-to-VIO relation. Eye extrinsics require VIO/IMU calibration.</summary>
    [Serializable]
    public sealed class SpatialRgbCalibration
    {
        public string profileId;
        public string source;
        public string cameraId;
        public int imageWidth;
        public int imageHeight;
        public float fx, fy, cx, cy;
        public float[] distortion;
        public float[] rgbToVioRightRotationRowMajor;
        public float[] rgbToVioRightTranslationMetres;
        public bool eyeTransformComplete;

        public static SpatialRgbCalibration LoadRhinoXReference()
        {
            var text = Resources.Load<TextAsset>("SpatialAI/Profiles/rhinox-rgb-vio-reference");
            if (text == null) return null;
            var calibration = JsonUtility.FromJson<SpatialRgbCalibration>(text.text);
            return calibration != null && calibration.distortion != null &&
                calibration.distortion.Length >= 8 && calibration.fx > 0 && calibration.fy > 0
                ? calibration : null;
        }

        /// <summary>Invert OpenCV's 8-coefficient rational camera model with a checked Newton solve.</summary>
        public bool TryCameraRay(SpatialHandJoint joint, int frameWidth, int frameHeight,
            out Vector3 ray)
        {
            ray = Vector3.forward;
            if (joint == null || frameWidth != imageWidth || frameHeight != imageHeight ||
                distortion == null || distortion.Length < 8) return false;
            double xd = (joint.x * frameWidth - cx) / fx;
            double yd = (joint.y * frameHeight - cy) / fy;
            double x = xd, y = yd;
            for (var iteration = 0; iteration < 30; iteration++)
            {
                var r2 = x * x + y * y;
                var r4 = r2 * r2;
                var r6 = r4 * r2;
                var numerator = 1 + distortion[0] * r2 + distortion[1] * r4 + distortion[4] * r6;
                var denominator = 1 + distortion[5] * r2 + distortion[6] * r4 + distortion[7] * r6;
                if (Math.Abs(denominator) < 1e-8) return false;
                var radial = numerator / denominator;
                var deltaX = 2 * distortion[2] * x * y + distortion[3] * (r2 + 2 * x * x);
                var deltaY = distortion[2] * (r2 + 2 * y * y) + 2 * distortion[3] * x * y;
                var ex = x * radial + deltaX - xd;
                var ey = y * radial + deltaY - yd;
                var error = ex * ex + ey * ey;
                if (error < 1e-14) break;
                var numeratorPrime = distortion[0] + 2 * distortion[1] * r2 + 3 * distortion[4] * r4;
                var denominatorPrime = distortion[5] + 2 * distortion[6] * r2 + 3 * distortion[7] * r4;
                var radialPrime = (numeratorPrime * denominator - numerator * denominatorPrime) /
                    (denominator * denominator);
                var jxx = radial + 2 * x * x * radialPrime + 2 * distortion[2] * y + 6 * distortion[3] * x;
                var jxy = 2 * x * y * radialPrime + 2 * distortion[2] * x + 2 * distortion[3] * y;
                var jyy = radial + 2 * y * y * radialPrime + 6 * distortion[2] * y + 2 * distortion[3] * x;
                var determinant = jxx * jyy - jxy * jxy;
                if (Math.Abs(determinant) < 1e-10) return false;
                var stepX = (jyy * ex - jxy * ey) / determinant;
                var stepY = (jxx * ey - jxy * ex) / determinant;
                var accepted = false;
                for (var damping = 1.0; damping >= 1.0 / 128.0; damping *= 0.5)
                {
                    var nextX = x - damping * stepX;
                    var nextY = y - damping * stepY;
                    var nextR2 = nextX * nextX + nextY * nextY;
                    var nextR4 = nextR2 * nextR2;
                    var nextR6 = nextR4 * nextR2;
                    var nextDenominator = 1 + distortion[5] * nextR2 + distortion[6] * nextR4 + distortion[7] * nextR6;
                    if (Math.Abs(nextDenominator) < 1e-8) continue;
                    var nextRadial = (1 + distortion[0] * nextR2 + distortion[1] * nextR4 + distortion[4] * nextR6) / nextDenominator;
                    var nextEx = nextX * nextRadial + 2 * distortion[2] * nextX * nextY +
                        distortion[3] * (nextR2 + 2 * nextX * nextX) - xd;
                    var nextEy = nextY * nextRadial + distortion[2] * (nextR2 + 2 * nextY * nextY) +
                        2 * distortion[3] * nextX * nextY - yd;
                    if (nextEx * nextEx + nextEy * nextEy >= error) continue;
                    x = nextX;
                    y = nextY;
                    accepted = true;
                    break;
                }
                if (!accepted) return false;
            }
            if (double.IsNaN(x) || double.IsNaN(y) || Math.Abs(x) > 4 || Math.Abs(y) > 4)
                return false;
            var finalR2 = x * x + y * y;
            var finalR4 = finalR2 * finalR2;
            var finalR6 = finalR4 * finalR2;
            var finalDenominator = 1 + distortion[5] * finalR2 + distortion[6] * finalR4 + distortion[7] * finalR6;
            if (Math.Abs(finalDenominator) < 1e-8) return false;
            var finalRadial = (1 + distortion[0] * finalR2 + distortion[1] * finalR4 + distortion[4] * finalR6) / finalDenominator;
            var projectedX = x * finalRadial + 2 * distortion[2] * x * y +
                distortion[3] * (finalR2 + 2 * x * x);
            var projectedY = y * finalRadial + distortion[2] * (finalR2 + 2 * y * y) +
                2 * distortion[3] * x * y;
            if (Math.Abs(projectedX - xd) * fx > 0.1 || Math.Abs(projectedY - yd) * fy > 0.1)
                return false;
            // OpenCV camera: right/down/forward. Unity eye-local: right/up/forward.
            ray = new Vector3((float)x, (float)-y, 1f);
            return true;
        }
    }
}
