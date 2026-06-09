# BattlegroundDB

酒馆战棋专用卡牌数据库，从 [hsbg.cards](https://hsbg.cards/) API 获取数据。

## 数据来源

| 来源 | 说明 |
|------|------|
| **hsbg.cards** | 卡牌主数据（英文名、属性、关键词等） |
| **hearthstonejson.com** | 中文名补充 |

### 数据流

```
hsbg.cards/api/v1/cards (批量)
        ↓ (HsbgFetcher 抓取)
    bg_cards.json (统一格式)
        ↓ (嵌入到 DLL)
    BattlegroundDB.dll (771KB)
```

## 包含数据

| 类型 | 数量 | 说明 |
|------|------|------|
| 随从 | 559 | 仅 pool=true |
| 英雄 | 119 | 含护甲、伙伴 |
| 畸变 | 38 | |
| 任务 | 11 | |
| 奖励 | 38 | |
| 饰品 | 218 | 小型/大型 |
| 法术 | 256 | |
| 其他 | 133 | 英雄技能、衍生物等 |
| **总计** | **1372** | |

## 版本更新流程

当炉石传说大版本更新时，按以下步骤操作：

### 步骤 1：获取最新卡牌数据

```powershell
cd D:\coding\HDT_Reverse\HsbgFetcher
dotnet run
```

输出示例：
```
=== HsbgFetcher - hsbg.cards 数据抓取器 ===

📡 获取中文名数据... ✓ 35046 条记录
📡 获取 hsbg.cards 数据... ✓ 1372 张卡牌
🔄 转换格式... ✓ 1372 张卡牌

✅ 已保存到: D:\coding\HDT_Reverse\BattlegroundDB\Data\bg_cards.json
   随从: 559
   英雄: 119
   ...
```

### 步骤 2：编译 DLL

```powershell
cd D:\coding\HDT_Reverse\BattlegroundDB
dotnet build -c Release
```

输出示例：
```
BattlegroundDB -> D:\coding\HDT_Reverse\BattlegroundDB\bin\Release\netstandard2.0\BattlegroundDB.dll
```

### 步骤 3：部署到 Cloudflare（可选）

```powershell
cd D:\coding\HDT_Reverse\CloudflareBgdb
.\update.ps1 -Version "35.6"
```

或手动操作：

```powershell
# 上传 DLL
wrangler kv key put --binding=BGDB_KV "dll/35.6" --path="..\BattlegroundDB\bin\Release\netstandard2.0\BattlegroundDB.dll" --remote

# 更新版本号（编辑 wrangler.toml）
# BGDB_VERSION = "35.6"

# 部署
wrangler deploy
```

### 步骤 4：本地使用（可选）

```powershell
# 复制到 LeagueTool
Copy-Item "D:\coding\HDT_Reverse\BattlegroundDB\bin\Release\netstandard2.0\BattlegroundDB.dll" `
          "D:\coding\HDT_BGTracker\LeagueTool\BattlegroundDB.dll" -Force
```

## 一键更新脚本

```powershell
# 完整更新流程
.\update.ps1 -Version "35.6"
```

脚本会自动执行：
1. 获取最新卡牌数据
2. 编译 DLL
3. 上传到 Cloudflare
4. 部署更新

## 使用方式

```csharp
using BattlegroundDB;

// 加载数据（自动）
Cards.Load();

// 获取元信息
Console.WriteLine($"版本: {Cards.Meta.Version}");
Console.WriteLine($"卡牌数: {Cards.Meta.TotalCards}");

// 按 CardId 查询
var minion = Cards.GetByCardId("BG29_611");
Console.WriteLine($"{minion.Name} ({minion.NameZh})");

// 按 DbfId 查询
var cordPuller = Cards.GetById(110101);
Console.WriteLine($"{cordPuller.Name} - {string.Join(", ", cordPuller.Keywords)}");

// 按星级查询随从
var tier1 = Cards.GetMinionsByTier(1);
Console.WriteLine($"Tier 1 随从: {tier1.Count}");

// 按种族查询
var mechs = Cards.GetMinionsByMinionType("Mech");
Console.WriteLine($"机械随从: {mechs.Count}");

// 按关键词查询
var taunt = Cards.GetMinionsByKeyword("Taunt");
Console.WriteLine($"嘲讽随从: {taunt.Count}");

// 搜索卡牌（支持中英文）
var results = Cards.Search("融合");
Console.WriteLine($"搜索 '融合': {results.Count} 张");

// 获取饰品
var lesser = Cards.GetLesserTrinkets();
var greater = Cards.GetGreaterTrinkets();
Console.WriteLine($"小型饰品: {lesser.Count}, 大型饰品: {greater.Count}");

// 获取伙伴
var buddies = Cards.GetBuddies();
Console.WriteLine($"伙伴: {buddies.Count}");

// 获取所有种族
var types = Cards.GetMinionTypes();
Console.WriteLine($"种族: {string.Join(", ", types)}");
```

## 项目结构

```
BattlegroundDB/
├── BattlegroundDB.csproj   # 项目文件 (netstandard2.0)
├── Cards.cs                # 数据库主类
├── Models.cs               # 数据模型
├── Data/
│   └── bg_cards.json       # 卡牌数据 (hsbg.cards 格式)
└── README.md               # 本文件

HsbgFetcher/
├── HsbgFetcher.csproj      # 控制台程序
├── Program.cs              # 数据抓取逻辑
└── README.md               # 抓取器文档

CloudflareBgdb/
├── src/index.js            # Worker 脚本
├── wrangler.toml           # Cloudflare 配置
├── update.ps1              # 一键更新脚本
└── README.md               # 部署文档
```

## 数据格式

### bg_cards.json 结构

```json
{
  "meta": {
    "version": "35.6",
    "fetchedAt": "2026-06-09T08:04:30.781Z",
    "totalCards": 1372
  },
  "cards": [
    {
      "id": 110101,
      "cardId": "BG29_611",
      "name": "Cord Puller",
      "nameZh": "拔线机",
      "tier": 1,
      "cardType": "minion",
      "minionType": "Mech",
      "minionTypes": ["Mech"],
      "attack": 1,
      "health": 1,
      "attackGold": 2,
      "healthGold": 2,
      "manaCost": 0,
      "keywords": ["Divine Shield", "Deathrattle"],
      "isDuosOnly": false,
      "isSolosOnly": false,
      "isTimewarped": false,
      "pool": true,
      "childIds": [96810]
    }
  ]
}
```

### 字段说明

| 字段 | 类型 | 说明 |
|------|------|------|
| id | int | DbfId |
| cardId | string | CardId |
| name | string | 英文名 |
| nameZh | string | 中文名 |
| tier | int | 星级 (1-7) |
| cardType | string | minion/hero/anomaly/quest/reward/trinket/spell |
| minionType | string | 种族 (Mech/Beast/Demon 等) |
| minionTypes | array | 种族列表 |
| attack | int | 攻击力 |
| health | int | 生命值 |
| attackGold | int | 金色攻击力 |
| healthGold | int | 金色生命值 |
| manaCost | int | 法力消耗 |
| armor | int? | 护甲 (英雄) |
| keywords | array | 关键词列表 |
| isDuosOnly | bool | 仅双人模式 |
| isSolosOnly | bool | 仅单人模式 |
| isTimewarped | bool | 时空扭曲 |
| pool | bool? | 是否在当前池中 |
| childIds | array | 关联卡牌 ID |
| trinketTier | string | lesser/greater (饰品) |
| companionId | int? | Duos 伙伴 ID |

## 相关项目

| 项目 | 说明 |
|------|------|
| [hsbg.cards](https://hsbg.cards/) | 卡牌数据来源 |
| [hearthstonejson.com](https://hearthstonejson.com/) | 中文名数据来源 |
| [Cloudflare BGDB](../CloudflareBgdb/) | 自动更新服务 |

## 注意事项

- 数据更新频率：跟随炉石版本更新（通常每2-4周）
- hsbg.cards 是社区维护项目，数据可能有延迟
- 如果遇到数据缺失，检查 hsbg.cards API 是否包含该卡牌
- DLL 大小约 771KB（包含完整卡牌数据）
