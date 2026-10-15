param(
    [string]$Serial='10.168.2.104:37475',
    [Parameter(Mandatory=$true)][ValidateSet('Baseline','CPU','GPU')][string]$Mode,
    [int]$SampleSeconds=20,
    [int]$WarmupSeconds=6,
    [string]$OutputDirectory=''
)
$ErrorActionPreference='Stop'
if($SampleSeconds -lt 5 -or $SampleSeconds -gt 45 -or $WarmupSeconds -lt 0 -or $WarmupSeconds -gt 10) {
    throw 'Use 5–45 seconds of sampling and 0–10 seconds of warmup.'
}
if(!$OutputDirectory) { $OutputDirectory=Join-Path (Split-Path -Parent $PSScriptRoot) 'build/hand-load' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
function Invoke-TaskAdb([string[]]$Arguments) {
    $result=& adb -s $Serial @Arguments 2>&1
    if($LASTEXITCODE -ne 0) { throw ($result -join "`n") }
    return $result
}
function Read-TaskCpu([string]$Line) {
    if($Line -notmatch '^(\d+) \((.*)\) (.*)$') { throw "Invalid process stat: $Line" }
    $taskTid=$Matches[1]; $taskName=$Matches[2]; $taskFields=$Matches[3] -split '\s+'
    return @{id=$taskTid;name=$taskName;ticks=([double]$taskFields[11]+[double]$taskFields[12])}
}
function Read-TaskSnapshot([string]$ProcessId) {
    # All paths are fixed system metrics or the numeric PID returned by pidof.
    $command='cat /proc/uptime; head -n 1 /proc/stat; cat /proc/{0}/stat; cat /sys/class/kgsl/kgsl-3d0/gpubusy; for taskFile in /proc/{0}/task/*/stat; do cat "$taskFile"; done' -f $ProcessId
    $lines=@(Invoke-TaskAdb @('shell',$command))
    if($lines.Count -lt 5) { throw 'Incomplete metric snapshot.' }
    $uptime=[double](($lines[0] -split '\s+')[0])
    $cpu=@(($lines[1] -split '\s+' | Select-Object -Skip 1) | ForEach-Object {[double]$_})
    $total=($cpu | Select-Object -First 8 | Measure-Object -Sum).Sum
    $idle=$cpu[3]+$cpu[4]
    $process=Read-TaskCpu $lines[2]
    $gpu=$lines[3].Trim() -split '\s+'
    $gpuPercent=$null
    if($gpu.Count -eq 2 -and $gpu[0] -match '^\d+$' -and $gpu[1] -match '^\d+$' -and [double]$gpu[1] -gt 0) {
        $gpuPercent=100.0*[double]$gpu[0]/[double]$gpu[1]
    }
    $threads=@{}
    foreach($line in $lines | Select-Object -Skip 4) {
        if($line -match '^\d+ \(') { $thread=Read-TaskCpu $line; $threads[$thread.id]=$thread }
    }
    return @{uptime=$uptime;total=$total;idle=$idle;process=$process;gpuPercent=$gpuPercent;threads=$threads;raw=$lines}
}
$backend=if($Mode -eq 'GPU') {'GPU'} else {'CPU'}
$enabled=if($Mode -eq 'Baseline') {'false'} else {'true'}
Invoke-TaskAdb @('shell','am','broadcast','-a','com.spatialai.samples.xr.SWITCH_SCENE','-p','com.spatialai.samples.xr',
    '--es','scene','xri','--es','cameraSource','uvc','--ei','cameraSizeTier','1','--es','schedulingMode','Fixed',
    '--ei','maxHands','1','--es','inferenceDelegate',$backend,'--ez','gestures',$enabled) | Out-Null
Write-Output "Sampling ${Mode}: warmup $WarmupSeconds s, capture $SampleSeconds s. Keep the camera and hand position unchanged."
Start-Sleep -Seconds $WarmupSeconds
$taskProcess=(@(Invoke-TaskAdb @('shell','pidof','com.spatialai.samples.xr')) -join '').Trim()
if($taskProcess -notmatch '^\d+$') { throw "Expected one running Demo process; got $taskProcess" }
$ticksPerSecond=[double]((@(Invoke-TaskAdb @('shell','getconf','CLK_TCK')) -join '').Trim())
$batteryStart=@(Invoke-TaskAdb @('shell','dumpsys','battery'))
$previous=Read-TaskSnapshot $taskProcess
$snapshots=@($previous.raw -join "`n")
$samples=@(); $threadTotals=@{}; $timer=[Diagnostics.Stopwatch]::StartNew()
while($timer.Elapsed.TotalSeconds -lt $SampleSeconds) {
    Start-Sleep -Milliseconds 1000
    $current=Read-TaskSnapshot $taskProcess
    $snapshots+=($current.raw -join "`n")
    $seconds=$current.uptime-$previous.uptime
    if($seconds -le 0) { continue }
    $cpuPercent=100.0*($current.process.ticks-$previous.process.ticks)/$ticksPerSecond/$seconds
    $systemDelta=$current.total-$previous.total
    $systemPercent=if($systemDelta -gt 0) {100.0*(1-($current.idle-$previous.idle)/$systemDelta)} else {$null}
    $samples+=[pscustomobject]@{uptime=$current.uptime;seconds=$seconds;appCpuPercentOneCore=$cpuPercent;
        systemCpuPercentAllCores=$systemPercent;deviceGpuBusyPercent=$current.gpuPercent}
    foreach($taskTid in $current.threads.Keys) {
        if(!$previous.threads.ContainsKey($taskTid)) { continue }
        $thread=$current.threads[$taskTid]; $delta=$thread.ticks-$previous.threads[$taskTid].ticks
        if($delta -lt 0) { continue }
        if(!$threadTotals.ContainsKey($taskTid)) { $threadTotals[$taskTid]=@{id=$taskTid;name=$thread.name;ticks=0.0} }
        $threadTotals[$taskTid].ticks+=$delta
    }
    $previous=$current
}
$duration=($samples | Measure-Object -Property seconds -Sum).Sum
$summary=[ordered]@{mode=$Mode;serial=$Serial;pid=$taskProcess;sampleSeconds=$duration;
    cpuConvention='100% equals one CPU core; app value includes rendering, capture, conversion and inference';
    gpuConvention='Device-wide KGSL busy/total snapshot percentage, includes Unity and other apps; not per-inference GPU time';
    appCpuPercentOneCore=if($duration -gt 0){($samples|ForEach-Object {$_.appCpuPercentOneCore*$_.seconds}|Measure-Object -Sum).Sum/$duration}else{$null};
    deviceGpuBusyPercentMean=($samples | Where-Object {$null -ne $_.deviceGpuBusyPercent} | Measure-Object -Property deviceGpuBusyPercent -Average).Average;
    threads=@($threadTotals.Values | ForEach-Object {[pscustomobject]@{id=$_.id;name=$_.name;cpuPercentOneCore=100.0*$_.ticks/$ticksPerSecond/$duration}} | Sort-Object cpuPercentOneCore -Descending);
    samples=$samples;batteryStart=$batteryStart;batteryEnd=@(Invoke-TaskAdb @('shell','dumpsys','battery'))}
$path=Join-Path $OutputDirectory ($Mode+'.json')
$summary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding utf8
$snapshots | Set-Content -LiteralPath (Join-Path $OutputDirectory ($Mode+'-raw.txt')) -Encoding utf8
Invoke-TaskAdb @('logcat','-d','-v','threadtime','-s','SpatialHands:I','Unity:I','SpatialUVC:I','*:S') |
    Set-Content -LiteralPath (Join-Path $OutputDirectory ($Mode+'-logcat.txt')) -Encoding utf8
Write-Output ('{0}: app CPU {1:F1}% (one core = 100%), device GPU busy {2:F1}%, saved {3}' -f
    $Mode,$summary.appCpuPercentOneCore,$summary.deviceGpuBusyPercentMean,$path)
