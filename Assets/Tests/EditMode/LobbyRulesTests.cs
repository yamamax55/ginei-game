using NUnit.Framework;
using UnityEngine;
using Ginei;
using LParams = Ginei.LobbyRules.LobbyParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 圧力団体＝ロビーの政治力学（LobbyRules）を固定する：影響力（金×数×コネの重み和）、
    /// 政策歪み（公益に反するほど害・一致すれば無害）、集中利益・分散費用の非対称
    /// （少数が大きく得て多数が薄く損する＝抵抗されにくい・オルソンの集合行為論）、
    /// 規制の虜（影響力−監督が閾値以上・強い監督は歯止め）、対抗ロビーの綱引き（拮抗で相殺）、
    /// 全体厚生損失（個別最適の集積が全体最適を壊す）。既定 Params の具体値で期待値固定。
    /// </summary>
    public class LobbyRulesTests
    {
        // 影響力：金0.4・数0.3・コネ0.3の重み和。均等入力は0.5・各単独で重みが出る
        [Test]
        public void InfluenceStrength_DefaultParams_FixedValues()
        {
            Assert.AreEqual(0.5f, LobbyRules.InfluenceStrength(0.5f, 0.5f, 0.5f), 1e-4f); // 均等
            Assert.AreEqual(0.4f, LobbyRules.InfluenceStrength(1f, 0f, 0f), 1e-4f);       // 資金のみ＝資金重み
            Assert.AreEqual(0.6f, LobbyRules.InfluenceStrength(0f, 1f, 1f), 1e-4f);       // 数＋コネ＝0.3+0.3
            Assert.AreEqual(1f, LobbyRules.InfluenceStrength(2f, 2f, 2f), 1e-4f);         // 入力クランプ
            Assert.AreEqual(0f, LobbyRules.InfluenceStrength(0f, 0f, 0f), 1e-4f);
        }

        // 政策歪み：影響力×(1−公益一致)。公益一致なら無害（0）・反公益の強いロビーほど害
        [Test]
        public void PolicyDistortion_AlignmentMakesItHarmless()
        {
            Assert.AreEqual(0.8f, LobbyRules.PolicyDistortion(0.8f, 0f), 1e-4f);  // 完全反公益＝影響力そのまま害
            Assert.AreEqual(0f, LobbyRules.PolicyDistortion(0.8f, 1f), 1e-4f);    // 公益一致＝無害
            Assert.AreEqual(0.4f, LobbyRules.PolicyDistortion(0.8f, 0.5f), 1e-4f);// 半分一致＝半減
            Assert.AreEqual(0f, LobbyRules.PolicyDistortion(0f, 0f), 1e-4f);      // 影響力なし＝歪みなし
            Assert.AreEqual(1f, LobbyRules.PolicyDistortion(2f, -1f), 1e-4f);     // 入力クランプ
        }

        // 集中利益・分散費用：(1−受益者シェア)の2乗。少数受益(0.1)ほど非対称が深い＝抵抗されにくい
        [Test]
        public void ConcentratedBenefitDiffusedCost_FewBeneficiariesWinEasier()
        {
            float few = LobbyRules.ConcentratedBenefitDiffusedCost(0.1f);  // (0.9)^2=0.81
            float half = LobbyRules.ConcentratedBenefitDiffusedCost(0.5f); // (0.5)^2=0.25
            float many = LobbyRules.ConcentratedBenefitDiffusedCost(0.9f); // (0.1)^2=0.01
            Assert.AreEqual(0.81f, few, 1e-4f);
            Assert.AreEqual(0.25f, half, 1e-4f);
            Assert.AreEqual(0.01f, many, 1e-4f);
            // 少数が得る政策ほど多数が組織できず非対称が大きい＝集中した少数が勝つ
            Assert.Greater(few, half);
            Assert.Greater(half, many);
            // 全員受益(share=1)は非対称なし・全員負担(share=0)は最大
            Assert.AreEqual(0f, LobbyRules.ConcentratedBenefitDiffusedCost(1f), 1e-4f);
            Assert.AreEqual(1f, LobbyRules.ConcentratedBenefitDiffusedCost(0f), 1e-4f);
        }

        // 規制の虜：影響力−監督が閾値0.5以上で成立。強い監督は高影響力でも歯止めになる
        [Test]
        public void RegulatoryCapture_StrongOversightResists()
        {
            Assert.IsTrue(LobbyRules.RegulatoryCapture(0.9f, 0.2f));   // 0.9−0.2=0.7≥0.5＝虜
            Assert.IsFalse(LobbyRules.RegulatoryCapture(0.9f, 0.5f));  // 0.9−0.5=0.4<0.5＝持ちこたえる
            Assert.IsTrue(LobbyRules.RegulatoryCapture(0.5f, 0f));     // 0.5−0=0.5＝閾値ちょうどで成立
            Assert.IsFalse(LobbyRules.RegulatoryCapture(0.4f, 0f));    // 0.4<0.5＝不成立
            Assert.IsFalse(LobbyRules.RegulatoryCapture(1f, 1f));      // 完全監督＝差0で不成立
        }

        // 対抗ロビーの綱引き：拮抗(a≈b)で相殺して0付近・一方が勝てばその差が正味の歪み圧
        [Test]
        public void CounterLobbyBalance_OffsetWhenMatched()
        {
            Assert.AreEqual(0f, LobbyRules.CounterLobbyBalance(0.7f, 0.7f), 1e-4f);   // 拮抗＝相殺
            Assert.AreEqual(0.5f, LobbyRules.CounterLobbyBalance(0.8f, 0.3f), 1e-4f); // Aが勝つ
            Assert.AreEqual(-0.5f, LobbyRules.CounterLobbyBalance(0.3f, 0.8f), 1e-4f);// Bが勝つ（符号反転）
            Assert.AreEqual(1f, LobbyRules.CounterLobbyBalance(2f, 0f), 1e-4f);       // 入力クランプ
        }

        // 全体厚生損失：1−Π(1−歪み)。一つ一つ小さくても積み重なると全体最適が壊れる
        [Test]
        public void AggregateWelfareLoss_IndividualOptimaWreckTheWhole()
        {
            // 0.5を二つ：1−(0.5×0.5)=0.75（各々は半分でも合計で4分の3が損なわれる）
            Assert.AreEqual(0.75f, LobbyRules.AggregateWelfareLoss(new[] { 0.5f, 0.5f }), 1e-4f);
            // 0.2/0.3/0.5：1−(0.8×0.7×0.5)=0.72
            Assert.AreEqual(0.72f, LobbyRules.AggregateWelfareLoss(new[] { 0.2f, 0.3f, 0.5f }), 1e-4f);
            // 単独はその歪みそのまま
            Assert.AreEqual(0.4f, LobbyRules.AggregateWelfareLoss(new[] { 0.4f }), 1e-4f);
            // null/空は0（陳情なし＝損失なし）
            Assert.AreEqual(0f, LobbyRules.AggregateWelfareLoss(null), 1e-4f);
            Assert.AreEqual(0f, LobbyRules.AggregateWelfareLoss(new float[0]), 1e-4f);
            // 合成は最大の単独歪みより必ず大きい＝積み上げ
            float agg = LobbyRules.AggregateWelfareLoss(new[] { 0.3f, 0.3f });
            Assert.Greater(agg, 0.3f);
        }

        // Params ctor：全フィールドがクランプされる（負・範囲外を入れても安全）
        [Test]
        public void Params_Constructor_ClampsAllFields()
        {
            var p = new LParams(-1f, 2f, 0.3f, -1f, 0.5f, 5f);
            Assert.AreEqual(0f, p.fundingWeight, 1e-4f);
            Assert.AreEqual(1f, p.membershipWeight, 1e-4f);
            Assert.AreEqual(0.3f, p.accessWeight, 1e-4f);
            Assert.AreEqual(0f, p.distortionScale, 1e-4f);
            Assert.AreEqual(1f, p.asymmetryExponent, 1e-4f);   // 線形未満にしない
            Assert.AreEqual(1f, p.captureThreshold, 1e-4f);
            // 重み合計0なら均等平均にフォールバック（0除算防止）
            var zero = new LParams(0f, 0f, 0f, 1f, 2f, 0.5f);
            Assert.AreEqual(0.5f, LobbyRules.InfluenceStrength(0.5f, 0.5f, 0.5f, zero), 1e-4f);
        }
    }
}
