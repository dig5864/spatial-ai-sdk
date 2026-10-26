"""Build an isolated, instrumented copy of the pinned Cardboard SDK for native trials."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile

ROOT=Path(__file__).resolve().parents[1]

def prepare(ndk, build, cardboard, aar):
    staging=build/'diagnostic-sdk-source'
    # Only copy into this dedicated generated tree; leave the shared SDK/AAR intact.
    for folder in ['sdk','third_party/unity_plugin_api']:
        shutil.copytree(cardboard/folder,staging/folder,dirs_exist_ok=True)
    header=ROOT/'samples/android/cardboard-imu-comparison/pose_diagnostic.h'
    shutil.copy2(header,staging/'sdk/pose_diagnostic.h')
    def replace(path,old,new):
        file=staging/path
        text=file.read_text(encoding='utf-8')
        if text.count(old)!=1: raise RuntimeError(f'Expected unique instrumentation site: {path}: {old[:60]}')
        file.write_text(text.replace(old,new),encoding='utf-8')
    replace('sdk/sensors/sensor_fusion_ekf.h','  RotationState current_state_;',
            '  RotationState current_state_;\n  int64_t spatial_receive_boot_ns_ = 0;')
    cc='sdk/sensors/sensor_fusion_ekf.cc'
    replace(cc,'#include <cmath>','''#include <cmath>
#include <time.h>
#include <cstring>
#include "pose_diagnostic.h"
namespace {
thread_local SpatialPoseDiagnostic spatial_diagnostic{};
int64_t SpatialBootNow() {
  timespec t{}; clock_gettime(CLOCK_BOOTTIME,&t);
  return int64_t(t.tv_sec)*1000000000LL+t.tv_nsec;
}
}
extern "C" __attribute__((visibility("default"))) int CardboardDebug_getLastPoseDiagnostic(
    SpatialPoseDiagnostic* out, int size) {
  if(!out || size!=sizeof(SpatialPoseDiagnostic)) return 0;
  *out=spatial_diagnostic; return out->version==1;
}''')
    replace(cc,'  return update * current_state_.sensor_from_start_rotation;', '''  const Rotation result=update * current_state_.sensor_from_start_rotation;
  spatial_diagnostic={}; spatial_diagnostic.version=1;
  spatial_diagnostic.targetBoot=requested_timestamp;
  spatial_diagnostic.queryBoot=SpatialBootNow();
  spatial_diagnostic.sampleBoot=current_state_.timestamp;
  spatial_diagnostic.fusionReceiveBoot=spatial_receive_boot_ns_;
  const auto base=current_state_.sensor_from_start_rotation.GetQuaternion();
  const auto predicted=result.GetQuaternion();
  for(int i=0;i<3;i++) spatial_diagnostic.velocity[i]=current_state_.sensor_from_start_rotation_velocity[i];
  for(int i=0;i<4;i++) {
    spatial_diagnostic.baseQuaternion[i]=base[i]; spatial_diagnostic.predictedQuaternion[i]=predicted[i];
  }
  return result;''')
    replace(cc,'void SensorFusionEkf::ProcessGyroscopeSample(const GyroscopeData& sample) {',
            'void SensorFusionEkf::ProcessGyroscopeSample(const GyroscopeData& sample) {\n  const int64_t received=SpatialBootNow();')
    replace(cc,'  current_state_.timestamp = sample.system_timestamp;',
            '  current_state_.timestamp = sample.system_timestamp;\n  spatial_receive_boot_ns_ = received;')
    native=build/'diagnostic-sdk-build'
    subprocess.run(['cmake','-S',str(staging/'sdk'),'-B',str(native),'-G','Ninja',
        '-DCMAKE_TOOLCHAIN_FILE='+str(ndk/'build/cmake/android.toolchain.cmake'),
        '-DANDROID_USE_LEGACY_TOOLCHAIN_FILE=ON','-DANDROID_ABI=arm64-v8a','-DANDROID_PLATFORM=android-26',
        '-DCARDBOARDSDK_RENDERING_VULKAN=ON'],check=True,timeout=120)
    subprocess.run(['cmake','--build',str(native),'--parallel','4'],check=True,timeout=300)
    library=(native/'libGfxPluginCardboard.so').read_bytes()
    output=build/'diagnostic-cardboard.aar'
    with zipfile.ZipFile(aar) as source, zipfile.ZipFile(output,'w') as dest:
        for info in source.infolist():
            dest.writestr(info,library if info.filename=='jni/arm64-v8a/libGfxPluginCardboard.so' else source.read(info))
    record={'nativeSha256':hashlib.sha256(library).hexdigest(),'instrumentation':'same-query EKF snapshot v1',
            'sourceSha256':{str(p.relative_to(staging)):hashlib.sha256(p.read_bytes()).hexdigest()
            for p in [staging/cc,staging/'sdk/sensors/sensor_fusion_ekf.h',staging/'sdk/pose_diagnostic.h']}}
    (build/'diagnostic-sdk.json').write_text(json.dumps(record,indent=2),encoding='utf-8')
    return output,record
