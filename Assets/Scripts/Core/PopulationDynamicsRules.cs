using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// POP の出生・死亡を惑星内政（<see cref="Province"/>）へ接続する純ロジック（LIFE-3 #153 の配線・唯一の窓口）。
    /// 既存の年齢コホート動態（<see cref="DemographicsRules"/>/<see cref="Population"/>）を、惑星の<b>安定度</b>で
    /// 出生・死亡率を増減させて回す＝<b>安定した惑星は人口が増え、荒れた惑星は出生減・死亡増で人口が減る</b>。
    /// 動態の本体は <see cref="DemographicsRules"/> に委譲（二重実装しない）。係数は実効値パターン（基準率は非破壊）。test-first。
    /// </summary>
    public static class PopulationDynamicsRules
    {
        // 初期年齢構成（コホート未設定の惑星を population から起こすときの既定比率・合計1.0）。
        public const float DefaultYouthShare = 0.25f;    // 年少
        public const float DefaultWorkingShare = 0.60f;  // 生産年齢
        public const float DefaultElderlyShare = 0.15f;  // 高齢

        // 安定度→出生・死亡の補正レンジ（マジックナンバー禁止＝const）。
        public const float MinBirthFactor = 0.4f;        // 安定0＝出生は4割（飢饉・戦災で産まれない）
        public const float MaxBirthFactor = 1.2f;        // 安定MAX＝出生1.2倍（豊かさで増える）
        public const float MinMortalityFactor = 0.8f;    // 安定MAX＝死亡は8割（医療・治安で減る）
        public const float MaxMortalityFactor = 2.0f;    // 安定0＝死亡2倍（飢饉・疫病・戦災）

        /// <summary>惑星にコホート人口(<see cref="Population"/>)が無ければ <see cref="Province.population"/> から既定構成で起こす（冪等）。</summary>
        public static Population EnsureDemographics(Province p)
        {
            if (p == null) return null;
            if (p.demographics == null)
            {
                float total = Mathf.Max(0f, p.population);
                p.demographics = new Population(
                    total * DefaultYouthShare, total * DefaultWorkingShare, total * DefaultElderlyShare);
            }
            return p.demographics;
        }

        /// <summary>
        /// 惑星の安定度で出生・死亡率を補正した実効動態率（基準率は非破壊＝実効値パターン）。
        /// 安定が高いほど出生↑・死亡↓、低いほど出生↓・死亡↑。加齢率（年少/生産の移行）は不変。
        /// </summary>
        public static DemographicsRules.VitalRates EffectiveVitalRates(Province p, DemographicsRules.VitalRates baseRates)
        {
            float t = (p == null) ? 1f : Mathf.Clamp01(p.stability / GovernanceRules.MaxStability);
            float birth = baseRates.birthRate * Mathf.Lerp(MinBirthFactor, MaxBirthFactor, t);
            float mortality = baseRates.elderMortality * Mathf.Lerp(MaxMortalityFactor, MinMortalityFactor, t);
            return new DemographicsRules.VitalRates(birth, baseRates.youthAging, baseRates.workAging, mortality);
        }

        /// <summary>
        /// 惑星の人口を1年ぶん動かす（出生→年少／加齢／高齢死亡）。<see cref="Province.population"/> をコホート合計へ同期する。
        /// 戻り値＝この1年の純増減（＋＝増加・−＝減少）。GalaxyView の年境界が回す。
        /// </summary>
        public static float TickYear(Province p, DemographicsRules.VitalRates baseRates)
        {
            if (p == null) return 0f;
            Population pop = EnsureDemographics(p);
            float before = pop.Total;
            DemographicsRules.Tick(pop, EffectiveVitalRates(p, baseRates));
            p.population = pop.Total; // 単一スケールの population をコホート合計に同期
            return pop.Total - before;
        }

        /// <summary>
        /// この惑星の見込み年間人口成長率（出生−死亡）/総人口。表示・見積り用（人口を変えない＝非破壊）。
        /// </summary>
        public static float ProjectedAnnualGrowth(Province p, DemographicsRules.VitalRates baseRates)
        {
            if (p == null) return 0f;
            Population pop = p.demographics ?? new Population(
                p.population * DefaultYouthShare, p.population * DefaultWorkingShare, p.population * DefaultElderlyShare);
            if (pop.Total <= 0f) return 0f;
            DemographicsRules.VitalRates r = EffectiveVitalRates(p, baseRates);
            float births = pop.working * r.birthRate;
            float deaths = pop.elderly * r.elderMortality;
            return (births - deaths) / pop.Total;
        }
    }
}
