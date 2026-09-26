# SciToolbox Windows 构建脚本
# 用法：
#   .\build.ps1            # Debug 构建
#   .\build.ps1 -Release   # Release 构建
#   .\build.ps1 -Publish   # 发布为自包含单文件 exe
param(
    [switch]$Release,
    [switch]$Publish
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_UI_LANGUAGE = "en"
$proj = Join-Path $PSScriptRoot "src\SciToolbox\SciToolbox.csproj"

if ($Publish) {
    Write-Host "发布自包含单文件 (win-x64) ..." -ForegroundColor Cyan
    dotnet publish $proj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -o (Join-Path $PSScriptRoot "publish")
    Write-Host "完成，产物位于 publish\SciToolbox.exe" -ForegroundColor Green
}
elseif ($Release) {
    Write-Host "Release 构建 ..." -ForegroundColor Cyan
    dotnet build $proj -c Release
}
else {
    Write-Host "Debug 构建 ..." -ForegroundColor Cyan
    dotnet build $proj -c Debug
}
