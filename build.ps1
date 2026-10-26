param(
    [string]$Output = 'd:\eee\release'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$stage = Join-Path $root 'artifacts'
$payloadDir = Join-Path $root 'src\DiskView.Setup\payload'
$appProj = Join-Path $root 'src\DiskView.App\DiskView.App.csproj'
$setupProj = Join-Path $root 'src\DiskView.Setup\DiskView.Setup.csproj'

Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $stage -Force | Out-Null
New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null
New-Item -ItemType Directory -Path $Output -Force | Out-Null

function Publish-App([string]$lang) {
    $out = Join-Path $stage "app-$lang"
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue

    dotnet publish $appProj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None -p:DebugSymbols=false `
        -p:Lang=$lang -o $out | Out-Host

    if ($LASTEXITCODE -ne 0) { throw "Publishing app ($lang) failed." }

    $exe = Join-Path $out 'DiskSpaceAnalyzer.exe'
    if (-not (Test-Path $exe)) { throw "App executable not found: $exe" }
    return $exe
}

function Build-Setup([string]$lang, [string]$appExe) {
    Copy-Item $appExe (Join-Path $payloadDir 'DiskSpaceAnalyzer.exe') -Force

    $zip = Join-Path $payloadDir 'app.zip'
    Remove-Item $zip -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $payloadDir 'DiskSpaceAnalyzer.exe') -DestinationPath $zip -CompressionLevel Optimal

    $out = Join-Path $stage "setup-$lang"
    Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue

    dotnet publish $setupProj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None -p:DebugSymbols=false `
        -p:Lang=$lang -o $out | Out-Host

    if ($LASTEXITCODE -ne 0) { throw "Publishing setup ($lang) failed." }

    $setupExe = Join-Path $out 'DiskSpaceAnalyzerSetup.exe'
    if (-not (Test-Path $setupExe)) { throw "Setup executable not found: $setupExe" }
    return $setupExe
}

$targets = @(
    @{ Lang = 'zh'; Folder = '中文版';   Portable = '磁盘空间分析器.exe';        Installer = '磁盘空间分析器-安装版.exe' },
    @{ Lang = 'en'; Folder = 'English';  Portable = 'Disk Space Analyzer.exe';   Installer = 'Disk Space Analyzer-Setup.exe' }
)

foreach ($target in $targets) {
    $exe = Publish-App $target.Lang
    $setup = Build-Setup $target.Lang $exe

    $folder = Join-Path $Output $target.Folder
    New-Item -ItemType Directory -Path $folder -Force | Out-Null

    Copy-Item $exe (Join-Path $folder $target.Portable) -Force
    Copy-Item $setup (Join-Path $folder $target.Installer) -Force

    Write-Host ("[{0}] {1}" -f $target.Lang, (Join-Path $folder $target.Portable))
    Write-Host ("[{0}] {1}" -f $target.Lang, (Join-Path $folder $target.Installer))
}

Remove-Item (Join-Path $payloadDir 'DiskSpaceAnalyzer.exe') -Force -ErrorAction SilentlyContinue
Write-Host 'Build finished.'
