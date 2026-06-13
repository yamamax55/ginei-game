using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>財政乗数（KEYN-2 #1542・ケインズ乗数効果 k=1/(1−c)）の純ロジックを担保する。</summary>
    public class MultiplierRulesTests
    {
        private MultiplierParams P => MultiplierParams.Default;

        /// <summary>乗数 k=1/(1−c)＝c=0.8 で k=5（消費性向が高いほど大きい）。</summary>
        [Test]
        public void SpendingMultiplier_ケインズ式()
        {
            Assert.AreEqual(5f, MultiplierRules.SpendingMultiplier(0.8f, P), 1e-4f);
            // c が大きいほど乗数が大きい
            Assert.Greater(MultiplierRules.SpendingMultiplier(0.9f, P), MultiplierRules.SpendingMultiplier(0.5f, P));
        }

        /// <summary>c→1 は発散するので上限クランプ（既定 maxMpc=0.95→k=20 で頭打ち・無限大にならない）。</summary>
        [Test]
        public void SpendingMultiplier_発散をクランプ()
        {
            Assert.AreEqual(20f, MultiplierRules.SpendingMultiplier(0.99f, P), 1e-3f);
            Assert.AreEqual(20f, MultiplierRules.SpendingMultiplier(1f, P), 1e-3f);
            Assert.False(float.IsInfinity(MultiplierRules.SpendingMultiplier(1f, P)));
        }

        /// <summary>総効果＝初期支出×乗数。</summary>
        [Test]
        public void TotalImpact_初期支出を乗数倍()
        {
            float k = MultiplierRules.SpendingMultiplier(0.8f, P); // 5
            Assert.AreEqual(5f, MultiplierRules.TotalImpact(1f, k), 1e-4f);
            Assert.AreEqual(10f, MultiplierRules.TotalImpact(2f, k), 1e-4f);
        }

        /// <summary>漏れ（貯蓄・税・輸入）が乗数を縮める＝c_eff=0.8×0.75×0.9=0.54→k≒2.17（漏れ無し5より小さい）。</summary>
        [Test]
        public void LeakageAdjusted_漏れで乗数が縮む()
        {
            Assert.AreEqual(0.54f, MultiplierRules.EffectiveMpc(0.8f, 0.25f, 0.1f), 1e-4f);
            float k = MultiplierRules.LeakageAdjustedMultiplier(0.8f, 0.25f, 0.1f, P);
            Assert.AreEqual(1f / 0.46f, k, 1e-3f);
            // 漏れ無し（税0・輸入0）なら素の乗数5に一致
            Assert.AreEqual(5f, MultiplierRules.LeakageAdjustedMultiplier(0.8f, 0f, 0f, P), 1e-3f);
            // 漏れが大きいほど乗数は小さい
            Assert.Less(k, MultiplierRules.SpendingMultiplier(0.8f, P));
        }

        /// <summary>第n波の所得増＝幾何級数 income×c^n（連鎖の各ラウンドが縮んでいく）。</summary>
        [Test]
        public void MultiplierRound_幾何級数()
        {
            Assert.AreEqual(1f, MultiplierRules.MultiplierRound(1f, 0.8f, 0), 1e-4f);
            Assert.AreEqual(0.8f, MultiplierRules.MultiplierRound(1f, 0.8f, 1), 1e-4f);
            Assert.AreEqual(0.64f, MultiplierRules.MultiplierRound(1f, 0.8f, 2), 1e-4f);
            Assert.AreEqual(0f, MultiplierRules.MultiplierRound(1f, 0.8f, -1), 1e-4f);
        }

        /// <summary>無限級数の収束値＝初期支出/(1−c)＝k 倍に収束（各ラウンドの和と一致）。</summary>
        [Test]
        public void ConvergedTotal_級数和がk倍に収束()
        {
            Assert.AreEqual(5f, MultiplierRules.ConvergedTotal(1f, 0.8f, P), 1e-4f);
            // 有限ラウンドの和は収束値へ近づく
            float sum = 0f;
            for (int r = 0; r < 200; r++) sum += MultiplierRules.MultiplierRound(1f, 0.8f, r);
            Assert.AreEqual(5f, sum, 1e-3f);
        }

        /// <summary>減税の乗数＝−c/(1−c)＝−4＝支出乗数(5)より絶対値が小さい（一部が貯蓄される）。</summary>
        [Test]
        public void TaxMultiplier_支出乗数より小さい()
        {
            Assert.AreEqual(-4f, MultiplierRules.TaxMultiplier(0.8f, P), 1e-4f);
            // 絶対値は支出乗数より1だけ小さい（k−1 の関係）
            float k = MultiplierRules.SpendingMultiplier(0.8f, P);
            Assert.AreEqual(k - 1f, Mathf.Abs(MultiplierRules.TaxMultiplier(0.8f, P)), 1e-3f);
        }

        /// <summary>均衡財政乗数＝1（増税と同額支出でも純効果1＝ハーヴェルモ）／流動性制約の閾値判定。</summary>
        [Test]
        public void BalancedBudget_と流動性制約()
        {
            Assert.AreEqual(1f, MultiplierRules.BalancedBudgetMultiplier(), 1e-6f);
            Assert.True(MultiplierRules.IsLiquidityConstrained(0.9f, P));   // c≥0.8
            Assert.False(MultiplierRules.IsLiquidityConstrained(0.5f, P));
        }
    }
}
