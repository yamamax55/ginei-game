using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 通貨戦争（<see cref="CurrencyWarRules"/>）の純ロジックテスト。既定 <see cref="CurrencyWarParams.Default"/> で
    /// 期待値を固定（輸出てこ1.0・規制上積み0.5・逃避増幅1.0・基軸特権重み0.5）。Sqrt箇所のみ許容差を緩める。
    /// </summary>
    public class CurrencyWarRulesTests
    {
        const float Eps = 1e-4f;
        const float SqrtEps = 1e-3f;

        [Test]
        public void DevaluationAdvantage_IsRateTimesShare()
        {
            // 0.3 * 0.5 * 1.0(てこ) = 0.15
            Assert.AreEqual(0.15f, CurrencyWarRules.DevaluationAdvantage(0.3f, 0.5f), Eps);
        }

        [Test]
        public void CurrencyAttack_IsSpecOverSpecPlusReserves()
        {
            // 60 / (60+40) = 0.6
            Assert.AreEqual(0.6f, CurrencyWarRules.CurrencyAttack(60f, 40f), Eps);
        }

        [Test]
        public void CurrencyAttack_ZeroBothIsZero()
        {
            Assert.AreEqual(0f, CurrencyWarRules.CurrencyAttack(0f, 0f), Eps);
        }

        [Test]
        public void ReserveDefense_ControlsAddOnTopOfReserves()
        {
            // 0.6 + 0.8 * 0.5(重み) * (1-0.6) = 0.6 + 0.16 = 0.76
            Assert.AreEqual(0.76f, CurrencyWarRules.ReserveDefense(0.6f, 0.8f), Eps);
        }

        [Test]
        public void AttackSuccess_IsAttackTimesGapInDefense()
        {
            // 0.6 * (1-0.4) = 0.36
            Assert.AreEqual(0.36f, CurrencyWarRules.AttackSuccess(0.6f, 0.4f), Eps);
        }

        [Test]
        public void ReserveCurrencyPrivilege_IsGeometricMean()
        {
            // sqrt(0.64 * 0.81) = sqrt(0.5184) = 0.72
            Assert.AreEqual(0.72f, CurrencyWarRules.ReserveCurrencyPrivilege(0.64f, 0.81f), SqrtEps);
        }

        [Test]
        public void CapitalFlight_IsSuccessTimesConfidenceLoss()
        {
            // 0.36 * 0.5 * 1.0(増幅) = 0.18
            Assert.AreEqual(0.18f, CurrencyWarRules.CapitalFlight(0.36f, 0.5f), Eps);
        }

        [Test]
        public void FinancialSanctionImpact_IsControlTimesIntegration()
        {
            // 0.7 * 0.6 = 0.42
            Assert.AreEqual(0.42f, CurrencyWarRules.FinancialSanctionImpact(0.7f, 0.6f), Eps);
        }

        [Test]
        public void MonetaryStability_CombinesDefenseFlightPrivilege()
        {
            // 0.76 - 0.18 + 0.72 * 0.5(重み) = 0.58 + 0.36 = 0.94
            Assert.AreEqual(0.94f, CurrencyWarRules.MonetaryStability(0.76f, 0.18f, 0.72f), Eps);
        }

        [Test]
        public void MonetaryStability_CapitalFlightCanCollapseCurrency()
        {
            // 防衛薄・逃避甚大・基軸特権なし → マイナス（通貨危機）
            float s = CurrencyWarRules.MonetaryStability(0.1f, 0.9f, 0f);
            Assert.Less(s, 0f);
            Assert.GreaterOrEqual(s, -1f);
        }

        [Test]
        public void Inputs_AreClampedAndNullSafeOnExtremes()
        {
            // 範囲外入力でも 0..1 / -1..1 に収まる
            Assert.AreEqual(1f, CurrencyWarRules.DevaluationAdvantage(5f, 5f), Eps);
            Assert.AreEqual(0f, CurrencyWarRules.AttackSuccess(-1f, 2f), Eps);
            float s = CurrencyWarRules.MonetaryStability(10f, -10f, 10f);
            Assert.LessOrEqual(s, 1f);
            Assert.GreaterOrEqual(s, -1f);
        }

        /// <summary>
        /// 物語テスト：基軸通貨を持つ覇権国 vs 投機による通貨攻撃。
        /// 厚い外貨準備＋資本規制で防衛が固く、基軸特権が安定を底上げ＝攻撃が通らず通貨は盤石。
        /// 一方、準備の薄い小国は同じ投機で攻撃が通り、信認喪失で資本逃避→通貨危機に陥る。
        /// </summary>
        [Test]
        public void Story_ReserveCurrencyHegemonWeathersAttackButSmallNationCollapses()
        {
            // --- 覇権国（厚い準備・資本規制・基軸通貨）---
            float hegemonAttack = CurrencyWarRules.CurrencyAttack(50f, 200f);     // 投機<<準備 → 攻撃薄い
            float hegemonDefense = CurrencyWarRules.ReserveDefense(0.9f, 0.5f);    // 防衛固い
            float hegemonSuccess = CurrencyWarRules.AttackSuccess(hegemonAttack, hegemonDefense);
            float hegemonPrivilege = CurrencyWarRules.ReserveCurrencyPrivilege(0.9f, 0.9f);
            float hegemonFlight = CurrencyWarRules.CapitalFlight(hegemonSuccess, 0.3f);
            float hegemonStability = CurrencyWarRules.MonetaryStability(hegemonDefense, hegemonFlight, hegemonPrivilege);

            // --- 小国（薄い準備・規制なし・基軸でない）---
            float smallAttack = CurrencyWarRules.CurrencyAttack(50f, 20f);        // 投機>準備 → 攻撃強い
            float smallDefense = CurrencyWarRules.ReserveDefense(0.2f, 0f);       // 防衛弱い
            float smallSuccess = CurrencyWarRules.AttackSuccess(smallAttack, smallDefense);
            float smallPrivilege = CurrencyWarRules.ReserveCurrencyPrivilege(0.1f, 0.4f);
            float smallFlight = CurrencyWarRules.CapitalFlight(smallSuccess, 0.9f);
            float smallStability = CurrencyWarRules.MonetaryStability(smallDefense, smallFlight, smallPrivilege);

            // 覇権国は攻撃を撥ね返し通貨は盤石、小国は攻撃が通り資本逃避で危機
            Assert.Less(hegemonSuccess, smallSuccess);
            Assert.Greater(hegemonStability, smallStability);
            Assert.Greater(hegemonStability, 0f);
            Assert.Greater(smallFlight, hegemonFlight);
        }
    }
}
