"""Build the native-demo timing policy into the Unity Cardboard plugin, preserving the shared SDK checkout."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import zipfile
from PrepareNativeImuDiagnostics import prepare

ROOT=Path(__file__).resolve().parents[1]
BUILD=ROOT/'build/native/unity-imu-timing'
PLAYER=Path('D:/unity/hub/editor/2022.3.14f1/Editor/Data/PlaybackEngines/AndroidPlayer')
SDK=ROOT/'third_party/checkouts/cardboard-sdk-v1.30.1'
AAR=ROOT/'third_party/checkouts/cardboard-xr-plugin-unity2022/Runtime/Android/GfxPluginCardboard.aar'
BASELINE='cf4c49318f366a12ba8f665dc2f1ae3f7fc5f91299f0517f4d9920cfed865dd4'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--install',action='store_true');args=parser.parse_args()
    BUILD.mkdir(parents=True,exist_ok=True)
    baseline=BUILD/'baseline-cardboard.aar'
    if not baseline.exists():
        with zipfile.ZipFile(AAR) as z:
            if hashlib.sha256(z.read('jni/arm64-v8a/libGfxPluginCardboard.so')).hexdigest()!=BASELINE:
                raise RuntimeError('Initial Unity baseline SDK differs from the validated original')
        shutil.copy2(AAR,baseline)
    source=BUILD/'diagnostic-sdk-source/sdk'
    source.mkdir(parents=True,exist_ok=True)
    shutil.copy2(ROOT/'third_party/rhinox/frame_timing/frame_cadence.h',source/'frame_cadence.h')
    diagnostic_aar,record=prepare(PLAYER/'NDK',BUILD,SDK,baseline)
    shutil.copy2(ROOT/'samples/android/cardboard-imu-comparison/pose_timing.h',source/'pose_timing.h')
    shutil.copy2(ROOT/'third_party/rhinox/frame_timing/spatial_frame_timing.cc',source/'spatial_frame_timing.cc')
    def replace(relative,old,new):
        path=source/relative;text=path.read_text()
        if text.count(old)!=1:raise RuntimeError('Missing unique Unity timing patch site: '+relative)
        path.write_text(text.replace(old,new))
    replace('cardboard_api.lds','    Cardboard*;','    Cardboard*;\n    Java_com_spatialai_cardboard_SpatialFrameTiming_*;')
    replace('unity/xr_unity_plugin/cardboard_input_api.cc','#include <cstdint>',
            '#include <cstdint>\nextern "C" int64_t CardboardUnity_getDisplayTimedPoseTarget();\nextern "C" void CardboardUnity_recordDisplayTimedPose();')
    replace('unity/xr_unity_plugin/cardboard_input_api.cc','GetBootTimeNano() + kPredictionTimeWithoutVsyncNanos','CardboardUnity_getDisplayTimedPoseTarget()')
    replace('unity/xr_unity_plugin/cardboard_input_api.cc','      selected_viewport_orientation_, position, orientation);',
            '      selected_viewport_orientation_, position, orientation);\n  CardboardUnity_recordDisplayTimedPose();')
    replace('unity/xr_provider/input.cc','#include <time.h>','#include <time.h>\nextern "C" void CardboardUnity_setPoseUpdatePhase(int);')
    replace('unity/xr_provider/input.cc','UnityXRInputUpdateType) {\n      return GetInstance()->Tick();',
            'UnityXRInputUpdateType phase) {\n      CardboardUnity_setPoseUpdatePhase(static_cast<int>(phase));\n      return GetInstance()->Tick();')
    replace('unity/xr_provider/display.cc','#include <array>',
            '#include <array>\nextern "C" void CardboardUnity_waitForFrameCadence();')
    replace('unity/xr_provider/display.cc','    // Allocate new color texture descriptors if needed and update device',
            '    CardboardUnity_waitForFrameCadence();\n    // Allocate new color texture descriptors if needed and update device')
    subprocess.run(['cmake','-S',str(source),'-B',str(BUILD/'diagnostic-sdk-build')],check=True)
    subprocess.run(['cmake','--build',str(BUILD/'diagnostic-sdk-build'),'--parallel','4'],check=True,timeout=300)
    library=BUILD/'diagnostic-sdk-build/libGfxPluginCardboard.so'
    result=BUILD/'GfxPluginCardboard-timed.aar'
    with zipfile.ZipFile(baseline) as original,zipfile.ZipFile(result,'w') as output:
        for info in original.infolist():output.writestr(info,library.read_bytes() if info.filename=='jni/arm64-v8a/libGfxPluginCardboard.so' else original.read(info))
    classes=BUILD/'java-classes';classes.mkdir(exist_ok=True)
    java=ROOT/'packages/com.spatialai.sdk/Runtime/Cardboard/Android~/SpatialFrameTiming.java'
    policy=java.parent/'FrameRatePolicy.java'
    subprocess.run([str(PLAYER/'OpenJDK/bin/javac.exe'),'-source','8','-target','8','-classpath','D:/android_sdk/platforms/android-34/android.jar','-d',str(classes),str(java),str(policy)],check=True)
    jar=BUILD/'classes.jar'
    subprocess.run([str(PLAYER/'OpenJDK/bin/jar.exe'),'cf',str(jar),'-C',str(classes),'.'],check=True)
    helper=BUILD/'spatial-frame-timing.aar'
    with zipfile.ZipFile(helper,'w') as z:
        z.write(jar,'classes.jar');z.writestr('AndroidManifest.xml','<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.spatialai.cardboard.timing"><uses-sdk android:minSdkVersion="28"/></manifest>')
        z.writestr('proguard.txt','-keep class com.spatialai.cardboard.SpatialFrameTiming { *; }\n-keep class com.spatialai.cardboard.SpatialFrameTiming$* { *; }\n')
    metadata={'revision':'unity-native-parity-v1','baselineNativeSha256':BASELINE,'nativeSha256':sha(library),
              'aarSha256':sha(result),'helperSha256':sha(helper),'diagnosticInstrumentation':record,
              'sourceSha256':{'java':sha(java),'timing':sha(source/'spatial_frame_timing.cc'),'policy':sha(source/'pose_timing.h'),
                             'frameRatePolicy':sha(policy),'cadence':sha(source/'frame_cadence.h')}}
    (BUILD/'build-info.json').write_text(json.dumps(metadata,indent=2))
    if args.install:
        shutil.copy2(result,AAR)
        shutil.copy2(helper,ROOT/'packages/com.spatialai.sdk/Plugins/Android/spatial-frame-timing.aar')
    print(json.dumps(metadata,indent=2));print('Installed:',args.install)
if __name__=='__main__':main()
