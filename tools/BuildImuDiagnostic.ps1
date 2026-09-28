param([Parameter(Mandatory=$true)][string]$AndroidPlayerPath)
. (Join-Path $PSScriptRoot 'CardboardCommon.ps1')
$sdkRoot = Split-Path -Parent $PSScriptRoot
$entry = (Read-CardboardLock $sdkRoot).repositories | Where-Object { $_.id -eq 'cardboard' }
$cardboard = Join-Path (Join-Path $sdkRoot 'third_party/checkouts') $entry.directory
Assert-CardboardCheckout $cardboard $entry
$source = Join-Path $sdkRoot 'samples/android/imu-diagnostic'
$build = Join-Path $sdkRoot 'build/samples/android/imu-diagnostic'
$nativeBuild = Join-Path $sdkRoot 'build/native/android/arm64-v8a/imu-diagnostic'
$jdk = Join-Path $AndroidPlayerPath 'OpenJDK'
$ndk = Join-Path $AndroidPlayerPath 'NDK'
$androidJar = Join-Path $AndroidPlayerPath 'SDK/platforms/android-32/android.jar'
$bt = Join-Path $AndroidPlayerPath 'SDK/build-tools/32.0.0'
foreach($path in @($androidJar,(Join-Path $bt 'aapt.exe'),(Join-Path $jdk 'bin/javac.exe'))) {
    if(!(Test-Path -LiteralPath $path)) { throw "Missing Android build dependency: $path" }
}
$env:JAVA_HOME=$jdk
if(!(Test-Path Env:PROCESSOR_ARCHITECTURE)) {
    if([Environment]::Is64BitOperatingSystem) { $env:PROCESSOR_ARCHITECTURE='AMD64' }
    else { throw 'This diagnostic build expects a Windows x64 host.' }
}
function Run-Checked {
    param([string]$Executable,[string[]]$Arguments)
    & $Executable @Arguments
    if($LASTEXITCODE -ne 0) { throw "Build command failed: $Executable ($LASTEXITCODE)" }
}
foreach($part in @('classes','dex','stage/lib/arm64-v8a','stage/assets')) { New-Item -ItemType Directory -Force -Path (Join-Path $build $part) | Out-Null }
Run-Checked 'cmake' @('-S',$source,'-B',$nativeBuild,'-G','Ninja',"-DCARDBOARD_ROOT=$cardboard","-DCMAKE_TOOLCHAIN_FILE=$ndk/build/cmake/android.toolchain.cmake",'-DANDROID_ABI=arm64-v8a','-DANDROID_PLATFORM=android-26','-DANDROID_STL=c++_static','-DCMAKE_BUILD_TYPE=Release')
Run-Checked 'cmake' @('--build',$nativeBuild,'--parallel','4')
Run-Checked "$jdk/bin/javac.exe" @('-source','8','-target','8','-bootclasspath',$androidJar,'-d',"$build/classes","$source/MainActivity.java")
Run-Checked "$jdk/bin/jar.exe" @('cf',"$build/classes.jar",'-C',"$build/classes",'.')
Run-Checked "$jdk/bin/java.exe" @('-cp',"$bt/lib/d8.jar",'com.android.tools.r8.D8','--lib',$androidJar,'--min-api','26','--output',"$build/dex","$build/classes.jar")
Copy-Item -LiteralPath "$build/dex/classes.dex" -Destination "$build/stage/classes.dex" -Force
Copy-Item -LiteralPath "$nativeBuild/libspatial_imu_diagnostic.so" -Destination "$build/stage/lib/arm64-v8a/libspatial_imu_diagnostic.so" -Force
Copy-Item -LiteralPath "$cardboard/LICENSE" -Destination "$build/stage/assets/LICENSE.cardboard" -Force
[IO.File]::WriteAllText("$build/stage/assets/UPSTREAM.txt","Cardboard $($entry.commit); Apache-2.0; unmodified sensors/*.cc and util/*.cc; diagnostic JNI wrapper is Spatial AI code.")
Run-Checked "$bt/aapt.exe" @('package','-f','-M',"$source/AndroidManifest.xml",'-I',$androidJar,'-F',"$build/unsigned.apk")
Push-Location "$build/stage"
try { Run-Checked "$bt/aapt.exe" @('add',"$build/unsigned.apk",'classes.dex','lib/arm64-v8a/libspatial_imu_diagnostic.so','assets/LICENSE.cardboard','assets/UPSTREAM.txt') }
finally { Pop-Location }
Run-Checked "$bt/zipalign.exe" @('-f','4',"$build/unsigned.apk","$build/aligned.apk")
$key="$build/debug.keystore"
if(!(Test-Path -LiteralPath $key)) {
    Run-Checked "$jdk/bin/keytool.exe" @('-genkeypair','-keystore',$key,'-storepass','android','-keypass','android','-alias','androiddebugkey','-dname','CN=Spatial AI Diagnostic','-keyalg','RSA','-validity','3650','-noprompt')
}
Run-Checked "$jdk/bin/java.exe" @('-jar',"$bt/lib/apksigner.jar",'sign','--ks',$key,'--ks-pass','pass:android','--key-pass','pass:android','--out',"$build/spatial-ai-imu-diagnostic.apk","$build/aligned.apk")
Run-Checked "$jdk/bin/java.exe" @('-jar',"$bt/lib/apksigner.jar",'verify',"$build/spatial-ai-imu-diagnostic.apk")
Write-Output "Diagnostic APK: $build/spatial-ai-imu-diagnostic.apk"
