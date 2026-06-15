using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// POP の引っ越し＝惑星/星系間の移住（#194 亡命・移民／#96・#153 連携・純ロジック・唯一の窓口）。
    /// 住民は<b>住みよい土地へ動く</b>＝安定度・統合度の高い惑星が人を<b>引き寄せ</b>、荒れた惑星から<b>流出</b>する。
    /// 勢力をまたぐ流れ＝<b>亡命（難民）</b>。<b>総量保存</b>（送り元から減らし送り先へ加える＝湧かない・消えない）。
    /// 出生死亡（<see cref="PopulationDynamicsRules"/>）とは別軸＝こちらは「移動」。コホート(<see cref="Population"/>)があれば
    /// 比率を保って動かし <see cref="Province.population"/> を同期。係数は実効値パターン（基準は非破壊）。test-first。
    /// </summary>
    public static class PopulationMigrationRules
    {
        /// <summary>移住の調整値（per-year の割合）。</summary>
        public readonly struct MigrationParams
        {
            /// <summary>引力差1あたり・1年で動く割合（送り元人口比）。</summary>
            public readonly float baseRate;
            /// <summary>この引力差未満は動かない（引っ越しの摩擦＝コスト）。</summary>
            public readonly float minDifferential;
            /// <summary>1tickで送り元から出る上限割合（急な空洞化を防ぐ）。</summary>
            public readonly float maxFraction;

            public MigrationParams(float baseRate, float minDifferential, float maxFraction)
            {
                this.baseRate = Mathf.Max(0f, baseRate);
                this.minDifferential = Mathf.Max(0f, minDifferential);
                this.maxFraction = Mathf.Clamp01(maxFraction);
            }

            /// <summary>既定＝引力差1で10%/年・差0.05未満は動かない・1tickで最大8%流出。</summary>
            public static MigrationParams Default => new MigrationParams(0.1f, 0.05f, 0.08f);
        }

        // 魅力度の重み（安定度を主・統合度を従）。const に集約。
        public const float StabilityWeight = 0.7f;
        public const float IntegrationWeight = 0.3f;

        /// <summary>
        /// 惑星の定住魅力度（0..1）。安定度・統合度が高いほど住みたい＝人を引き寄せる。荒れた/未統合の惑星は低く流出する。
        /// </summary>
        public static float Attractiveness(Province p)
        {
            if (p == null) return 0f;
            float stab = Mathf.Clamp01(p.stability / GovernanceRules.MaxStability);
            float integ = Mathf.Clamp01(p.integration);
            return StabilityWeight * stab + IntegrationWeight * integ;
        }

        /// <summary>
        /// from→to の移住量（人）。<b>to の方が魅力的なときだけ正</b>（引力差が摩擦 minDifferential を超えたら動く）。
        /// dt 比例・送り元人口比・1tick上限割合でクランプ。逆向き（to が魅力薄）は0。
        /// </summary>
        public static float MigrationFlow(Province from, Province to, MigrationParams prm, float dt)
        {
            if (from == null || to == null || dt <= 0f || from.population <= 0f) return 0f;
            float diff = Attractiveness(to) - Attractiveness(from);
            if (diff < prm.minDifferential) return 0f;
            float flow = from.population * prm.baseRate * diff * dt;
            float cap = from.population * prm.maxFraction;
            return Mathf.Clamp(flow, 0f, cap);
        }

        /// <summary>from→to へ amount 人を移す（総量保存・コホート比率を保つ・population 同期）。移民は移住先で働き手になる。</summary>
        public static void Move(Province from, Province to, float amount)
        {
            if (from == null || to == null) return;
            amount = Mathf.Clamp(amount, 0f, from.population);
            if (amount <= 0f) return;
            RemovePopulation(from, amount); // 送り元：比率を保って減らす
            AddPopulation(to, amount);      // 送り先：働き手として加える（移民＝生産年齢）
        }

        /// <summary>from→to を1年ぶん移住させ、移した人数を返す（算出＋適用）。GalaxyView の年境界が隣接間で回す。</summary>
        public static float TickPair(Province from, Province to, MigrationParams prm, float dt)
        {
            float amt = MigrationFlow(from, to, prm, dt);
            Move(from, to, amt);
            return amt;
        }

        // 送り元から比率を保って減らす（コホートがあれば各層を同率で・無ければ float のみ）。
        private static void RemovePopulation(Province p, float amount)
        {
            if (p.demographics != null && p.demographics.Total > 0f)
            {
                float k = Mathf.Clamp01(amount / p.demographics.Total);
                p.demographics.youth -= p.demographics.youth * k;
                p.demographics.working -= p.demographics.working * k;
                p.demographics.elderly -= p.demographics.elderly * k;
                p.population = p.demographics.Total;
            }
            else
            {
                p.population = Mathf.Max(0f, p.population - amount);
            }
        }

        // 送り先へ加える（コホートがあれば働き手＝生産年齢へ・無ければ float のみ）。
        private static void AddPopulation(Province p, float amount)
        {
            if (p.demographics != null)
            {
                p.demographics.working += amount; // 移民は生産年齢（労働移民・難民の働き手）
                p.population = p.demographics.Total;
            }
            else
            {
                p.population += amount;
            }
        }
    }
}
