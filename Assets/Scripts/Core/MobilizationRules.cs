using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 徴募↔生産労働の競合＝総力戦（POPLAB-6・#2026・#96/#93/#113 連携・純ロジック）。
    /// 徴募（軍属/保安#96）への動員は生産労働（工員/農民/鉱員）を奪う＝兵を増やすほど産出#93が痩せ、過大な動員は支持#113を削る。
    /// 動員は生産年齢（とくに男性比）から引く。係数で背景的に（タイクン回避）。test-first。
    /// </summary>
    public static class MobilizationRules
    {
        /// <summary>動員プール＝労働力×動員率（軍属#96 へ回す）。</summary>
        public static float MobilizedPool(float laborForce, float mobilizationRate)
            => Mathf.Max(0f, laborForce) * Mathf.Clamp01(mobilizationRate);

        /// <summary>動員後の生産労働＝max(0, 生産労働−生産から取られた動員)。</summary>
        public static float ProductionLaborAfterMobilization(float productionLabor, float mobilizedFromProduction)
            => Mathf.Max(0f, Mathf.Max(0f, productionLabor) - Mathf.Max(0f, mobilizedFromProduction));

        /// <summary>産出ペナルティ係数＝1−動員率×感応度（動員で生産労働が減り産出#93↓）。下限0。</summary>
        public static float OutputFactor(float mobilizationRate, float sensitivity)
            => Mathf.Max(0f, 1f - Mathf.Clamp01(mobilizationRate) * Mathf.Max(0f, sensitivity));

        /// <summary>支持ペナルティ＝max(0, 動員率−閾値)×スケール（持続可能な動員率を超えると厭戦#113）。</summary>
        public static float SupportPenalty(float mobilizationRate, float threshold, float scale)
            => Mathf.Max(0f, Mathf.Clamp01(mobilizationRate) - Mathf.Clamp01(threshold)) * Mathf.Max(0f, scale);

        /// <summary>男性偏重の動員＝動員プールのうち男性から引く割合（男女比 #sex 連携）。男性比が低いと徴募源が細る。</summary>
        public static float MaleDrawnMobilization(float mobilizedPool, float maleShare, float maleBias)
            => Mathf.Max(0f, mobilizedPool) * Mathf.Clamp01(maleShare * Mathf.Max(0f, maleBias) + (1f - Mathf.Clamp01(maleBias)));
    }
}
