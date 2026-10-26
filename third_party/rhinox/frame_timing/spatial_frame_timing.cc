#include <jni.h>
#include <android/log.h>
#include <android/trace.h>
#include <time.h>
#include <mutex>
#include <condition_variable>
#include <chrono>
#include <cstdio>
#include <cstring>
#include "pose_timing.h"
#include "pose_diagnostic.h"
#include "frame_cadence.h"
extern "C" int CardboardDebug_getLastPoseDiagnostic(SpatialPoseDiagnostic*,int);
namespace {
int64_t Clock(clockid_t id){timespec t{};clock_gettime(id,&t);return int64_t(t.tv_sec)*1000000000LL+t.tv_nsec;}
struct Timeline {int64_t callback=0,expected=0,deadline=0,id=-1,sequence=0;int generation=0;};
std::mutex mutex;
std::condition_variable cadenceChanged;
int cadenceDivisor=0;
int cadenceSourceMilliHz=0,cadenceTargetMilliHz=0;
int64_t cadenceConsumed=-1,cadenceReport=0;
int cadenceFrames=0,cadenceTimeouts=0;
Timeline latest;
bool enabled=false;
FILE* csv=nullptr;
int64_t started=0,report=0,rows=0;
int count=0,expired=0,fallback=0;
double sum=0,minimum=1e9,maximum=0;
thread_local int phase=-1;
thread_local struct Query {
    Timeline frame;int64_t mono=0,boot=0,target=0,start=0;const char* source="fixed";
} query;
}
// Unity's XR provider supports cadence waits in PopulateNextFrameDesc.
// Disabled for FollowDisplay; enabled by the SDK Locked60 policy.
extern "C" void CardboardUnity_waitForFrameCadence() {
    std::unique_lock<std::mutex> lock(mutex);
    if(!enabled || cadenceTargetMilliHz==0)return;
    ATrace_beginSection("SpatialUnity.waitFrameCadence");
    bool ready=cadenceChanged.wait_for(lock,std::chrono::milliseconds(60),[]{
        return !enabled || cadenceTargetMilliHz==0 ||
            (SpatialCadenceEligible(latest.sequence,cadenceSourceMilliHz,cadenceTargetMilliHz) && latest.sequence>cadenceConsumed);
    });
    ATrace_endSection();
    if(!enabled || cadenceTargetMilliHz==0)return;
    if(ready){cadenceConsumed=latest.sequence;cadenceFrames++;}else cadenceTimeouts++;
    const int64_t now=Clock(CLOCK_MONOTONIC);
    if(now-cadenceReport>=5000000000LL){
        __android_log_print(ANDROID_LOG_INFO,"SpatialUnityTiming",
            "CADENCE_WINDOW divisor=%d sourceMilliHz=%d targetMilliHz=%d frames=%d timeouts=%d backend=xr-populate",
            cadenceDivisor,cadenceSourceMilliHz,cadenceTargetMilliHz,cadenceFrames,cadenceTimeouts);
        cadenceReport=now;cadenceFrames=cadenceTimeouts=0;
    }
}
extern "C" void CardboardUnity_setPoseUpdatePhase(int updatePhase){phase=updatePhase;}
extern "C" int64_t CardboardUnity_getDisplayTimedPoseTarget() {
    Timeline frame;bool active;
    {std::lock_guard<std::mutex> lock(mutex);frame=latest;active=enabled;}
    const int64_t before=Clock(CLOCK_MONOTONIC),boot=Clock(CLOCK_BOOTTIME),after=Clock(CLOCK_MONOTONIC);
    const int64_t mono=before+(after-before)/2;
    const auto timing=selectPoseTiming(active,50,mono,boot,frame.callback,frame.expected,frame.deadline);
    query={frame,mono,boot,timing.targetBoot,after,timing.source};
    ATrace_beginSection("SpatialUnity.getPose");
    return timing.targetBoot;
}
extern "C" void CardboardUnity_recordDisplayTimedPose() {
    const int64_t end=Clock(CLOCK_MONOTONIC);
    ATrace_endSection();
    SpatialPoseDiagnostic d{};
    bool valid=CardboardDebug_getLastPoseDiagnostic(&d,sizeof(d)) && d.targetBoot==query.target && d.sampleBoot>0;
    std::lock_guard<std::mutex> lock(mutex);
    if(!enabled)return;
    const double horizon=(query.target-query.boot)/1e6;
    count++;sum+=horizon;minimum=minimum<horizon?minimum:horizon;maximum=maximum>horizon?maximum:horizon;
    expired+=std::strcmp(query.source,"expired_clamp_zero")==0;
    fallback+=std::strncmp(query.source,"fallback_",9)==0;
    if(csv){
        std::fprintf(csv,"%lld,%d,%lld,%lld,%lld,%lld,%lld,%lld,%d,%s,%.6f,%lld,%lld,%lld,%d,%.6f,%.6f,%.6f,%d\n",
            (long long)++rows,phase,(long long)query.mono,(long long)query.boot,(long long)query.target,
            (long long)query.frame.expected,(long long)query.frame.deadline,(long long)query.frame.sequence,
            query.frame.generation,query.source,horizon,(long long)d.sampleBoot,(long long)d.fusionReceiveBoot,
            (long long)d.queryBoot,valid?1:0,(end-query.start)/1e6,
            valid?(d.queryBoot-d.sampleBoot)/1e6:-1.,valid?(query.target-d.sampleBoot)/1e6:-1.,
            query.mono-started<5000000000LL?1:0);
        if(rows%120==0)std::fflush(csv);
    }
    if(end-report>=5000000000LL){
        __android_log_print(ANDROID_LOG_INFO,"SpatialUnityTiming",
            "POSE_WINDOW n=%d phase=%d horizonMeanMs=%.3f minMs=%.3f maxMs=%.3f expired=%d fallback=%d sampleAgeMs=%.3f expectedIsActual=false",
            count,phase,sum/count,minimum,maximum,expired,fallback,valid?(d.queryBoot-d.sampleBoot)/1e6:-1.);
        report=end;count=expired=fallback=0;sum=0;minimum=1e9;maximum=0;if(csv)std::fflush(csv);
    }
}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_cardboard_SpatialFrameTiming_nativeStart(JNIEnv* env,jclass,jstring path){
    const char* filename=env->GetStringUTFChars(path,nullptr);
    std::lock_guard<std::mutex> lock(mutex);
    if(csv)std::fclose(csv);csv=std::fopen(filename,"a");env->ReleaseStringUTFChars(path,filename);
    enabled=true;latest={};cadenceDivisor=0;cadenceSourceMilliHz=cadenceTargetMilliHz=0;cadenceConsumed=-1;
    started=report=cadenceReport=Clock(CLOCK_MONOTONIC);cadenceFrames=cadenceTimeouts=0;
    count=expired=fallback=0;sum=0;minimum=1e9;maximum=0;
    if(csv){std::setvbuf(csv,nullptr,_IOFBF,65536);std::fseek(csv,0,SEEK_END);
        if(std::ftell(csv)==0)std::fprintf(csv,"sequence,update_phase,query_mono_ns,query_boot_ns,target_boot_ns,expected_present_mono_ns,deadline_mono_ns,request_sequence,refresh_generation,prediction_source,effective_prediction_ms,sample_boot_ns,fusion_receive_boot_ns,sdk_query_boot_ns,snapshot_valid,pose_cpu_ms,sample_age_ms,total_extrapolation_ms,warmup\n");}
    __android_log_print(ANDROID_LOG_INFO,"SpatialUnityTiming","CONFIG revision=unity-native-parity-v1 prediction=display-time fixedFallbackMs=50 csv=%d",csv!=nullptr);
}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_cardboard_SpatialFrameTiming_nativeStop(JNIEnv*,jclass){
    std::lock_guard<std::mutex> lock(mutex);enabled=false;latest={};cadenceDivisor=0;cadenceSourceMilliHz=cadenceTargetMilliHz=0;
    cadenceChanged.notify_all();if(csv){std::fclose(csv);csv=nullptr;}
}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_cardboard_SpatialFrameTiming_nativeInvalidate(JNIEnv*,jclass){
    std::lock_guard<std::mutex> lock(mutex);latest={};cadenceConsumed=-1;cadenceChanged.notify_all();
}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_cardboard_SpatialFrameTiming_nativeSetCadence(JNIEnv*,jclass,jint divisor){
    std::lock_guard<std::mutex> lock(mutex);
    cadenceDivisor=divisor>0&&divisor<=16?divisor:0;cadenceConsumed=-1;
    cadenceSourceMilliHz=cadenceDivisor*60000;cadenceTargetMilliHz=cadenceDivisor>0?60000:0;
    cadenceChanged.notify_all();
}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_cardboard_SpatialFrameTiming_nativeConfigureCadence(JNIEnv*,jclass,jint source,jint target){
    std::lock_guard<std::mutex> lock(mutex);
    bool valid=source>0 && source<=1000000 && target>0 && target<=source;
    cadenceSourceMilliHz=valid?source:0;cadenceTargetMilliHz=valid?target:0;
    cadenceDivisor=valid && source%target==0?source/target:0;cadenceConsumed=-1;
    cadenceChanged.notify_all();
}
extern "C" JNIEXPORT void JNICALL Java_com_spatialai_cardboard_SpatialFrameTiming_nativePublish(JNIEnv*,jclass,jlong callback,jlong expected,jlong deadline,jlong id,jlong sequence,jint generation){
    std::lock_guard<std::mutex> lock(mutex);
    if(enabled){latest={callback,expected,deadline,id,sequence,generation};cadenceChanged.notify_all();}
}
