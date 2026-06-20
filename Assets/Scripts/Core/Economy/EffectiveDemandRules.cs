using UnityEngine;

namespace Ginei
{
    /// <summary>有効需要の調整値（マジックナンバー禁止＝集約・top-level）。</summary>
    public readonly struct EffectiveDemandParams
    {
        /// <summary>産出ギャップ→非自発的失業の係数（オークン的＝負ギャップが失業を生む傾き）。</summary>
        public readonly float okunCoefficient;
        /// <summary>失業率の上限（産出が完全に遊んでもこの値で頭打ち）。</summary>
        public readonly float maxUnemployment;
        /// <summary>政府支出の需要刺激の効き（需要ギャップを埋める強さ＝乗数前の一次効果）。</summary>
        public readonly float stimulusEffectiveness;
        /// <summary>需要制約と見なすギャップ閾値（これより深い負ギャップ＝供給力はあるのに需要がない）。</summary>
        public readonly float demandConstraintThreshold;

        public EffectiveDemandParams(float okunCoefficient, float maxUnemployment, float stimulusEffectiveness, float demandConstraintThreshold)
        {
            this.okunCoefficient = Mathf.Max(0f, okunCoefficient);
            this.maxUnemployment = Mathf.Clamp01(maxUnemployment);
            this.stimulusEffectiveness = Mathf.Max(0f, stimulusEffectiveness);
            this.demandConstraintThreshold = Mathf.Max(0f, demandConstraintThreshold);
        }

        /// <summary>
        /// 既定＝オークン係数0.5（産出ギャップ1割で失業0.5割）・失業上限0.5・刺激効率0.8・需要制約閾値0.05。
        /// </summary>
        public static EffectiveDemandParams Default => new EffectiveDemandParams(0.5f, 0.5f, 0.8f, 0.05f);
    }

    /// <summary>
    /// 有効需要ギャップ＝ケインズの有効需要の原理（KEYN-1 #1540・『一般理論』参考）。
    /// <b>供給が需要を生むのでなく、需要が産出を決める＝セイの法則の否定</b>：総需要(C+I+G)が潜在産出を下回ると、
    /// 現実産出は需要に制約され、潜在−現実の差＝<see cref="OutputGap"/> ぶん資源（失業・遊休設備）が遊ぶ。
    /// 需要不足は非自発的失業を生み（オークン的）、政府支出はその需要ギャップを埋める。需要が潜在を超えると過熱インフレギャップ。
    /// <see cref="MarketRules"/>（個別財の需給・価格均衡）/<see cref="FiscalRules"/>（国家財政・国債）とは別＝<b>マクロの需要不足と OutputGap</b>。
    /// OutputGap は同EPIC KEYN の <c>MultiplierRules</c>（財政乗数・需要刺激の波及先）の主要入力になる。
    /// 全入力クランプ・乱数なし決定論。調整値は <see cref="EffectiveDemandParams"/>（既定 <see cref="EffectiveDemandParams.Default"/>）。
    /// </summary>
    public static class EffectiveDemandRules
    {
        /// <summary>
        /// 総需要＝消費＋投資＋政府支出（C+I+G・各0..1）。有効需要の原理の左辺＝これが産出を決める。
        /// 合計は青天井にせず0..3にクランプ（各成分が満額でも3＝潜在の3倍まで）。
        /// </summary>
        public static float AggregateDemand(float consumption, float investment, float government)
        {
            float c = Mathf.Clamp01(consumption);
            float i = Mathf.Clamp01(investment);
            float g = Mathf.Clamp01(government);
            return Mathf.Clamp(c + i + g, 0f, 3f);
        }

        /// <summary>
        /// 現実産出＝総需要に制約される（有効需要の原理の核）。<b>需要が産出を決める</b>：潜在産出があっても
        /// 需要を超えては生産しない＝min(総需要, 潜在産出)。需要不足なら潜在を下回り、資源が遊ぶ。
        /// </summary>
        public static float ActualOutput(float aggregateDemand, float potentialOutput)
        {
            float ad = Mathf.Max(0f, aggregateDemand);
            float pot = Mathf.Clamp01(potentialOutput);
            return Mathf.Min(ad, pot); // 供給力でなく需要が天井＝セイの法則の否定
        }

        /// <summary>
        /// 産出ギャップ＝潜在−現実（負＝需要不足の遊休／正＝需要過熱）。
        /// 負のギャップこそケインズの問題＝供給力があるのに需要がなく遊ぶ。MultiplierRules 等の主要入力。
        /// </summary>
        public static float OutputGap(float actualOutput, float potentialOutput)
        {
            float pot = Mathf.Clamp01(potentialOutput);
            float act = Mathf.Max(0f, actualOutput);
            return pot - act; // >0＝遊休（需要不足）／<0＝現実が潜在超え（過熱）
        }

        /// <summary>
        /// 需要不足が遊ばせる資源（失業・遊休設備の総量）＝負でない産出ギャップ。
        /// ギャップが正（需要不足）のときだけ資源が遊ぶ＝max(0, gap)。過熱（負ギャップ）は遊休0。
        /// </summary>
        public static float IdleResources(float outputGap)
            => Mathf.Max(0f, outputGap);

        /// <summary>
        /// 産出ギャップが生む非自発的失業（オークン的・0..maxUnemployment）。
        /// 遊休資源×オークン係数＝需要不足が深いほど失業が増える。需要起因の失業＝完全雇用の自発的失業とは別。
        /// </summary>
        public static float UnemploymentFromGap(float outputGap, EffectiveDemandParams p)
        {
            float idle = IdleResources(outputGap);
            return Mathf.Clamp(idle * p.okunCoefficient, 0f, p.maxUnemployment);
        }

        /// <summary>
        /// 政府支出が需要ギャップを埋める一次効果（<b>需要不足時のみ有効</b>）。
        /// 需要不足（gap>0）なら政府支出×効率で埋め、埋められるのは遊休ぶんまで（過熱時は0＝呼び水不要）。
        /// 返値はギャップを縮める需要量＝乗数波及（MultiplierRules）の手前の一次刺激。
        /// </summary>
        public static float DemandStimulus(float governmentSpending, float gap, EffectiveDemandParams p)
        {
            if (gap <= 0f) return 0f; // 過熱・均衡では刺激は需要不足を埋めない
            float g = Mathf.Clamp01(governmentSpending);
            float raw = g * p.stimulusEffectiveness;
            return Mathf.Min(raw, gap); // 埋められるのは遊休ぶんまで
        }

        /// <summary>
        /// インフレギャップ＝需要が潜在を超えた過熱ぶん（max(0, 総需要−潜在)）。
        /// 供給力の天井を需要が超える＝産出は増えず物価が上がる（需要超過分は遊休でなくインフレ圧）。
        /// </summary>
        public static float InflationaryGap(float actualDemand, float potentialOutput)
        {
            float ad = Mathf.Max(0f, actualDemand);
            float pot = Mathf.Clamp01(potentialOutput);
            return Mathf.Max(0f, ad - pot); // 潜在超過＝過熱インフレ圧
        }

        /// <summary>
        /// 需要制約の判定（供給力はあるのに需要がない＝ケインズ的不況）。
        /// 産出ギャップが閾値より深い正＝需要不足で資源が遊んでいる＝true。閾値内/過熱は false。
        /// </summary>
        public static bool IsDemandConstrained(float outputGap, float threshold)
            => outputGap > Mathf.Max(0f, threshold);

        /// <summary>需要制約の判定（既定閾値＝<paramref name="p"/>.demandConstraintThreshold）。</summary>
        public static bool IsDemandConstrained(float outputGap, EffectiveDemandParams p)
            => IsDemandConstrained(outputGap, p.demandConstraintThreshold);
    }
}
