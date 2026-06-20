using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 難民フロー規則：戦争や飢饉が住民を星系外へ押し出し、受け入れ側は労働力を得る一方で
    /// 公共サービスが逼迫し、難民の発生元への心象が悪化する（純ロジック・決定論）。
    /// </summary>
    public static class RefugeeFlowRules
    {
        // ゼロ除算回避用の微小値
        private const float Epsilon = 0.0001f;

        /// <summary>難民フローの調整パラメータ。</summary>
        public readonly struct Params
        {
            /// <summary>戦争強度が流出に寄与する重み。</summary>
            public readonly float warPush;
            /// <summary>飢饉度が流出に寄与する重み。</summary>
            public readonly float faminePush;
            /// <summary>1暦境界あたりの最大流出率（人口比）。</summary>
            public readonly float maxOutflowRate;
            /// <summary>難民1人あたりの受け入れ側サービス逼迫度。</summary>
            public readonly float strainPerCapita;

            public Params(float warPush, float faminePush, float maxOutflowRate, float strainPerCapita)
            {
                this.warPush = warPush;
                this.faminePush = faminePush;
                this.maxOutflowRate = maxOutflowRate;
                this.strainPerCapita = strainPerCapita;
            }
        }

        /// <summary>既定パラメータ。</summary>
        public static Params Default => new Params(0.3f, 0.25f, 0.2f, 0.0001f);

        /// <summary>
        /// 発生元星系からの難民流出数＝人口×clamp(戦争×warPush＋飢饉×faminePush, 0, maxOutflowRate)。
        /// </summary>
        public static float Outflow(float population, float warIntensity01, float famine01, Params p)
        {
            if (population <= 0f) return 0f; // 人口なしは流出なし
            float war = Mathf.Clamp01(warIntensity01);
            float fam = Mathf.Clamp01(famine01);
            float rate = Mathf.Clamp(war * p.warPush + fam * p.faminePush, 0f, p.maxOutflowRate);
            return population * rate;
        }

        /// <summary>既定パラメータ版。</summary>
        public static float Outflow(float population, float warIntensity01, float famine01)
            => Outflow(population, warIntensity01, famine01, Default);

        /// <summary>
        /// 受け入れ側のサービス逼迫度（0..1）＝clamp01((難民/受け入れ人口)×strainPerCapita)。
        /// </summary>
        public static float HostStrain(float refugees, float hostPopulation, Params p)
        {
            if (refugees <= 0f) return 0f;
            float denom = Mathf.Max(Epsilon, hostPopulation); // 受け入れ人口ゼロを保護
            return Mathf.Clamp01((refugees / denom) * p.strainPerCapita);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float HostStrain(float refugees, float hostPopulation)
            => HostStrain(refugees, hostPopulation, Default);

        /// <summary>
        /// 受け入れ側の心象変化（負＝発生元への心象が悪化）＝-clamp01(逼迫度)×max(0,maxOpinionPenalty)。
        /// </summary>
        public static float OpinionShift(float hostStrain01, float maxOpinionPenalty)
            => -Mathf.Clamp01(hostStrain01) * Mathf.Max(0f, maxOpinionPenalty);
    }
}
