using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 集落の規模段階（#1094・Pillars of the Earth）。人口規模で <see cref="CityGrowthRules.TierOf"/> が写像する。
    /// 集積（人口流入×市場成立）が小さな集落を村→町→都市へ育てる＝大事業がもたらす都市化の梯子。
    /// </summary>
    public enum SettlementTier
    {
        集落,   // 数十人規模の最小単位
        村,     // 自給的な定住
        町,     // 市場が立つ交易の結節
        都市,   // 集積による生産性の都市
        大都市  // 広域経済の中核
    }

    /// <summary>大事業による都市成長の調整係数（#1094）。既定値は <see cref="Default"/> に集約（マジックナンバー禁止・ctorクランプ）。</summary>
    public readonly struct CityGrowthParams
    {
        /// <summary>事業活発(projectActivity=1)×魅力(attractiveness=1)での最大人口流入（人/戦略秒）。雇用が人を呼ぶ強さ。</summary>
        public readonly float maxInflowPerSecond;
        /// <summary>事業が止まった（activity=0）ときの基準流出率（人口比/戦略秒）。建てかけの街から職人が去る速さ。</summary>
        public readonly float baseOutflowRate;
        /// <summary>市場が成立し始める人口閾値（これ未満は市場が立たない）。</summary>
        public readonly float marketThreshold;
        /// <summary>市場が飽和（marketLevel=1）に達する人口（閾値からこの幅で立ち上がる）。</summary>
        public readonly float marketSaturation;
        /// <summary>中断による衰退が最大率へ達するまでの中断継続時間（戦略秒）。長く止まるほど空洞化が進む。</summary>
        public readonly float haltRampSeconds;

        public CityGrowthParams(float maxInflowPerSecond, float baseOutflowRate, float marketThreshold,
            float marketSaturation, float haltRampSeconds)
        {
            this.maxInflowPerSecond = Mathf.Max(0f, maxInflowPerSecond);
            this.baseOutflowRate = Mathf.Clamp01(baseOutflowRate);
            this.marketThreshold = Mathf.Max(0f, marketThreshold);
            // 飽和は閾値より必ず大きく（ゼロ割回避）
            this.marketSaturation = Mathf.Max(marketThreshold + 1f, marketSaturation);
            this.haltRampSeconds = Mathf.Max(1f, haltRampSeconds);
        }

        public static CityGrowthParams Default => new CityGrowthParams(
            maxInflowPerSecond: 5f,
            baseOutflowRate: 0.02f,
            marketThreshold: 200f,
            marketSaturation: 1000f,
            haltRampSeconds: 60f);
    }

    /// <summary>
    /// 大事業が都市を育てる純ロジック（#1094・Pillars of the Earth・前提 PIL-1 #1090・test-first）。
    /// 進行中のメガプロジェクトが立地に人口流入と市場成立をもたらし、集落→村→町→都市→大都市へ成長する。
    /// 事業を中断すれば職人が去り、建てかけの街は空洞化して衰退する＝集積の経済（規模の利益）。
    ///
    /// 役割分担：事業そのものの建設/中断/頓挫は <see cref="MegaProjectRules"/>(#1090)、安定度/統合/反乱は
    /// <see cref="GovernanceRules"/>、人口動態の出生死亡コホートは DemographicsRules、需給均衡の価格は MarketRules。
    /// ここは「事業の活発さ→人口集積→市場成立→Province 成長」という<b>集積による成長</b>だけを扱う
    /// （GovernanceRules の安定度収束とは別系統）。Province.population/stability への加算寄与は
    /// <see cref="ProvinceGrowthContribution"/> が返し、配線側が GovernanceRules/Province へ積む想定。
    /// 全入力クランプ・乱数なし決定論・Core層（Game型不参照）。調整値は CityGrowthParams に集約。
    /// </summary>
    public static class CityGrowthRules
    {
        // ===== 集落段階の人口閾値（const で調整可・マジックナンバー禁止） =====
        public const float VillagePopulation = 100f;     // これ以上で 村
        public const float TownPopulation = 500f;        // これ以上で 町
        public const float CityPopulation = 2000f;       // これ以上で 都市
        public const float MetropolisPopulation = 10000f;// これ以上で 大都市

        // ===== 集積ボーナス（段階別の生産性倍率＝都市化の利益。大きい都市ほど高い） =====
        public const float AgglomerationHamlet = 1f;     // 集落＝基準
        public const float AgglomerationVillage = 1.05f; // 村
        public const float AgglomerationTown = 1.15f;    // 町＝市場が立つ
        public const float AgglomerationCity = 1.3f;     // 都市
        public const float AgglomerationMetropolis = 1.5f;// 大都市

        /// <summary>
        /// 人口流入/流出の1tick（#1094）。事業の活発さ projectActivity(0..1)が雇用を生み、立地の魅力
        /// attractiveness(0..1)に応じて人を呼ぶ＝<b>大事業は街を生む</b>。事業が止まる（activity→0）と流入が
        /// 細り、止まったぶんだけ流出へ転じる＝<b>建てかけの街から職人が去る</b>。新しい人口を返す（非負）。
        /// </summary>
        public static float PopulationInflowTick(float population, float projectActivity, float attractiveness,
            float dt, CityGrowthParams p)
        {
            float pop = Mathf.Max(0f, population);
            float t = Mathf.Max(0f, dt);
            if (t <= 0f) return pop;

            float activity = Mathf.Clamp01(projectActivity);
            float pull = Mathf.Clamp01(attractiveness);

            // 流入＝活発さ×魅力で増える（雇用が人を呼ぶ）
            float inflow = p.maxInflowPerSecond * activity * pull * t;
            // 流出＝事業が止まっているぶん（1-activity）に比例した人口比の減少（職人が去る）
            float outflow = pop * p.baseOutflowRate * (1f - activity) * t;

            return Mathf.Max(0f, pop + inflow - outflow);
        }

        public static float PopulationInflowTick(float population, float projectActivity, float attractiveness, float dt)
            => PopulationInflowTick(population, projectActivity, attractiveness, dt, CityGrowthParams.Default);

        /// <summary>人口規模での集落段階判定（#1094）。閾値で集落→村→町→都市→大都市へ。</summary>
        public static SettlementTier TierOf(float population)
        {
            float pop = Mathf.Max(0f, population);
            if (pop >= MetropolisPopulation) return SettlementTier.大都市;
            if (pop >= CityPopulation) return SettlementTier.都市;
            if (pop >= TownPopulation) return SettlementTier.町;
            if (pop >= VillagePopulation) return SettlementTier.村;
            return SettlementTier.集落;
        }

        /// <summary>
        /// 市場の成立度(0..1)（#1094）。人口集積が市場を生む＝marketThreshold を超えると交易が立ち始め、
        /// marketSaturation で飽和する。交易路の通り tradeAccess(0..1)が低いと市場は十分に立たない
        /// （集積があっても外と繋がらねば市は栄えない）。
        /// </summary>
        public static float MarketEmergence(float population, float tradeAccess, CityGrowthParams p)
        {
            float pop = Mathf.Max(0f, population);
            if (pop <= p.marketThreshold) return 0f;
            float scale = Mathf.InverseLerp(p.marketThreshold, p.marketSaturation, pop); // 0..1
            return Mathf.Clamp01(scale) * Mathf.Clamp01(tradeAccess);
        }

        public static float MarketEmergence(float population, float tradeAccess)
            => MarketEmergence(population, tradeAccess, CityGrowthParams.Default);

        /// <summary>集積の経済ボーナス（#1094）。大きい都市ほど生産性が高い＝都市化の利益（段階別倍率）。</summary>
        public static float AgglomerationBonus(SettlementTier tier)
        {
            switch (tier)
            {
                case SettlementTier.村: return AgglomerationVillage;
                case SettlementTier.町: return AgglomerationTown;
                case SettlementTier.都市: return AgglomerationCity;
                case SettlementTier.大都市: return AgglomerationMetropolis;
                default: return AgglomerationHamlet;
            }
        }

        /// <summary>
        /// 事業中断による衰退の1tick（#1094）。事業が止まったまま haltDuration が経つほど流出が増す
        /// ＝<b>建てかけの街は空洞化する</b>。中断時間が haltRampSeconds に達すると最大率（baseOutflowRate）
        /// へ漸増する。中断していない（haltDuration≤0）なら衰退しない。衰退後の人口を返す（非負）。
        /// </summary>
        public static float DeclineOnProjectHalt(float population, float haltDuration, float dt, CityGrowthParams p)
        {
            float pop = Mathf.Max(0f, population);
            float t = Mathf.Max(0f, dt);
            float halt = Mathf.Max(0f, haltDuration);
            if (t <= 0f || halt <= 0f) return pop;

            // 中断が長引くほど衰退率が baseOutflowRate へ立ち上がる（職人が順に去る）
            float severity = Mathf.Clamp01(halt / p.haltRampSeconds);
            float loss = pop * p.baseOutflowRate * severity * t;
            return Mathf.Max(0f, pop - loss);
        }

        public static float DeclineOnProjectHalt(float population, float haltDuration, float dt)
            => DeclineOnProjectHalt(population, haltDuration, dt, CityGrowthParams.Default);

        /// <summary>
        /// Province の成長への寄与(0..1)（#1094）。集落段階の集積ボーナスと市場成立度 marketLevel(0..1)を
        /// 合成し、配線側が <see cref="GovernanceRules"/>/<see cref="Province"/> の population・stability へ
        /// 加算する想定の係数を返す（集積した都市ほど内政が潤う＝都市が地方を育てる）。
        /// 段階1.0基準の集積ボーナスを 0..1 の寄与へ正規化し、市場成立で底上げする。
        /// </summary>
        public static float ProvinceGrowthContribution(SettlementTier tier, float marketLevel)
        {
            // 集積ボーナス（1.0〜大都市1.5）を 0..1 へ：集落=0、大都市で最大。
            float agg = AgglomerationBonus(tier);
            float aggNorm = Mathf.Clamp01((agg - AgglomerationHamlet) /
                                          (AgglomerationMetropolis - AgglomerationHamlet));
            float market = Mathf.Clamp01(marketLevel);
            // 集積と市場の両輪で成長寄与（市場が立つと都市の生産が地方へ波及）
            return Mathf.Clamp01(0.6f * aggNorm + 0.4f * market);
        }
    }
}
