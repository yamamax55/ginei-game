using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 中央集権 vs 地方自治のトレードオフ（純ロジック）。
    /// 中央統制が強いほど統治の協調は増すが地方の不満が高まる。
    /// 自治が強いほど不満は下がるが中央の行政到達は弱まる。
    /// 既存ゲーム型に依存しない自己完結の static ルール。
    /// </summary>
    public static class ProvincialAutonomyRules
    {
        /// <summary>調整パラメータ（マジックナンバーの単一の置き場）。</summary>
        public readonly struct Params
        {
            /// <summary>中央統制が不満へ寄与する係数。</summary>
            public readonly float unrestFromControl;
            /// <summary>中央統制が行政到達へ寄与する係数。</summary>
            public readonly float adminFromControl;
            /// <summary>素の地方不満（統制・文化に依らない下駄）。</summary>
            public readonly float baseUnrest;

            public Params(float unrestFromControl, float adminFromControl, float baseUnrest)
            {
                this.unrestFromControl = unrestFromControl;
                this.adminFromControl = adminFromControl;
                this.baseUnrest = baseUnrest;
            }

            /// <summary>既定値。</summary>
            public static Params Default => new Params(0.6f, 1.0f, 0.1f);
        }

        // 0..1 へクランプ
        private static float Clamp01(float v) => Mathf.Clamp01(v);

        /// <summary>
        /// 地方の不満度（0..1）。中央統制は文化的距離が大きいほど不満を増幅する。
        /// </summary>
        public static float LocalUnrest(float centralControl01, float culturalDistance01, Params p)
        {
            float control = Clamp01(centralControl01);
            float culture = Clamp01(culturalDistance01);
            // 素の不満＋（統制×係数×文化的距離）
            return Clamp01(p.baseUnrest + control * p.unrestFromControl * culture);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float LocalUnrest(float centralControl01, float culturalDistance01)
            => LocalUnrest(centralControl01, culturalDistance01, Params.Default);

        /// <summary>
        /// 中央の行政到達度（0..1）。統制が強いほど高いが、距離が遠いほど減衰する。
        /// </summary>
        public static float AdminReach(float centralControl01, float distance01, Params p)
        {
            float control = Clamp01(centralControl01);
            float distance = Clamp01(distance01);
            // 統制×係数×（1-距離）＝遠方ほど投射が弱まる
            return Clamp01(control * p.adminFromControl * (1f - distance));
        }

        /// <summary>既定パラメータ版。</summary>
        public static float AdminReach(float centralControl01, float distance01)
            => AdminReach(centralControl01, distance01, Params.Default);

        /// <summary>地方自治度（0..1）。中央統制の補数。</summary>
        public static float Autonomy(float centralControl01) => 1f - Clamp01(centralControl01);
    }
}
