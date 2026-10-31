$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($fps in @(30,60)) {
    $process=Start-Process -FilePath (Join-Path $root 'Builds\Windows\Mika.exe') -WindowStyle Hidden -PassThru -ArgumentList @('--mika-profile','--mika-fps',"$fps",'-logFile',('"'+$root+"\Artifacts\profile-$fps.log"+'"'))
    if (!$process.WaitForExit(45000)) {Stop-Process -Id $process.Id; throw "Profile $fps timed out"}
    if ($process.ExitCode -ne 0) {throw "Profile $fps failed"}
    $source=Join-Path $env:USERPROFILE "AppData\LocalLow\Mika\MIKA LIVES HERE\profile-$fps.txt"
    Copy-Item -LiteralPath $source -Destination (Join-Path $root "Artifacts\profile-$fps.txt")
    Get-Content -LiteralPath $source
}
