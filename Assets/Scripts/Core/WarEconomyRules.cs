using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦時経済（軍需転換）の調整値。民需→軍需の転換効率・生活苦が士気を削る重み・経済疲弊の年あたり累積・
    /// 持続可能な転換率の基準と国民支持の上積み。符号付き指標は -1..1。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct WarEconomyParams
    {
        /// <summary>民需能力を軍需へ転換するときの効率（1未満＝転換ロス。1.0で無損失）。</summary>
        public readonly float conversionEfficiency;
        /// <summary>生活苦が国民の戦争支持（士気）を削る重み（苦境1あたりの士気喪失）。</summary>
        public readonly float hardshipMoraleWeight;
        /// <summary>総力戦の経済疲弊の年あたり累積（転換率×戦争年数あたりの疲弊増分）。</summary>
        public readonly float strainPerYear;
        /// <summary>無理のない転換率の基準（疲弊・支持ゼロ時に持続できる上限）。</summary>
        public readonly float baseSustainable;
        /// <summary>国民支持が持続可能転換率を押し上げる上積み（挙国一致なら無理が利く）。</summary>
        public readonly float supportBonus;

        public WarEconomyParams(float conversionEfficiency, float hardshipMoraleWeight, float strainPerYear,
            float baseSustainable, float supportBonus)
        {
            this.conversionEfficiency = Mathf.Clamp(conversionEfficiency, 0.1f, 1f);
            this.hardshipMoraleWeight = Mathf.Clamp(hardshipMoraleWeight, 0f, 2f);
            this.strainPerYear = Mathf.Clamp(strainPerYear, 0f, 1f);
            this.baseSustainable = Mathf.Clamp01(baseSustainable);
            this.supportBonus = Mathf.Clamp(supportBonus, 0f, 1f);
        }

        /// <summary>既定：転換効率0.8・生活苦士気重み0.6・年疲弊0.1・持続基準0.5・支持上積み0.3。</summary>
        public static WarEconomyParams Default => new WarEconomyParams(
            DefaultConversionEfficiency, DefaultHardshipMoraleWeight, DefaultStrainPerYear,
            DefaultBaseSustainable, DefaultSupportBonus);

        public const float DefaultConversionEfficiency = 0.8f;
        public const float DefaultHardshipMoraleWeight = 0.6f;
        public const float DefaultStrainPerYear = 0.1f;
        public const float DefaultBaseSustainable = 0.5f;
        public const float DefaultSupportBonus = 0.3f;
    }

    /// <summary>
    /// 戦時経済の純ロジック（唯一の窓口）＝<b>平時の民需経済を戦時の軍需生産へ転換する（総力戦）</b>。
    /// 転換率を上げれば軍事生産は増えるが、民生が圧迫され国民の不満・生活水準低下を招く。総力戦は経済を疲弊させ、
    /// 過剰動員（持続不能な転換）は経済破綻寸前に陥る＝軍需と民需のトレードオフ（guns vs butter）。
    /// <b>分担</b>：<see cref="FiscalRules"/>／<c>CampaignRules</c>（財政・経済・既存）とは別＝こちらは戦時の<b>軍需転換</b>。
    /// <c>MobilizationTempoRules</c>（動員の速さ）とは別＝こちらは経済の<b>軍需化</b>（生産能力の振り向け）。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・test-first。
    /// </summary>
    public static class WarEconomyRules
    {
        /// <summary>既定パラメータで民需から転換した軍需生産能力。</summary>
        public static float WarConversion(float civilianCapacity, float conversionRate)
            => WarConversion(civilianCapacity, conversionRate, WarEconomyParams.Default);

        /// <summary>
        /// 民需から軍需へ転換した生産能力。conversion＝民需能力×転換率×転換効率。
        /// 転換率を上げるほど多くの民需が軍需へ回るが、効率ロスぶんは失われる。
        /// </summary>
        public static float WarConversion(float civilianCapacity, float conversionRate, WarEconomyParams p)
        {
            float capacity = Mathf.Max(0f, civilianCapacity);
            float rate = Mathf.Clamp01(conversionRate);
            return capacity * rate * p.conversionEfficiency;
        }

        /// <summary>
        /// 民生圧迫による国民の苦境（0..1）。hardship＝転換率（軍需へ回すほど民需が痩せ生活が苦しくなる）。
        /// 転換率と直結＝民需を削った割合がそのまま生活の苦しさになる。
        /// </summary>
        public static float CivilianHardship(float conversionRate)
        {
            return Mathf.Clamp01(conversionRate);
        }

        /// <summary>
        /// 軍需生産量。output＝転換した軍需能力×工業効率。
        /// 同じ転換でも工業効率（技術・熟練）が高いほど多くの兵器を生み出す。
        /// </summary>
        public static float MilitaryOutput(float warConversion, float industrialEfficiency)
        {
            float conv = Mathf.Max(0f, warConversion);
            float eff = Mathf.Clamp01(industrialEfficiency);
            return conv * eff;
        }

        /// <summary>既定パラメータで総力戦による経済疲弊の累積。</summary>
        public static float EconomicStrain(float conversionRate, float warDuration)
            => EconomicStrain(conversionRate, warDuration, WarEconomyParams.Default);

        /// <summary>
        /// 総力戦による経済疲弊の累積（0..1）。strain＝clamp01(転換率×戦争年数×年あたり疲弊)。
        /// 高い転換率を長く続けるほど経済が摩耗する＝短期決戦は軽傷・長期総力戦は破綻へ近づく。
        /// </summary>
        public static float EconomicStrain(float conversionRate, float warDuration, WarEconomyParams p)
        {
            float rate = Mathf.Clamp01(conversionRate);
            float duration = Mathf.Max(0f, warDuration);
            return Mathf.Clamp01(rate * duration * p.strainPerYear);
        }

        /// <summary>既定パラメータで生活苦が削る戦争支持（士気）。</summary>
        public static float MoraleCostFromHardship(float civilianHardship)
            => MoraleCostFromHardship(civilianHardship, WarEconomyParams.Default);

        /// <summary>
        /// 生活苦が国民の戦争支持を削る士気コスト（-1..0）。cost＝-clamp01(苦境×士気重み)。
        /// 生活が苦しいほど厭戦が広がり士気が下がる（負値＝喪失）。苦境ゼロなら0。
        /// </summary>
        public static float MoraleCostFromHardship(float civilianHardship, WarEconomyParams p)
        {
            float hardship = Mathf.Clamp01(civilianHardship);
            return -Mathf.Clamp01(hardship * p.hardshipMoraleWeight);
        }

        /// <summary>
        /// 軍需と民需のトレードオフ（-1民需／+1軍需）。balance＝clamp(2×転換率−1, -1, 1)。
        /// 転換率0＝全て民需(-1)／0.5＝拮抗(0)／1.0＝全て軍需(+1)。guns vs butter の天秤。
        /// </summary>
        public static float GunsVsButter(float conversionRate)
        {
            float rate = Mathf.Clamp01(conversionRate);
            return Mathf.Clamp(2f * rate - 1f, -1f, 1f);
        }

        /// <summary>既定パラメータで持続可能な転換率の上限。</summary>
        public static float SustainableConversion(float economicStrain, float popularSupport)
            => SustainableConversion(economicStrain, popularSupport, WarEconomyParams.Default);

        /// <summary>
        /// 持続可能な転換率の上限（0..1）。sustainable＝clamp01(基準＋支持×上積み−疲弊)。
        /// 国民支持が高いほど無理が利き（挙国一致）、経済が疲弊するほど耐えられる転換率は下がる。
        /// </summary>
        public static float SustainableConversion(float economicStrain, float popularSupport, WarEconomyParams p)
        {
            float strain = Mathf.Clamp01(economicStrain);
            float support = Mathf.Clamp01(popularSupport);
            return Mathf.Clamp01(p.baseSustainable + support * p.supportBonus - strain);
        }

        /// <summary>
        /// 過剰動員で経済が破綻寸前か（bool）＝実際の転換率が持続可能上限をしきい値ぶん超えている。
        /// 持続限界を僅かに超える程度は許容（既定0.1）＝無理を押し通すと経済が破綻に向かう。
        /// </summary>
        public static bool IsOvermobilized(float conversionRate, float sustainableConversion, float threshold = 0.1f)
        {
            float rate = Mathf.Clamp01(conversionRate);
            float sustainable = Mathf.Clamp01(sustainableConversion);
            float margin = Mathf.Clamp01(threshold);
            return rate > sustainable + margin;
        }
    }
}
