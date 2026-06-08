namespace Ginei
{
    /// <summary>
    /// 階級の序列（tier）に関する判定の唯一の窓口（static）。
    /// 階級は勢力ごとに体系が異なるため、共通の tier（大きいほど上位）を基準に
    /// 「同列」「上下比較」「昇進先」「欠番の解決」を提供する。
    /// 階級表そのものは各 <see cref="FactionData.ranks"/> が持つ。
    /// </summary>
    public static class RankSystem
    {
        /// <summary>2つの階級が同列か（勢力をまたいでも tier が同じなら同列）。</summary>
        public static bool AreEquivalent(int tierA, int tierB) => tierA == tierB;

        /// <summary>tier の上下比較（上位ほど大。a&gt;b で正、a&lt;b で負、同列で0）。</summary>
        public static int Compare(int tierA, int tierB) => tierA.CompareTo(tierB);

        /// <summary>tierA が tierB より上位か。</summary>
        public static bool IsHigher(int tierA, int tierB) => tierA > tierB;

        /// <summary>
        /// 昇進先の tier を返す。自勢力の階級表で「現 tier より上の最小 tier」。
        /// 既に最高位、または上位が無ければ現 tier を据え置きで返す。
        /// </summary>
        public static int NextRankTier(FactionData faction, int currentTier)
        {
            if (faction == null || faction.ranks == null) return currentTier;
            int best = int.MaxValue;
            bool found = false;
            foreach (var r in faction.ranks)
            {
                if (r == null) continue;
                if (r.tier > currentTier && r.tier < best) { best = r.tier; found = true; }
            }
            return found ? best : currentTier;
        }

        /// <summary>
        /// 降格先の tier を返す。自勢力の階級表で「現 tier より下の最大 tier」。
        /// 既に最下位、または下位が無ければ現 tier を据え置きで返す。
        /// </summary>
        public static int PreviousRankTier(FactionData faction, int currentTier)
        {
            if (faction == null || faction.ranks == null) return currentTier;
            int best = int.MinValue;
            bool found = false;
            foreach (var r in faction.ranks)
            {
                if (r == null) continue;
                if (r.tier < currentTier && r.tier > best) { best = r.tier; found = true; }
            }
            return found ? best : currentTier;
        }

        /// <summary>
        /// 指定 tier をその勢力に「存在する」 tier へ丸める。
        /// 完全一致があればそのまま。無ければ直近下位、それも無ければ直近上位。
        /// （例：tier 9＝上級大将を持たない同盟では大将(8)へスナップ）
        /// 階級が一つも無ければ tier をそのまま返す。
        /// </summary>
        public static int ResolveTier(FactionData faction, int tier)
        {
            if (faction == null || faction.ranks == null || faction.ranks.Count == 0) return tier;

            int lower = int.MinValue;   // 直近下位
            int higher = int.MaxValue;  // 直近上位
            bool hasLower = false, hasHigher = false, exact = false;

            foreach (var r in faction.ranks)
            {
                if (r == null) continue;
                if (r.tier == tier) { exact = true; break; }
                if (r.tier < tier && r.tier > lower) { lower = r.tier; hasLower = true; }
                if (r.tier > tier && r.tier < higher) { higher = r.tier; hasHigher = true; }
            }

            if (exact) return tier;
            if (hasLower) return lower;
            if (hasHigher) return higher;
            return tier;
        }

        /// <summary>
        /// 指定 tier の表示用階級名を、所属勢力の階級表から解決する（#14・HUD表示用）。
        /// tier が 0 以下（未設定）または faction が null なら空文字＝階級を出さない（後方互換）。
        /// 欠番 tier は <see cref="ResolveTier"/> で直近 tier へ丸めてから名称を引く。
        /// </summary>
        public static string ResolveRankName(FactionData faction, int tier)
        {
            if (faction == null || tier <= 0) return string.Empty;
            return faction.GetRankName(ResolveTier(faction, tier));
        }
    }
}
