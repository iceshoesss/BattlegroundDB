using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HsbgFetcher;

class Program
{
    private static readonly HttpClient HttpClient = new();
    private const string HsbgApiBase = "https://hsbg.cards/api/v1";
    private const string HearthstoneJsonZhCn = "https://api.hearthstonejson.com/v1/latest/zhCN/cards.json";

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== HsbgFetcher - hsbg.cards 数据抓取器 ===\n");

        var outputDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BattlegroundDB", "Data");
        Directory.CreateDirectory(outputDir);

        // 1. 获取中文名映射
        Console.Write("📡 获取中文名数据... ");
        var zhCnMap = await FetchZhCnNames();
        Console.WriteLine($"✓ {zhCnMap.Count} 条记录");

        // 2. 分页获取所有 hsbg.cards 数据
        Console.Write("📡 获取 hsbg.cards 数据... ");
        var allCards = await FetchAllCards();
        Console.WriteLine($"✓ {allCards.Count} 张卡牌");

        // 3. 合并数据并转换格式
        Console.Write("🔄 转换格式... ");
        var result = MergeAndConvert(allCards, zhCnMap);
        Console.WriteLine($"✓ {result.Cards.Count} 张卡牌（已筛选 pool=true）");

        // 4. 保存 JSON
        var outputPath = Path.Combine(outputDir, "bg_cards.json");
        SaveJson(outputPath, result);
        Console.WriteLine($"\n✅ 已保存到: {outputPath}");
        Console.WriteLine($"   随从: {result.Cards.Count(c => c.CardType == "minion")}");
        Console.WriteLine($"   英雄: {result.Cards.Count(c => c.CardType == "hero")}");
        Console.WriteLine($"   畸变: {result.Cards.Count(c => c.CardType == "anomaly")}");
        Console.WriteLine($"   任务: {result.Cards.Count(c => c.CardType == "quest")}");
        Console.WriteLine($"   奖励: {result.Cards.Count(c => c.CardType == "reward")}");
        Console.WriteLine($"   饰品: {result.Cards.Count(c => c.CardType == "trinket")}");
        Console.WriteLine($"   法术: {result.Cards.Count(c => c.CardType == "spell")}");
        Console.WriteLine($"   其他: {result.Cards.Count(c => !new[] { "minion", "hero", "anomaly", "quest", "reward", "trinket", "spell" }.Contains(c.CardType))}");
    }

    static async Task<Dictionary<string, string>> FetchZhCnNames()
    {
        var map = new Dictionary<string, string>();

        var response = await HttpClient.GetAsync(HearthstoneJsonZhCn);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var cards = JsonSerializer.Deserialize<List<HearthstoneJsonCard>>(json);

        if (cards != null)
        {
            foreach (var card in cards)
            {
                if (!string.IsNullOrEmpty(card.Id) && !string.IsNullOrEmpty(card.Name))
                {
                    map[card.Id] = card.Name;
                }
            }
        }

        return map;
    }

    static async Task<List<HsbgCard>> FetchAllCards()
    {
        var allCards = new List<HsbgCard>();
        var offset = 0;
        const int limit = 100;  // API 实际最大返回 100

        while (true)
        {
            var url = $"{HsbgApiBase}/cards?pool=current&limit={limit}&offset={offset}";
            
            var response = await HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var page = JsonSerializer.Deserialize<HsbgPageResponse>(json);

            if (page?.Data == null || page.Data.Count == 0)
                break;

            allCards.AddRange(page.Data);

            // 检查是否还有更多数据
            if (page.Pagination?.NextOffset == null || page.Data.Count < limit)
                break;

            offset = page.Pagination.NextOffset.Value;
        }

        return allCards;
    }

    static BgdbResult MergeAndConvert(List<HsbgCard> hsbgCards, Dictionary<string, string> zhCnMap)
    {
        var result = new BgdbResult
        {
            Meta = new BgdbMeta
            {
                Version = "35.6",
                FetchedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                TotalCards = 0
            },
            Cards = new List<BgdbCard>()
        };

        foreach (var card in hsbgCards)
        {
            // 随从只保留 pool=true
            if (card.CardType == "minion" && card.Pool != true)
                continue;

            var bgdbCard = new BgdbCard
            {
                Id = card.Id,
                CardId = card.ExternalId,
                Name = card.Name,
                NameZh = zhCnMap.GetValueOrDefault(card.ExternalId, ""),
                Tier = card.Tier,
                CardType = card.CardType,
                MinionType = card.MinionType,
                MinionTypes = card.MinionTypes,
                Attack = card.Attack,
                Health = card.Health,
                AttackGold = card.AttackGold,
                HealthGold = card.HealthGold,
                ManaCost = card.ManaCost,
                Armor = card.Armor,
                Keywords = card.Keywords ?? new List<string>(),
                IsDuosOnly = card.IsDuosOnly,
                IsSolosOnly = card.IsSolosOnly,
                IsTimewarped = card.IsTimewarped,
                Pool = card.Pool,
                ChildIds = card.ChildIds ?? new List<int>(),
                TrinketTier = card.TrinketTier,
                CompanionId = card.CompanionId,
                IsToken = IsTokenCard(card),
                IsBuddy = card.ExternalId?.EndsWith("_Buddy") == true
            };

            result.Cards.Add(bgdbCard);
        }

        result.Meta.TotalCards = result.Cards.Count;
        return result;
    }

    static void SaveJson(string path, BgdbResult data)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(path, json);
    }

    static bool IsTokenCard(HsbgCard card)
    {
        // 只有 minion 类型才可能是衍生物
        if (card.CardType != "minion") return false;

        var id = card.ExternalId;
        if (string.IsNullOrEmpty(id)) return false;

        // t 结尾 或 t+数字 结尾（如 BG28_603t, BG27_004t2）
        if (Regex.IsMatch(id, @"t\d*$")) return true;

        // pt 结尾 或 pt+数字 结尾（如 BG23_HERO_201pt）
        if (Regex.IsMatch(id, @"pt\d*$")) return true;

        return false;
    }
}

// === hsbg.cards API 模型 ===

public class HsbgPageResponse
{
    [JsonPropertyName("data")]
    public List<HsbgCard>? Data { get; set; }

    [JsonPropertyName("pagination")]
    public HsbgPagination? Pagination { get; set; }
}

public class HsbgPagination
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("nextOffset")]
    public int? NextOffset { get; set; }
}

public class HsbgCard
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("externalId")]
    public string ExternalId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("tier")]
    public int? Tier { get; set; }

    [JsonPropertyName("cardType")]
    public string CardType { get; set; } = "";

    [JsonPropertyName("minionType")]
    public string? MinionType { get; set; }

    [JsonPropertyName("minionTypes")]
    public List<string>? MinionTypes { get; set; }

    [JsonPropertyName("attack")]
    public int Attack { get; set; }

    [JsonPropertyName("health")]
    public int Health { get; set; }

    [JsonPropertyName("attackGold")]
    public int AttackGold { get; set; }

    [JsonPropertyName("healthGold")]
    public int HealthGold { get; set; }

    [JsonPropertyName("manaCost")]
    public int ManaCost { get; set; }

    [JsonPropertyName("armor")]
    public int? Armor { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("isDuosOnly")]
    public bool IsDuosOnly { get; set; }

    [JsonPropertyName("isSolosOnly")]
    public bool IsSolosOnly { get; set; }

    [JsonPropertyName("isTimewarped")]
    public bool IsTimewarped { get; set; }

    [JsonPropertyName("pool")]
    public bool? Pool { get; set; }

    [JsonPropertyName("childIds")]
    public List<int>? ChildIds { get; set; }

    [JsonPropertyName("trinketTier")]
    public string? TrinketTier { get; set; }

    [JsonPropertyName("companionId")]
    public int? CompanionId { get; set; }
}

// === HearthstoneJSON 中文名模型 ===

public class HearthstoneJsonCard
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

// === 输出模型 ===

public class BgdbResult
{
    [JsonPropertyName("meta")]
    public BgdbMeta Meta { get; set; } = new();

    [JsonPropertyName("cards")]
    public List<BgdbCard> Cards { get; set; } = new();
}

public class BgdbMeta
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("fetchedAt")]
    public string FetchedAt { get; set; } = "";

    [JsonPropertyName("totalCards")]
    public int TotalCards { get; set; }
}

public class BgdbCard
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("cardId")]
    public string CardId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("nameZh")]
    public string NameZh { get; set; } = "";

    [JsonPropertyName("tier")]
    public int? Tier { get; set; }

    [JsonPropertyName("cardType")]
    public string CardType { get; set; } = "";

    [JsonPropertyName("minionType")]
    public string? MinionType { get; set; }

    [JsonPropertyName("minionTypes")]
    public List<string>? MinionTypes { get; set; }

    [JsonPropertyName("attack")]
    public int Attack { get; set; }

    [JsonPropertyName("health")]
    public int Health { get; set; }

    [JsonPropertyName("attackGold")]
    public int AttackGold { get; set; }

    [JsonPropertyName("healthGold")]
    public int HealthGold { get; set; }

    [JsonPropertyName("manaCost")]
    public int ManaCost { get; set; }

    [JsonPropertyName("armor")]
    public int? Armor { get; set; }

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("isDuosOnly")]
    public bool IsDuosOnly { get; set; }

    [JsonPropertyName("isSolosOnly")]
    public bool IsSolosOnly { get; set; }

    [JsonPropertyName("isTimewarped")]
    public bool IsTimewarped { get; set; }

    [JsonPropertyName("pool")]
    public bool? Pool { get; set; }

    [JsonPropertyName("childIds")]
    public List<int> ChildIds { get; set; } = new();

    [JsonPropertyName("trinketTier")]
    public string? TrinketTier { get; set; }

    [JsonPropertyName("companionId")]
    public int? CompanionId { get; set; }

    [JsonPropertyName("isToken")]
    public bool IsToken { get; set; }

    [JsonPropertyName("isBuddy")]
    public bool IsBuddy { get; set; }
}
