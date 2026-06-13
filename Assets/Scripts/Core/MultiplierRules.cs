using UnityEngine;

namespace Ginei
{
    /// <summary>財政乗数の調整値（マジックナンバー禁止＝集約・top-level）。</summary>
    public readonly struct MultiplierParams
    {
        public readonly float maxMpc;        // 限界消費性向の上限（c→1 で k 発散を防ぐクランプ）
        public readonly float epsilon;       // 1−c などの分母の下限（ゼロ除算/発散回避）
        public readonly float liquidityMpcThreshold; // ここを超える c は乗数が大きい状況（流動性制約・手元現金で即消費）

        public MultiplierParams(float maxMpc, float epsilon, float liquidityMpcThreshold)
        {
            this.maxMpc = Mathf.Clamp(maxMpc, 0f, 0.999f);
            this.epsilon = Mathf.Max(0.0001f, epsilon);
            this.liquidityMpcThreshold = Mathf.Clamp01(liquidityMpcThreshold);
        }

        /// <summary>既定＝c 上限0.95（k=20で頭打ち）・分母下限0.0001・流動性制約しきい値0.8。</summary>
        public static MultiplierParams Default => new MultiplierParams(0.95f, 0.0001f, 0.8f);
    }

    /// <summary>
    /// 財政乗数の純ロジック（KEYN-2 #1542・ケインズ『一般理論』参考・唯一の窓口）。
    /// <b>乗数効果</b>＝政府支出1単位が所得→消費→所得…の連鎖で増幅し、乗数 k=1/(1−c)（c=限界消費性向）倍の総需要を生む。
    /// 消費性向が高いほど波及が大きく、貯蓄・税・輸入の<b>漏れ</b>が乗数を縮める。減税は一部が貯蓄されるため支出乗数より小さく、
    /// 増税と同額支出の均衡財政乗数は1（ハーヴェルモ）。<see cref="FiscalRules"/>(国家財政の収支)／
    /// <see cref="EffectiveDemandRules"/>(需要ギャップ・同EPIC)／<see cref="ThriftParadoxRules"/>(節約のパラドックス・同EPIC) とは別＝
    /// 支出の所得連鎖による<b>増幅</b>のみを扱う。乱数なし決定論。test-first。
    /// </summary>
    public static class MultiplierRules
    {
        /// <summary>財政乗数 k=1/(1−c)（c=限界消費性向・0..1）。c が高いほど連鎖が大きく増幅。c→1 は発散するので上限クランプ。</summary>
        public static float SpendingMultiplier(float marginalPropensityToConsume, MultiplierParams p)
        {
            float c = Mathf.Min(Mathf.Clamp01(marginalPropensityToConsume), p.maxMpc);
            return 1f / Mathf.Max(p.epsilon, 1f - c);
        }

        /// <summary>総効果＝初期支出×乗数＝所得連鎖で増幅した総需要への効果。</summary>
        public static float TotalImpact(float initialSpending, float multiplier)
            => Mathf.Max(0f, initialSpending) * Mathf.Max(0f, multiplier);

        /// <summary>
        /// 漏れ調整後の実効限界消費性向＝c×(1−税率)×(1−輸入性向)。
        /// 所得の一部は貯蓄(1−c)・税・輸入で<b>漏れ</b>て再消費されない＝連鎖が早く減衰する。
        /// </summary>
        public static float EffectiveMpc(float mpc, float taxRate, float importPropensity)
        {
            float c = Mathf.Clamp01(mpc);
            float afterTax = 1f - Mathf.Clamp01(taxRate);
            float afterImport = 1f - Mathf.Clamp01(importPropensity);
            return Mathf.Clamp01(c * afterTax * afterImport);
        }

        /// <summary>漏れ調整後の現実の乗数 k=1/(1−c_eff)。漏れが大きいほど c_eff が小さく乗数も小さい。</summary>
        public static float LeakageAdjustedMultiplier(float mpc, float taxRate, float importPropensity, MultiplierParams p)
            => SpendingMultiplier(EffectiveMpc(mpc, taxRate, importPropensity), p);

        /// <summary>
        /// 第 round 波の所得増＝income×c^round（連鎖の各ラウンド＝幾何級数）。
        /// round=0 は初期所得そのもの、以降は前の波の c 倍ずつ縮む。
        /// </summary>
        public static float MultiplierRound(float income, float mpc, int round)
        {
            if (round < 0) return 0f;
            float c = Mathf.Clamp01(mpc);
            return Mathf.Max(0f, income) * Mathf.Pow(c, round);
        }

        /// <summary>
        /// 無限級数の収束値＝initialSpending×(1+c+c²+…)＝initialSpending/(1−c_eff)＝k 倍に収束。
        /// 各ラウンドの和（幾何級数）が乗数倍へ収束することを示す。
        /// </summary>
        public static float ConvergedTotal(float initialSpending, float effectiveMpc, MultiplierParams p)
        {
            float c = Mathf.Min(Mathf.Clamp01(effectiveMpc), p.maxMpc);
            return Mathf.Max(0f, initialSpending) / Mathf.Max(p.epsilon, 1f - c);
        }

        /// <summary>
        /// 減税の乗数＝−c/(1−c)。減税は所得を直接は増やさず<b>消費を介して</b>波及するため、
        /// 一部(1−c)が貯蓄され支出乗数(k)より絶対値が小さい（符号は需要押し上げ＝正の効果の係数）。
        /// </summary>
        public static float TaxMultiplier(float mpc, MultiplierParams p)
        {
            float c = Mathf.Min(Mathf.Clamp01(mpc), p.maxMpc);
            return -c / Mathf.Max(p.epsilon, 1f - c);
        }

        /// <summary>均衡財政乗数＝1（増税と同額支出は k+(−(k−1))=1＝純効果1・ハーヴェルモの定理）。</summary>
        public static float BalancedBudgetMultiplier() => 1f;

        /// <summary>流動性制約下か＝c が高く乗数が大きい状況（手元現金が即消費に回り波及が大きい）。</summary>
        public static bool IsLiquidityConstrained(float mpc, MultiplierParams p)
            => Mathf.Clamp01(mpc) >= p.liquidityMpcThreshold;
    }
}
