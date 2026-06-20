using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 安全保障の供与（大国の保護下宣言）。
    /// 保護国を庇う大国の供与が侵略者を抑止する。
    /// 抑止力は保証国の国力と信頼性（言行一致＝本当に介入するか）に依存する。
    /// 純ロジック・決定論・クランプ済み（外部ゲーム型に依存しない）。
    /// </summary>
    public static class SecurityGuaranteeRules
    {
        /// <summary>抑止力計算のパラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>保証国の国力の重み。</summary>
            public readonly float strengthWeight;
            /// <summary>保証国の信頼性（介入する蓋然性）の重み。</summary>
            public readonly float credibilityWeight;

            public Params(float strengthWeight, float credibilityWeight)
            {
                this.strengthWeight = strengthWeight;
                this.credibilityWeight = credibilityWeight;
            }

            /// <summary>既定値（国力と信頼性を均等に評価）。</summary>
            public static Params Default => new Params(1f, 1f);
        }

        /// <summary>
        /// 抑止力（0..1）。国力と信頼性を重み付き加重平均し、重み和で正規化する。
        /// 両者が最大なら抑止力は 1 になる（重み和正規化）。
        /// </summary>
        public static float DeterrenceValue(float guarantorStrength01, float guarantorCredibility01, Params p)
        {
            float s = Mathf.Clamp01(guarantorStrength01);
            float c = Mathf.Clamp01(guarantorCredibility01);
            float weightSum = p.strengthWeight + p.credibilityWeight;
            if (weightSum <= 0f) return 0f; // 重み和ゼロは無効＝抑止なし
            float weighted = (s * p.strengthWeight + c * p.credibilityWeight) / weightSum;
            return Mathf.Clamp01(weighted);
        }

        /// <summary>抑止力（既定パラメータ）。</summary>
        public static float DeterrenceValue(float guarantorStrength01, float guarantorCredibility01)
            => DeterrenceValue(guarantorStrength01, guarantorCredibility01, Params.Default);

        /// <summary>
        /// 侵略者の躊躇（0..1）。抑止力から侵略者の決意を引いた差。
        /// 正＝抑止が決意を上回り思いとどまる、0＝決意が勝り抑止されない。
        /// </summary>
        public static float AggressorHesitation(float deterrence01, float aggressorResolve01)
        {
            float d = Mathf.Clamp01(deterrence01);
            float r = Mathf.Clamp01(aggressorResolve01);
            return Mathf.Clamp01(d - r);
        }

        /// <summary>侵略を抑止できるか（抑止力が決意以上なら抑止成立）。</summary>
        public static bool DetersAggression(float deterrence01, float aggressorResolve01)
            => Mathf.Clamp01(deterrence01) >= Mathf.Clamp01(aggressorResolve01);
    }
}
