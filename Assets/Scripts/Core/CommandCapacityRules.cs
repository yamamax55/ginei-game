namespace Ginei
{
    /// <summary>
    /// 階級ごとの指揮可能規模（RANKCMD-2 #1712・銀河英雄伝説準拠）。人物が率いられる最大兵力（隻）を階級 tier から定める。
    /// <b>兵力（艦隊数）は艦隊が持ち</b>（<see cref="FleetUnitData"/>/`FleetStrength`・RANKCMD-1）、<b>人物は「どれだけ率いられるか」だけを階級で持つ</b>。
    /// tier は <see cref="RankSystem"/> 既定ラダー（准将5/少将6/中将7/大将8/上級大将9/元帥10）。
    /// 各所のインライン判定を増やさずここへ集約（<see cref="CombatModifiers"/>#106 と同方針）。梯団種別↔規模の一表化（EchelonType 拡張後）は ORBAT-2 で。
    /// 純ロジック・test-first。
    /// </summary>
    public static class CommandCapacityRules
    {
        // 銀英伝準拠の指揮可能兵力ラダー（隻・目安）。一個艦隊 ≒ 1.2〜1.5万隻＝中将/大将。調整可。
        public const int Cap准将 = 3000;     // tier5：分艦隊
        public const int Cap少将 = 6000;     // tier6：分艦隊
        public const int Cap中将 = 12000;    // tier7：一個艦隊の司令官になれる下限
        public const int Cap大将 = 15000;    // tier8：標準的な一個艦隊
        public const int Cap上級大将 = 30000; // tier9：複数艦隊/方面
        public const int Cap元帥 = 60000;    // tier10：宇宙艦隊総司令（数個艦隊）
        public const int CapSub = 1000;      // 准将未満（佐官/尉官＝戦隊以下）

        /// <summary>その階級 tier が指揮できる最大兵力（隻）。10超は元帥級、准将(5)未満は最小。</summary>
        public static int MaxStrengthForTier(int tier)
        {
            switch (tier)
            {
                case 5:  return Cap准将;
                case 6:  return Cap少将;
                case 7:  return Cap中将;
                case 8:  return Cap大将;
                case 9:  return Cap上級大将;
                case 10: return Cap元帥;
                default: return tier > 10 ? Cap元帥 : CapSub;
            }
        }

        /// <summary>その兵力の艦隊をその階級が指揮できるか（兵力 ≤ 指揮限界）。</summary>
        public static bool CanCommand(int tier, int fleetStrength) => fleetStrength <= MaxStrengthForTier(tier);

        /// <summary>その兵力を率いるのに要る最小階級 tier（艦隊指揮の下限＝准将5）。</summary>
        public static int RequiredTierForStrength(int strength)
        {
            if (strength <= Cap准将) return 5;
            if (strength <= Cap少将) return 6;
            if (strength <= Cap中将) return 7;
            if (strength <= Cap大将) return 8;
            if (strength <= Cap上級大将) return 9;
            return 10;
        }
    }
}
