using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 中央銀行・金融政策の薄いオーケストレータ（純ロジック・唯一の窓口の上位の配線）。
    /// 既存の <see cref="MonetaryPolicyRules"/>（テイラー則・公開市場操作・独立性）・<see cref="FiscalRules"/>
    /// （財政リスクプレミアム）・<see cref="LenderOfLastResortRules"/>（危機時の流動性供給）を<b>合成するだけ</b>＝
    /// 数式は委譲先へ任せ二重実装しない。状態（<see cref="CentralBank"/> 等）は引数で受ける（呼び出し側＝GalaxyView が保持）。
    /// 暦境界（年次/日次）の Tick から呼ぶ想定。実効値パターン（基準非破壊・OMO は cb を破壊的に更新）。乱数なし・決定論。test-first。
    /// </summary>
    public static class MonetaryAuthorityRules
    {
        /// <summary>政策金利の1tickあたりの調整速度（テイラー目標へ寄せる比率。緩やかに追従＝振動回避）。</summary>
        public const float RateAdjustSpeed = 0.5f;

        /// <summary>マネーサプライ調整の感度（インフレ・ギャップ1あたりにマネーを絞る／撒く割合）。</summary>
        public const float MoneyAdjustSensitivity = 0.1f;

        // ===== 政策の1tick（テイラー則で政策金利・マネーサプライを調整） =====

        /// <summary>
        /// 中央銀行の年次（または日次）金融政策：テイラー則（<see cref="MonetaryPolicyRules.TaylorRate"/>）で目標金利を算出し、
        /// 現行 policyRate をそこへ <see cref="RateAdjustSpeed"/>×dt で緩やかに寄せる（ZLB でクランプ）。
        /// 併せてマネーサプライを引き締め/緩和：インフレ目標超過なら絞り、不況（産出ギャップ負）なら撒く（OMO へ委譲）。
        /// cb を破壊的に更新する。
        /// </summary>
        public static void TickYear(CentralBank cb, float inflationRate, float outputGap, float dt)
        {
            if (cb == null || dt <= 0f) return;

            // テイラー則の目標金利（委譲）。
            float target = MonetaryPolicyRules.TaylorRate(cb, inflationRate, outputGap);

            // 目標へ緩やかに寄せる（比率は dt で1tick上限にクランプ）。
            float step = Mathf.Clamp01(RateAdjustSpeed * dt);
            cb.policyRate = Mathf.Max(MonetaryPolicyRules.ZeroLowerBound,
                Mathf.Lerp(cb.policyRate, target, step));

            // マネーサプライ調整：インフレが目標を超えるほど絞り（<0）、産出ギャップが負（不況）なら撒く（>0）。
            // 委譲＝OpenMarketOperation（売りオペ／買いオペ）。
            float inflationGap = inflationRate - cb.inflationTarget;
            float operation = (-inflationGap + outputGap) * MoneyAdjustSensitivity * cb.moneySupply * dt;
            MonetaryPolicyRules.OpenMarketOperation(cb, operation);
        }

        // ===== 実効金利（政策金利＋財政リスクプレミアム） =====

        /// <summary>
        /// 市場の実効金利＝中央銀行の政策金利（市場基準＝<see cref="MonetaryPolicyRules.MarketBaseRate"/>）に、
        /// 財政状態の債務リスクプレミアム（<see cref="FiscalRules.InterestRate"/> が基準金利を含むので、基準ぶんを差し引いた
        /// プレミアムのみ）を上乗せ。高債務国は政策金利より高い利回りを市場から要求される。
        /// </summary>
        public static float EffectiveInterestRate(CentralBank cb, FiscalState fs, float economy)
        {
            float policy = MonetaryPolicyRules.MarketBaseRate(cb); // cb==null で0
            if (fs == null) return policy;

            var p = FiscalRules.FiscalParams.Default;
            // FiscalRules.InterestRate = baseInterestRate + リスクプレミアム。基準金利は政策金利で置換し、
            // プレミアムぶんだけ上乗せする（基準の二重計上を避ける）。
            float fiscalRate = FiscalRules.InterestRate(fs, economy, p);
            float premium = Mathf.Max(0f, fiscalRate - p.baseInterestRate);
            return Mathf.Max(MonetaryPolicyRules.ZeroLowerBound, policy + premium);
        }

        // ===== 最後の貸し手（危機時の緊急流動性供給の判定） =====

        /// <summary>
        /// 危機時に中央銀行が緊急流動性を供給すべきか＝取付けの深さ×非流動資産で必要流動性を見積もり
        /// （<see cref="LenderOfLastResortRules.LiquidityNeeded"/>）、それが正なら支援対象。
        /// 支払不能（ゾンビ）は救わない＝<see cref="LenderOfLastResortRules.IlliquidVsInsolvent"/> で弁別。
        /// 流動性不足だが支払可能（solvent）な機関のみ true。委譲のみ・状態は変えない。
        /// </summary>
        public static bool ShouldProvideLiquidity(float panicSeverity, float illiquidAssets,
            float assetQuality, float liabilities)
        {
            var p = LenderOfLastResortParams.Default;
            float needed = LenderOfLastResortRules.LiquidityNeeded(panicSeverity, illiquidAssets);
            if (needed <= 0f) return false; // 危機なし＝供給不要
            // 救うのは流動性不足（solvent）のみ。支払不能なら救済は損失の付け替え。
            return LenderOfLastResortRules.IlliquidVsInsolvent(assetQuality, liabilities, p);
        }

        /// <summary>
        /// 緊急流動性の供給額＝取付けの深さ×非流動資産（<see cref="LenderOfLastResortRules.LiquidityNeeded"/>）。
        /// バジョット原則「無制限に貸す」の最小実装＝必要量を満たす（非負）。供給して cb のマネーを増やす運用は
        /// 呼び出し側で <see cref="MonetaryPolicyRules.QuantitativeEasing"/> 等へ橋渡しする。委譲のみ。
        /// </summary>
        public static float LiquidityToProvide(float panicSeverity, float illiquidAssets)
            => LenderOfLastResortRules.LiquidityNeeded(panicSeverity, illiquidAssets);
    }
}
