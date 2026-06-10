using UnityEngine;

namespace Ginei
{
    /// <summary>秘密警察（シュタージ型・#166）の調整係数。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct SecurityParams
    {
        /// <summary>監視網が反対派抑圧に効く重み。</summary>
        public readonly float surveillanceWeight;
        /// <summary>密告者網が反対派抑圧に効く重み。</summary>
        public readonly float informantWeight;
        /// <summary>クーデター摘発の基準成功率（装置ゼロでもこの確率で偶発的に露見）。</summary>
        public readonly float detectionBase;
        /// <summary>弾圧1.0あたりの支持低下の最大幅。</summary>
        public readonly float repressionSupportCost;

        public SecurityParams(float surveillanceWeight, float informantWeight, float detectionBase, float repressionSupportCost)
        {
            this.surveillanceWeight = surveillanceWeight;
            this.informantWeight = informantWeight;
            this.detectionBase = detectionBase;
            this.repressionSupportCost = repressionSupportCost;
        }

        public static SecurityParams Default => new SecurityParams(0.6f, 0.4f, 0.1f, 0.5f);
    }

    /// <summary>
    /// 秘密警察（シュタージ型・#166）の純ロジック。治安装置(<see cref="SecurityApparatus"/>)を使い、
    /// 反対派の抑圧度・クーデター謀議の摘発率・弾圧による支持低下を解決する唯一の窓口。
    /// 監視網＋密告者網が反対派を抑え込み陰謀を暴く一方、弾圧は支持を蝕む（恐怖統治のトレードオフ）。
    /// 値は徹底して 0..1 に clamp。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SecurityRules
    {
        /// <summary>
        /// 反対派の抑圧度(0..1)：監視網と密告者網の加重和を反対派の規模(dissent)に掛ける。
        /// 装置が広いほど・反対派が小さいほど抑え込める。dissent=0 なら抑圧の余地なし＝0。
        /// </summary>
        public static float DissentSuppression(SecurityApparatus a, float dissent, SecurityParams p)
        {
            if (a == null) return 0f;
            float reach = a.surveillance * p.surveillanceWeight + a.informantNetwork * p.informantWeight;
            return Mathf.Clamp01(reach) * Mathf.Clamp01(dissent);
        }

        public static float DissentSuppression(SecurityApparatus a, float dissent)
            => DissentSuppression(a, dissent, SecurityParams.Default);

        /// <summary>
        /// クーデター謀議の摘発確率(0..1)：基準率＋監視網＋密告者網で上がり、謀議者が多いほど
        /// 露見しやすい（密談の漏れ）。装置ゼロでも detectionBase ぶんは偶発的に露見する。
        /// </summary>
        public static float CoupDetectionChance(SecurityApparatus a, int plotters, SecurityParams p)
        {
            float reach = a == null ? 0f : a.surveillance * p.surveillanceWeight + a.informantNetwork * p.informantWeight;
            float scale = Mathf.Clamp01(plotters / (float)PlotterScale); // 謀議者が多いほど漏れる
            return Mathf.Clamp01(p.detectionBase + reach * scale);
        }

        public static float CoupDetectionChance(SecurityApparatus a, int plotters)
            => CoupDetectionChance(a, plotters, SecurityParams.Default);

        /// <summary>弾圧による支持低下(0..1)：弾圧が苛烈なほど支持を蝕む（恐怖統治の代償）。</summary>
        public static float RepressionSupportPenalty(float repression, SecurityParams p)
            => Mathf.Clamp01(Mathf.Clamp01(repression) * p.repressionSupportCost);

        public static float RepressionSupportPenalty(float repression)
            => RepressionSupportPenalty(repression, SecurityParams.Default);

        // --- 調整値（const に集約） ---
        /// <summary>摘発率が謀議者数で飽和する規模（これ以上は漏れやすさ一定）。</summary>
        public const int PlotterScale = 10;
    }
}
