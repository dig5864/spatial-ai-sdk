param([Parameter(Mandatory=$true)][string]$AndroidPlayerPath)
$ErrorActionPreference='Stop'
$sdkRoot=Split-Path -Parent $PSScriptRoot
$source=Join-Path $sdkRoot 'samples/android/camera-diagnostic'
$cameraSource=Join-Path $sdkRoot 'runtime/platform/android/camera/CameraService.java'
$build=Join-Path $sdkRoot 'build/samples/android/camera-diagnostic'
$jdk=Join-Path $AndroidPlayerPath 'OpenJDK'
$androidJar=Join-Path $AndroidPlayerPath 'SDK/platforms/android-32/android.jar'
$bt=Join-Path $AndroidPlayerPath 'SDK/build-tools/32.0.0'
foreach($path in @($androidJar,(Join-Path $bt 'aapt.exe'),(Join-Path $jdk 'bin/javac.exe'),$cameraSource)) {
    if(!(Test-Path -LiteralPath $path)) { throw "Missing build dependency: $path" }
}
function Run-Checked {
    param([string]$Executable,[string[]]$Arguments)
    & $Executable @Arguments
    if($LASTEXITCODE -ne 0) { throw "Build failed: $Executable ($LASTEXITCODE)" }
}
foreach($part in @('classes','dex','stage')) { New-Item -ItemType Directory -Force -Path (Join-Path $build $part) | Out-Null }
Run-Checked "$jdk/bin/javac.exe" @('-source','8','-target','8','-bootclasspath',$androidJar,'-d',"$build/classes",$cameraSource,"$source/MainActivity.java")
Run-Checked "$jdk/bin/jar.exe" @('cf',"$build/classes.jar",'-C',"$build/classes",'.')
Run-Checked "$jdk/bin/java.exe" @('-cp',"$bt/lib/d8.jar",'com.android.tools.r8.D8','--lib',$androidJar,'--min-api','28','--output',"$build/dex","$build/classes.jar")
Copy-Item -LiteralPath "$build/dex/classes.dex" -Destination "$build/stage/classes.dex" -Force
Run-Checked "$bt/aapt.exe" @('package','-f','-M',"$source/AndroidManifest.xml",'-I',$androidJar,'-F',"$build/unsigned.apk")
Push-Location "$build/stage"
try { Run-Checked "$bt/aapt.exe" @('add',"$build/unsigned.apk",'classes.dex') }
finally { Pop-Location }
Run-Checked "$bt/zipalign.exe" @('-f','4',"$build/unsigned.apk","$build/aligned.apk")
$key="$build/debug.keystore"
if(!(Test-Path -LiteralPath $key)) {
    Run-Checked "$jdk/bin/keytool.exe" @('-genkeypair','-keystore',$key,'-storepass','android','-keypass','android','-alias','androiddebugkey','-dname','CN=Spatial AI Camera Diagnostic','-keyalg','RSA','-validity','3650','-noprompt')
}
Run-Checked "$jdk/bin/java.exe" @('-jar',"$bt/lib/apksigner.jar",'sign','--ks',$key,'--ks-pass','pass:android','--key-pass','pass:android','--out',"$build/spatial-ai-camera-diagnostic.apk","$build/aligned.apk")
Run-Checked "$jdk/bin/java.exe" @('-jar',"$bt/lib/apksigner.jar",'verify',"$build/spatial-ai-camera-diagnostic.apk")
Write-Output "Diagnostic APK: $build/spatial-ai-camera-diagnostic.apk"
