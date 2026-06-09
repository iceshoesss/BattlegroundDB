using System.Collections.Generic;
using Newtonsoft.Json;

namespace BattlegroundDB
{
    /// <summary>
    /// BGDB 数据根对象
    /// </summary>
    public class BgdbData
    {
        [JsonProperty("meta")]
        public BgdbMeta Meta { get; set; }

        [JsonProperty("cards")]
        public List<BgdbCard> Cards { get; set; }
    }

    /// <summary>
    /// 数据元信息
    /// </summary>
    public class BgdbMeta
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("fetchedAt")]
        public string FetchedAt { get; set; }

        [JsonProperty("totalCards")]
        public int TotalCards { get; set; }
    }

    /// <summary>
    /// 统一卡牌模型（hsbg.cards 格式）
    /// </summary>
    public class BgdbCard
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("cardId")]
        public string CardId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("nameZh")]
        public string NameZh { get; set; }

        [JsonProperty("tier")]
        public int? Tier { get; set; }

        [JsonProperty("cardType")]
        public string CardType { get; set; }

        [JsonProperty("minionType")]
        public string MinionType { get; set; }

        [JsonProperty("minionTypes")]
        public List<string> MinionTypes { get; set; }

        [JsonProperty("attack")]
        public int Attack { get; set; }

        [JsonProperty("health")]
        public int Health { get; set; }

        [JsonProperty("attackGold")]
        public int AttackGold { get; set; }

        [JsonProperty("healthGold")]
        public int HealthGold { get; set; }

        [JsonProperty("manaCost")]
        public int ManaCost { get; set; }

        [JsonProperty("armor")]
        public int? Armor { get; set; }

        [JsonProperty("keywords")]
        public List<string> Keywords { get; set; }

        [JsonProperty("isDuosOnly")]
        public bool IsDuosOnly { get; set; }

        [JsonProperty("isSolosOnly")]
        public bool IsSolosOnly { get; set; }

        [JsonProperty("isTimewarped")]
        public bool IsTimewarped { get; set; }

        [JsonProperty("pool")]
        public bool? Pool { get; set; }

        [JsonProperty("childIds")]
        public List<int> ChildIds { get; set; }

        [JsonProperty("trinketTier")]
        public string TrinketTier { get; set; }

        [JsonProperty("companionId")]
        public int? CompanionId { get; set; }

        [JsonProperty("isToken")]
        public bool IsToken { get; set; }

        [JsonProperty("isBuddy")]
        public bool IsBuddy { get; set; }

        // === 便捷属性 ===

        /// <summary>是否为随从</summary>
        [JsonIgnore]
        public bool IsMinion => CardType == "minion";

        /// <summary>是否为英雄</summary>
        [JsonIgnore]
        public bool IsHero => CardType == "hero";

        /// <summary>是否为畸变</summary>
        [JsonIgnore]
        public bool IsAnomaly => CardType == "anomaly";

        /// <summary>是否为任务</summary>
        [JsonIgnore]
        public bool IsQuest => CardType == "quest";

        /// <summary>是否为奖励</summary>
        [JsonIgnore]
        public bool IsReward => CardType == "reward";

        /// <summary>是否为饰品</summary>
        [JsonIgnore]
        public bool IsTrinket => CardType == "trinket";

        /// <summary>是否为法术</summary>
        [JsonIgnore]
        public bool IsSpell => CardType == "spell";

        /// <summary>是否为小型饰品</summary>
        [JsonIgnore]
        public bool IsLesserTrinket => TrinketTier == "lesser";

        /// <summary>是否为大型饰品</summary>
        [JsonIgnore]
        public bool IsGreaterTrinket => TrinketTier == "greater";

        /// <summary>是否包含某个关键词</summary>
        public bool HasKeyword(string keyword)
        {
            return Keywords != null && Keywords.Contains(keyword);
        }

        /// <summary>是否为中文名</summary>
        [JsonIgnore]
        public bool HasZhName => !string.IsNullOrEmpty(NameZh);
    }
}
