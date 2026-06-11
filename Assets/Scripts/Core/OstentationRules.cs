using UnityEngine;

namespace Ginei
{
    /// <summary>誇示的浪費（宮殿・式典・記念碑）の調整係数。</summary>
    public readonly struct OstentationParams
    {
        /// <summary>規模最大・聴衆最大の浪費が正統性に返す最大ボーナス。</summary>
        public readonly float legitimacyScale;
        /// <summary>正統性上昇の逓減の強さ（大きいほど頭打ちが早い＝盛大なほど効くが慣れる）。</summary>
        public readonly float diminishingStrength;
        /// <summary>国庫容量を超えた浪費の財政圧迫の急増係数（身の丈超過は非線形に痛い）。</summary>
        public readonly float strainScale;
        /// <summary>長期崩壊リスクの蓄積速度（身の丈超過の浪費が積もるほど早く育つ）。</summary>
        public readonly float collapseRate;
        /// <summary>破滅的奢侈の既定閾値（浪費が国庫容量のこの倍率を超えると破滅的）。</summary>
        public readonly float ruinousThreshold;

        public OstentationParams(float legitimacyScale, float diminishingStrength, float strainScale, float collapseRate, float ruinousThreshold)
        {
            this.legitimacyScale = Mathf.Max(0f, legitimacyScale);
            this.diminishingStrength = Mathf.Max(0f, diminishingStrength);
            this.strainScale = Mathf.Max(0f, strainScale);
            this.collapseRate = Mathf.Max(0f, collapseRate);
            this.ruinousThreshold = Mathf.Max(0.01f, ruinousThreshold);
        }

        /// <summary>既定＝正統性0.3・逓減1.5・圧迫2.0・崩壊率0.05・破滅閾値1.5。</summary>
        public static OstentationParams Default => new OstentationParams(0.3f, 1.5f, 2.0f, 0.05f, 1.5f);
    }

    /// <summary>
    /// 誇示的浪費と正統性の純ロジック（VEBL-3 #1601・ヴェブレン『有閑階級の理論』参考）。
    /// 地位は浪費の誇示で示される＝目立つ浪費（宮殿・式典・記念碑）が短期的に威信・正統性を買うが、
    /// 身の丈（国庫容量）を超えると財政を蝕み、その蓄積が長期に崩壊を招く両刃。
    /// 儀礼イベントの効果は <see cref="CeremonyRules"/>、栄典の半減期は <see cref="HonorsRules"/>、
    /// 財政の実体は FiscalRules が扱い、ここは誇示的浪費そのものの正統性効果と財政崩壊リスクのみ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OstentationRules
    {
        /// <summary>
        /// 浪費がもたらす正統性上昇＝規模(0..1)×聴衆の広さ(0..1)を逓減カーブに通す。
        /// 盛大なほど効くが頭打ち（1-exp 近似＝1/(1+k)で実装、k=diminishingStrength×規模×聴衆）。
        /// 見せる相手が居なければ（聴衆0）誇示は無意味＝0。
        /// </summary>
        public static float LegitimacyGain(float spending, float audienceReach, OstentationParams p)
        {
            float s = Mathf.Clamp01(spending);
            float a = Mathf.Clamp01(audienceReach);
            float raw = s * a; // 浪費の規模×見せる相手の広さ
            // 逓減：raw が大きいほど効率が落ちる（盛大なほど効くが頭打ち）
            float diminished = raw / (1f + p.diminishingStrength * raw);
            return diminished * p.legitimacyScale;
        }

        public static float LegitimacyGain(float spending, float audienceReach)
            => LegitimacyGain(spending, audienceReach, OstentationParams.Default);

        /// <summary>
        /// 財政圧迫＝浪費が国庫容量(0..1)を超えた分の二乗で急増（身の丈を超えると国庫を蝕む）。
        /// 容量内（spending ≤ treasuryCapacity）なら圧迫は0＝賄える浪費は痛まない。
        /// </summary>
        public static float FiscalStrain(float spending, float treasuryCapacity, OstentationParams p)
        {
            float s = Mathf.Clamp01(spending);
            float cap = Mathf.Clamp01(treasuryCapacity);
            float over = Mathf.Max(0f, s - cap); // 身の丈を超えた分
            return over * over * p.strainScale;  // 超過は非線形に痛む
        }

        public static float FiscalStrain(float spending, float treasuryCapacity)
            => FiscalStrain(spending, treasuryCapacity, OstentationParams.Default);

        /// <summary>
        /// 正統性の純効果＝短期の威信（正統性上昇）から財政圧迫を差し引く。
        /// 身の丈内なら丸ごとプラス、超過すると圧迫が威信を食い、やがてマイナスへ＝両刃が一式に出る。
        /// </summary>
        public static float NetLegitimacy(float legitimacyGain, float fiscalStrain)
        {
            return legitimacyGain - Mathf.Max(0f, fiscalStrain);
        }

        /// <summary>
        /// 浪費を重ねるほど一回の効果が薄れる倍率（0..1・慣れ）。累計誇示(0..1)が大きいほど低い。
        /// 1-累計の線形＝何もしていなければ満額1.0、誇示が極まれば0付近。
        /// </summary>
        public static float DiminishingReturns(float cumulativeOstentation)
        {
            return 1f - Mathf.Clamp01(cumulativeOstentation);
        }

        /// <summary>
        /// 長期崩壊リスクの増分＝身の丈を超えた浪費の蓄積(0..)×財政の弱さ(1-fiscalHealth)×時間×崩壊率。
        /// 健全な国庫（fiscalHealth=1）なら蓄積しても育たない＝浪費だけでは即崩壊しない。
        /// 戻り値は dt 当たりのリスク増分（呼び出し側が積算）。
        /// </summary>
        public static float LongTermCollapseRisk(float cumulativeSpending, float fiscalHealth, float dt, OstentationParams p)
        {
            float accum = Mathf.Max(0f, cumulativeSpending); // 身の丈を超えた浪費の蓄積
            float weakness = 1f - Mathf.Clamp01(fiscalHealth);
            float t = Mathf.Max(0f, dt);
            return accum * weakness * p.collapseRate * t;
        }

        public static float LongTermCollapseRisk(float cumulativeSpending, float fiscalHealth, float dt)
            => LongTermCollapseRisk(cumulativeSpending, fiscalHealth, dt, OstentationParams.Default);

        /// <summary>
        /// 財政を壊さず威信を最大化する適正浪費(0..1)。国庫容量までは圧迫ゼロで威信が伸びるので、
        /// 容量いっぱいが適正＝身の丈ぎりぎりの誇示が最も賢い。聴衆が広いほど効くため容量内で出し切る。
        /// 聴衆ゼロ（誰も見ない）なら浪費は無駄＝適正は0。
        /// </summary>
        public static float OptimalSpending(float treasuryCapacity, float audienceReach)
        {
            float cap = Mathf.Clamp01(treasuryCapacity);
            float a = Mathf.Clamp01(audienceReach);
            return cap * a; // 見せる相手が居る分だけ身の丈いっぱいまで誇示する
        }

        /// <summary>
        /// 破滅的奢侈か＝浪費が国庫容量の threshold 倍を超える（身の丈を大きく踏み越えた誇示）。
        /// </summary>
        public static bool IsRuinousLuxury(float spending, float treasuryCapacity, float threshold)
        {
            float s = Mathf.Clamp01(spending);
            float cap = Mathf.Clamp01(treasuryCapacity);
            float t = Mathf.Max(0.01f, threshold);
            return s > cap * t;
        }

        public static bool IsRuinousLuxury(float spending, float treasuryCapacity)
            => IsRuinousLuxury(spending, treasuryCapacity, OstentationParams.Default.ruinousThreshold);
    }
}
