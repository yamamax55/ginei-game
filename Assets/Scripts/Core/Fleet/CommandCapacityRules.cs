namespace Ginei
{
    /// <summary>
    /// 階級ごとの指揮可能規模（RANKCMD-2 #1712・銀河英雄伝説準拠）。人物が率いられる最大兵力（隻）を階級 tier から定める。
    /// <b>兵力（艦隊数）は艦隊が持ち</b>（<see cref="FleetUnitData"/>/`FleetStrength`・RANKCMD-1）、<b>人物は「どれだけ率いられるか」だけを階級で持つ</b>。
    /// tier は <see cref="RankSystem"/> 既定ラダー（准将5/少将6/中将7/大将8/上級大将9/元帥10）。
    /// 各所のインライン判定を増やさずここへ集約（<see cref="CombatModifiers"/>#106 と同方針）。階級→自然な梯団の段は <see cref="EchelonForTier"/>（RANKCMD-4・分艦隊 echelon 追加後）。
    /// 純ロジック・test-first。
    /// </summary>
    public static class CommandCapacityRules
    {
        // 銀英伝準拠の指揮可能兵力ラダー（隻・目安）。一個艦隊 ≒ 1.2〜1.5万隻＝中将/大将。調整可。
        // 階級 tier は完全12段ラダー（少尉1〜元帥12・RankSystem）。将官は准将7〜元帥12。
        public const int Cap准将 = 3000;     // tier7：分艦隊
        public const int Cap少将 = 6000;     // tier8：分艦隊
        public const int Cap中将 = 12000;    // tier9：一個艦隊の司令官になれる下限
        public const int Cap大将 = 15000;    // tier10：標準的な一個艦隊
        public const int Cap上級大将 = 30000; // tier11：複数艦隊/方面
        public const int Cap元帥 = 60000;    // tier12：宇宙艦隊総司令（数個艦隊）
        public const int CapSub = 1000;      // 准将未満（佐官/尉官＝戦隊以下）

        /// <summary>その階級 tier が指揮できる最大兵力（隻）。12超は元帥級、准将(7)未満は最小。</summary>
        public static int MaxStrengthForTier(int tier)
        {
            switch (tier)
            {
                case 7:  return Cap准将;
                case 8:  return Cap少将;
                case 9:  return Cap中将;
                case 10: return Cap大将;
                case 11: return Cap上級大将;
                case 12: return Cap元帥;
                default: return tier > 12 ? Cap元帥 : CapSub;
            }
        }

        /// <summary>その兵力の艦隊をその階級が指揮できるか（兵力 ≤ 指揮限界）。</summary>
        public static bool CanCommand(int tier, int fleetStrength) => fleetStrength <= MaxStrengthForTier(tier);

        /// <summary>その兵力を率いるのに要る最小階級 tier（艦隊指揮の下限＝准将5）。</summary>
        public static int RequiredTierForStrength(int strength)
        {
            if (strength <= Cap准将) return 7;
            if (strength <= Cap少将) return 8;
            if (strength <= Cap中将) return 9;
            if (strength <= Cap大将) return 10;
            if (strength <= Cap上級大将) return 11;
            return 12;
        }

        /// <summary>
        /// その階級 tier が指揮するのに<b>自然な梯団の段</b>（RANKCMD-4 #1714・銀英伝準拠）。
        /// 准将/少将＝分艦隊、中将/大将＝艦隊（大将も艦隊司令が自然）、上級大将＝軍団（艦隊群/方面）、元帥＝軍集団（宇宙艦隊）。
        /// 配属の「下限」は <see cref="OrderOfBattle.RequiredTier"/>（≥判定）で別に効く＝これは「その階級らしい段」を示す目安。
        /// </summary>
        public static EchelonType EchelonForTier(int tier)
        {
            if (tier <= 8) return EchelonType.分艦隊;   // 〜准将7/少将8（尉官・佐官も最小段）
            if (tier <= 10) return EchelonType.艦隊;    // 中将9/大将10
            if (tier == 11) return EchelonType.軍団;    // 上級大将＝艦隊群/方面
            return EchelonType.軍集団;                  // 元帥12＝宇宙艦隊
        }

        // ===== ORBAT-2 #1718：梯団↔指揮官階級↔規模（隻）の一表 =====
        // 現実準拠（軍隊の編制）の宇宙艦隊系。各梯団の「標準指揮官階級 tier」と「規模レンジ（隻）」をここで一元定義し、
        // <see cref="OrderOfBattle.RequiredTier"/> の出所にする（二重定義しない）。指揮官 tier は >= ゲートの下限。
        // ※実際の配属ゲートは配下兵力（StrengthUnder）で RANKCMD-3 が別途効く＝この規模は「その段らしい目安」。

        // 標準指揮官階級 tier（#14 完全12段ラダー・段が上がるほど単調非減少）
        public const int Tier戦隊 = 6;     // 大佐
        public const int Tier分艦隊 = 8;   // 少将
        public const int Tier艦隊 = 9;     // 中将
        public const int Tier軍団 = 10;    // 大将
        public const int Tier軍 = 11;      // 上級大将
        public const int Tier軍集団 = 12;  // 元帥（＝方面軍）
        public const int Tier宇宙艦隊 = 12; // 元帥（＝総軍・最上段）

        // 規模レンジ（隻・目安）。艦隊＝基幹単位 1.2〜1.5万隻に合わせて上下を現実準拠で配する。
        public const int Ships戦隊Min = 300, Ships戦隊Max = 1500;
        public const int Ships分艦隊Min = 1500, Ships分艦隊Max = 6000;
        public const int Ships艦隊Min = 12000, Ships艦隊Max = 15000;
        public const int Ships軍団Min = 24000, Ships軍団Max = 45000;   // 2〜3艦隊
        public const int Ships軍Min = 24000, Ships軍Max = 60000;       // 2〜4艦隊
        public const int Ships軍集団Min = 60000, Ships軍集団Max = 90000; // 数個艦隊
        public const int Ships宇宙艦隊Min = 90000;                      // 全軍（上限なし）

        /// <summary>その梯団の標準プロファイル（指揮官階級 tier ＋規模レンジ）。ORBAT-2 #1718 の一表。</summary>
        public static EchelonProfile ProfileFor(EchelonType echelon)
        {
            switch (echelon)
            {
                case EchelonType.戦隊:    return new EchelonProfile(echelon, Tier戦隊, Ships戦隊Min, Ships戦隊Max);
                case EchelonType.分艦隊:  return new EchelonProfile(echelon, Tier分艦隊, Ships分艦隊Min, Ships分艦隊Max);
                case EchelonType.艦隊:    return new EchelonProfile(echelon, Tier艦隊, Ships艦隊Min, Ships艦隊Max);
                case EchelonType.軍団:    return new EchelonProfile(echelon, Tier軍団, Ships軍団Min, Ships軍団Max);
                case EchelonType.軍:      return new EchelonProfile(echelon, Tier軍, Ships軍Min, Ships軍Max);
                case EchelonType.軍集団:  return new EchelonProfile(echelon, Tier軍集団, Ships軍集団Min, Ships軍集団Max);
                case EchelonType.宇宙艦隊: return new EchelonProfile(echelon, Tier宇宙艦隊, Ships宇宙艦隊Min, int.MaxValue);
                default:                  return new EchelonProfile(EchelonType.艦隊, Tier艦隊, Ships艦隊Min, Ships艦隊Max);
            }
        }

        /// <summary>その梯団の標準指揮官階級 tier（<see cref="OrderOfBattle.RequiredTier"/> の出所）。</summary>
        public static int CommanderTierFor(EchelonType echelon) => ProfileFor(echelon).commanderTier;
    }
}
