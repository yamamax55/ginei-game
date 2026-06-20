using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略手段（孫子「謀攻篇」の上策→下策の順・#1130）。
    /// 謀略＝敵の計画を挫く（上兵伐謀）＞外交＝敵の同盟を断つ（其次伐交）＞
    /// 野戦＝兵を交える（其次伐兵）＞攻城＝城を攻める（其下攻城）。
    /// </summary>
    public enum StrategicMeans
    {
        謀略,
        外交,
        野戦,
        攻城,
    }

    /// <summary>
    /// 謀攻優先ドクトリン（孫子「謀攻篇」・Issue #1130）。AIの戦略手段選択の純ロジック。
    /// 「百戦百勝は善の善なる者に非ず、戦わずして人の兵を屈するは善の善なる者なり」＝
    /// 戦わずして勝つ（謀略・外交）を最上とし、兵を交える野戦・城を攻める攻城ほど下策とする選好で
    /// 各手段をスコアリングし、最善手段を選ぶ。攻城は最も兵を損ない時間を食う下策として罰を与える。
    /// 数値モデルのみ＝具体的な謀略の演出・成否処理は <see cref="DeceptionRules"/>（バックログ）、
    /// 攻城の実体（制空権制圧→占領）は <see cref="PlanetSiegeRules"/> が担う。ここは AI の手段選好のみ。
    /// 全入力クランプ・乱数は roll 引数で決定論。純ロジック・test-first。
    /// </summary>
    public static class SunziDoctrineRules
    {
        /// <summary>
        /// 手段の基礎選好を返す（孫子の上策→下策＝謀＞交＞兵＞攻城）。
        /// </summary>
        public static float Preference(StrategicMeans means, SunziDoctrineParams p)
        {
            switch (means)
            {
                case StrategicMeans.謀略: return p.StratagemPreference;
                case StrategicMeans.外交: return p.DiplomacyPreference;
                case StrategicMeans.野戦: return p.BattlePreference;
                case StrategicMeans.攻城:
                default: return p.SiegePreference;
            }
        }

        /// <summary>
        /// 各手段のAIスコア＝孫子の選好×実行可能性×利得/コスト。
        /// feasibility＝その手段が実行可能か(0..1)、expectedGain＝期待利得(0..1)、expectedCost＝期待コスト(0..1)。
        /// 実行不能（feasibility=0）なら 0。コストは costWeight で割り引く（攻城のように高コストほどスコアが下がる）。
        /// </summary>
        public static float MeansScore(StrategicMeans means, float feasibility, float expectedCost, float expectedGain, SunziDoctrineParams p)
        {
            float feas = Mathf.Clamp01(feasibility);
            float gain = Mathf.Clamp01(expectedGain);
            float cost = Mathf.Clamp01(expectedCost);
            float net = gain - p.CostWeight * cost; // 利得からコストを差し引く
            if (net < 0f) net = 0f;
            return Preference(means, p) * feas * net;
        }

        /// <summary>
        /// 最善手段を選ぶ＝4手段（謀略/外交/野戦/攻城の順）のスコア最大を返す。
        /// 配列は <see cref="StrategicMeans"/> の序列（謀略=0,外交=1,野戦=2,攻城=3）に対応。
        /// 同点なら序列が上（孫子の上策側＝謀略寄り）を優先＝戦わずして勝つを選ぶ。
        /// 配列長不足・null は安全側で攻城を返す。
        /// </summary>
        public static StrategicMeans BestMeans(float[] feasibilities, float[] costs, float[] gains, SunziDoctrineParams p)
        {
            if (feasibilities == null || costs == null || gains == null) return StrategicMeans.攻城;
            int n = feasibilities.Length;
            if (n > costs.Length) n = costs.Length;
            if (n > gains.Length) n = gains.Length;
            if (n <= 0) return StrategicMeans.攻城;
            if (n > 4) n = 4;

            StrategicMeans best = StrategicMeans.攻城;
            float bestScore = -1f;
            // 序列順（謀略→攻城）に走査し、厳密に上回ったときだけ更新＝同点は上策側を温存
            for (int i = 0; i < n; i++)
            {
                var means = (StrategicMeans)i;
                float score = MeansScore(means, feasibilities[i], costs[i], gains[i], p);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = means;
                }
            }
            return best;
        }

        /// <summary>
        /// 戦わずして勝つ価値（無血勝利＝善の善なる者）。enemyDisruption＝謀略・外交で敵を瓦解させた度合い(0..1)。
        /// 兵を損なわず城を抜くほど価値が高い＝1.0 で <see cref="SunziDoctrineParams.BloodlessMax"/>（野戦・攻城の通常勝利を超える）。
        /// </summary>
        public static float BloodlessVictoryValue(float enemyDisruption, SunziDoctrineParams p)
        {
            return p.BloodlessBase + (p.BloodlessMax - p.BloodlessBase) * Mathf.Clamp01(enemyDisruption);
        }

        /// <summary>
        /// 城攻めの罰（孫子が戒めた最後の手段＝最も兵を損ない時間を食う）。
        /// siegeDuration＝攻城に要する時間（長いほど罰が増す）、ownStrength＝自軍兵力（大きいほど損耗の絶対量が増す）。
        /// 罰は時間と兵力に比例＝攻城は下策ゆえスコアから引かれる損失コスト。0以上。
        /// </summary>
        public static float SiegePenalty(float siegeDuration, float ownStrength, SunziDoctrineParams p)
        {
            float dur = Mathf.Max(0f, siegeDuration);
            float str = Mathf.Max(0f, ownStrength);
            return p.SiegeTimePenalty * dur + p.SiegeBloodPenalty * str;
        }

        /// <summary>
        /// 外交で敵を孤立させる効果＝伐交（其次伐交＝敵の同盟を断つ）。
        /// enemyAlliances＝敵の同盟数、ownDiplomaticPower＝自国の外交力(0..1)。
        /// 同盟が多いほど断つ余地が大きく、外交力が高いほど深く孤立させられる(0..1)。
        /// </summary>
        public static float DiplomaticIsolation(int enemyAlliances, float ownDiplomaticPower, SunziDoctrineParams p)
        {
            if (enemyAlliances <= 0) return 0f; // 断つべき同盟が無い
            float power = Mathf.Clamp01(ownDiplomaticPower);
            // 同盟数を 0..1 へ飽和（多いほど断つ余地は増すが逓減）
            float severable = (float)enemyAlliances / (enemyAlliances + p.AllianceHalfCount);
            return Mathf.Clamp01(severable * power * p.IsolationGain);
        }

        /// <summary>
        /// 謀略の成否（敵の聡明さが防壁・roll∈[0,1) で決定論）。
        /// deceptionSkill＝謀略の巧拙(0..1)、enemyIntelligence＝敵の情報・洞察(0..1)。
        /// 成功率＝巧拙×(1−敵の聡明さ)＋下駄。聡明な敵ほど計を見破る。roll が成功率未満で成功。
        /// </summary>
        public static bool StratagemSuccess(float deceptionSkill, float enemyIntelligence, float roll)
        {
            float skill = Mathf.Clamp01(deceptionSkill);
            float enemy = Mathf.Clamp01(enemyIntelligence);
            float chance = Mathf.Clamp01(skill * (1f - enemy));
            return roll < chance;
        }
    }

    /// <summary>
    /// SunziDoctrineRules の調整値（#1130・マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// 基礎選好は孫子の上策→下策＝謀略＞外交＞野戦＞攻城。
    /// </summary>
    public readonly struct SunziDoctrineParams
    {
        /// <summary>謀略の基礎選好（上兵伐謀＝最上策）。</summary>
        public readonly float StratagemPreference;
        /// <summary>外交の基礎選好（其次伐交）。</summary>
        public readonly float DiplomacyPreference;
        /// <summary>野戦の基礎選好（其次伐兵）。</summary>
        public readonly float BattlePreference;
        /// <summary>攻城の基礎選好（其下攻城＝下策）。</summary>
        public readonly float SiegePreference;
        /// <summary>スコア計算でコストを割り引く重み（高いほどコストがスコアを削る）。</summary>
        public readonly float CostWeight;
        /// <summary>無血勝利の最小価値（敵を瓦解させていない場合）。</summary>
        public readonly float BloodlessBase;
        /// <summary>無血勝利の最大価値（完全に瓦解＝善の善なる者）。</summary>
        public readonly float BloodlessMax;
        /// <summary>攻城の時間あたり罰（長期化するほど損失）。</summary>
        public readonly float SiegeTimePenalty;
        /// <summary>攻城の兵力あたり罰（兵を損なう損失）。</summary>
        public readonly float SiegeBloodPenalty;
        /// <summary>同盟数の飽和半値（この同盟数で断つ余地が半分）。</summary>
        public readonly float AllianceHalfCount;
        /// <summary>外交孤立化の最大係数。</summary>
        public readonly float IsolationGain;

        public SunziDoctrineParams(
            float stratagemPreference, float diplomacyPreference, float battlePreference, float siegePreference,
            float costWeight,
            float bloodlessBase, float bloodlessMax,
            float siegeTimePenalty, float siegeBloodPenalty,
            float allianceHalfCount, float isolationGain)
        {
            StratagemPreference = stratagemPreference;
            DiplomacyPreference = diplomacyPreference;
            BattlePreference = battlePreference;
            SiegePreference = siegePreference;
            CostWeight = costWeight;
            BloodlessBase = bloodlessBase;
            BloodlessMax = bloodlessMax;
            SiegeTimePenalty = siegeTimePenalty;
            SiegeBloodPenalty = siegeBloodPenalty;
            AllianceHalfCount = allianceHalfCount;
            IsolationGain = isolationGain;
        }

        /// <summary>
        /// 既定。選好＝謀略1.0＞外交0.8＞野戦0.5＞攻城0.25（孫子の上策→下策）。
        /// costWeight=0.5、無血勝利 base0.5→max1.5、攻城罰 時間0.1/兵力0.2、同盟半値2.0、孤立化係数1.0。
        /// </summary>
        public static SunziDoctrineParams Default => new SunziDoctrineParams(
            stratagemPreference: 1.0f, diplomacyPreference: 0.8f, battlePreference: 0.5f, siegePreference: 0.25f,
            costWeight: 0.5f,
            bloodlessBase: 0.5f, bloodlessMax: 1.5f,
            siegeTimePenalty: 0.1f, siegeBloodPenalty: 0.2f,
            allianceHalfCount: 2.0f, isolationGain: 1.0f);
    }
}
