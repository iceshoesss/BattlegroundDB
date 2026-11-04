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

    static async Task<int> Main(string[] args)
    {
        try
        {
            await RunAsync(args);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n❌ 失败: {ex.Message}");
            return 1;
        }
    }

    static async Task RunAsync(string[] args)
    {
        Console.WriteLine("=== HsbgFetcher - hsbg.cards 数据抓取器 ===\n");

        var outputDir = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BattlegroundDB", "Data");
        Directory.CreateDirectory(outputDir);

        // 1. 获取中文名和中文描述映射
        Console.Write("📡 获取中文名和描述数据... ");
        var (zhCnMap, zhTextMap) = await FetchZhCnData();
        Console.WriteLine($"✓ {zhCnMap.Count} 条记录");

        // 2. 分页获取所有 hsbg.cards 数据
        Console.Write("📡 获取 hsbg.cards 数据... ");
        var allCards = await FetchAllCards();
        Console.WriteLine($"✓ {allCards.Count} 张卡牌");
        if (allCards.Count == 0)
            throw new InvalidOperationException("hsbg.cards 返回 0 张卡牌，拒绝写出空数据");

        // 3. 合并数据并转换格式
        Console.Write("🔄 转换格式... ");
        var result = await MergeAndConvertAsync(allCards, zhCnMap, zhTextMap);
        Console.WriteLine($"✓ {result.Cards.Count} 张卡牌（已筛选 pool=true）");
        if (result.Cards.Count == 0)
            throw new InvalidOperationException("转换后卡牌数为 0，拒绝写出空数据");

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

    static async Task<(Dictionary<string, string> names, Dictionary<string, string> texts)> FetchZhCnData()
    {
        var names = new Dictionary<string, string>();
        var texts = new Dictionary<string, string>();

        var response = await HttpClient.GetAsync(HearthstoneJsonZhCn);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var cards = JsonSerializer.Deserialize<List<HearthstoneJsonCard>>(json);

        if (cards != null)
        {
            foreach (var card in cards)
            {
                if (!string.IsNullOrEmpty(card.Id))
                {
                    if (!string.IsNullOrEmpty(card.Name))
                        names[card.Id] = card.Name;

                    if (!string.IsNullOrEmpty(card.Text))
                        texts[card.Id] = card.Text;
                }
            }
        }

        return (names, texts);
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

    static async Task<BgdbResult> MergeAndConvertAsync(List<HsbgCard> hsbgCards, Dictionary<string, string> zhCnMap, Dictionary<string, string> zhTextMap)
    {
        // 从 hsbg.cards 获取最新版本号
        var version = await FetchLatestVersion();
        
        var result = new BgdbResult
        {
            Meta = new BgdbMeta
            {
                Version = version,
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
                CardId = card.ExternalId ?? "",
                Name = card.Name ?? "",
                NameZh = zhCnMap.GetValueOrDefault(card.ExternalId ?? "", ""),
                TextZh = zhTextMap.GetValueOrDefault(card.ExternalId ?? "", ""),
                Tier = card.Tier,
                CardType = card.CardType ?? "",
                MinionType = card.MinionType,
                MinionTypes = card.MinionTypes,
                Attack = card.Attack,
                Health = card.Health,
                AttackGold = card.AttackGold,
                HealthGold = card.HealthGold,
                ManaCost = card.ManaCost,
                Armor = card.Armor,
                Keywords = card.Keywords ?? new List<string>(),
                // hsbg.cards 的分类标签（如 "darkgift"、"token"）。下游（卡牌浏览器）靠它
                // 识别黑暗之赐等机制，不再依赖上游已不再收录的 BG36_MidGameEffect_000。
                Categories = card.Categories,
                IsDuosOnly = card.IsDuosOnly,
                IsSolosOnly = card.IsSolosOnly,
                IsTimewarped = card.IsTimewarped,
                Pool = card.Pool,
                ChildIds = card.ChildIds ?? new List<int>(),
                DbfIdGold = card.DbfIdGold,
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

    /// <summary>
    /// 解析 hsbg.cards 当前版本号。
    ///
    /// 历史坑：早期用 /api/v1/patches 的 currentPatch，但该端点已被官方下线（返回 404）。
    /// 旧代码在请求失败时会“静默”返回写死的 "35.6"，于是：
    ///   - 版本号永远变回 35.6，Release v35.6 早已存在 → CI 判定“不是新版本” → 后续步骤全部跳过；
    ///   - 手动 force 时又把 35.6 当成新版本发布出去，KV 的 json/latest 和 Worker 版本被一起改回 35.6。
    /// 所以这里必须：健康检查接口优先、失败就抛异常，绝不回退到任何写死的版本号。
    /// </summary>
    static async Task<string> FetchLatestVersion()
    {
        var errors = new List<string>();

        var sources = new (string Name, string Url, Func<string, string?> Pick)[]
        {
            ("healthz", $"{HsbgApiBase}/healthz", ReadPatchDisplay),
            ("patches", $"{HsbgApiBase}/patches", ReadLegacyCurrentPatch),
        };

        foreach (var source in sources)
        {
            try
            {
                var version = await RetryAsync(() => FetchVersionAsync(source.Url, source.Pick));
                if (!string.IsNullOrWhiteSpace(version) && PatchVersionRegex.IsMatch(version))
                {
                    Console.Write($"(版本: {version}) ");
                    return version;
                }

                errors.Add($"{source.Name}: 响应中没有可用的版本号");
            }
            catch (Exception ex)
            {
                errors.Add($"{source.Name}: {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            "无法从 hsbg.cards 解析最新版本号，已中止（不会回退到写死的旧版本）。" + string.Join(" | ", errors));
    }

    private static readonly Regex PatchVersionRegex = new(@"^\d+\.\d+(\.\d+)?$", RegexOptions.Compiled);

    private static async Task<T> RetryAsync<T>(Func<Task<T>> action, int attempts = 3)
    {
        Exception? last = null;
        for (var i = 1; i <= attempts; i++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                last = ex;
                if (i < attempts)
                    await Task.Delay(TimeSpan.FromSeconds(3 * i));
            }
        }

        throw last!;
    }

    private static async Task<string?> FetchVersionAsync(string url, Func<string, string?> pick)
    {
        using var response = await HttpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return pick(json);
    }

    /// <summary>当前 API：GET /api/v1/healthz → {"ok":true,"version":"1.0.0","cardsLoaded":2867,"patchDisplay":"37.0.2"}</summary>
    private static string? ReadPatchDisplay(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object &&
            doc.RootElement.TryGetProperty("patchDisplay", out var element) &&
            element.ValueKind == JsonValueKind.String)
        {
            return element.GetString()?.Trim();
        }

        return null;
    }

    /// <summary>旧 API：GET /api/v1/patches（已下线，仅作兼容兜底）</summary>
    private static string? ReadLegacyCurrentPatch(string json)
    {
        var patches = JsonSerializer.Deserialize<HsbgPatchesResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var latest = patches?.Data?.FirstOrDefault();
        return string.IsNullOrWhiteSpace(latest?.CurrentPatch) ? null : latest.CurrentPatch.Trim();
    }

    static bool IsTokenCard(HsbgCard card)
    {
        // 只有 minion 类型才可能是衍生物
        if (card.CardType != "minion") return false;

        var id = card.ExternalId;
        if (string.IsNullOrEmpty(id)) return false;

        // 1. t 结尾 或 t+数字 结尾（如 BG28_603t, BG27_004t2）
        if (Regex.IsMatch(id, @"t\d*$")) return true;

        // 2. pt 结尾 或 pt+数字 结尾（如 BG23_HERO_201pt）
        if (Regex.IsMatch(id, @"pt\d*$")) return true;

        // 3. 包含 SKIN（皮肤变体，如 TB_BaconShop_HP_033t_SKIN_D）
        if (id.Contains("SKIN")) return true;

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

public class HsbgPatchesResponse
{
    [JsonPropertyName("data")]
    public List<HsbgPatch>? Data { get; set; }
}

public class HsbgPatch
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("currentPatch")]
    public string CurrentPatch { get; set; } = "";

    [JsonPropertyName("previousPatch")]
    public string PreviousPatch { get; set; } = "";
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

    [JsonPropertyName("categories")]
    public List<string>? Categories { get; set; }

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

    [JsonPropertyName("dbfIdGold")]
    public int? DbfIdGold { get; set; }

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

    [JsonPropertyName("text")]
    public string? Text { get; set; }
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

    [JsonPropertyName("textZh")]
    public string TextZh { get; set; } = "";

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

    // null 时不写进 JSON（DefaultIgnoreCondition = WhenWritingNull），避免给所有卡加空数组
    [JsonPropertyName("categories")]
    public List<string>? Categories { get; set; }

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

    [JsonPropertyName("dbfIdGold")]
    public int? DbfIdGold { get; set; }

    [JsonPropertyName("trinketTier")]
    public string? TrinketTier { get; set; }

    [JsonPropertyName("companionId")]
    public int? CompanionId { get; set; }

    [JsonPropertyName("isToken")]
    public bool IsToken { get; set; }

    [JsonPropertyName("isBuddy")]
    public bool IsBuddy { get; set; }
}
