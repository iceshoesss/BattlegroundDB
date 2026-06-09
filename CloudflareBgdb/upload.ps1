# upload.ps1 - 上传 DLL 到 Cloudflare KV
#
# 用法:
#   .\upload.ps1                    # 使用默认版本号
#   .\upload.ps1 -Version "2.0.1"  # 指定版本号

param(
    [string]$Version = "2.0.0"
)

$ErrorActionPreference = "Stop"

# 配置
$DLL_PATH = "D:\coding\HDT_Reverse\BattlegroundDB\bin\Release\netstandard2.0\BattlegroundDB.dll"
$KV_BINDING = "BGDB_KV"
$KV_KEY = "dll/$Version"

Write-Host "=== BGDB DLL 上传工具 ===" -ForegroundColor Cyan
Write-Host ""

# 1. 检查 DLL 文件
Write-Host "📦 检查 DLL 文件..." -ForegroundColor Yellow
if (-not (Test-Path $DLL_PATH)) {
    Write-Host "❌ DLL 文件不存在: $DLL_PATH" -ForegroundColor Red
    Write-Host "   请先构建 BattlegroundDB 项目" -ForegroundColor Yellow
    exit 1
}

$DLL_SIZE = (Get-Item $DLL_PATH).Length / 1KB
Write-Host "   ✓ 找到 DLL: $DLL_SIZE KB" -ForegroundColor Green

# 2. 上传到 KV
Write-Host "⬆️  上传到 Cloudflare KV..." -ForegroundColor Yellow
Write-Host "   Key: $KV_KEY" -ForegroundColor Gray

try {
    wrangler kv key put --binding=$KV_BINDING $KV_KEY --path=$DLL_PATH
    Write-Host "   ✓ 上传成功" -ForegroundColor Green
} catch {
    Write-Host "❌ 上传失败: $_" -ForegroundColor Red
    exit 1
}

# 3. 更新环境变量（需要手动更新 wrangler.toml）
Write-Host ""
Write-Host "📝 请更新 wrangler.toml 中的版本号:" -ForegroundColor Yellow
Write-Host "   BGDB_VERSION = `"$Version`"" -ForegroundColor Cyan

Write-Host ""
Write-Host "🔧 然后运行以下命令部署:" -ForegroundColor Yellow
Write-Host "   wrangler deploy" -ForegroundColor Cyan

Write-Host ""
Write-Host "✅ 完成！" -ForegroundColor Green
