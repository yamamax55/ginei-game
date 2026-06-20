using UnityEngine;

namespace Ginei
{
    /// <summary>断言・反復・感染の調整係数（ル・ボン『群衆心理』型）。</summary>
    public readonly struct AssertionRepetitionParams
    {
        /// <summary>断言1のときの被信力スケール（証明抜きの単純な言い切りがどれだけ効くか）。</summary>
        public readonly float assertScale;
        /// <summary>真理性の錯覚の成長係数（反復回数あたりの被信度の伸び・逓減級数の係数 k）。</summary>
        public readonly float truthGain;
        /// <summary>反復累積の上乗せスケール（被信度がどれだけ押し上がるか）。</summary>
        public readonly float accumScale;
        /// <summary>感染（伝播）の速度スケール（人から人への広がりやすさ）。</summary>
        public readonly float contagionScale;
        /// <summary>植え付いた観念の反証耐性スケール（一度信じると覆りにくい強さ）。</summary>
        public readonly float resistanceScale;
        /// <summary>飽和の開始する反復回数の目安（これを超えると効果が頭打ち〜逆効果）。</summary>
        public readonly float saturationOnset;

        public AssertionRepetitionParams(float assertScale, float truthGain, float accumScale,
                                         float contagionScale, float resistanceScale, float saturationOnset)
        {
            this.assertScale = Mathf.Clamp01(assertScale);
            this.truthGain = Mathf.Max(0f, truthGain);
            this.accumScale = Mathf.Clamp01(accumScale);
            this.contagionScale = Mathf.Max(0f, contagionScale);
            this.resistanceScale = Mathf.Clamp01(resistanceScale);
            this.saturationOnset = Mathf.Max(1f, saturationOnset);
        }

        /// <summary>既定＝断言1.0・真理錯覚0.3・累積0.8・感染0.5・反証耐性0.6・飽和開始10回。</summary>
        public static AssertionRepetitionParams Default => new AssertionRepetitionParams(1.0f, 0.3f, 0.8f, 0.5f, 0.6f, 10f);
    }

    /// <summary>
    /// 断言・反復・感染（CRWD-2 #1821・ル・ボン『群衆心理』参考）の純ロジック。観念を群衆に植え付ける三段＝
    /// ①断言（assertion・証明抜きの単純で断定的な言い切り）→②反復（repetition・繰り返すほど被信度が累積し
    /// 「真理性の錯覚」を生む＝何度も聞いた話は本当に思える）→③感染（contagion・人から人へ伝播）。
    /// 反復で被信度が閾値を超えると感染段階に入り、植え付いた観念は反証に抗う＝一度信じると覆りにくい。
    /// ただし反復しすぎると飽和して効果が頭打ち〜逆効果になる。
    /// マクロの世論戦（<see cref="PropagandaRules"/>＝到達×信用×主張×検閲）とは別＝ル・ボンの断言・反復・感染の
    /// 観念植え付けメカニズムに特化。マニア感染（<see cref="ManiaRules"/>）/群衆感染（CrowdContagionRules・同EPIC CRWD）
    /// とは別＝反復による被信度累積に焦点。<see cref="BeliefPenetration"/> は BEH-7 の増幅率を connectivity 入力に取れる想定。
    /// 乱数なし（決定論）。盤面非依存の plain 引数。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AssertionRepetitionRules
    {
        /// <summary>
        /// 断言の強さ（0..1）＝単純さ simplicity(0..1)×断定の確信 confidence(0..1)×断言スケール。
        /// 単純で言い切るほど（証明や留保が少ないほど）群衆に刺さる＝ル・ボンの「単純化された断言」。
        /// </summary>
        public static float AssertionStrength(float simplicity, float confidence, AssertionRepetitionParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(simplicity) * Mathf.Clamp01(confidence) * p.assertScale);
        }

        public static float AssertionStrength(float simplicity, float confidence)
            => AssertionStrength(simplicity, confidence, AssertionRepetitionParams.Default);

        /// <summary>
        /// 真理性の錯覚（0..1）＝反復回数 repetitions(≥0) が増すほど 1−1/(1+truthGain×r) で逓減的に飽和上昇。
        /// 繰り返し聞いた主張は（中身を吟味せずとも）本当らしく感じる＝illusory truth effect。回数0で0、∞で1へ漸近。
        /// </summary>
        public static float IllusoryTruth(int repetitions, AssertionRepetitionParams p)
        {
            float r = Mathf.Max(0, repetitions);
            return Mathf.Clamp01(1f - 1f / (1f + p.truthGain * r));
        }

        public static float IllusoryTruth(int repetitions)
            => IllusoryTruth(repetitions, AssertionRepetitionParams.Default);

        /// <summary>
        /// 反復で累積した被信度（0..1）。現在の被信度 currentBelief に、断言の強さ×真理性の錯覚×残余の余地(1−belief)×
        /// 累積スケールを上乗せ。強い断言を繰り返すほど被信度が上限へ向けて積み上がる（逓減しつつ上限へ）。
        /// </summary>
        public static float RepetitionAccumulation(float currentBelief, float assertionStrength, int repetitions, AssertionRepetitionParams p)
        {
            float b = Mathf.Clamp01(currentBelief);
            float a = Mathf.Clamp01(assertionStrength);
            float illusory = IllusoryTruth(repetitions, p);
            float gain = a * illusory * (1f - b) * p.accumScale;   // 残余の余地ぶんだけ上乗せ（逓減）
            return Mathf.Clamp01(b + gain);
        }

        public static float RepetitionAccumulation(float currentBelief, float assertionStrength, int repetitions)
            => RepetitionAccumulation(currentBelief, assertionStrength, repetitions, AssertionRepetitionParams.Default);

        /// <summary>
        /// 飽和による減衰倍率（0..1）＝反復が飽和開始 saturationOnset を超えた超過分に比例して効果が頭打ち〜逆効果。
        /// 超過 0 で 1.0（無減衰）、超過が大きいほど倍率が下がる＝同じ主張を繰り返しすぎると食傷・反発を招く。
        /// </summary>
        public static float SaturationDecay(int repetitions, AssertionRepetitionParams p)
        {
            float r = Mathf.Max(0, repetitions);
            float over = Mathf.Max(0f, r - p.saturationOnset);
            return Mathf.Clamp01(1f / (1f + over / p.saturationOnset));   // 超過1単位ぶんで半減のなだらかな逓減
        }

        public static float SaturationDecay(int repetitions)
            => SaturationDecay(repetitions, AssertionRepetitionParams.Default);

        /// <summary>
        /// 感染段階に入ったか＝累積被信度 accumulatedBelief が感染閾値 threshold(0..1) を超えたか（bool）。
        /// 反復で被信度が閾値を越えると、観念は伝播力を得て人から人へ感染的に広がりはじめる。
        /// </summary>
        public static bool ContagionThreshold(float accumulatedBelief, float threshold)
        {
            return Mathf.Clamp01(accumulatedBelief) > Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// 感染の伝播速度（0..1）＝種となる被信度 seedBelief×社会的結合度 socialConnectivity×受け手の被暗示性
        /// susceptibility×感染スケール。観念は信じる者が多く・つながりが密で・暗示にかかりやすい群衆ほど速く広がる。
        /// </summary>
        public static float ContagionSpread(float seedBelief, float socialConnectivity, float susceptibility, AssertionRepetitionParams p)
        {
            float s = Mathf.Clamp01(seedBelief) * Mathf.Clamp01(socialConnectivity) * Mathf.Clamp01(susceptibility) * p.contagionScale;
            return Mathf.Clamp01(s);
        }

        public static float ContagionSpread(float seedBelief, float socialConnectivity, float susceptibility)
            => ContagionSpread(seedBelief, socialConnectivity, susceptibility, AssertionRepetitionParams.Default);

        /// <summary>
        /// 反証への抵抗倍率（0..1）＝植え付いた観念に対し、反証 counterEvidence(0..1) がどれだけ被信度を削れるか。
        /// 強く植え付いた（accumulatedBelief が高い）観念ほど反証を跳ね返す＝(1−counterEvidence×(1−belief×resistanceScale))。
        /// 返り値は反証後に残る被信度の割合＝1に近いほど覆りにくい。
        /// </summary>
        public static float CounterMessageResistance(float accumulatedBelief, float counterEvidence, AssertionRepetitionParams p)
        {
            float b = Mathf.Clamp01(accumulatedBelief);
            float ce = Mathf.Clamp01(counterEvidence);
            float effectiveCounter = ce * (1f - b * p.resistanceScale);   // 信じるほど反証が効かない
            return Mathf.Clamp01(1f - effectiveCounter);
        }

        public static float CounterMessageResistance(float accumulatedBelief, float counterEvidence)
            => CounterMessageResistance(accumulatedBelief, counterEvidence, AssertionRepetitionParams.Default);

        /// <summary>
        /// 観念の浸透度（0..1）＝断言・反復・感染の三段の総合。断言の強さ×真理性の錯覚（反復）×(0.5+0.5×結合度)
        /// ×飽和減衰。強い断言を、よく結合した群衆へ、飽和しない範囲で反復するほど深く浸透する。
        /// connectivity には BEH-7 の増幅率を入力に取れる想定。
        /// </summary>
        public static float BeliefPenetration(float assertionStrength, int repetitions, float connectivity, AssertionRepetitionParams p)
        {
            float a = Mathf.Clamp01(assertionStrength);
            float illusory = IllusoryTruth(repetitions, p);
            float conn = 0.5f + 0.5f * Mathf.Clamp01(connectivity);   // 結合度0でも下限0.5（断言自体の浸透）
            float saturation = SaturationDecay(repetitions, p);
            return Mathf.Clamp01(a * illusory * conn * saturation);
        }

        public static float BeliefPenetration(float assertionStrength, int repetitions, float connectivity)
            => BeliefPenetration(assertionStrength, repetitions, connectivity, AssertionRepetitionParams.Default);

        /// <summary>観念が植え付いたか＝累積被信度 accumulatedBelief が植え付け閾値 threshold(0..1) を超えた状態（bool）。</summary>
        public static bool IsImplanted(float accumulatedBelief, float threshold)
        {
            return Mathf.Clamp01(accumulatedBelief) > Mathf.Clamp01(threshold);
        }
    }
}
