"""Offline native Cardboard comparator, pinned to the Unity comparison's SDK binary."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT/'samples/android/cardboard-imu-comparison'
BUILD = ROOT/'build/samples/android/cardboard-imu-comparison'
CARDBOARD = ROOT/'third_party/checkouts/cardboard-sdk-v1.30.1'
MESH_HASH = '9ce72f86ada58cf53ab0d440fd504c1197987918be09770f0852faea5512d969'
NATIVE_HASH = 'cf4c49318f366a12ba8f665dc2f1ae3f7fc5f91299f0517f4d9920cfed865dd4'

def digest(data): return hashlib.sha256(data).hexdigest()

def verify_apk(apk, configuration=None):
    with zipfile.ZipFile(apk) as archive:
        actual = archive.read('lib/arm64-v8a/libGfxPluginCardboard.so')
        if digest(actual)!=(configuration['nativeSha256'] if configuration else NATIVE_HASH): raise RuntimeError('Packaged SDK binary changed during packaging')
        if any(name.endswith('/libunity.so') for name in archive.namelist()):
            raise RuntimeError('Native comparator unexpectedly contains the Unity engine')
        packaged=json.loads(archive.read('assets/comparison-config.json'))
        if configuration is not None and packaged!=configuration: raise RuntimeError('Packaged configuration differs from build inputs')
    return {'apk':str(Path(apk).resolve()),'apkSha256':digest(Path(apk).read_bytes()),
            'nativeSdkSha256':digest(actual),'calibrationHash':packaged['calibrationHash'],
            'package':'com.spatialai.samples.cardboardnative','containsUnityEngine':False}

def build(args):
    sdk = Path(args.android_sdk)
    player = Path(args.android_player)
    jdk = player/'OpenJDK'
    ndk = player/'NDK'
    bt = sdk/'build-tools/34.0.0'
    android_jar = sdk/'platforms/android-34/android.jar'
    for path in [jdk/'bin/javac.exe', ndk/'build/cmake/android.toolchain.cmake', bt/'aapt.exe', android_jar]:
        if not path.exists(): raise RuntimeError(f'Missing build tool: {path}')
    subprocess.run(['git','-C',str(CARDBOARD),'rev-parse','HEAD'],check=True,stdout=subprocess.PIPE)
    commit = subprocess.check_output(['git','-C',str(CARDBOARD),'rev-parse','HEAD'],text=True).strip()
    if commit != '67bee10850562bf8cbe563a22b47eca8ffd8f1eb': raise RuntimeError('Unexpected Cardboard checkout')
    mesh = ROOT/'third_party/rhinox/spatial_rhinox_mesh_data.cc'
    if digest(mesh.read_bytes()) != MESH_HASH: raise RuntimeError('Reference mesh hash changed')
    aar = ROOT/'third_party/checkouts/cardboard-xr-plugin-unity2022/Runtime/Android/GfxPluginCardboard.aar'
    # Unity timing migration preserves the validated comparator SDK here.
    preserved = ROOT/'build/native/unity-imu-timing/baseline-cardboard.aar'
    if preserved.exists():
        aar = preserved
    with zipfile.ZipFile(aar) as z:
        native = z.read('jni/arm64-v8a/libGfxPluginCardboard.so')
        classes = z.read('classes.jar')
    if digest(native) != NATIVE_HASH: raise RuntimeError('SDK binary differs from the tested Unity baseline')
    diagnostic_record=None
    if args.diagnostics_sdk:
        from PrepareNativeImuDiagnostics import prepare
        BUILD.mkdir(parents=True,exist_ok=True)
        aar,diagnostic_record=prepare(ndk,BUILD,CARDBOARD,aar)
        with zipfile.ZipFile(aar) as diagnostic_aar:
            native=diagnostic_aar.read('jni/arm64-v8a/libGfxPluginCardboard.so')
    log = Path(args.unity_log).read_text(encoding='utf-8-sig')
    match = re.search(r'Spatial phone display: .*?2400x1080; rects=([0-9.,-]+)',log)
    baseline = re.search(r'signedBaselineMetres=(-?[0-9.]+)',log)
    if not match or not baseline: raise RuntimeError('Unity log must contain its applied phone rectangles and stereo baseline')
    rects = [float(n) for n in match.group(1).split(',')]
    if len(rects)!=8: raise RuntimeError('Expected two explicit eye rectangles')
    rig_path = ROOT/'samples/unity/SpatialXR/Assets/Resources/SpatialAI/ScreenRig.json'
    rig = json.loads(rig_path.read_text(encoding='utf-8-sig'))
    optics_path = ROOT/'packages/com.spatialai.sdk/Runtime/Unity/Resources/SpatialAI/Profiles/rhinox-pro-reference.json'
    optics = json.loads(optics_path.read_text(encoding='utf-8-sig'))
    imu = rig['phoneImu']
    if imu['invertInputRotation'] or imu['preserveNeckModel']:
        raise RuntimeError('This comparison expects the current phone 3DoF mapping without neck/invert-input')
    for token in ['configured=True','invertViewPitch=True','invertViewRoll=True','M=(0.3826834,0,0,0.9238795)']:
        if token not in log: raise RuntimeError(f'Unity tracking context missing: {token}')
    configuration = {'revision':'native-cardboard-diagnostics-v6','cardboardCommit':commit,'nativeSha256':digest(native),'baselineNativeSha256':NATIVE_HASH,'diagnostics':diagnostic_record,
        'meshSha256':MESH_HASH,'width':2400,'height':1080,'eyeRectsTopLeftPx':rects,
        'rotateEachEye180':rig['rotateOutput180'],'signedBaselineMetres':float(baseline.group(1)),
        'frustum':{k:optics[k] for k in ['frustumNearMetres','frustumLeftMetres','frustumRightMetres','frustumTopMetres','frustumBottomMetres']},
        'rig':rig,'preferredFps':60,'refreshPolicy':'prefer60; fallback displayHz/integerDivisor',
        'defaultPredictionMs':50,'predictionMsRange':[0,100],'unityLog':str(Path(args.unity_log).resolve()),
        'refreshPolicies':['prefer60','prefer120','follow-display'],'defaultPredictionMode':'fixed','predictionModes':['fixed','display-time'],
        'displayTimeMinimumApi':33,'expiredTimelinePolicy':'clamp-to-query-time',
        'unityLogSha256':digest(log.encode('utf-8')),'sceneSource':'samples/unity/SpatialXR/Assets/SpatialXR/ImuDiagnostics.cs',
        'sceneSourceSha256':digest((ROOT/'samples/unity/SpatialXR/Assets/SpatialXR/ImuDiagnostics.cs').read_bytes())}
    configuration['calibrationHash'] = digest(json.dumps(configuration,sort_keys=True).encode())
    for relative in ['stage/lib/arm64-v8a','stage/assets','generated','gradle/app/jniLibs/arm64-v8a']:
        (BUILD/relative).mkdir(parents=True,exist_ok=True)
    (BUILD/'stage/assets/comparison-config.json').write_text(json.dumps(configuration,indent=2),encoding='utf-8')
    (BUILD/'stage/assets/LICENSE.cardboard').write_bytes((CARDBOARD/'LICENSE').read_bytes())
    (BUILD/'stage/assets/UPSTREAM.txt').write_text('Cardboard '+commit+'; Apache-2.0. Same patched SDK binary as Unity; independent Java/NDK renderer.\n',encoding='utf-8')
    (BUILD/'cardboard-classes.jar').write_bytes(classes)
    (BUILD/'stage/lib/arm64-v8a/libGfxPluginCardboard.so').write_bytes(native)
    def number(n): return str(float(n))+'f'
    header = '#pragma once\nconstexpr int kWidth=2400,kHeight=1080;\n'
    header += 'constexpr float kEyeRects[]={'+','.join(map(number,rects))+'};\n'
    for name,key in [('kNear','frustumNearMetres'),('kFrustumLeft','frustumLeftMetres'),('kFrustumRight','frustumRightMetres'),('kTop','frustumTopMetres'),('kBottom','frustumBottomMetres')]:
        header += f'constexpr float {name}={number(optics[key])};\n'
    header += 'constexpr float kBaseline='+number(configuration['signedBaselineMetres'])+';\n'
    header += 'constexpr bool kRotate180='+str(rig['rotateOutput180']).lower()+';\n'
    header += 'constexpr int kInvertPitch='+str(int(rig['invertViewPitch']))+',kInvertRoll='+str(int(rig['invertViewRoll']))+';\n'
    for name,prefix in [('kBasis','basis'),('kMount','mount')]:
        header += 'constexpr float '+name+'[]={'+','.join(number(imu[prefix+c]) for c in 'XYZW')+'};\n'
    for name,value in [('kMeshHash',MESH_HASH),('kCalibrationHash',configuration['calibrationHash']),('kRectsText',','.join(map(str,rects)))]:
        header += 'constexpr char '+name+'[]='+json.dumps(value)+';\n'
    (BUILD/'generated/comparison_config.h').write_text(header,encoding='utf-8')
    env = os.environ.copy()
    env['JAVA_HOME']=str(jdk)
    env['PROCESSOR_ARCHITECTURE']='AMD64'
    env.setdefault('ALLUSERSPROFILE','C:/ProgramData')
    def run(exe,*arguments,cwd=None):
        print(Path(exe).name, ' '.join(map(str,arguments[:3])),flush=True)
        normalized=[value.as_posix() if isinstance(value,Path) else
                    str(value).replace('\\','/') if str(value).startswith('-D') else str(value) for value in arguments]
        subprocess.run([str(exe),*normalized],check=True,env=env,cwd=cwd,timeout=300)
    native_build=ROOT/'build/native/android/arm64-v8a/cardboard-imu-comparison'
    run('cmake','-S',SOURCE,'-B',native_build,'-G','Ninja',
        '-DCMAKE_TOOLCHAIN_FILE='+str(ndk/'build/cmake/android.toolchain.cmake'),'-DANDROID_ABI=arm64-v8a',
        '-DANDROID_PLATFORM=android-26','-DANDROID_STL=c++_static','-DCMAKE_BUILD_TYPE=Release',
        '-DCARDBOARD_ROOT='+str(CARDBOARD),'-DCARDBOARD_LIBRARY='+str(BUILD/'stage/lib/arm64-v8a/libGfxPluginCardboard.so'),
        '-DMESH_SOURCE='+str(mesh),'-DGENERATED_DIR='+str(BUILD/'generated'))
    run('cmake','--build',native_build,'--parallel','4')
    shutil.copy2(native_build/'libspatial_cardboard_compare.so',BUILD/'stage/lib/arm64-v8a/libspatial_cardboard_compare.so')
    key=BUILD/'debug.keystore'
    if not key.exists():
        run(jdk/'bin/keytool.exe','-genkeypair','-keystore',key,'-storepass','android','-keypass','android',
            '-alias','androiddebugkey','-dname','CN=Spatial AI Native Comparison','-keyalg','RSA','-validity','3650','-noprompt')
    apk=BUILD/'spatial-cardboard-imu-comparison.apk'
    # Cardboard's Android initializer registers the QR Activity even when QR
    # scanning is unused. Package its real AndroidX / Vision / protobuf runtime
    # dependencies and resources rather than shipping unresolved DEX references.
    gradle=BUILD/'gradle'
    (gradle/'settings.gradle').write_text("rootProject.name='NativeImuComparison'\ninclude ':app'\n",encoding='utf-8')
    (gradle/'build.gradle').write_text("buildscript { repositories { google(); mavenCentral() }; dependencies { classpath 'com.android.tools.build:gradle:7.1.2' } }\nallprojects { repositories { google(); mavenCentral() } }\n",encoding='utf-8')
    (gradle/'gradle.properties').write_text('android.useAndroidX=true\nandroid.enableJetifier=true\norg.gradle.jvmargs=-Xmx2048m -Dfile.encoding=UTF-8\nandroid.suppressUnsupportedCompileSdk=34\n',encoding='utf-8')
    (gradle/'local.properties').write_text('sdk.dir='+sdk.as_posix()+'\n',encoding='utf-8')
    groovy=lambda path: "'"+path.as_posix().replace("'","\\'")+"'"
    source_sets="manifest.srcFile "+groovy(SOURCE/'AndroidManifest.xml')+"; java.srcDirs=["+groovy(SOURCE)+"]; assets.srcDirs=["+groovy(BUILD/'stage/assets')+"]; jniLibs.srcDirs=['jniLibs']"
    app_gradle="""apply plugin: 'com.android.application'
android {
  compileSdkVersion 34
  defaultConfig { applicationId 'com.spatialai.samples.cardboardnative'; minSdkVersion 26; targetSdkVersion 34; versionCode 6; versionName '1.5-diagnostics'; ndk { abiFilters 'arm64-v8a' } }
  sourceSets { main { SOURCE_SETS } }
  compileOptions { sourceCompatibility JavaVersion.VERSION_1_8; targetCompatibility JavaVersion.VERSION_1_8 }
  signingConfigs { comparison { storeFile file(KEYFILE); storePassword 'android'; keyAlias 'androiddebugkey'; keyPassword 'android' } }
  buildTypes { debug { signingConfig signingConfigs.comparison } }
  packagingOptions { doNotStrip '**/libGfxPluginCardboard.so' }
}
dependencies {
  implementation files(CARDBOARD_AAR)
  implementation 'androidx.appcompat:appcompat:1.6.1'
  implementation 'com.google.android.gms:play-services-vision:20.1.3'
  implementation 'com.google.android.material:material:1.12.0'
  implementation 'com.google.protobuf:protobuf-javalite:4.26.1'
  implementation 'androidx.annotation:annotation-experimental:1.4.0'
  implementation 'androidx.annotation:annotation:1.8.0'
  implementation 'androidx.core:core:1.13.1'
  implementation 'com.google.guava:guava:30.1-jre'
}
""".replace('SOURCE_SETS',source_sets).replace('KEYFILE',groovy(key)).replace('CARDBOARD_AAR',groovy(aar))
    (gradle/'app/build.gradle').write_text(app_gradle,encoding='utf-8')
    shutil.copy2(native_build/'libspatial_cardboard_compare.so',gradle/'app/jniLibs/arm64-v8a/libspatial_cardboard_compare.so')
    env['GRADLE_USER_HOME']=str(ROOT/'build/gradle-cache')
    run(jdk/'bin/java.exe','-classpath',player/'Tools/gradle/lib/gradle-launcher-7.2.jar','org.gradle.launcher.GradleMain','--offline','--no-daemon','--console=plain','assembleDebug',cwd=gradle)
    shutil.copy2(gradle/'app/build/outputs/apk/debug/app-debug.apk',apk)
    run(jdk/'bin/java.exe','-jar',bt/'lib/apksigner.jar','verify',apk)
    (BUILD/'build-info.json').write_text(json.dumps(verify_apk(apk,configuration),indent=2),encoding='utf-8')
    print('Native comparison APK:',apk,flush=True)
    print('Calibration hash:',configuration['calibrationHash'],flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--diagnostics-sdk',action='store_true',help='Build isolated SDK with same-query sensor diagnostics')
    parser.add_argument('--unity-log',required=True,help='Saved Unity startup log containing the effective applied parameters')
    parser.add_argument('--android-player',default='D:/unity/hub/editor/2022.3.14f1/Editor/Data/PlaybackEngines/AndroidPlayer')
    parser.add_argument('--android-sdk',default='D:/android_sdk')
    build(parser.parse_args())
