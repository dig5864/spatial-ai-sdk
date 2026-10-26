#pragma once
#include <cstdint>

// Select a fixed set of VSyncs, including fractional ratios such as 60/90
// and 60/144. Late consumers cannot move the phase to an ineligible VSync.
inline bool SpatialCadenceEligible(int64_t sequence, int sourceMilliHz, int targetMilliHz) {
    if (sequence <= 0 || sourceMilliHz <= 0 || targetMilliHz <= 0 || targetMilliHz > sourceMilliHz) return false;
    return sequence * targetMilliHz / sourceMilliHz > (sequence - 1) * targetMilliHz / sourceMilliHz;
}
