param([Parameter(Mandatory=$true)][string]$AndroidPlayerPath)
$ErrorActionPreference='Stop'
$sdkRoot=Split-Path -Parent $PSScriptRoot
$source=Join-Path $sdkRoot 'samples/android/hand-diagnostic'
$output=Join-Path $sdkRoot 'build/samples/android/hand-diagnostic'
$assets=Join-Path $sdkRoot 'build/mediapipe/models'
$model=Join-Path $assets 'hand_landmarker.task'
$expected='FBC2A30080C3C557093B5DDFC334698132EB341044CCEE322CCF8BCF3607CDE1'
$jdk=Join-Path $AndroidPlayerPath 'OpenJDK'
$gradle=Join-Path $AndroidPlayerPath 'Tools/gradle/lib/gradle-launcher-7.2.jar'
$androidSdk=Join-Path $AndroidPlayerPath 'SDK'
foreach($path in @($gradle,(Join-Path $jdk 'bin/java.exe'),(Join-Path $androidSdk 'platforms/android-32/android.jar'))) {
    if(!(Test-Path -LiteralPath $path)) { throw "Missing build dependency: $path" }
}
New-Item -ItemType Directory -Force -Path $output,$assets | Out-Null
if(!(Test-Path -LiteralPath $model) -or (Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash -ne $expected) {
    Invoke-WebRequest 'https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task' -OutFile $model
}
if((Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash -ne $expected) { throw 'MediaPipe model SHA256 mismatch' }
$env:JAVA_HOME=$jdk
$env:ANDROID_HOME=$androidSdk
$env:ANDROID_SDK_ROOT=$androidSdk
Push-Location $source
try {
    & "$jdk/bin/java.exe" -cp $gradle org.gradle.launcher.GradleMain ':app:assembleDebug' ':hand-unity:assembleRelease' "-PmodelAssetDir=$assets" '--no-daemon' '--stacktrace'
    if($LASTEXITCODE -ne 0) { throw "Gradle build failed ($LASTEXITCODE)" }
}
finally { Pop-Location }
$apk=Join-Path $source 'app/build/outputs/apk/debug/app-debug.apk'
Copy-Item -LiteralPath $apk -Destination (Join-Path $output 'spatial-ai-hand-diagnostic.apk') -Force
Write-Output "Hand diagnostic APK: $output/spatial-ai-hand-diagnostic.apk"
$aar=Join-Path $source 'hand-unity/build/outputs/aar/hand-unity-release.aar'
$pluginDir=Join-Path $sdkRoot 'packages/com.spatialai.sdk/Plugins/Android'
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath $aar -Destination (Join-Path $pluginDir 'spatial-hand-unity.aar') -Force
Write-Output "Unity hand plugin: $pluginDir/spatial-hand-unity.aar"
