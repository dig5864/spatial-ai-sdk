param(
    [switch]$CheckOnly,
    [int]$TimeoutMinutes = 45
)

$ErrorActionPreference = 'Stop'
# Unity Package Manager's Windows config lookup expects this standard variable.
# Some command-launch environments omit it, causing package resolution to fail.
if (-not $env:ALLUSERSPROFILE) {
    $env:ALLUSERSPROFILE = [Environment]::GetFolderPath('CommonApplicationData')
}
if (-not $env:PROCESSOR_ARCHITECTURE) {
    $env:PROCESSOR_ARCHITECTURE = if ([Environment]::Is64BitOperatingSystem) { 'AMD64' } else { 'x86' }
}
$project = [IO.Path]::GetFullPath($PSScriptRoot)
$sdkRoot = [IO.Path]::GetFullPath((Join-Path $project '..\..\..'))
$apk = Join-Path $sdkRoot 'build\samples\android\spatialxr.apk'
$requestPath = Join-Path $sdkRoot 'build\automation\SpatialXRBuildRequest.json'
$statusPath = Join-Path $sdkRoot 'build\automation\SpatialXRBuildStatus.json'
$editorVersion = (Select-String -LiteralPath (Join-Path $project 'ProjectSettings\ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches.Groups[1].Value.Trim()

function Get-ProjectEditor {
    $normalizedProject = $project.Replace('/', '\').TrimEnd('\').ToLowerInvariant()
    $processes = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'")
    @($processes | Where-Object {
        $line = [string]$_.CommandLine
        $normalized = $line.Replace('/', '\').ToLowerInvariant()
        $line -notmatch 'AssetImportWorker|\-adb2' -and
        $normalized.Contains($normalizedProject) -and
        $line -match '\-projectPath'
    })
}

function Find-UnityEditor {
    $candidates = @(
        $env:SPATIALAI_UNITY_EDITOR,
        (Join-Path 'D:\Unity Hub' "$editorVersion\Editor\Unity.exe"),
        (Join-Path $env:ProgramFiles "Unity\Hub\Editor\$editorVersion\Editor\Unity.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "Unity\Hub\Editor\$editorVersion\Editor\Unity.exe")
    )
    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }
    throw "找不到 Unity $editorVersion。可设置 SPATIALAI_UNITY_EDITOR 为 Unity.exe 的完整路径。"
}

function Find-AndroidSdk {
    $candidates = @($env:SPATIALAI_ANDROID_SDK_ROOT, $env:ANDROID_SDK_ROOT, $env:ANDROID_HOME, 'D:\AndroidSdk\sdk')
    foreach ($candidate in $candidates) {
        if (!$candidate) { continue }
        $platform = Join-Path $candidate 'platforms\android-34\android.jar'
        $tools = Join-Path $candidate 'build-tools\30.0.3\aapt2.exe'
        if ((Test-Path -LiteralPath $platform -PathType Leaf) -and
            (Test-Path -LiteralPath $tools -PathType Leaf)) {
            return [IO.Path]::GetFullPath($candidate)
        }
    }
    throw '找不到 Android SDK Platform 34 和 Build Tools 30.0.3。请安装后设置 SPATIALAI_ANDROID_SDK_ROOT。'
}

try {
    if (!(Test-Path -LiteralPath (Join-Path $project 'Assets\Scenes\HelloSpatialXR.unity') -PathType Leaf)) {
        throw '示例场景不存在：Assets\Scenes\HelloSpatialXR.unity'
    }
    if (!(Test-Path -LiteralPath (Join-Path $project 'Assets\Editor\SpatialXRBuildAutomation.cs') -PathType Leaf)) {
        throw 'Unity 自动编译监听脚本不存在。'
    }
    $unity = Find-UnityEditor
    $androidSdk = Find-AndroidSdk
    $env:SPATIALAI_ANDROID_SDK_ROOT = $androidSdk
    $editors = @(Get-ProjectEditor)
    if ($editors.Count -gt 1) { throw '检测到多个打开此工程的 Unity 编辑器，请先保留一个。' }

    Write-Host "Unity: $unity"
    Write-Host "工程: $project"
    Write-Host "Android SDK: $androidSdk"
    Write-Host "APK 输出: $apk"
    if ($CheckOnly) {
        Write-Host "预检查通过。工程编辑器运行数量: $($editors.Count)"
        exit 0
    }

    if (Test-Path -LiteralPath $requestPath) {
        throw '已有待处理的构建请求。请等待当前构建完成后再运行。'
    }
    New-Item -ItemType Directory -Path (Split-Path $requestPath) -Force | Out-Null
    $id = [guid]::NewGuid().ToString('N')
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($statusPath, (@{ id = $id; state = 'queued'; message = 'Waiting for Unity editor.' } | ConvertTo-Json -Compress), $utf8)
    $temporaryRequest = "$requestPath.tmp"
    [IO.File]::WriteAllText($temporaryRequest, (@{ id = $id } | ConvertTo-Json -Compress), $utf8)
    [IO.File]::Move($temporaryRequest, $requestPath)

    $launched = $null
    if ($editors.Count -eq 0) {
        Write-Host '正在启动 Unity，导入完成后将自动编译 APK...'
        $launched = Start-Process -FilePath $unity -ArgumentList @('-projectPath', ('"' + $project + '"')) -PassThru
    } else {
        Write-Host '已连接到打开的 Unity 工程，等待编辑器开始编译...'
        Write-Host '若这是首次使用，请切回 Unity 窗口一次，让新脚本完成导入。'
    }

    $deadline = [DateTime]::UtcNow.AddMinutes($TimeoutMinutes)
    $seenRunning = $false
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $statusPath) {
            try { $status = Get-Content -LiteralPath $statusPath -Raw -Encoding UTF8 | ConvertFrom-Json }
            catch { $status = $null }
            if ($status -and $status.id -eq $id) {
                if ($status.state -eq 'running' -and !$seenRunning) {
                    Write-Host 'Unity 已开始编译，请等待...'
                    $seenRunning = $true
                }
                if ($status.state -eq 'succeeded') {
                    if (!(Test-Path -LiteralPath $apk -PathType Leaf)) { throw 'Unity 报告成功，但找不到 APK。' }
                    Write-Host "编译成功：$apk"
                    Write-Host "日志：$($status.logPath)"
                    exit 0
                }
                if ($status.state -eq 'failed') {
                    Write-Host "编译失败：$($status.message)" -ForegroundColor Red
                    if ($status.logPath -and (Test-Path -LiteralPath $status.logPath)) {
                        Write-Host "日志：$($status.logPath)"
                        Get-Content -LiteralPath $status.logPath -Tail 30
                    }
                    exit 1
                }
            }
        }
        if ($launched -and $launched.HasExited -and !$seenRunning) {
            throw "Unity 在开始构建前退出。查看 Unity Editor.log：$env:LOCALAPPDATA\Unity\Editor\Editor.log"
        }
        Start-Sleep -Seconds 1
    }
    if ((Test-Path -LiteralPath $requestPath) -and !$seenRunning) {
        Remove-Item -LiteralPath $requestPath
    }
    throw "等待 Unity 编译超时（$TimeoutMinutes 分钟）。请查看 Unity Console 和 Editor.log。"
}
catch {
    Write-Host "自动编译未完成：$($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
