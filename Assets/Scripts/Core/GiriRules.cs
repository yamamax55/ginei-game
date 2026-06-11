using UnityEngine;

namespace Ginei
{
    /// <summary>義理・恩の負債構造の調整係数（『菊と刀』KIKU-2 #1835）。</summary>
    public readonly struct GiriParams
    {
        /// <summary>施し手の格1での恩の重さ加算スケール（格上から受けた恩ほど重い）。</summary>
        public readonly float statusWeightScale;
        /// <summary>返せない重荷の最大ペナルティ幅（返済能力ゼロでの重荷の上限）。</summary>
        public readonly float burdenScale;
        /// <summary>返済が忠誠に変わる効率（返した割合1での忠誠の上限）。</summary>
        public readonly float loyaltyScale;
        /// <summary>未返済の恥の最大幅（可視性1・未返済最大での恥の上限）。</summary>
        public readonly float shameScale;
        /// <summary>感謝↔怨みの転換閾値（重荷がこれを超えると怨みに傾く）。</summary>
        public readonly float gratitudeThreshold;

        public GiriParams(float statusWeightScale, float burdenScale, float loyaltyScale,
                          float shameScale, float gratitudeThreshold)
        {
            this.statusWeightScale = Mathf.Max(0f, statusWeightScale);
            this.burdenScale = Mathf.Clamp01(burdenScale);
            this.loyaltyScale = Mathf.Clamp01(loyaltyScale);
            this.shameScale = Mathf.Clamp01(shameScale);
            this.gratitudeThreshold = Mathf.Clamp01(gratitudeThreshold);
        }

        /// <summary>既定＝格重み0.5・重荷幅0.9・忠誠効率1.0・恥幅0.8・感謝閾値0.5。</summary>
        public static GiriParams Default => new GiriParams(0.5f, 0.9f, 1.0f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 恩の負債（受けた恩 totalOn と返した分 repaid の貸借・施し手の格 giverStatus）。純データ。
    /// コンストラクタで非負クランプ＋repaid は totalOn を超えない。Remaining＝未返済の恩。
    /// </summary>
    public readonly struct ObligationDebt
    {
        /// <summary>これまで受けた恩の総量（負債として累積・返すまで消えない）。</summary>
        public readonly float totalOn;
        /// <summary>これまで返した恩の量（totalOn を超えない）。</summary>
        public readonly float repaid;
        /// <summary>施し手の社会的格（0..1・格が高いほど恩が重い）。</summary>
        public readonly float giverStatus;

        public ObligationDebt(float totalOn, float repaid, float giverStatus)
        {
            this.totalOn = Mathf.Max(0f, totalOn);
            this.repaid = Mathf.Clamp(repaid, 0f, this.totalOn);
            this.giverStatus = Mathf.Clamp01(giverStatus);
        }

        /// <summary>未返済の恩＝受けた恩−返した分（返さねばならない残りの負債）。</summary>
        public float Remaining => Mathf.Max(0f, totalOn - repaid);

        /// <summary>返済済みの割合（0..1・totalOn=0なら完済扱いで1）。</summary>
        public float RepaidRatio => totalOn <= 0f ? 1f : Mathf.Clamp01(repaid / totalOn);
    }

    /// <summary>
    /// 義理・恩の負債構造の純ロジック（KIKU-2 #1835・『菊と刀』参考）。受けた恩（恵み）は返さねばならない
    /// 負債として累積し、返すまで消えない。返せば信頼・忠誠を生み（恩を返し合う関係）、返せぬ恩は重荷となり、
    /// 重荷が許容を超えれば感謝が怨みへ転じる。返さぬ恩は恥（恥の文化）に直結する。日本的な人間関係の貸借。
    /// 栄典＝名誉の経済（<see cref="HonorsRules"/>＝授与インフレ）とは別＝返さねばならない恩の貸借構造。
    /// 義理と人情の葛藤（GiriNinjoTensionRules・同EPIC）の入力源となり、恥（ShameRules・同EPIC＝恩を返さぬ恥）と接続。
    /// 旗幟の解決（<see cref="LoyaltyRules"/>）とは別＝恩義による忠誠の素を供給する。
    /// 乱数なし（決定論）。純ロジック（非 MonoBehaviour・盤面非依存・test-first）。
    /// </summary>
    public static class GiriRules
    {
        /// <summary>
        /// 発生する恩の重さ（0..1）＝受けた恵み benefitReceived(0..1)×(1＋施し手の格 giverStatus×格重み)。
        /// 同じ恵みでも格上から受けるほど重い恩になる。
        /// </summary>
        public static float OnIncurred(float benefitReceived, float giverStatus, GiriParams p)
        {
            float b = Mathf.Clamp01(benefitReceived);
            float s = Mathf.Clamp01(giverStatus);
            return Mathf.Clamp01(b * (1f + s * p.statusWeightScale));
        }

        public static float OnIncurred(float benefitReceived, float giverStatus)
            => OnIncurred(benefitReceived, giverStatus, GiriParams.Default);

        /// <summary>
        /// 恩の負債累積（0..∞）＝現在の負債 currentDebt に新たな恩 newOn を足す（返すまで消えない）。
        /// 義理・恩は受けるたびに積み上がる負債。
        /// </summary>
        public static float DebtAccumulation(float currentDebt, float newOn)
        {
            return Mathf.Max(0f, currentDebt) + Mathf.Max(0f, newOn);
        }

        /// <summary>
        /// 返済行為が負債を減らす量（0..debtSize）＝返済行為の大きさ repaymentAction(0..1)×負債規模 debtSize。
        /// 大きな返済ほど多くの負債を消すが、もとの負債を超えては返せない。
        /// </summary>
        public static float RepaymentValue(float repaymentAction, float debtSize)
        {
            float d = Mathf.Max(0f, debtSize);
            return Mathf.Clamp(Mathf.Clamp01(repaymentAction) * d, 0f, d);
        }

        /// <summary>
        /// 返せない恩の重荷（0..burdenScale）＝未返済の負債 obligationDebt×(1−返済能力 capacityToRepay)×重荷幅。
        /// 返済能力が低いほど（返せないほど）同じ負債が重くのしかかる。
        /// </summary>
        public static float DebtBurden(float obligationDebt, float capacityToRepay, GiriParams p)
        {
            float d = Mathf.Clamp01(obligationDebt);
            float inability = 1f - Mathf.Clamp01(capacityToRepay);
            return Mathf.Clamp01(d * inability * p.burdenScale);
        }

        public static float DebtBurden(float obligationDebt, float capacityToRepay)
            => DebtBurden(obligationDebt, capacityToRepay, GiriParams.Default);

        /// <summary>
        /// 返済から育つ忠誠（0..loyaltyScale）＝返した割合 repaidRatio(0..1)×忠誠効率。
        /// 恩を返し合うほど信頼・忠誠が育つ＝恩義による忠誠の素。
        /// </summary>
        public static float LoyaltyFromRepayment(float repaidRatio, GiriParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(repaidRatio) * p.loyaltyScale);
        }

        public static float LoyaltyFromRepayment(float repaidRatio)
            => LoyaltyFromRepayment(repaidRatio, GiriParams.Default);

        /// <summary>
        /// 恩を返さぬ恥（0..shameScale）＝未返済の負債 unpaidDebt(0..1)×可視性 visibility(0..1)×恥幅。
        /// 人目に晒される未返済ほど恥が大きい＝恥の文化（ShameRules）への接続。
        /// </summary>
        public static float ShameFromDefault(float unpaidDebt, float visibility, GiriParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(unpaidDebt) * Mathf.Clamp01(visibility) * p.shameScale);
        }

        public static float ShameFromDefault(float unpaidDebt, float visibility)
            => ShameFromDefault(unpaidDebt, visibility, GiriParams.Default);

        /// <summary>
        /// 複数の恩のどちらを優先して返すか（-1=A優先 / 0=同等 / +1=B優先）。
        /// 未返済が大きい恩を優先（より重い義理ほど先に返す＝格の高い恩・大きい恩を立てる）。
        /// </summary>
        public static int ObligationPriority(ObligationDebt debtA, ObligationDebt debtB)
        {
            // 優先度の重み＝未返済の恩×(1＋格)。重い義理ほど先に返すべき。
            float wA = debtA.Remaining * (1f + debtA.giverStatus);
            float wB = debtB.Remaining * (1f + debtB.giverStatus);
            if (wA > wB) return -1;   // A の義理が重い＝A を先に返す
            if (wB > wA) return 1;    // B の義理が重い
            return 0;
        }

        /// <summary>
        /// 恩が感謝になるか怨みになるか（-1..+1）＝重荷 debtBurden が感謝閾値を下回れば感謝（正）、
        /// 超えれば怨み（負）。返せる恩は感謝に、返せぬ重荷は怨みに転じる。
        /// </summary>
        public static float GratitudeVsResentment(float debtBurden, GiriParams p)
        {
            float burden = Mathf.Clamp01(debtBurden);
            float t = p.gratitudeThreshold;
            if (burden <= t)
            {
                // 重荷が軽いほど感謝（閾値で0、重荷ゼロで+1）
                float denom = Mathf.Max(0.0001f, t);
                return Mathf.Clamp01((t - burden) / denom);
            }
            // 閾値超は怨み（閾値で0、重荷最大で-1）
            float denomR = Mathf.Max(0.0001f, 1f - t);
            return -Mathf.Clamp01((burden - t) / denomR);
        }

        public static float GratitudeVsResentment(float debtBurden)
            => GratitudeVsResentment(debtBurden, GiriParams.Default);

        /// <summary>
        /// 恩義に縛られた状態か＝未返済の恩 obligationDebt が閾値 threshold(0..1) を超えていれば true。
        /// 返さねばならない義理が一定以上たまると、人は恩義に縛られて自由に動けない。
        /// </summary>
        public static bool IsIndebted(float obligationDebt, float threshold)
        {
            return Mathf.Clamp01(obligationDebt) > Mathf.Clamp01(threshold);
        }
    }
}
