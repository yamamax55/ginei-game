using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 超越（人類進化）適格判定の調整係数（CEND-1 #2355）。安定・正統性・成熟（希望）の重み付き合成で
    /// 適格スコアを出し、<see cref="threshold"/> を超えると超越へ進める。重みの合計はおよそ1。
    /// </summary>
    public readonly struct TranscendenceParams
    {
        /// <summary>超越適格となる閾値 0..1（スコアがこれ以上で適格）。</summary>
        public readonly float threshold;

        /// <summary>安定度（<see cref="FactionStateRules.Stability"/>）の重み。</summary>
        public readonly float stabilityWeight;

        /// <summary>正統性（regime.legitimacy）の重み。</summary>
        public readonly float legitimacyWeight;

        /// <summary>成熟＝希望（community.hope）の重み。</summary>
        public readonly float researchWeight;

        public TranscendenceParams(float threshold, float stabilityWeight, float legitimacyWeight, float researchWeight)
        {
            this.threshold = threshold;
            this.stabilityWeight = stabilityWeight;
            this.legitimacyWeight = legitimacyWeight;
            this.researchWeight = researchWeight;
        }

        /// <summary>既定＝高い達成度を要求（閾値0.9）。重みは安定0.4/正統性0.3/成熟0.3＝合計1.0。</summary>
        public static TranscendenceParams Default => new TranscendenceParams(0.9f, 0.4f, 0.3f, 0.3f);
    }
}
