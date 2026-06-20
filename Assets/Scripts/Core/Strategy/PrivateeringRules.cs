using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 私掠（国家が認可した民間の通商破壊＝私掠免許 Letter of Marque）の調整値。
    /// 私掠の動機（分け前×敵交易の豊かさ）・拿捕の成否・国家統制と規律問題（海賊化）・正味価値の重み。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct PrivateeringParams
    {
        /// <summary>私掠の動機に占める「分け前」の効き（大きいほど分け前が動機を強める）。</summary>
        public readonly float prizeShareWeight;
        /// <summary>私掠の動機に占める「敵交易の豊かさ」の効き（豊かな獲物ほど動機を強める）。</summary>
        public readonly float tradeRichnessWeight;
        /// <summary>敵交易への打撃の感度（私掠船数×拿捕成功→打撃。1艦あたりの効き）。</summary>
        public readonly float commerceDamageScale;
        /// <summary>正規海軍を私掠で代替できるコスト削減の感度（私掠船数×海軍予算→節約）。</summary>
        public readonly float costSavingScale;
        /// <summary>規律問題（海賊化傾向）の感度＝分け前×(1-国家統制) に掛ける。</summary>
        public readonly float disciplineScale;
        /// <summary>国際的非難の感度＝行き過ぎ×中立船被害に掛ける。</summary>
        public readonly float condemnationScale;
        /// <summary>正味価値で「敵交易への打撃」を加点する重み。</summary>
        public readonly float netDamageWeight;
        /// <summary>正味価値で「コスト削減」を加点する重み。</summary>
        public readonly float netSavingWeight;
        /// <summary>正味価値で「国際的非難」を減点する重み。</summary>
        public readonly float netCondemnationWeight;

        public PrivateeringParams(float prizeShareWeight, float tradeRichnessWeight, float commerceDamageScale,
            float costSavingScale, float disciplineScale, float condemnationScale,
            float netDamageWeight, float netSavingWeight, float netCondemnationWeight)
        {
            this.prizeShareWeight = Mathf.Clamp(prizeShareWeight, 0f, 4f);
            this.tradeRichnessWeight = Mathf.Clamp(tradeRichnessWeight, 0f, 4f);
            this.commerceDamageScale = Mathf.Clamp(commerceDamageScale, 0f, 10f);
            this.costSavingScale = Mathf.Clamp(costSavingScale, 0f, 10f);
            this.disciplineScale = Mathf.Clamp(disciplineScale, 0f, 4f);
            this.condemnationScale = Mathf.Clamp(condemnationScale, 0f, 4f);
            this.netDamageWeight = Mathf.Clamp(netDamageWeight, 0f, 4f);
            this.netSavingWeight = Mathf.Clamp(netSavingWeight, 0f, 4f);
            this.netCondemnationWeight = Mathf.Clamp(netCondemnationWeight, 0f, 4f);
        }

        /// <summary>
        /// 既定：分け前重み1.0・交易の豊かさ重み1.0・打撃感度1.0・節約感度1.0・規律感度1.0・非難感度1.0・
        /// 正味の打撃/節約/非難の重み各1.0。
        /// </summary>
        public static PrivateeringParams Default => new PrivateeringParams(
            DefaultPrizeShareWeight, DefaultTradeRichnessWeight, DefaultCommerceDamageScale,
            DefaultCostSavingScale, DefaultDisciplineScale, DefaultCondemnationScale,
            DefaultNetDamageWeight, DefaultNetSavingWeight, DefaultNetCondemnationWeight);

        public const float DefaultPrizeShareWeight = 1.0f;
        public const float DefaultTradeRichnessWeight = 1.0f;
        public const float DefaultCommerceDamageScale = 1.0f;
        public const float DefaultCostSavingScale = 1.0f;
        public const float DefaultDisciplineScale = 1.0f;
        public const float DefaultCondemnationScale = 1.0f;
        public const float DefaultNetDamageWeight = 1.0f;
        public const float DefaultNetSavingWeight = 1.0f;
        public const float DefaultNetCondemnationWeight = 1.0f;
    }

    /// <summary>
    /// 私掠（認可された海賊＝民間の通商破壊）の純ロジック（唯一の窓口）。国家が<b>私掠免許</b>を発行し、民間船が
    /// 敵交易を襲って<b>拿捕</b>（鹵獲）の<b>分け前</b>で動く。正規海軍を代替して<b>コスト削減</b>になる一方、
    /// 分け前と国家統制の弱さは<b>規律問題（海賊化）</b>を生み、行き過ぎと中立船被害は<b>国際的非難</b>を招く。
    /// <b>分担</b>：軍艦による迎撃・補給途絶の解決そのものは <see cref="CommerceRaidingRules"/>（L-3 #95）が担う
    /// ＝こちらは<b>民間私掠の動機・拿捕利益・規律・外交コスト</b>の数値判断。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・決定論・test-first。
    /// </summary>
    public static class PrivateeringRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        // ── 私掠の動機（分け前×敵交易の豊かさ） ──────────────────

        /// <summary>私掠免許の魅力＝分け前×敵交易の豊かさ（0..1）。獲物が豊かで取り分が多いほど私掠者が群がる。</summary>
        public static float LetterOfMarqueIncentive(float prizeShare, float enemyTradeRichness)
            => LetterOfMarqueIncentive(prizeShare, enemyTradeRichness, PrivateeringParams.Default);

        /// <summary>私掠免許の魅力（0..1）＝clamp01(分け前×share重み)×clamp01(交易の豊かさ×richness重み)。</summary>
        public static float LetterOfMarqueIncentive(float prizeShare, float enemyTradeRichness, PrivateeringParams p)
        {
            float share = Mathf.Clamp01(Mathf.Clamp01(prizeShare) * p.prizeShareWeight);
            float rich = Mathf.Clamp01(Mathf.Clamp01(enemyTradeRichness) * p.tradeRichnessWeight);
            return Mathf.Clamp01(share * rich);
        }

        // ── 拿捕の成否 ────────────────────────────────────────────

        /// <summary>拿捕の成功確率（0..1）＝私掠船戦力/(戦力+標的防御)。互角で0.5、防御0なら1.0。</summary>
        public static float PrizeCapture(float privateerStrength, float targetDefense)
            => PrizeCapture(privateerStrength, targetDefense, PrivateeringParams.Default);

        /// <summary>拿捕の成功確率（0..1）。私掠船戦力÷(戦力＋標的防御)。両者0は防御なし扱い＝1.0。</summary>
        public static float PrizeCapture(float privateerStrength, float targetDefense, PrivateeringParams p)
        {
            float s = Mathf.Max(0f, privateerStrength);
            float d = Mathf.Max(0f, targetDefense);
            float denom = s + d;
            if (denom <= Epsilon) return 1f;
            return Mathf.Clamp01(s / denom);
        }

        // ── 鹵獲の価値 ────────────────────────────────────────────

        /// <summary>鹵獲の価値＝拿捕成功×積荷価値（拿捕できねば0）。</summary>
        public static float PrizeValue(float prizeCapture, float cargoValue)
            => PrizeValue(prizeCapture, cargoValue, PrivateeringParams.Default);

        /// <summary>鹵獲の価値（非負）＝clamp01(拿捕成功)×max(0,積荷価値)。</summary>
        public static float PrizeValue(float prizeCapture, float cargoValue, PrivateeringParams p)
            => Mathf.Clamp01(prizeCapture) * Mathf.Max(0f, cargoValue);

        // ── 敵交易への打撃 ────────────────────────────────────────

        /// <summary>敵交易への打撃＝私掠船数×拿捕成功×感度（私掠船が多く拿捕が上手いほど敵の交易が干上がる）。</summary>
        public static float EnemyCommerceDamage(int privateerCount, float prizeCapture)
            => EnemyCommerceDamage(privateerCount, prizeCapture, PrivateeringParams.Default);

        /// <summary>敵交易への打撃（非負）＝max(0,私掠船数)×clamp01(拿捕成功)×commerceDamageScale。</summary>
        public static float EnemyCommerceDamage(int privateerCount, float prizeCapture, PrivateeringParams p)
        {
            float n = Mathf.Max(0, privateerCount);
            return n * Mathf.Clamp01(prizeCapture) * p.commerceDamageScale;
        }

        // ── 国家のコスト削減 ──────────────────────────────────────

        /// <summary>正規海軍を私掠で代替するコスト削減＝私掠船数×海軍予算×感度（民間に肩代わりさせて国費を浮かす）。</summary>
        public static float StateCostSaving(int privateerCount, float navalBudget)
            => StateCostSaving(privateerCount, navalBudget, PrivateeringParams.Default);

        /// <summary>コスト削減（非負）＝max(0,私掠船数)×max(0,海軍予算)×costSavingScale。</summary>
        public static float StateCostSaving(int privateerCount, float navalBudget, PrivateeringParams p)
        {
            float n = Mathf.Max(0, privateerCount);
            return n * Mathf.Max(0f, navalBudget) * p.costSavingScale;
        }

        // ── 規律問題（海賊化の傾向） ──────────────────────────────

        /// <summary>規律問題（海賊化傾向・0..1）＝分け前×(1-国家統制)。取り分が大きく統制が緩いほど海賊に堕ちる。</summary>
        public static float DisciplineProblem(float prizeShare, float stateControl)
            => DisciplineProblem(prizeShare, stateControl, PrivateeringParams.Default);

        /// <summary>規律問題（0..1）＝clamp01(分け前×(1-国家統制)×disciplineScale)。統制が完全(1)なら0。</summary>
        public static float DisciplineProblem(float prizeShare, float stateControl, PrivateeringParams p)
        {
            float share = Mathf.Clamp01(prizeShare);
            float looseness = 1f - Mathf.Clamp01(stateControl);
            return Mathf.Clamp01(share * looseness * p.disciplineScale);
        }

        // ── 国際的非難 ────────────────────────────────────────────

        /// <summary>国際的非難（0..1）＝私掠の行き過ぎ×中立船被害（やり過ぎて中立国の船まで襲うと外交的に孤立する）。</summary>
        public static float InternationalCondemnation(float privateerExcess, float neutralShippingHarm)
            => InternationalCondemnation(privateerExcess, neutralShippingHarm, PrivateeringParams.Default);

        /// <summary>国際的非難（0..1）＝clamp01(行き過ぎ×中立船被害×condemnationScale)。どちらか0なら非難なし。</summary>
        public static float InternationalCondemnation(float privateerExcess, float neutralShippingHarm, PrivateeringParams p)
        {
            float excess = Mathf.Clamp01(privateerExcess);
            float harm = Mathf.Clamp01(neutralShippingHarm);
            return Mathf.Clamp01(excess * harm * p.condemnationScale);
        }

        // ── 私掠の正味価値 ────────────────────────────────────────

        /// <summary>私掠の正味（-1..1）＝敵交易への打撃＋コスト削減−国際的非難。差し引きで私掠が割に合うか。</summary>
        public static float NetPrivateeringValue(float enemyCommerceDamage, float stateCostSaving, float internationalCondemnation)
            => NetPrivateeringValue(enemyCommerceDamage, stateCostSaving, internationalCondemnation, PrivateeringParams.Default);

        /// <summary>
        /// 私掠の正味（-1..1）＝clamp01(打撃)×netDamageWeight＋clamp01(節約)×netSavingWeight
        /// −clamp01(非難)×netCondemnationWeight。打撃/節約は便益・非難はコスト。
        /// </summary>
        public static float NetPrivateeringValue(float enemyCommerceDamage, float stateCostSaving,
            float internationalCondemnation, PrivateeringParams p)
        {
            float benefit = Mathf.Clamp01(enemyCommerceDamage) * p.netDamageWeight
                          + Mathf.Clamp01(stateCostSaving) * p.netSavingWeight;
            float cost = Mathf.Clamp01(internationalCondemnation) * p.netCondemnationWeight;
            return Mathf.Clamp(benefit - cost, -1f, 1f);
        }
    }
}
