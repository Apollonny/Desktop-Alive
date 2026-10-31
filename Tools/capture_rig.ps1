param([string]$OutputFolder='rig-audit')
$ErrorActionPreference='Stop'
if ($OutputFolder -notmatch '^[a-zA-Z0-9-]+$') {throw 'Use a simple artifact directory name'}
$root=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $root ('Artifacts\'+$OutputFolder)
$process=Start-Process -FilePath (Join-Path $root 'Builds\Windows\Mika.exe') -WindowStyle Hidden -PassThru -ArgumentList @(
    '-batchmode','--mika-smoke','--mika-rig-audit',('"'+$folder+'"'),
    '--mika-capture',('"'+$root+'\Artifacts\'+$OutputFolder+'-startup.png"'),
    '-logFile',('"'+$root+'\Artifacts\'+$OutputFolder+'.log"'))
$process.WaitForExit()
if ($process.ExitCode -ne 0) {throw "Rig capture failed: $($process.ExitCode)"}
Write-Output "RIG_CAPTURE_READY $folder"
