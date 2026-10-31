param([string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dotnet=Join-Path $root '.tools\dotnet\dotnet.exe'
if (!(Test-Path $dotnet)) {$dotnet='dotnet'}
& rtk proxy $dotnet run --project (Join-Path $root 'Tests\Mika.CoreTests.csproj') -c Release
if ($LASTEXITCODE -ne 0) {throw 'Core tests failed'}
& rtk proxy $dotnet publish (Join-Path $root 'MediaHost\Mika.MediaHost.csproj') -c Release -o (Join-Path $root 'MikaUnity\Assets\StreamingAssets\MediaHost')
if ($LASTEXITCODE -ne 0) {throw 'Media build failed'}
$process=Start-Process -FilePath $Unity -WindowStyle Hidden -PassThru -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',('"'+$root+'\MikaUnity"'),'-executeMethod','Mika.Editor.BuildMika.Build','-logFile',('"'+$root+'\Artifacts\unity-build.log"'))
$process.WaitForExit()
if ($process.ExitCode -ne 0) {throw 'Unity build failed; see Artifacts\unity-build.log'}
Write-Output (Join-Path $root 'Builds\Windows\Mika.exe')
