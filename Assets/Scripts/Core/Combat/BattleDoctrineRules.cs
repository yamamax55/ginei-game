namespace Ginei
{
    /// <summary>
    /// 戦術ドクトリン（<see cref="BattleDoctrine"/>）ごとの係数の単一窓口（#2365 STSH-5）。陣形特性
    /// (<see cref="FormationTraitRules"/>) と直交する次元＝姿勢が攻撃/生存性/集中リスク/機動へ与える倍率を返す。
    /// 集中＝火力集約（攻撃↑・広域攻撃に脆い＝集中リスク↑）／分散＝広域展開（生存性↑・集中リスク↓・火力やや控えめ）／
    /// 機動＝速度重視（機動↑）。すべて 1.0 中心の小さな振れ幅（実効値パターン・基準値非破壊）。test-first。
    /// </summary>
    public static class BattleDoctrineRules
    {
        // --- 攻撃倍率（与ダメージ）。集中で火力集約↑、機動はやや控えめ ---
        public const float ConcentratedAttack = 1.20f; // 集中＝火力一点集約
        public const float DispersedAttack = 0.95f;    // 分散＝火力はやや散る
        public const float ManeuverAttack = 0.90f;     // 機動＝速度優先で火力控えめ

        // --- 生存性倍率（>1ほど打たれ強い）。分散で広域攻撃に強靭、集中は脆い ---
        public const float ConcentratedSurvival = 0.90f; // 集中＝固まって脆い
        public const float DispersedSurvival = 1.20f;    // 分散＝広く散って強靭
        public const float ManeuverSurvival = 1.05f;     // 機動＝回避でやや強い

        // --- 集中リスク倍率（広域・範囲攻撃への被弾しやすさ。>1ほど脆い）。集中で↑、分散で↓ ---
        public const float ConcentratedRisk = 1.20f; // 集中＝密集して範囲攻撃に脆い
        public const float DispersedRisk = 0.85f;    // 分散＝散開して範囲攻撃に強い
        public const float ManeuverRisk = 1.00f;     // 機動＝中立

        // --- 機動倍率（速度/回頭）。機動↑、他は中立 ---
        public const float ManeuverMobility = 1.20f;     // 機動＝速度重視
        public const float ConcentratedMobility = 1.00f; // 集中＝中立
        public const float DispersedMobility = 0.95f;    // 分散＝展開で取り回しやや鈍い

        public const float NeutralFactor = 1.00f;

        // --- 陣形との相性。姿勢×陣形が噛み合うと有利、外すと不利。0.85..1.20 ---
        public const float CompatGood = 1.20f;    // 噛み合う
        public const float CompatNeutral = 1.00f; // 標準
        public const float CompatPoor = 0.85f;    // 外している

        /// <summary>与ダメージ倍率（攻撃姿勢）。集中が最大。</summary>
        public static float AttackFactor(BattleDoctrine d)
        {
            switch (d)
            {
                case BattleDoctrine.集中: return ConcentratedAttack;
                case BattleDoctrine.分散: return DispersedAttack;
                case BattleDoctrine.機動: return ManeuverAttack;
                default: return NeutralFactor;
            }
        }

        /// <summary>生存性倍率（&gt;1ほど打たれ強い）。分散が最大。</summary>
        public static float SurvivabilityFactor(BattleDoctrine d)
        {
            switch (d)
            {
                case BattleDoctrine.集中: return ConcentratedSurvival;
                case BattleDoctrine.分散: return DispersedSurvival;
                case BattleDoctrine.機動: return ManeuverSurvival;
                default: return NeutralFactor;
            }
        }

        /// <summary>集中リスク倍率（広域攻撃への脆さ・&gt;1ほど脆い）。集中が最大・分散が最小。</summary>
        public static float ConcentrationRisk(BattleDoctrine d)
        {
            switch (d)
            {
                case BattleDoctrine.集中: return ConcentratedRisk;
                case BattleDoctrine.分散: return DispersedRisk;
                case BattleDoctrine.機動: return ManeuverRisk;
                default: return NeutralFactor;
            }
        }

        /// <summary>速度/回頭倍率（機動姿勢）。機動が最大。</summary>
        public static float MobilityFactor(BattleDoctrine d)
        {
            switch (d)
            {
                case BattleDoctrine.集中: return ConcentratedMobility;
                case BattleDoctrine.分散: return DispersedMobility;
                case BattleDoctrine.機動: return ManeuverMobility;
                default: return NeutralFactor;
            }
        }

        /// <summary>
        /// 姿勢と陣形の相性倍率（0.85..1.20）。集中×紡錘陣（中央突破）・分散×円陣（全周防御）・
        /// 機動×鶴翼陣（展開包囲）が噛み合い、逆の組み合わせは外す。それ以外は標準。
        /// </summary>
        public static float FormationCompatibility(BattleDoctrine d, Formation f)
        {
            switch (d)
            {
                case BattleDoctrine.集中:
                    // 火力集約は中央突破（紡錘陣）・全砲門（横陣）と好相性。展開の円陣とは噛み合わない。
                    if (f == Formation.紡錘陣 || f == Formation.横陣) return CompatGood;
                    if (f == Formation.円陣) return CompatPoor;
                    return CompatNeutral;
                case BattleDoctrine.分散:
                    // 広域展開は全周防御（円陣）・均整（方陣）と好相性。一点突破の紡錘陣とは噛み合わない。
                    if (f == Formation.円陣 || f == Formation.方陣) return CompatGood;
                    if (f == Formation.紡錘陣) return CompatPoor;
                    return CompatNeutral;
                case BattleDoctrine.機動:
                    // 速度重視は展開包囲（鶴翼陣）・中央突破（紡錘陣）と好相性。鈍重な方陣とは噛み合わない。
                    if (f == Formation.鶴翼陣 || f == Formation.紡錘陣) return CompatGood;
                    if (f == Formation.方陣) return CompatPoor;
                    return CompatNeutral;
                default:
                    return CompatNeutral;
            }
        }

        /// <summary>
        /// 姿勢の戦闘修正子を <see cref="ModifierStack"/> に合成して返す（攻撃×生存性×機動＝戦闘総合の係数）。
        /// 基準値非破壊（実効値パターン）。集中リスク/陣形相性は文脈依存のため呼び出し側で別途積む。
        /// </summary>
        public static ModifierStack DoctrineModifiers(BattleDoctrine d)
        {
            var stack = ModifierStack.Start();
            stack.Mul(AttackFactor(d));
            stack.Mul(SurvivabilityFactor(d));
            stack.Mul(MobilityFactor(d));
            return stack;
        }
    }
}
