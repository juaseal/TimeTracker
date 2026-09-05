param(
    [Parameter(Mandatory=$true)][string]$PackageIdentityName,
    [Parameter(Mandatory=$true)][string]$Publisher,
    [Parameter(Mandatory=$true)][string]$PublisherDisplayName,
    [ValidateSet("win-x64","win-arm64")][string]$RuntimeIdentifier="win-x64",
    [string]$Version="1.0.0.0"
)
$ErrorActionPreference="Stop"
$architecture=if($RuntimeIdentifier -eq "win-arm64"){"arm64"}else{"x64"}
$root=Split-Path $PSScriptRoot -Parent
$stage=Join-Path $root "artifacts\msix\$RuntimeIdentifier"
$output=Join-Path $root "artifacts\TaskUp-$Version-$architecture.msix"
$kits=Join-Path ([Environment]::GetFolderPath("ProgramFilesX86")) "Windows Kits\10\bin"
$makeAppx=Get-ChildItem $kits -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
if(-not $makeAppx){throw "makeappx.exe was not found. Install the Windows SDK from Visual Studio Installer."}
if(Test-Path $stage){Remove-Item -LiteralPath $stage -Recurse -Force}
New-Item -ItemType Directory -Force $stage,(Split-Path $output -Parent) | Out-Null
dotnet publish (Join-Path $root "TimeTracker.csproj") -c Release -r $RuntimeIdentifier --self-contained true -p:PublishSingleFile=false -o $stage
if($LASTEXITCODE -ne 0){throw "dotnet publish exited with code $LASTEXITCODE"}
Copy-Item (Join-Path $PSScriptRoot "Assets") (Join-Path $stage "Assets") -Recurse -Force
$manifest=[IO.File]::ReadAllText((Join-Path $PSScriptRoot "Package.appxmanifest.template"))
$manifest=$manifest.Replace("__PACKAGE_IDENTITY_NAME__",$PackageIdentityName).Replace("__PUBLISHER__",$Publisher).Replace("__PUBLISHER_DISPLAY_NAME__",$PublisherDisplayName).Replace("__VERSION__",$Version).Replace("__ARCHITECTURE__",$architecture)
[IO.File]::WriteAllText((Join-Path $stage "AppxManifest.xml"),$manifest,[Text.UTF8Encoding]::new($false))
if(Test-Path $output){Remove-Item -LiteralPath $output -Force}
& $makeAppx.FullName pack /d $stage /p $output /o
if($LASTEXITCODE -ne 0){throw "makeappx exited with code $LASTEXITCODE"}
Write-Host "Package created: $output"
