#pragma once
#include <stdint.h>
// Copied to the diagnostic SDK staging tree. Same-thread snapshot of the exact
// EKF state used by PredictRotation, not a second sensor listener/query.
struct SpatialPoseDiagnostic {
    int64_t version, targetBoot, queryBoot, sampleBoot, fusionReceiveBoot;
    double velocity[3], baseQuaternion[4], predictedQuaternion[4];
};
