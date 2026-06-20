using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 貿易依存規則：非対称な相互依存が外交的てこ（leverage）を生む。
    /// 相手への依存度が低い側ほど、より依存している側に対し影響力を持つ（純ロジック・決定論）。
    /// </summary>
    public static class TradeDependencyRules
    {
        // ゼロ除算回避用の微小値
        private const float Epsilon = 0.0001f;

        /// <summary>貿易てこの調整パラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>依存度の差をてこへ写す重み。</summary>
            public readonly float leverageWeight;

            public Params(float leverageWeight)
            {
                this.leverageWeight = leverageWeight;
            }
        }

        /// <summary>既定パラメータ。</summary>
        public static Params Default => new Params(1f);

        /// <summary>
        /// 相手への依存度（0..1）＝clamp01(相手からの輸入 / 総輸入)。
        /// </summary>
        public static float Dependence(float importsFromPartner, float totalImports)
        {
            if (importsFromPartner <= 0f) return 0f;
            float denom = Mathf.Max(Epsilon, totalImports); // 総輸入ゼロを保護
            return Mathf.Clamp01(importsFromPartner / denom);
        }

        /// <summary>
        /// 自勢力のてこ（-1..1）＝clamp(-1,1, (相手の依存度 - 自分の依存度)×leverageWeight)。
        /// 正＝こちらが影響力を持つ（相手の方が依存している）。
        /// </summary>
        public static float Leverage(float ourDependence01, float theirDependence01, Params p)
        {
            float ours = Mathf.Clamp01(ourDependence01);
            float theirs = Mathf.Clamp01(theirDependence01);
            return Mathf.Clamp((theirs - ours) * p.leverageWeight, -1f, 1f);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float Leverage(float ourDependence01, float theirDependence01)
            => Leverage(ourDependence01, theirDependence01, Default);

        /// <summary>
        /// 強制力ポテンシャル＝max(0, leverage)。正のてこだけが相手を強制できる。
        /// </summary>
        public static float CoercionPotential(float leverage)
            => Mathf.Max(0f, leverage);
    }
}
