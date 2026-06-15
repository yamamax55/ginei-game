using UnityEngine;

namespace Ginei
{
    /// <summary>闇市の調整係数。</summary>
    public readonly struct BlackMarketParams
    {
        /// <summary>統制×品不足が闇市を湧かせる速度（per dt・統制最強×欠乏最大のとき）。</summary>
        public readonly float spawnRate;
        /// <summary>闇市が需要で自己成長する率（per dt・既存規模に比例＝複利）。</summary>
        public readonly float growthRate;
        /// <summary>取り締まりが闇市を削る速度（per dt・取締努力1のとき）。</summary>
        public readonly float crackdownRate;
        /// <summary>統制の骨抜き上限（闇市が十分大きいとき配給・動員の実効がこの割合まで漏れる）。</summary>
        public readonly float maxLeakage;
        /// <summary>骨抜きが上限の半分に達する闇市規模（飽和カーブの肩）。</summary>
        public readonly float leakageHalfSize;
        /// <summary>取り締まりの資源コスト係数（努力1×規模0で基礎コストこの値）。</summary>
        public readonly float enforcementCostRate;
        /// <summary>闇市が市民の生存を支える裏の効用の上限（欠乏最大×規模十分のとき）。</summary>
        public readonly float maxSurvivalValue;
        /// <summary>根絶しても安全な品不足の閾値（これ以下＝配給がほぼ完璧＝命綱が要らない）。</summary>
        public readonly float safeScarcity;

        public BlackMarketParams(float spawnRate, float growthRate, float crackdownRate,
                                 float maxLeakage, float leakageHalfSize, float enforcementCostRate,
                                 float maxSurvivalValue, float safeScarcity)
        {
            this.spawnRate = Mathf.Max(0f, spawnRate);
            this.growthRate = Mathf.Max(0f, growthRate);
            this.crackdownRate = Mathf.Max(0f, crackdownRate);
            this.maxLeakage = Mathf.Clamp01(maxLeakage);
            this.leakageHalfSize = Mathf.Max(0.01f, leakageHalfSize);
            this.enforcementCostRate = Mathf.Max(0f, enforcementCostRate);
            this.maxSurvivalValue = Mathf.Max(0f, maxSurvivalValue);
            this.safeScarcity = Mathf.Clamp01(safeScarcity);
        }

        /// <summary>既定＝湧き0.05・成長0.03・取締0.1・骨抜き上限50%・半飽和規模10・取締コスト0.1・生存効用上限0.6・安全欠乏閾値0.1。</summary>
        public static BlackMarketParams Default
            => new BlackMarketParams(0.05f, 0.03f, 0.1f, 0.5f, 10f, 0.1f, 0.6f, 0.1f);
    }

    /// <summary>
    /// 闇市の純ロジック＝統制経済の影。統制（controlSeverity）が強く品（scarcity＝品不足）が無いほど湧き育つ
    /// ＝闇市は統制の鏡像であり、統制を強めるほど育つ。取り締まり（enforcement）で削れるが資源を食い、
    /// 黙認すれば統制が骨抜きになる（<see cref="ControlLeakage"/>）。一方で闇市は配給が破れた市民の命綱でもあり
    /// （<see cref="SurvivalValue"/>）、根絶が安全なのは配給がほぼ完璧な時だけ（<see cref="IsEradicationSafe"/>）。
    /// 統制そのもの（動員・配給の表側）は <see cref="MobilizationRules"/>、合法な自由市場の需給は
    /// <see cref="MarketRules"/> が扱い、ここは両者の隙間に湧く非合法の並行市場のみを扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BlackMarketRules
    {
        /// <summary>
        /// 闇市規模の1tick後の値。需要＝統制(0..1)×品不足(0..1)。需要に応じて湧き（統制が強く品が無いほど）、
        /// 取り締まりの薄い分だけ既存規模に比例して肥え（複利）、取締努力 enforcement(0..1) で削れる。下限0。
        /// 統制と欠乏が続くかぎり根絶しても湧き直す＝つぶせるのは規模であって需要ではない。
        /// </summary>
        public static float MarketSizeTick(float size, float controlSeverity, float scarcity,
                                           float enforcement, float dt, BlackMarketParams p)
        {
            float s = Mathf.Max(0f, size);
            float d = Mathf.Max(0f, dt);
            float e = Mathf.Clamp01(enforcement);
            // 需要＝統制の強さ×品不足：統制が無い（自由市場が満たす）or 品が足りているなら闇市の出番は無い
            float demand = Mathf.Clamp01(controlSeverity) * Mathf.Clamp01(scarcity);
            float spawn = p.spawnRate * demand * d;
            // 需要が太く取り締まりが薄いほど既存規模が肥える（複利）＝統制を強めるほど育つ
            float feast = p.growthRate * s * demand * (1f - e) * d;
            float cull = p.crackdownRate * e * s * d;
            return Mathf.Max(0f, s + spawn + feast - cull);
        }

        public static float MarketSizeTick(float size, float controlSeverity, float scarcity, float enforcement, float dt)
            => MarketSizeTick(size, controlSeverity, scarcity, enforcement, dt, BlackMarketParams.Default);

        /// <summary>
        /// 統制の骨抜き度（0..maxLeakage）。配給・動員の実効にこの割合の漏れが生じる＝実効へは（1−漏れ）を掛けて使う。
        /// 規模に対して飽和（leakageHalfSize で上限の半分）＝黙認し続けると統制は名ばかりになる。
        /// </summary>
        public static float ControlLeakage(float size, BlackMarketParams p)
        {
            float s = Mathf.Max(0f, size);
            if (s <= 0f) return 0f;
            return p.maxLeakage * s / (s + p.leakageHalfSize);
        }

        public static float ControlLeakage(float size) => ControlLeakage(size, BlackMarketParams.Default);

        /// <summary>
        /// 取り締まりの資源コスト（per dt 相当）＝enforcementCostRate×努力×（1＋規模）。
        /// 大きな闇市ほど高くつく＝育ててから叩くのは割に合わない。
        /// </summary>
        public static float EnforcementCost(float enforcement, float size, BlackMarketParams p)
        {
            float e = Mathf.Clamp01(enforcement);
            float s = Mathf.Max(0f, size);
            return p.enforcementCostRate * e * (1f + s);
        }

        public static float EnforcementCost(float enforcement, float size)
            => EnforcementCost(enforcement, size, BlackMarketParams.Default);

        /// <summary>
        /// 闇市が市民の生存を支える裏の効用（0..maxSurvivalValue）。品不足が深いほど・規模があるほど大きい
        /// （規模は leakageHalfSize で飽和）。つぶし切る（規模0）と配給が破れたとき市民に命綱が無い。
        /// 品不足0（配給完璧）なら効用0＝そのときだけ根絶は無痛。
        /// </summary>
        public static float SurvivalValue(float size, float scarcity, BlackMarketParams p)
        {
            float s = Mathf.Max(0f, size);
            if (s <= 0f) return 0f;
            return p.maxSurvivalValue * Mathf.Clamp01(scarcity) * s / (s + p.leakageHalfSize);
        }

        public static float SurvivalValue(float size, float scarcity)
            => SurvivalValue(size, scarcity, BlackMarketParams.Default);

        /// <summary>
        /// 黙認の均衡点＝既存規模の自己成長と取り締まりが釣り合う取締努力（0..1）。
        /// growth×需要×(1−e)＝crackdown×e を解いて e*＝g·demand/(g·demand＋c)。
        /// これ未満の努力＝黙認＝闇市は育ち、超えれば縮む。統制が強く品が無いほど均衡点は高くつく
        /// ＝統制を強めるほど取り締まりも重くなる（鏡像の証明）。需要0なら0＝取り締まる相手がいない。
        /// </summary>
        public static float ToleranceEquilibrium(float controlSeverity, float scarcity, BlackMarketParams p)
        {
            float demand = Mathf.Clamp01(controlSeverity) * Mathf.Clamp01(scarcity);
            float grow = p.growthRate * demand;
            float denom = grow + p.crackdownRate;
            if (denom <= 0f) return 0f;
            return grow / denom;
        }

        public static float ToleranceEquilibrium(float controlSeverity, float scarcity)
            => ToleranceEquilibrium(controlSeverity, scarcity, BlackMarketParams.Default);

        /// <summary>
        /// 根絶が安全か＝品不足が safeScarcity 以下（配給がほぼ完璧で市民が闇市に命を預けていない）。
        /// 欠乏下での根絶は <see cref="SurvivalValue"/> を失わせる＝配給失敗時に市民が死ぬ。
        /// </summary>
        public static bool IsEradicationSafe(float scarcity, BlackMarketParams p)
        {
            return Mathf.Clamp01(scarcity) <= p.safeScarcity;
        }

        public static bool IsEradicationSafe(float scarcity)
            => IsEradicationSafe(scarcity, BlackMarketParams.Default);
    }
}
