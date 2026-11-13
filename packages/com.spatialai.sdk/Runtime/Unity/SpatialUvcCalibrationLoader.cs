using System;
using SpatialAI.Core;
using UnityEngine;

namespace SpatialAI.Unity
{
    /// <summary>Reads SDK hand calibration or a VISOR-X exported calibration bundle.</summary>
    public static class SpatialUvcCalibrationLoader
    {
        [Serializable] private sealed class Bundle { public string format; public int version; public Intrinsics intrinsics; public Setup setup; public Alignment alignment; public float[] cameraMatrix; }
        [Serializable] private sealed class Intrinsics { public int width, height; public bool mirrored; public string source; public float[] cameraMatrix, distortion; }
        [Serializable] private sealed class Setup { public string calibrationId; public float[] cameraToHead; }
        [Serializable] private sealed class Alignment { public Pose pose; }
        [Serializable] private sealed class Pose { public float x, y, z, pitch, yaw, roll; }

        public static UvcHandCalibration Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Calibration JSON is empty.");
            var bundle = JsonUtility.FromJson<Bundle>(json);
            if (bundle == null) throw new ArgumentException("Invalid calibration JSON.");
            if (string.IsNullOrEmpty(bundle.format))
            {
                if ((bundle.cameraMatrix != null && bundle.cameraMatrix.Length > 0) ||
                    (bundle.intrinsics != null && bundle.intrinsics.cameraMatrix != null && bundle.intrinsics.cameraMatrix.Length > 0) ||
                    (bundle.setup != null && bundle.setup.cameraToHead != null && bundle.setup.cameraToHead.Length > 0))
                    throw new ArgumentException("Use a complete VISOR-X calibration bundle with format and version.");
                return JsonUtility.FromJson<UvcHandCalibration>(json);
            }
            if (bundle.format != "visorx-uvc-calibration" || bundle.version != 1)
                throw new ArgumentException("Unsupported calibration bundle format or version.");
            var i = bundle.intrinsics;
            var m = bundle.setup == null ? null : bundle.setup.cameraToHead;
            if (i == null || i.width <= 0 || i.height <= 0 || i.source != "uvc" || i.mirrored ||
                i.cameraMatrix == null || i.cameraMatrix.Length != 9 || i.distortion == null || i.distortion.Length != 5 ||
                m == null || m.Length != 16) throw new ArgumentException("Incomplete or unsupported VISOR-X UVC calibration.");
            foreach (float v in i.cameraMatrix) if (!Finite(v)) throw new ArgumentException("Invalid camera matrix.");
            foreach (float v in i.distortion) if (!Finite(v)) throw new ArgumentException("Invalid distortion.");
            foreach (float v in m) if (!Finite(v)) throw new ArgumentException("Invalid camera-to-head matrix.");
            var k = i.cameraMatrix;
            if (k[0] <= 0 || k[4] <= 0 || k[2] < 0 || k[2] > i.width || k[5] < 0 || k[5] > i.height ||
                Mathf.Abs(k[1]) > 1e-5f || Mathf.Abs(k[3]) > 1e-5f || Mathf.Abs(k[6]) > 1e-5f ||
                Mathf.Abs(k[7]) > 1e-5f || Mathf.Abs(k[8] - 1) > 1e-5f)
                throw new ArgumentException("Unsupported camera matrix.");
            if (Mathf.Abs(m[12]) > 1e-5f || Mathf.Abs(m[13]) > 1e-5f || Mathf.Abs(m[14]) > 1e-5f || Mathf.Abs(m[15]-1) > 1e-5f)
                throw new ArgumentException("Invalid homogeneous camera-to-head matrix.");
            // VISOR-X uses OpenCV camera coordinates and a right-handed head frame (forward -Z).
            // Unity uses camera and head frames with Y up and forward +Z: H * R * C.
            var x = new Vector3(m[0], m[4], -m[8]);
            var y = new Vector3(-m[1], -m[5], m[9]);
            var z = new Vector3(m[2], m[6], -m[10]);
            if (Mathf.Abs(x.sqrMagnitude-1)>0.001f || Mathf.Abs(y.sqrMagnitude-1)>0.001f ||
                Mathf.Abs(z.sqrMagnitude-1)>0.001f || Mathf.Abs(Vector3.Dot(x,y))>0.001f ||
                Mathf.Abs(Vector3.Dot(x,z))>0.001f || Mathf.Abs(Vector3.Dot(y,z))>0.001f ||
                Vector3.Dot(Vector3.Cross(x,y),z)<0.999f)
                throw new ArgumentException("Camera-to-head matrix is not a rigid rotation.");
            var q = Quaternion.LookRotation(z, y);
            var translation = new Vector3(m[3], m[7], -m[11]);
            // The calibrator applies alignment.pose AFTER cameraToHead in its
            // right-handed head frame. Convert its rotation and translation to
            // Unity with H=diag(1,1,-1), then compose H*A*cameraToHead*C.
            var pose = bundle.alignment == null ? null : bundle.alignment.pose;
            if (pose != null)
            {
                if (!Finite(pose.x) || !Finite(pose.y) || !Finite(pose.z) ||
                    !Finite(pose.pitch) || !Finite(pose.yaw) || !Finite(pose.roll) ||
                    Mathf.Abs(pose.x)>300 || Mathf.Abs(pose.y)>300 || Mathf.Abs(pose.z)>300 ||
                    Mathf.Abs(pose.pitch)>30 || Mathf.Abs(pose.yaw)>30 || Mathf.Abs(pose.roll)>180)
                    throw new ArgumentException("Invalid VISOR-X alignment pose.");
                // Three.js Euler XYZ means Rx * Ry * Rz; reflection across Z
                // maps an xyzw quaternion to (-x,-y,z,w).
                var web = Quaternion.AngleAxis(pose.pitch, Vector3.right) *
                    Quaternion.AngleAxis(pose.yaw, Vector3.up) * Quaternion.AngleAxis(pose.roll, Vector3.forward);
                var correction = new Quaternion(-web.x, -web.y, web.z, web.w);
                translation = correction * translation + new Vector3(pose.x * .001f, pose.y * .001f, -pose.z * .001f);
                q = correction * q;
            }
            return new UvcHandCalibration {
                calibrationVersion = string.IsNullOrEmpty(bundle.setup.calibrationId) ? "visorx-import-v1" : bundle.setup.calibrationId,
                source = "VISOR-X UVC calibration bundle", imageWidth = i.width, imageHeight = i.height,
                fx = k[0], fy = k[4], cx = k[2], cy = k[5], distortion = (float[])i.distortion.Clone(),
                inputMirrored = i.mirrored, imageRotationClockwise = 0,
                headFromCameraTranslationMetres = new[] { translation.x, translation.y, translation.z },
                headFromCameraRotationXyzw = new[] { q.x, q.y, q.z, q.w },
                intrinsicsCalibrated = true
            };
        }
        private static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v); }
    }
}
