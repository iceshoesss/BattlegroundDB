# Cloudflare BGDB 下载服务

使用 Cloudflare Workers 提供 BattlegroundDB.dll 自动更新服务。

## 服务地址

```
https://api.iceshoes.dpdns.org
```

## API 端点

| 端点 | 方法 | 说明 | 返回 |
|------|------|------|------|
| `/` | GET | 版本信息 | JSON |
| `/version` | GET | 仅版本号 | JSON |
| `/download` | GET | 下载 DLL | 二进制文件 |

### 响应示例

**GET /version**
```json
{
  "version": "35.6",
  "filename": "BattlegroundDB.dll",
  "downloadUrl": "/download"
}
```

**GET /download**
- 返回 DLL 文件
- 支持 ETag 缓存（304 Not Modified）
- 响应头：`ETag: "35.6"`

---

## 版本更新流程

当炉石传说大版本更新时，按以下步骤操作：

### 步骤 1：获取最新卡牌数据

```powershell
cd D:\coding\HDT_Reverse\HsbgFetcher
dotnet run
```

输出：
```
=== HsbgFetcher - hsbg.cards 数据抓取器 ===

📡 获取中文名数据... ✓ 35046 条记录
📡 获取 hsbg.cards 数据... ✓ 1372 张卡牌
🔄 转换格式... ✓ 1372 张卡牌

✅ 已保存到: D:\coding\HDT_Reverse\BattlegroundDB\Data\bg_cards.json
```

### 步骤 2：编译 DLL

```powershell
cd D:\coding\HDT_Reverse\BattlegroundDB
dotnet build -c Release
```

输出：
```
BattlegroundDB -> D:\coding\HDT_Reverse\BattlegroundDB\bin\Release\netstandard2.0\BattlegroundDB.dll
```

### 步骤 3：更新版本号

编辑 `D:\coding\HDT_Reverse\CloudflareBgdb\wrangler.toml`：

```toml
[vars]
BGDB_VERSION = "35.6"  # 修改为新版本号
```

### 步骤 4：上传 DLL 到 Cloudflare

```powershell
cd D:\coding\HDT_Reverse\CloudflareBgdb

# 上传 DLL（版本号必须与上面一致）
wrangler kv key put --binding=BGDB_KV "dll/35.6" --path="D:\coding\HDT_Reverse\BattlegroundDB\bin\Release\netstandard2.0\BattlegroundDB.dll" --remote
```

### 步骤 5：部署更新

```powershell
wrangler deploy
```

### 步骤 6：验证

```powershell
# 检查版本
curl https://api.iceshoes.dpdns.org/version

# 测试下载
curl -I https://api.iceshoes.dpdns.org/download
```

---

## 一键更新脚本

创建 `update.ps1`：

```powershell
# update.ps1 - 一键更新 BGDB

param(
    [Parameter(Mandatory=$true)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

Write-Host "=== BGDB 一键更新 ===" -ForegroundColor Cyan
Write-Host ""

# 1. 获取最新数据
Write-Host "📡 步骤 1/4: 获取最新卡牌数据..." -ForegroundColor Yellow
dotnet run --project "D:\coding\HDT_Reverse\HsbgFetcher"
if ($LASTEXITCODE -ne 0) { exit 1 }

# 2. 编译 DLL
Write-Host ""
Write-Host "🔨 步骤 2/4: 编译 DLL..." -ForegroundColor Yellow
dotnet build "D:\coding\HDT_Reverse\BattlegroundDB\BattlegroundDB.csproj" -c Release
if ($LASTEXITCODE -ne 0) { exit 1 }

# 3. 上传到 Cloudflare
Write-Host ""
Write-Host "⬆️  步骤 3/4: 上传到 Cloudflare..." -ForegroundColor Yellow

$DLL_PATH = "D:\coding\HDT_Reverse\BattlegroundDB\bin\Release\netstandard2.0\BattlegroundDB.dll"
$KV_KEY = "dll/$Version"

wrangler kv key put --binding=BGDB_KV $KV_KEY --path=$DLL_PATH --remote

# 4. 更新版本号并部署
Write-Host ""
Write-Host "🚀 步骤 4/4: 部署更新..." -ForegroundColor Yellow

# 更新 wrangler.toml 中的版本号
$TOML_PATH = "D:\coding\HDT_Reverse\CloudflareBgdb\wrangler.toml"
(Get-Content $TOML_PATH) -replace 'BGDB_VERSION = ".*"', "BGDB_VERSION = `"$Version`"" | Set-Content $TOML_PATH

wrangler deploy

# 5. 验证
Write-Host ""
Write-Host "✅ 验证..." -ForegroundColor Yellow
$versionInfo = Invoke-RestMethod -Uri "https://api.iceshoes.dpdns.org/version"
Write-Host "当前版本: $($versionInfo.version)" -ForegroundColor Green

Write-Host ""
Write-Host "✅ 更新完成！" -ForegroundColor Green
```

使用方法：

```powershell
.\update.ps1 -Version "35.6"
```

---

## 部署说明（首次）

### 前置条件

1. 安装 Node.js
2. 安装 Wrangler CLI
   ```powershell
   npm install -g wrangler
   ```
3. 登录 Cloudflare
   ```powershell
   wrangler login
   ```

### 首次部署

```powershell
cd D:\coding\HDT_Reverse\CloudflareBgdb

# 1. 创建 KV 命名空间
wrangler kv namespace create BGDB_KV
# 记录返回的 id，更新到 wrangler.toml

# 2. 部署 Worker
wrangler deploy

# 3. 上传 DLL
wrangler kv key put --binding=BGDB_KV "dll/35.6" --path="D:\coding\HDT_Reverse\BattlegroundDB\bin\Release\netstandard2.0\BattlegroundDB.dll" --remote
```

### 绑定自定义域名

在 Cloudflare Dashboard 中：

1. 进入 Workers & Pages → bgdb-api → Settings → Triggers
2. 添加自定义域名：`api.iceshoes.dpdns.org`

---

## 文件结构

```
CloudflareBgdb/
├── src/
│   └── index.js          # Worker 脚本
├── wrangler.toml         # Cloudflare 配置
├── package.json          # 项目配置
├── update.ps1            # 一键更新脚本
└── README.md             # 本文档
```

---

## 故障排查

### 问题：下载返回 404

```powershell
# 检查 KV 中是否有对应版本的 DLL
wrangler kv key list --binding=BGDB_KV --remote

# 手动上传
wrangler kv key put --binding=BGDB_KV "dll/35.6" --path="path/to/BattlegroundDB.dll" --remote
```

### 问题：版本号不一致

确保以下三处版本号一致：

1. `wrangler.toml` 中的 `BGDB_VERSION`
2. KV 中的 key 名称 `dll/{version}`
3. `bg_cards.json` 中的 `meta.version`

### 问题：自定义域名无法访问

1. 检查 DNS 解析：`nslookup api.iceshoes.dpdns.org`
2. 检查 SSL 证书状态
3. 等待几分钟（DNS 生效需要时间）

---

## 费用

| 项目 | 免费额度 | 预估使用 |
|------|----------|----------|
| Workers 请求 | 100,000 次/天 | ~1,000 次/天 |
| KV 读取 | 100,000 次/天 | ~1,000 次/天 |
| KV 存储 | 1 GB | ~1 MB |

**结论：完全免费**。
