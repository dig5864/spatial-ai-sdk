#include <jni.h>
#include <cmath>
#include <memory>
#include "sensors/sensor_fusion_ekf.h"

// Diagnostic only: every JNI call is serialized on the Activity main thread.
// Uses unmodified, pinned EKF sources. Output is Start -> Sensor rotation,
// NOT the HeadTracker viewport transform or a Unity/RhinoX calibrated pose.
static std::unique_ptr<cardboard::SensorFusionEkf> fusion;
static uint64_t last_accel = 0, last_gyro = 0;
extern "C" JNIEXPORT void JNICALL
Java_com_spatialai_diagnostics_imu_MainActivity_resetFusion(JNIEnv*, jclass) {
    fusion.reset(new cardboard::SensorFusionEkf());
    last_accel = last_gyro = 0;
}
extern "C" JNIEXPORT void JNICALL
Java_com_spatialai_diagnostics_imu_MainActivity_releaseFusion(JNIEnv*, jclass) {
    fusion.reset();
    last_accel = last_gyro = 0;
}
extern "C" JNIEXPORT jboolean JNICALL
Java_com_spatialai_diagnostics_imu_MainActivity_feedFusion(JNIEnv*, jclass, jint type, jlong timestamp, jfloat x, jfloat y, jfloat z) {
    if (!fusion || timestamp <= 0 || !std::isfinite(x) || !std::isfinite(y) || !std::isfinite(z)) return JNI_FALSE;
    const auto t = static_cast<uint64_t>(timestamp);
    const cardboard::Vector3 value(x, y, z);
    if (type == 1 && t > last_accel) {
        if (x*x+y*y+z*z < 0.01f) return JNI_FALSE;
        fusion->ProcessAccelerometerSample({t, t, value});
        last_accel = t;
    } else if (type == 16 && last_accel != 0 && t > last_gyro) {
        fusion->ProcessGyroscopeSample({t, t, value});
        last_gyro = t;
    } else return JNI_FALSE;
    return JNI_TRUE;
}
extern "C" JNIEXPORT jfloatArray JNICALL
Java_com_spatialai_diagnostics_imu_MainActivity_readFusion(JNIEnv* env, jclass) {
    // No extrapolation for this acquisition baseline; use latest EKF state.
    if (!fusion || !last_accel || !last_gyro) return nullptr;
    auto q = fusion->PredictRotation(0).GetQuaternion();
    float out[4] = {static_cast<float>(q[0]), static_cast<float>(q[1]), static_cast<float>(q[2]), static_cast<float>(q[3])};
    auto result = env->NewFloatArray(4);
    if (result) env->SetFloatArrayRegion(result, 0, 4, out);
    return result;
}
