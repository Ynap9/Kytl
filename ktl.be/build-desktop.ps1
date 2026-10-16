$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo    = Split-Path -Parent $root
$fe      = Join-Path $repo 'ktl.fe'
$desktop = Join-Path $root 'ktl.be.desktop'
$publish = Join-Path $repo 'publish'

Get-Process -Name 'SaoMai-KySo' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Push-Location $fe
npm run build -- --configuration desktop
Pop-Location

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish $desktop -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $publish

$dist    = Join-Path $fe 'dist/ktl.fe/browser'
$wwwroot = Join-Path $publish 'wwwroot'
if (-not (Test-Path $dist)) { throw "Không thấy dist Angular: $dist" }
New-Item -ItemType Directory -Force -Path $wwwroot | Out-Null
Copy-Item (Join-Path $dist '*') $wwwroot -Recurse -Force

$driverSrc = Join-Path $root 'installer/drivers'
$driverOut = Join-Path $publish 'drivers'
New-Item -ItemType Directory -Force -Path $driverOut | Out-Null
$drivers = @(
    'MicrosoftEdgeWebView2Setup.exe',
    'bit4id_xpki_1.4.10.764-ng-user-vgca-pkimgr-bwc.exe',
    'VGCAv1_Installer_1.0.21.0402.exe'
)
foreach ($driver in $drivers) {
    $path = Join-Path $driverSrc $driver
    if (-not (Test-Path $path)) { throw "Thiếu bộ cài trình điều khiển: $path" }
    Copy-Item $path (Join-Path $driverOut $driver) -Force
}

New-Item -ItemType Directory -Force -Path (Join-Path $publish 'Log') | Out-Null

$makensis = @(
    "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
    "$env:ProgramFiles\NSIS\makensis.exe",
    "$env:LOCALAPPDATA\Programs\NSIS\makensis.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $makensis) { throw 'Không thấy makensis.exe. Cài NSIS: winget install --id NSIS.NSIS' }

New-Item -ItemType Directory -Force -Path (Join-Path $root 'installer/installer-output') | Out-Null
Push-Location (Join-Path $root 'installer')
& $makensis /V2 'SaoMai-KySo.nsi'
$nsisExit = $LASTEXITCODE
Pop-Location
if ($nsisExit -ne 0) { throw "NSIS biên dịch lỗi (mã $nsisExit)." }

$version = ((Get-Item (Join-Path $publish 'SaoMai-KySo.exe')).VersionInfo.ProductVersion -split '\+')[0]
$setup = Join-Path $repo "SaoMai-KySo-v$version-win-x64-Setup.exe"
Copy-Item (Join-Path $root 'installer/installer-output/SaoMai-KySo-Setup.exe') $setup -Force

$zip = Join-Path $repo "SaoMai-KySo-v$version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $publish, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Update)
@($archive.Entries | Where-Object { $_.FullName -eq 'Log\' }) | ForEach-Object { $_.Delete() }
if (-not ($archive.Entries | Where-Object { $_.FullName -eq 'Log/' })) {
    $archive.CreateEntry('Log/') | Out-Null
}
$archive.Dispose()

Write-Host "Xong: $publish" -ForegroundColor Green
Write-Host ("Bản zip: {0} ({1} MB)" -f $zip, [math]::Round((Get-Item $zip).Length / 1MB, 1)) -ForegroundColor Green
Write-Host ("Bộ cài: {0} ({1} MB)" -f $setup, [math]::Round((Get-Item $setup).Length / 1MB, 1)) -ForegroundColor Green
