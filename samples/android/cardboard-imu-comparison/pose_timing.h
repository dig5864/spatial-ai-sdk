#pragma once
#include <stdint.h>

// Android FrameTimeline uses MONOTONIC; Cardboard uses BOOTTIME. Translate
// relative to a fresh, paired clock sample instead of retaining a suspend offset.
struct PoseTiming {
    int64_t targetBoot;
    const char* source;
};
inline PoseTiming selectPoseTiming(bool displayTime, int fixedMs, int64_t queryMono,
                                  int64_t queryBoot, int64_t callback,
                                  int64_t expected, int64_t deadline) {
    const int64_t fixedTarget=queryBoot+int64_t(fixedMs)*1000000;
    if(!displayTime) return {fixedTarget,"fixed"};
    if(callback<=0 || expected<=0 || deadline<=0)
        return {fixedTarget,"fallback_missing"};
    if(expected<=callback || deadline<callback || deadline>expected ||
       expected-callback>100000000 || expected-queryMono>100000000 || queryMono<callback)
        return {fixedTarget,"fallback_invalid"};
    // Never query an already expired pose or invent a later display timeline.
    if(expected<=queryMono) return {queryBoot,"expired_clamp_zero"};
    return {queryBoot+(expected-queryMono),"display_time"};
}
