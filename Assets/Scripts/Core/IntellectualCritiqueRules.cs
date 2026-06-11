using UnityEngine;

namespace Ginei
{
    /// <summary>知識人階級と正統性侵食（シュンペーター型）の調整係数。</summary>
    public readonly struct IntellectualCritiqueParams
    {
        /// <summary>繁栄×高等教育から生まれる知識人余剰の上限係数（0..1。豊かさが知識人を養う上限）。</summary>
        public readonly float surplusCeiling;
        /// <summary>職にあぶれた知識人の不満の鋭さ（≥0。供給超過がどれだけ不満に転じるか）。</summary>
        public readonly float discontentSharpness;
        /// <summary>不満が体制批判へ転じる係数（0..1。自由な環境での批判への変換率）。</summary>
        public readonly float critiqueWeight;
        /// <summary>批判圧が正統性を侵食する基礎速度（per dt・批判圧1のとき）。</summary>
        public readonly float erosionRate;
        /// <summary>取り込み（庇護・官職）が批判を和らげる係数（0..1。飼い慣らしの効き）。</summary>
        public readonly float cooptationWeight;
        /// <summary>弾圧が殉教者と反発を生む係数（≥0。言論弾圧の逆効果の鋭さ）。</summary>
        public readonly float backlashWeight;
        /// <summary>知識人反乱の既定閾値（0..1。批判圧がこれを超えると反乱）。</summary>
        public readonly float revoltThreshold;

        public IntellectualCritiqueParams(float surplusCeiling, float discontentSharpness, float critiqueWeight,
            float erosionRate, float cooptationWeight, float backlashWeight, float revoltThreshold)
        {
            this.surplusCeiling = Mathf.Clamp01(surplusCeiling);
            this.discontentSharpness = Mathf.Max(0f, discontentSharpness);
            this.critiqueWeight = Mathf.Clamp01(critiqueWeight);
            this.erosionRate = Mathf.Max(0f, erosionRate);
            this.cooptationWeight = Mathf.Clamp01(cooptationWeight);
            this.backlashWeight = Mathf.Max(0f, backlashWeight);
            this.revoltThreshold = Mathf.Clamp01(revoltThreshold);
        }

        /// <summary>既定＝余剰上限0.9・不満鋭さ1.0・批判重み0.8・侵食0.05・取り込み0.6・反発0.5・反乱閾値0.6。</summary>
        public static IntellectualCritiqueParams Default
            => new IntellectualCritiqueParams(0.9f, 1f, 0.8f, 0.05f, 0.6f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 知識人階級と正統性侵食の純ロジック（SCHU-5 #1595・シュンペーター『資本主義・社会主義・民主主義』参考）。
    /// 資本主義は自らの成功で敵対的な知識人階級を養い、内側から自壊する＝繁栄が高等教育を広げて知識人を増やし、
    /// 職を得られない不満な知識人が体制批判の担い手になる＝繁栄が体制の墓掘り人を育てる逆説。
    /// 報道による腐敗発見は <see cref="FreePressRules"/>、政権の世論操作は <see cref="PropagandaRules"/> が担い、
    /// ここは「繁栄が生む知識人階級の批判圧と正統性侵食」を扱う。批判が地下化して過激化する先＝隠密網は
    /// <see cref="SecretSocietyRules"/>（こちらは公然たる知識人の批判圧＝その手前の段階）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class IntellectualCritiqueRules
    {
        /// <summary>
        /// 知識人の余剰（0..1）＝余剰上限 × 繁栄 prosperity(0..1) × 高等教育の普及 education(0..1)。
        /// 豊かさが高等教育を広げ知識人を養う＝両方が高いほど余剰が大きい。
        /// 貧しいか教育が無ければ知識人は生まれない（その日の糧で手一杯＝批判の余裕がない）。
        /// </summary>
        public static float IntellectualSurplus(float prosperity, float education, IntellectualCritiqueParams p)
        {
            return p.surplusCeiling * Mathf.Clamp01(prosperity) * Mathf.Clamp01(education);
        }

        public static float IntellectualSurplus(float prosperity, float education)
            => IntellectualSurplus(prosperity, education, IntellectualCritiqueParams.Default);

        /// <summary>
        /// 職にあぶれた不満（0..1）＝知識人余剰 intellectualSurplus(0..1) のうち、職の受け皿
        /// jobAbsorption(0..1) で吸収しきれなかった供給超過 × 不満の鋭さ。
        /// 受け皿が知識人を全員吸収すれば不満ゼロ（皆が職を得れば批判の担い手は生まれない）、
        /// 供給が受け皿を超えるほど不満な知識人が溢れる（高学歴ワーキングプア＝最も危険な層）。
        /// </summary>
        public static float UnderemployedDiscontent(float intellectualSurplus, float jobAbsorption, IntellectualCritiqueParams p)
        {
            float surplus = Mathf.Clamp01(intellectualSurplus);
            float unabsorbed = surplus * (1f - Mathf.Clamp01(jobAbsorption));
            return Mathf.Clamp01(unabsorbed * p.discontentSharpness);
        }

        public static float UnderemployedDiscontent(float intellectualSurplus, float jobAbsorption)
            => UnderemployedDiscontent(intellectualSurplus, jobAbsorption, IntellectualCritiqueParams.Default);

        /// <summary>
        /// 体制批判圧（0..1）＝不満 unemployedDiscontent(0..1) × 批判重み × 言論の自由 intellectualFreedom(0..1)。
        /// 不満な知識人は自由な環境でこそ批判を強める（自由な言論空間が批判を増幅する）。
        /// 自由がゼロ（言論統制）なら表向きの批判圧はゼロ＝ただし不満が消えたわけではない（地下化の伏線）。
        /// </summary>
        public static float CritiquePressure(float unemployedDiscontent, float intellectualFreedom, IntellectualCritiqueParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(unemployedDiscontent) * p.critiqueWeight * Mathf.Clamp01(intellectualFreedom));
        }

        public static float CritiquePressure(float unemployedDiscontent, float intellectualFreedom)
            => CritiquePressure(unemployedDiscontent, intellectualFreedom, IntellectualCritiqueParams.Default);

        /// <summary>
        /// 正統性侵食の1tick後の正統性（0..1）＝正統性 − 侵食速度 × 批判圧 critiquePressure(0..1) × dt。
        /// 知識人の批判が体制の正統性を時間をかけて内側から削る＝成功が育てた墓掘り人がじわじわ効く。
        /// 批判圧ゼロなら正統性は不変（批判の担い手がいなければ侵食もない）。
        /// </summary>
        public static float LegitimacyErosionTick(float legitimacy, float critiquePressure, float dt, IntellectualCritiqueParams p)
        {
            float leg = Mathf.Clamp01(legitimacy);
            float erosion = p.erosionRate * Mathf.Clamp01(critiquePressure) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(leg - erosion);
        }

        public static float LegitimacyErosionTick(float legitimacy, float critiquePressure, float dt)
            => LegitimacyErosionTick(legitimacy, critiquePressure, dt, IntellectualCritiqueParams.Default);

        /// <summary>
        /// 取り込み後の批判圧（0..1）＝批判圧 × (1 − 取り込み重み × 庇護 patronage(0..1))。
        /// 知識人を官職・庇護で取り込めば批判は和らぐ（飼い慣らし＝体制内に取り込まれた知識人は牙を抜かれる）。
        /// 庇護ゼロなら批判圧は素通り、庇護を厚くするほど批判が鈍る（ただし重み&lt;1＝完全には黙らせられない）。
        /// </summary>
        public static float CooptationEffect(float critiquePressure, float patronage, IntellectualCritiqueParams p)
        {
            float damped = Mathf.Clamp01(critiquePressure) * (1f - p.cooptationWeight * Mathf.Clamp01(patronage));
            return Mathf.Clamp01(damped);
        }

        public static float CooptationEffect(float critiquePressure, float patronage)
            => CooptationEffect(critiquePressure, patronage, IntellectualCritiqueParams.Default);

        /// <summary>
        /// 弾圧の反発（0..1）＝批判圧 × (1 + 反発重み × 弾圧 repression(0..1))。
        /// 言論弾圧はかえって殉教者を生み反発を増幅する＝抑えつけるほど批判圧が膨らむ逆効果。
        /// 弾圧ゼロなら批判圧は素通り、弾圧を強めるほど跳ね返りが大きくなる（取り込みの正反対）。
        /// </summary>
        public static float RepressionBacklash(float critiquePressure, float repression, IntellectualCritiqueParams p)
        {
            float amplified = Mathf.Clamp01(critiquePressure) * (1f + p.backlashWeight * Mathf.Clamp01(repression));
            return Mathf.Clamp01(amplified);
        }

        public static float RepressionBacklash(float critiquePressure, float repression)
            => RepressionBacklash(critiquePressure, repression, IntellectualCritiqueParams.Default);

        /// <summary>
        /// 自壊度（0..1）＝繁栄 prosperity(0..1) × (1 − 正統性 legitimacy(0..1))。
        /// 繁栄が高いほど墓掘り人（知識人）を多く育て、正統性が低いほど自壊が進む
        /// ＝成功と正統性低下が重なるとき自壊が最大＝資本主義は自らの成功で内側から崩れる。
        /// 繁栄ゼロ（墓掘り人を養えない）か正統性満点（批判が効かない）なら自壊しない。
        /// </summary>
        public static float SelfUnderminingIndex(float prosperity, float legitimacy)
        {
            return Mathf.Clamp01(Mathf.Clamp01(prosperity) * (1f - Mathf.Clamp01(legitimacy)));
        }

        /// <summary>
        /// 知識人反乱の判定＝批判圧 critiquePressure が閾値 threshold を超えたか。
        /// 公然たる批判が臨界を超えると反乱へ転じる（地下化・過激化は <see cref="SecretSocietyRules"/> の領分）。
        /// </summary>
        public static bool IsIntelligentsiaRevolt(float critiquePressure, float threshold)
        {
            return Mathf.Clamp01(critiquePressure) > Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（revoltThreshold）版の知識人反乱判定。</summary>
        public static bool IsIntelligentsiaRevolt(float critiquePressure, IntellectualCritiqueParams p)
            => IsIntelligentsiaRevolt(critiquePressure, p.revoltThreshold);

        public static bool IsIntelligentsiaRevolt(float critiquePressure)
            => IsIntelligentsiaRevolt(critiquePressure, IntellectualCritiqueParams.Default.revoltThreshold);
    }
}
