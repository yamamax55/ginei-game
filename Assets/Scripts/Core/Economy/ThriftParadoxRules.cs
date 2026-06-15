using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 節約のパラドックス（合成の誤謬）の調整値（#1552 KEYN-5）。
    /// 個人の貯蓄合理性・総需要の落ち込み・乗数・デフレ罠閾値の係数を集約する（マジックナンバー禁止）。
    /// </summary>
    public readonly struct ThriftParadoxParams
    {
        /// <summary>個人が貯蓄を1単位増やしたときの主観的便益の最大値（ミクロの美徳の上限）。</summary>
        public readonly float individualBenefitScale;
        /// <summary>集団の貯蓄率が総需要を削る感度（全員の節約が消費を減らす連鎖の強さ）。</summary>
        public readonly float demandSensitivity;
        /// <summary>合成の誤謬が顕在化する集団貯蓄率の閾値（これを超えると個人合理が集団非合理に転じる）。</summary>
        public readonly float compositionThreshold;
        /// <summary>デフレ不況の罠に陥る集団貯蓄率の既定閾値（節約の連鎖が需要を底に固定する点）。</summary>
        public readonly float deflationaryThreshold;

        public ThriftParadoxParams(float individualBenefitScale, float demandSensitivity, float compositionThreshold, float deflationaryThreshold)
        {
            this.individualBenefitScale = Mathf.Max(0f, individualBenefitScale);
            this.demandSensitivity = Mathf.Clamp01(demandSensitivity);
            this.compositionThreshold = Mathf.Clamp01(compositionThreshold);
            this.deflationaryThreshold = Mathf.Clamp01(deflationaryThreshold);
        }

        /// <summary>既定＝個人便益スケール1.0・需要感度0.8・合成の誤謬閾値0.5・デフレ罠閾値0.6。</summary>
        public static ThriftParadoxParams Default => new ThriftParadoxParams(1.0f, 0.8f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 節約のパラドックス＝合成の誤謬の純ロジック（#1552 KEYN-5・ケインズ『一般理論』paradox of thrift）。
    /// 個人が貯蓄を増やすのは賢明（ミクロの美徳）だが、全員が一斉に節約すると消費＝総需要が崩壊し、
    /// 乗数を通じて所得が縮み、意図した貯蓄増は所得減で相殺され<b>総貯蓄もかえって減る</b>＝合成の誤謬。
    /// 集合的行為問題（自分だけ使うと損だから皆使わない）と協調の失敗で需要が底に張り付く。
    /// <see cref="MultiplierRules"/>(乗数効果・同EPIC)＝需要変化の増幅本体、
    /// <see cref="EffectiveDemandRules"/>(有効需要・同EPIC)＝需要水準そのものとは別＝こちらは「節約の逆説」専従。
    /// 集合行為の利得構造は <see cref="GameTheoryRules"/>(#388) を補完する（緊縮の囚人のジレンマ）。test-first。
    /// </summary>
    public static class ThriftParadoxRules
    {
        /// <summary>
        /// 個人にとっての貯蓄の便益（ミクロの美徳）。貯蓄率×自分の所得×係数。
        /// 個人視点では貯蓄を増やすほど将来の備えが増え合理的＝単調増加（合成の誤謬の出発点）。
        /// </summary>
        public static float IndividualBenefit(float savingsRate, float ownIncome, ThriftParadoxParams p)
        {
            float s = Mathf.Clamp01(savingsRate);
            float income = Mathf.Clamp01(ownIncome);
            return s * income * p.individualBenefitScale;
        }

        public static float IndividualBenefit(float savingsRate, float ownIncome)
            => IndividualBenefit(savingsRate, ownIncome, ThriftParadoxParams.Default);

        /// <summary>
        /// 総需要の落ち込み（0..1）＝全員が一斉に貯蓄を増やすと消費が減り総需要が下がる。
        /// 集団貯蓄率×需要感度。貯蓄＝消費しない分なので集団では需要の純減として効く。
        /// </summary>
        public static float AggregateDemandDrop(float collectiveSavingsRate, ThriftParadoxParams p)
            => Mathf.Clamp01(collectiveSavingsRate) * p.demandSensitivity;

        public static float AggregateDemandDrop(float collectiveSavingsRate)
            => AggregateDemandDrop(collectiveSavingsRate, ThriftParadoxParams.Default);

        /// <summary>
        /// 所得の縮小（0..1）＝需要減が乗数で所得を縮める（連鎖崩壊）。
        /// 需要減×(1+乗数)＝1単位の需要減が乗数ぶん追加の所得減を呼ぶ（消費→所得→消費の負の連鎖）。
        /// </summary>
        public static float IncomeContraction(float aggregateDemandDrop, float multiplier)
        {
            float drop = Mathf.Clamp01(aggregateDemandDrop);
            float m = Mathf.Clamp01(multiplier);
            return Mathf.Clamp01(drop * (1f + m));
        }

        /// <summary>
        /// 逆説的な貯蓄変化（パラドックスの核）＝意図した貯蓄増−所得減で失われた貯蓄。
        /// 所得が縮むと貯蓄の原資（所得）も減るため、意図した貯蓄増が相殺され、
        /// 値が負になりうる＝<b>全員が節約したのに総貯蓄はかえって減る</b>。
        /// </summary>
        public static float ParadoxicalSavingsChange(float intendedSaving, float incomeContraction)
        {
            float intended = Mathf.Clamp01(intendedSaving);
            float lost = Mathf.Clamp01(incomeContraction);
            // 所得減は貯蓄原資の喪失として意図した貯蓄増を食い、差し引きが負になりうる
            return intended - lost;
        }

        /// <summary>
        /// 合成の誤謬の度合い（0..1）＝個人の合理が集団で非合理になる強さ。
        /// 個人が合理的（貯蓄が合理）でなければ誤謬は生じない（0）。
        /// 個人合理かつ集団貯蓄率が閾値を超えるほど、閾値超過分が誤謬として現れる。
        /// </summary>
        public static float FallacyOfComposition(bool individualRational, float collectiveSavingsRate, ThriftParadoxParams p)
        {
            if (!individualRational) return 0f;
            float rate = Mathf.Clamp01(collectiveSavingsRate);
            if (rate <= p.compositionThreshold) return 0f;
            // 閾値超過分を残り幅で正規化＝閾値で0・全員貯蓄で1
            float span = Mathf.Max(0.0001f, 1f - p.compositionThreshold);
            return Mathf.Clamp01((rate - p.compositionThreshold) / span);
        }

        public static float FallacyOfComposition(bool individualRational, float collectiveSavingsRate)
            => FallacyOfComposition(individualRational, collectiveSavingsRate, ThriftParadoxParams.Default);

        /// <summary>
        /// 集合的行為問題の強さ（0..1）＝みなが緊縮（貯蓄）へ走る圧力。
        /// 参加率（緊縮に走る者の割合）×離反便益（自分だけ消費すると損＝皆使わない）。
        /// 自分だけ使う者が損をするほど全員が貯蓄へ集まり需要が崩れる。
        /// </summary>
        public static float CollectiveActionProblem(float participants, float defectionBenefit)
        {
            float part = Mathf.Clamp01(participants);
            float defect = Mathf.Clamp01(defectionBenefit);
            return part * defect;
        }

        /// <summary>
        /// 協調の失敗で底に張り付いた需要（0..1）＝協調できれば回復するはずの需要が出ない。
        /// 残存需要 = 需要の地力×(1−貯蓄率)。貯蓄率が高いほど協調失敗で需要は底へ近づく。
        /// </summary>
        public static float CoordinationFailure(float savingsRate, float demandStrength)
        {
            float s = Mathf.Clamp01(savingsRate);
            float strength = Mathf.Clamp01(demandStrength);
            return strength * (1f - s);
        }

        /// <summary>
        /// デフレ不況の罠か（節約の連鎖がデフレ不況に陥ったか）＝集団貯蓄率が閾値を超えたか。
        /// 閾値超で需要崩壊→所得減→さらに防衛的貯蓄、の悪循環に固定される。
        /// </summary>
        public static bool IsDeflationaryTrap(float collectiveSavingsRate, float threshold)
            => Mathf.Clamp01(collectiveSavingsRate) > Mathf.Clamp01(threshold);

        public static bool IsDeflationaryTrap(float collectiveSavingsRate, ThriftParadoxParams p)
            => IsDeflationaryTrap(collectiveSavingsRate, p.deflationaryThreshold);

        public static bool IsDeflationaryTrap(float collectiveSavingsRate)
            => IsDeflationaryTrap(collectiveSavingsRate, ThriftParadoxParams.Default.deflationaryThreshold);
    }
}
