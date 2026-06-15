using UnityEngine;

namespace Ginei
{
    /// <summary>フレーミング効果の調整係数（カーネマン＝KAHN-4）。</summary>
    public readonly struct FramingParams
    {
        /// <summary>利得フレームのリスク回避バイアス（利得提示は安全策を魅力的に見せる強さ）。</summary>
        public readonly float gainRiskAversion;
        /// <summary>損失フレームのリスク選好バイアス（損失提示は博打を魅力的に見せる強さ）。</summary>
        public readonly float lossRiskSeeking;
        /// <summary>肯定/否定の言い回しが評価をずらす最大幅（言い回しだけで動く評価のレンジ）。</summary>
        public readonly float phrasingShiftScale;
        /// <summary>熟慮(System2)がフレーミングに抗う係数（計数能力×熟慮の抵抗強度）。</summary>
        public readonly float deliberationResistance;

        public FramingParams(float gainRiskAversion, float lossRiskSeeking,
                             float phrasingShiftScale, float deliberationResistance)
        {
            this.gainRiskAversion = Mathf.Clamp01(gainRiskAversion);
            this.lossRiskSeeking = Mathf.Clamp01(lossRiskSeeking);
            this.phrasingShiftScale = Mathf.Clamp01(phrasingShiftScale);
            this.deliberationResistance = Mathf.Clamp01(deliberationResistance);
        }

        /// <summary>既定＝利得回避0.6・損失選好0.6・言い回し幅0.3・熟慮抵抗0.8。</summary>
        public static FramingParams Default => new FramingParams(0.6f, 0.6f, 0.3f, 0.8f);
    }

    /// <summary>
    /// フレーミング効果の純ロジック（KAHN-4 #1840・カーネマン『ファスト&スロー』参考）。同一の事実でも、
    /// 提示の枠組み（利得フレーム vs 損失フレーム、肯定 vs 否定の言い回し）で受け手の選好が変わる
    /// ＝「90%生存」と「10%死亡」は同じ事実だが選好が反転する。利得フレームはリスク回避（安全策）を、
    /// 損失フレームはリスク選好（博打）を誘い、両者の差が同一事実に対する選好の振れ＝フレーミング・シフト。
    /// 計数能力と熟慮（System2）はフレーミングに抗う＝枠組みの影響を薄める。
    /// 世論戦の <see cref="PropagandaRules"/>（到達×信用×主張×検閲のマクロ説得）とは別＝同一事実の提示枠が
    /// 選好を反転させるミクロな認知効果。<see cref="ProspectRules"/>（損失回避の価値関数）の応用＝フレームが
    /// 参照点を動かす／<see cref="DualProcessRules"/>（System1/2）の抵抗を入力に取る。盤面非依存の plain 引数。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FramingRules
    {
        /// <summary>
        /// 利得フレームでの魅力（0..1）＝事実 outcome(0..1) を「得られるもの」として提示した時の選好。
        /// 利得提示はリスク回避を誘うため、確実な結果ほど魅力が増す＝outcome×(1+回避バイアス) を 0..1 へ。
        /// </summary>
        public static float GainFrameAppeal(float outcome, FramingParams p)
        {
            float o = Mathf.Clamp01(outcome);
            return Mathf.Clamp01(o * (1f + p.gainRiskAversion));
        }

        public static float GainFrameAppeal(float outcome)
            => GainFrameAppeal(outcome, FramingParams.Default);

        /// <summary>
        /// 損失フレームでの魅力（0..1）＝同じ事実 outcome を「失うもの」として提示した時の選好。
        /// 損失提示はリスク選好を誘うため、確実な結果が割り引かれる＝outcome×(1−選好バイアス) を 0..1 へ
        /// （同じ outcome でも利得フレームより低く出る＝反転の源）。
        /// </summary>
        public static float LossFrameAppeal(float outcome, FramingParams p)
        {
            float o = Mathf.Clamp01(outcome);
            return Mathf.Clamp01(o * (1f - p.lossRiskSeeking));
        }

        public static float LossFrameAppeal(float outcome)
            => LossFrameAppeal(outcome, FramingParams.Default);

        /// <summary>
        /// フレーミング・シフト（0..1）＝同一事実なのに利得/損失フレームで選好がどれだけ振れるか
        /// ＝|利得魅力−損失魅力|。大きいほどフレーム依存で判断が揺れる（合理的なら 0 のはず）。
        /// </summary>
        public static float FramingShift(float gainAppeal, float lossAppeal)
        {
            return Mathf.Clamp01(Mathf.Abs(Mathf.Clamp01(gainAppeal) - Mathf.Clamp01(lossAppeal)));
        }

        /// <summary>
        /// 肯定/否定の言い回しが評価をずらした値（0..1）＝素の評価 rawValue(0..1) を中心に、肯定性
        /// framingPositivity(0..1) が 0.5 を境に上下へずらす。同じ事実でも「90%成功」と「10%失敗」で
        /// 評価が変わる＝(positivity−0.5)×2×言い回し幅 を加える。
        /// </summary>
        public static float PositiveNegativePhrasing(float rawValue, float framingPositivity, FramingParams p)
        {
            float v = Mathf.Clamp01(rawValue);
            float bias = (Mathf.Clamp01(framingPositivity) - 0.5f) * 2f * p.phrasingShiftScale; // -scale..+scale
            return Mathf.Clamp01(v + bias);
        }

        public static float PositiveNegativePhrasing(float rawValue, float framingPositivity)
            => PositiveNegativePhrasing(rawValue, framingPositivity, FramingParams.Default);

        /// <summary>
        /// フレームで選好が反転するか＝利得フレームでの選択 gainFrameChoice と損失フレームでの選択
        /// lossFrameChoice が食い違えば反転（true）。同一事実なのに枠組みだけで結論が変わる古典的兆候。
        /// </summary>
        public static bool PreferenceReversal(bool gainFrameChoice, bool lossFrameChoice)
        {
            return gainFrameChoice != lossFrameChoice;
        }

        /// <summary>
        /// フレームの効力（0..1）＝受け手の被影響度 audienceSusceptibility(0..1)×枠組みの強さ
        /// frameStrength(0..1)。被影響な受け手に強い枠組みを当てるほど効く（抵抗を引く前の生の効力）。
        /// </summary>
        public static float FrameEffectiveness(float audienceSusceptibility, float frameStrength)
        {
            return Mathf.Clamp01(audienceSusceptibility) * Mathf.Clamp01(frameStrength);
        }

        /// <summary>
        /// フレーミング抵抗（0..1）＝計数能力 numeracy(0..1) と熟慮 deliberation(0..1) がフレーミングに抗う。
        /// 数字を吟味し（同じ事実だと見抜き）熟慮する受け手ほど枠組みに惑わされない＝平均×抵抗係数。
        /// System2（<see cref="DualProcessRules"/>）が立つほど効果が薄まる。
        /// </summary>
        public static float FramingResistance(float numeracy, float deliberation, FramingParams p)
        {
            float avg = (Mathf.Clamp01(numeracy) + Mathf.Clamp01(deliberation)) * 0.5f;
            return Mathf.Clamp01(avg * p.deliberationResistance);
        }

        public static float FramingResistance(float numeracy, float deliberation)
            => FramingResistance(numeracy, deliberation, FramingParams.Default);

        /// <summary>
        /// 正味のフレーミング影響（0..1）＝生の効力 frameEffectiveness から抵抗 framingResistance を
        /// 差し引く＝効力×(1−抵抗)。熟慮が立つほど枠組みの影響が削られる（実効値パターン）。
        /// </summary>
        public static float NetFramingInfluence(float frameEffectiveness, float framingResistance)
        {
            float e = Mathf.Clamp01(frameEffectiveness);
            float r = Mathf.Clamp01(framingResistance);
            return Mathf.Clamp01(e * (1f - r));
        }

        /// <summary>フレーム駆動の判断か＝正味影響 netInfluence が閾値 threshold(0..1) を超えた状態
        /// （事実でなく提示の枠組みが結論を決めている）。</summary>
        public static bool IsFramingDriven(float netInfluence, float threshold)
        {
            return Mathf.Clamp01(netInfluence) > Mathf.Clamp01(threshold);
        }
    }
}
