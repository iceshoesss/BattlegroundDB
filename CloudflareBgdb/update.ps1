# update.ps1 - 一键更新 BGDB
#
# 用法:
#   .\update.ps1 -Version "35.6"

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host "=== BGDB 一键更新 ===" -ForegroundColor Cyan
Write-Host "版本: $Version" -ForegroundColor Gray
Write-Host ""

# 1. 获取最新数据
Write-Host "📡 步骤 1/4: 获取最新卡牌数据..." -ForegroundColor Yellow
dotnet run --project "D:\coding\HDT_Reverse\HsbgFetcher"
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 获取数据失败" -ForegroundColor Red
    exit 1
}

# 2. 编译 DLL
Write-Host ""
Write-Host "🔨 步骤 2/4: 编译 DLL..." -ForegroundColor Yellow
dotnet build "D:\coding\HDT_Reverse\BattlegroundDB\BattlegroundDB.csproj" -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 编译失败" -ForegroundColor Red
    exit 1
}

# 3. 上传到 Cloudflare
Write-Host ""
Write-Host "⬆️  步骤 3/4: 上传到 Cloudflare..." -ForegroundColor Yellow

$DLL_PATH = "D:\coding\HDT_Reverse\BattlegroundDB\bin\Release\netstandard2.0\BattlegroundDB.dll"
$KV_KEY = "dll/$Version"

if (-not (Test-Path $DLL_PATH)) {
    Write-Host "❌ DLL 文件不存在: $DLL_PATH" -ForegroundColor Red
    exit 1
}

wrangler kv key put --binding=BGDB_KV $KV_KEY --path=$DLL_PATH --remote
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 上传失败" -ForegroundColor Red
    exit 1
}

# 4. 更新版本号并部署
Write-Host ""
Write-Host "🚀 步骤 4/4: 部署更新..." -ForegroundColor Yellow

# 更新 wrangler.toml 中的版本号
$TOML_PATH = "D:\coding\HDT_Reverse\CloudflareBgdb\wrangler.toml"
(Get-Content $TOML_PATH) -replace 'BGDB_VERSION = ".*"', "BGDB_VERSION = `"$Version`"" | Set-Content $TOML_PATH

wrangler deploy
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ 部署失败" -ForegroundColor Red
    exit 1
}

# 5. 验证
Write-Host ""
Write-Host "✅ 验证..." -ForegroundColor Yellow
try {
    $versionInfo = Invoke-RestMethod -Uri "https://api.iceshoes.dpdns.org/version"
    Write-Host "当前版本: $($versionInfo.version)" -ForegroundColor Green
} catch {
    Write-Host "⚠️ 验证失败: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✅ 更新完成！" -ForegroundColor Green
Write-Host "   API: https://api.iceshoes.dpdns.org" -ForegroundColor Gray
