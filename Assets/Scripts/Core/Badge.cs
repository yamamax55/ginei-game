namespace Ginei
{
    /// <summary>
    /// 徽章（insignia/badge）の区分。<b>勲章（#2263＝戦功→恩給/名誉）とは別軸</b>＝資格・身分・所属の標章。
    /// 階級章（rank insignia）/兵科章（branch）/技能章（qualification・SOFのトライデント等）/部隊章（unit）。
    /// </summary>
    public enum BadgeKind { 階級章, 兵科章, 技能章, 部隊章 }

    /// <summary>技能章（資格徽章）の種類。既存システムの資格に対応＝特殊作戦#SOF/参謀#参謀本部/操艦。</summary>
    public enum SkillBadge { 特殊作戦, 参謀, 操艦 }

    /// <summary>
    /// 1つの徽章（#徽章）。種別＋表示名＋（階級章のみ）階級tier。識別・表示の標章であり、merit の勲章とは別物。
    /// </summary>
    public readonly struct Badge
    {
        public readonly BadgeKind kind;
        public readonly string name;
        public readonly int tier; // 階級章のみ有効（階級tier）。他は0。

        public Badge(BadgeKind kind, string name, int tier = 0)
        {
            this.kind = kind; this.name = name ?? ""; this.tier = tier;
        }
    }
}
