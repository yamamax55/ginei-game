using UnityEngine;

namespace Ginei
{
    /// <summary>配給制の調整係数。</summary>
    public readonly struct RationingParams
    {
        /// <summary>不透明な制度の公平感の天井（透明性0でも、例外無しならこの値までは信じられる＝疑念の割引）。</summary>
        public readonly float opaqueFairnessCap;
        /// <summary>士気に占める公平感の比重（残り＝配給量の比重。公平＞量が配給制の本体）。</summary>
        public readonly float fairnessWeight;
        /// <summary>不足を感じない一人あたり配給量（これ以上は士気への寄与が飽和）。</summary>
        public readonly float comfortRation;
        /// <summary>不公平が闇市流出圧を増幅する倍率（公平感0のとき欠乏がこの倍率ぶん余計に流れる）。</summary>
        public readonly float unfairnessMarketBoost;
        /// <summary>不足予想がパニックを新規に湧かせる速度（per dt・予想最大×信頼0のとき）。</summary>
        public readonly float panicSpawnRate;
        /// <summary>既存パニックが不足予想で自己増殖する率（per dt・買いだめが棚を空にし予想を強める複利）。</summary>
        public readonly float panicGrowthRate;
        /// <summary>信頼がパニックを鎮める速度（per dt・信頼1のとき既存パニックに比例して減衰）。</summary>
        public readonly float calmRate;

        public RationingParams(float opaqueFairnessCap, float fairnessWeight, float comfortRation,
                               float unfairnessMarketBoost, float panicSpawnRate, float panicGrowthRate,
                               float calmRate)
        {
            this.opaqueFairnessCap = Mathf.Clamp01(opaqueFairnessCap);
            this.fairnessWeight = Mathf.Clamp01(fairnessWeight);
            this.comfortRation = Mathf.Max(0.01f, comfortRation);
            this.unfairnessMarketBoost = Mathf.Max(0f, unfairnessMarketBoost);
            this.panicSpawnRate = Mathf.Max(0f, panicSpawnRate);
            this.panicGrowthRate = Mathf.Max(0f, panicGrowthRate);
            this.calmRate = Mathf.Max(0f, calmRate);
        }

        /// <summary>既定＝不透明天井0.6・公平比重0.7・充足配給1.0・不公平増幅1.0・パニック湧き0.2・自己増殖0.3・鎮静0.5。</summary>
        public static RationingParams Default
            => new RationingParams(0.6f, 0.7f, 1f, 1f, 0.2f, 0.3f, 0.5f);
    }

    /// <summary>
    /// 配給制の純ロジック＝統制経済の表側の政策。<see cref="BlackMarketRules"/>（統制の影＝非合法の並行市場）と対をなす。
    /// 配給の本体は物資でなく公平＝飢えは耐えられるが不正は耐えられない：特権層の例外（eliteExemption）が
    /// 公平感を殺し（<see cref="FairnessPerception"/>＝指導層が同じ列に並ぶ国は耐える）、欠乏下の士気は量より
    /// 公平で決まる（<see cref="MoraleUnderScarcity"/>＝少なくても公平なら耐えられる・豊富でも不公平なら荒れる）。
    /// 生存ライン割れと不公平は闇市への流出圧になり（<see cref="BlackMarketPressure"/>＝
    /// <see cref="BlackMarketRules.MarketSizeTick"/> の scarcity 入力に渡す想定）、不足の予想は買いだめで
    /// 不足を作る（<see cref="HoardingPanicTick"/>＝自己成就・信頼が防波堤）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RationingRules
    {
        /// <summary>
        /// 一人あたり配給量＝供給÷人口。供給は下限0、人口0以下は配るべき相手がいない＝0。
        /// </summary>
        public static float RationLevel(float supply, float population)
        {
            if (population <= 0f) return 0f;
            return Mathf.Max(0f, supply) / population;
        }

        /// <summary>
        /// 生存ラインとの差（0..1）＝(生存必要量−配給量)/生存必要量。0＝足りている、1＝配給ゼロ＝飢餓最大。
        /// 必要量0以下は飢えようがない＝0。<see cref="BlackMarketPressure"/> の欠乏入力になる。
        /// </summary>
        public static float SubsistenceGap(float rationLevel, float subsistenceNeed)
        {
            if (subsistenceNeed <= 0f) return 0f;
            return Mathf.Clamp01((subsistenceNeed - Mathf.Max(0f, rationLevel)) / subsistenceNeed);
        }

        /// <summary>
        /// 公平感（0..1）＝(1−特権層の例外)×透明性の天井。例外（eliteExemption 0..1）は乗算で効く
        /// ＝指導層が同じ列に並ぶ国は耐え、例外が公平感を殺す。透明性（transparency 0..1）は信の天井を
        /// opaqueFairnessCap から 1 へ引き上げる＝見えない制度は例外が無くても疑われる。
        /// </summary>
        public static float FairnessPerception(float eliteExemption, float transparency, RationingParams p)
        {
            float ceiling = Mathf.Lerp(p.opaqueFairnessCap, 1f, Mathf.Clamp01(transparency));
            return Mathf.Clamp01((1f - Mathf.Clamp01(eliteExemption)) * ceiling);
        }

        public static float FairnessPerception(float eliteExemption, float transparency)
            => FairnessPerception(eliteExemption, transparency, RationingParams.Default);

        /// <summary>
        /// 欠乏下の士気（0..1）＝公平感×fairnessWeight＋充足度×(1−fairnessWeight)。
        /// 充足度は配給量/comfortRation（1で飽和）。公平の比重が量より重い
        /// ＝少なくても公平なら耐えられ、豊富でも不公平なら荒れる（飢えは耐えられるが不正は耐えられない）。
        /// </summary>
        public static float MoraleUnderScarcity(float rationLevel, float fairness, RationingParams p)
        {
            float fullness = Mathf.Clamp01(Mathf.Max(0f, rationLevel) / p.comfortRation);
            return Mathf.Clamp01(p.fairnessWeight * Mathf.Clamp01(fairness)
                                 + (1f - p.fairnessWeight) * fullness);
        }

        public static float MoraleUnderScarcity(float rationLevel, float fairness)
            => MoraleUnderScarcity(rationLevel, fairness, RationingParams.Default);

        /// <summary>
        /// 闇市への流出圧（0..1）＝生存ライン割れ×(1＋不公平×unfairnessMarketBoost)。
        /// 飢えが源泉（gap 0なら不公平でも流れない＝配る物があるうちは列に並ぶ）、不公平の露見が増幅する
        /// ＝「上が抜け駆けするなら俺も」。<see cref="BlackMarketRules.MarketSizeTick"/> の scarcity 入力に渡す想定。
        /// </summary>
        public static float BlackMarketPressure(float subsistenceGap, float fairness, RationingParams p)
        {
            float gap = Mathf.Clamp01(subsistenceGap);
            float unfair = 1f - Mathf.Clamp01(fairness);
            return Mathf.Clamp01(gap * (1f + p.unfairnessMarketBoost * unfair));
        }

        public static float BlackMarketPressure(float subsistenceGap, float fairness)
            => BlackMarketPressure(subsistenceGap, fairness, RationingParams.Default);

        /// <summary>
        /// 買いだめパニックの1tick後の値（0..1）。不足の予想（expectedShortage 0..1）×不信（1−trust）で湧き、
        /// 既存パニックは予想×不信で自己増殖（買いだめが棚を空にし予想を強める＝不足の予想が不足を作る自己成就）、
        /// 信頼（trust 0..1）が既存パニックを鎮める＝信頼が防波堤。実際の不足でなく「予想」で動くのが核。
        /// </summary>
        public static float HoardingPanicTick(float panic, float expectedShortage, float trust,
                                              float dt, RationingParams p)
        {
            float pa = Mathf.Clamp01(panic);
            float e = Mathf.Clamp01(expectedShortage);
            float t = Mathf.Clamp01(trust);
            float d = Mathf.Max(0f, dt);
            float spawn = p.panicSpawnRate * e * (1f - t) * d;
            // 既存パニックが予想を裏書きして肥える（複利）＝自己成就
            float feed = p.panicGrowthRate * pa * e * (1f - t) * d;
            float calm = p.calmRate * t * pa * d;
            return Mathf.Clamp01(pa + spawn + feed - calm);
        }

        public static float HoardingPanicTick(float panic, float expectedShortage, float trust, float dt)
            => HoardingPanicTick(panic, expectedShortage, trust, dt, RationingParams.Default);
    }
}
