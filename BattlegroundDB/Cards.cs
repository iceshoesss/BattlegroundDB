using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace BattlegroundDB
{
    /// <summary>
    /// 战棋卡牌数据库（v2 - hsbg.cards 格式）
    /// </summary>
    public static class Cards
    {
        // ── 全部卡牌 ──
        public static List<BgdbCard> AllCards { get; private set; } = new List<BgdbCard>();

        // ── 按 CardId 索引 ──
        public static Dictionary<string, BgdbCard> ByCardId { get; private set; } = new Dictionary<string, BgdbCard>();

        // ── 按 DbfId (id) 索引 ──
        public static Dictionary<int, BgdbCard> ById { get; private set; } = new Dictionary<int, BgdbCard>();

        // ── 按类型分组 ──
        public static List<BgdbCard> Minions { get; private set; } = new List<BgdbCard>();
        public static List<BgdbCard> Heroes { get; private set; } = new List<BgdbCard>();
        public static List<BgdbCard> Anomalies { get; private set; } = new List<BgdbCard>();
        public static List<BgdbCard> Quests { get; private set; } = new List<BgdbCard>();
        public static List<BgdbCard> Rewards { get; private set; } = new List<BgdbCard>();
        public static List<BgdbCard> Trinkets { get; private set; } = new List<BgdbCard>();
        public static List<BgdbCard> Spells { get; private set; } = new List<BgdbCard>();

        // ── 元信息 ──
        public static BgdbMeta Meta { get; private set; }

        private static bool _loaded = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// 加载所有数据（自动调用，无需手动调用）
        /// </summary>
        public static void Load()
        {
            if (_loaded) return;

            lock (_lock)
            {
                if (_loaded) return;

                var data = LoadJson<BgdbData>("bg_cards.json");
                if (data == null) return;

                Meta = data.Meta;
                AllCards = data.Cards ?? new List<BgdbCard>();

                // 建立索引
                ByCardId = AllCards.Where(c => !string.IsNullOrEmpty(c.CardId))
                    .GroupBy(c => c.CardId)
                    .ToDictionary(g => g.Key, g => g.First());

                ById = AllCards.Where(c => c.Id > 0)
                    .GroupBy(c => c.Id)
                    .ToDictionary(g => g.Key, g => g.First());

                // 按类型分组
                Minions = AllCards.Where(c => c.IsMinion).ToList();
                Heroes = AllCards.Where(c => c.IsHero).ToList();
                Anomalies = AllCards.Where(c => c.IsAnomaly).ToList();
                Quests = AllCards.Where(c => c.IsQuest).ToList();
                Rewards = AllCards.Where(c => c.IsReward).ToList();
                Trinkets = AllCards.Where(c => c.IsTrinket).ToList();
                Spells = AllCards.Where(c => c.IsSpell).ToList();

                _loaded = true;
            }
        }

        // === 查询方法 ===

        /// <summary>
        /// 获取卡牌（按 CardId）
        /// </summary>
        public static BgdbCard GetByCardId(string cardId)
        {
            Load();
            return ByCardId.TryGetValue(cardId, out var card) ? card : null;
        }

        /// <summary>
        /// 获取卡牌（按 DbfId）
        /// </summary>
        public static BgdbCard GetById(int id)
        {
            Load();
            return ById.TryGetValue(id, out var card) ? card : null;
        }

        /// <summary>
        /// 获取随从（按星级）
        /// </summary>
        public static List<BgdbCard> GetMinionsByTier(int tier)
        {
            Load();
            return Minions.Where(m => m.Tier == tier).ToList();
        }

        /// <summary>
        /// 获取随从（按种族）
        /// </summary>
        public static List<BgdbCard> GetMinionsByMinionType(string minionType)
        {
            Load();
            return Minions.Where(m => m.MinionType != null &&
                m.MinionType.Equals(minionType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// 获取随从（按关键词）
        /// </summary>
        public static List<BgdbCard> GetMinionsByKeyword(string keyword)
        {
            Load();
            return Minions.Where(m => m.HasKeyword(keyword)).ToList();
        }

        /// <summary>
        /// 获取所有种族
        /// </summary>
        public static HashSet<string> GetMinionTypes()
        {
            Load();
            return new HashSet<string>(Minions
                .Select(m => m.MinionType)
                .Where(t => !string.IsNullOrEmpty(t) && t != "All"));
        }

        /// <summary>
        /// 获取英雄
        /// </summary>
        public static BgdbCard GetHero(string cardId)
        {
            Load();
            return Heroes.FirstOrDefault(h => h.CardId == cardId);
        }

        /// <summary>
        /// 获取英雄（按 DbfId）
        /// </summary>
        public static BgdbCard GetHero(int id)
        {
            Load();
            return Heroes.FirstOrDefault(h => h.Id == id);
        }

        /// <summary>
        /// 获取畸变
        /// </summary>
        public static BgdbCard GetAnomaly(string cardId)
        {
            Load();
            return Anomalies.FirstOrDefault(a => a.CardId == cardId);
        }

        /// <summary>
        /// 获取任务
        /// </summary>
        public static BgdbCard GetQuest(string cardId)
        {
            Load();
            return Quests.FirstOrDefault(q => q.CardId == cardId);
        }

        /// <summary>
        /// 获取饰品（按 CardId）
        /// </summary>
        public static BgdbCard GetTrinket(string cardId)
        {
            Load();
            return Trinkets.FirstOrDefault(t => t.CardId == cardId);
        }

        /// <summary>
        /// 获取小型饰品
        /// </summary>
        public static List<BgdbCard> GetLesserTrinkets()
        {
            Load();
            return Trinkets.Where(t => t.IsLesserTrinket).ToList();
        }

        /// <summary>
        /// 获取大型饰品
        /// </summary>
        public static List<BgdbCard> GetGreaterTrinkets()
        {
            Load();
            return Trinkets.Where(t => t.IsGreaterTrinket).ToList();
        }

        /// <summary>
        /// 获取伙伴
        /// </summary>
        public static List<BgdbCard> GetBuddies()
        {
            Load();
            return AllCards.Where(c => c.CardId != null && c.CardId.Contains("_Buddy")).ToList();
        }

        /// <summary>
        /// 搜索卡牌（按名称，支持中英文）
        /// </summary>
        public static List<BgdbCard> Search(string query)
        {
            Load();
            if (string.IsNullOrWhiteSpace(query)) return new List<BgdbCard>();

            var lowerQuery = query.ToLower();
            return AllCards.Where(c =>
                (c.Name != null && c.Name.ToLower().Contains(lowerQuery)) ||
                (c.NameZh != null && c.NameZh.ToLower().Contains(lowerQuery)) ||
                (c.CardId != null && c.CardId.ToLower().Contains(lowerQuery))
            ).ToList();
        }

        // === 私有方法 ===

        private static T LoadJson<T>(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fullName = $"BattlegroundDB.Data.{resourceName}";

            using (var stream = assembly.GetManifestResourceStream(fullName))
            {
                if (stream == null)
                    return default;

                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<T>(json);
                }
            }
        }
    }
}
