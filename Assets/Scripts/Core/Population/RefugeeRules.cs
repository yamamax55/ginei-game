using UnityEngine;

namespace Ginei
{
    /// <summary>難民の調整係数。</summary>
    public readonly struct RefugeeParams
    {
        /// <summary>戦火の激しさが人口を追い立てる割合の上限（warIntensity=1 で人口のこれだけが流出）。</summary>
        public readonly float maxDisplacedRatio;
        /// <summary>受け入れ負担が安定度を削る強さ（難民比に掛ける）。</summary>
        public readonly float burdenScale;
        /// <summary>難民が受け入れ先へ溶け込む速度（统合度の上昇・per dt）。</summary>
        public readonly float integrationRate;
        /// <summary>帰還が成立する故郷の安全度の閾値（safety がこれ以上で帰り始める）。</summary>
        public readonly float returnSafetyThreshold;
        /// <summary>帰還速度（安全な故郷へ戻る割合・per dt）。</summary>
        public readonly float returnRate;

        public RefugeeParams(float maxDisplacedRatio, float burdenScale, float integrationRate,
                             float returnSafetyThreshold, float returnRate)
        {
            this.maxDisplacedRatio = Mathf.Clamp01(maxDisplacedRatio);
            this.burdenScale = Mathf.Max(0f, burdenScale);
            this.integrationRate = Mathf.Max(0f, integrationRate);
            this.returnSafetyThreshold = Mathf.Clamp01(returnSafetyThreshold);
            this.returnRate = Mathf.Max(0f, returnRate);
        }

        /// <summary>既定＝最大流出30%・負担係数0.5・溶け込み0.05・帰還閾値0.7・帰還率0.1。</summary>
        public static RefugeeParams Default => new RefugeeParams(0.3f, 0.5f, 0.05f, 0.7f, 0.1f);
    }

    /// <summary>
    /// 難民の純ロジック。戦火（warIntensity）が住民を故郷から追い立て、受け入れ星系には人口という資源と
    /// 安定度を削る負担が同時に来る＝戦争の被害は前線の外へ波及する。難民は時間とともに溶け込み（統合）、
    /// 故郷が安全になれば帰還していく。占領の不満（<see cref="GovernanceRules"/>）とは別系統で、
    /// 人の移動だけを扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RefugeeRules
    {
        /// <summary>戦火で流出する人口＝人口×maxDisplacedRatio×戦火の激しさ(0..1)。</summary>
        public static float Displaced(float population, float warIntensity, RefugeeParams p)
        {
            return Mathf.Max(0f, population) * p.maxDisplacedRatio * Mathf.Clamp01(warIntensity);
        }

        public static float Displaced(float population, float warIntensity)
            => Displaced(population, warIntensity, RefugeeParams.Default);

        /// <summary>受け入れ先の難民比（0..1）＝難民数÷（受け入れ人口＋難民数）。負担の入力。</summary>
        public static float RefugeeFraction(float refugees, float hostPopulation)
        {
            float r = Mathf.Max(0f, refugees);
            float h = Mathf.Max(0f, hostPopulation);
            if (r + h <= 0f) return 0f;
            return r / (r + h);
        }

        /// <summary>
        /// 受け入れ負担＝安定度を削る量（0..burdenScale）。溶け込みが進むほど負担は軽くなる
        /// （integration 0..1＝未統合の難民だけが摩擦を生む）。
        /// </summary>
        public static float HostBurden(float refugees, float hostPopulation, float integration, RefugeeParams p)
        {
            float unintegrated = 1f - Mathf.Clamp01(integration);
            return RefugeeFraction(refugees, hostPopulation) * unintegrated * p.burdenScale;
        }

        public static float HostBurden(float refugees, float hostPopulation, float integration)
            => HostBurden(refugees, hostPopulation, integration, RefugeeParams.Default);

        /// <summary>溶け込みの1tick後の統合度（0..1）。integrationRate×dt で進む。</summary>
        public static float IntegrationTick(float integration, float dt, RefugeeParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(integration) + p.integrationRate * Mathf.Max(0f, dt));
        }

        public static float IntegrationTick(float integration, float dt)
            => IntegrationTick(integration, dt, RefugeeParams.Default);

        /// <summary>帰還が始まるか＝故郷の安全度 homeSafety(0..1) が閾値以上。</summary>
        public static bool CanReturn(float homeSafety, RefugeeParams p)
        {
            return Mathf.Clamp01(homeSafety) >= p.returnSafetyThreshold;
        }

        public static bool CanReturn(float homeSafety) => CanReturn(homeSafety, RefugeeParams.Default);

        /// <summary>
        /// 1tick の帰還者数。故郷が安全（CanReturn）なら難民×returnRate×dt が戻る。安全でなければ0。
        /// 溶け込みが進んだ難民は帰らない（integration ぶん帰還対象から除く＝定住）。
        /// </summary>
        public static float ReturnTick(float refugees, float homeSafety, float integration, float dt, RefugeeParams p)
        {
            if (!CanReturn(homeSafety, p)) return 0f;
            float mobile = Mathf.Max(0f, refugees) * (1f - Mathf.Clamp01(integration));
            return Mathf.Min(mobile, mobile * p.returnRate * Mathf.Max(0f, dt));
        }

        public static float ReturnTick(float refugees, float homeSafety, float integration, float dt)
            => ReturnTick(refugees, homeSafety, integration, dt, RefugeeParams.Default);
    }
}
