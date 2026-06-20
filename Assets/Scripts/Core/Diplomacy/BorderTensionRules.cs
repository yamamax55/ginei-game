using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 国境緊張ルール（純ロジック）。
    /// 隣接勢力間の緊張を「兵力集中」「領土紛争」から算出し、
    /// 関係良好なら下げ、平時には自然減衰させる。発火点判定も提供する。
    /// </summary>
    public static class BorderTensionRules
    {
        /// <summary>緊張算出のパラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>兵力集中が緊張へ寄与する重み。</summary>
            public readonly float concentrationWeight;
            /// <summary>領土紛争が緊張へ寄与する重み。</summary>
            public readonly float disputeWeight;
            /// <summary>1tick・単位dt あたりの自然減衰量。</summary>
            public readonly float decayPerTick;
            /// <summary>緊張の上限値。</summary>
            public readonly float maxTension;

            public Params(float concentrationWeight, float disputeWeight, float decayPerTick, float maxTension)
            {
                this.concentrationWeight = concentrationWeight;
                this.disputeWeight = disputeWeight;
                this.decayPerTick = decayPerTick;
                this.maxTension = maxTension;
            }

            /// <summary>既定パラメータ。</summary>
            public static Params Default => new Params(60f, 40f, 5f, 100f);
        }

        // 関係opinionの想定範囲（-100..100）。良好なほど緊張を下げる係数に使う。
        private const float OpinionMin = -100f;
        private const float OpinionMax = 100f;
        // opinionによる緊張軽減の最大倍率（最良で兵力/紛張寄与をこの割合だけ削る）。
        private const float MaxOpinionRelief = 0.5f;

        /// <summary>
        /// 緊張の目標値を算出する。兵力集中・領土紛争が上げ、良好な関係が下げる。
        /// [0, maxTension] にクランプ。
        /// </summary>
        public static float TensionTarget(float troopConcentration01, float territorialDispute01, float relationOpinion, Params p)
        {
            // 入力を 0..1 に正規化（堅牢化）。
            float conc = Mathf.Clamp01(troopConcentration01);
            float disp = Mathf.Clamp01(territorialDispute01);

            // 緊張の素の寄与。
            float raw = conc * p.concentrationWeight + disp * p.disputeWeight;

            // opinion を 0(最悪)..1(最良) へ写し、良好なほど軽減倍率を増やす。
            float opinion01 = Mathf.InverseLerp(OpinionMin, OpinionMax, Mathf.Clamp(relationOpinion, OpinionMin, OpinionMax));
            float relief = 1f - MaxOpinionRelief * opinion01; // 1.0(最悪)..0.5(最良)
            float result = raw * relief;

            return Mathf.Clamp(result, 0f, p.maxTension);
        }

        /// <summary>TensionTarget の既定パラメータ版。</summary>
        public static float TensionTarget(float troopConcentration01, float territorialDispute01, float relationOpinion)
            => TensionTarget(troopConcentration01, territorialDispute01, relationOpinion, Params.Default);

        /// <summary>
        /// 緊張を目標値へ収束させる。目標が現在より低ければ平時とみなし自然減衰
        /// （decayPerTick*dt）も加える。1ステップで目標を行き過ぎない。[0, maxTension]。
        /// </summary>
        public static float Tick(float currentTension, float target, float dt, Params p)
        {
            float cur = Mathf.Clamp(currentTension, 0f, p.maxTension);
            float tgt = Mathf.Clamp(target, 0f, p.maxTension);
            float step = Mathf.Max(0f, p.decayPerTick * Mathf.Max(0f, dt));

            float next;
            if (tgt < cur)
            {
                // 平時＝目標へ向けて減衰。目標を下回らない（行き過ぎ防止）。
                next = Mathf.Max(tgt, cur - step);
            }
            else if (tgt > cur)
            {
                // 緊張上昇＝目標へ向けて上げる。目標を超えない（行き過ぎ防止）。
                next = Mathf.Min(tgt, cur + step);
            }
            else
            {
                next = cur;
            }

            return Mathf.Clamp(next, 0f, p.maxTension);
        }

        /// <summary>Tick の既定パラメータ版。</summary>
        public static float Tick(float currentTension, float target, float dt)
            => Tick(currentTension, target, dt, Params.Default);

        /// <summary>緊張が閾値以上なら発火点（衝突の火種）とみなす。</summary>
        public static bool IsFlashpoint(float tension, float threshold) => tension >= threshold;
    }
}
