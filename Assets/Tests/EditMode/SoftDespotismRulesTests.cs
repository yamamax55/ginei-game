using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 穏やかな専制（TOCQ-4 #1492・トクヴィル）の純ロジックのテスト。
    /// 後見的国家の力・市民の受動化・自律の萎縮・快適さと自由の取引・幼児化・静かな支配・
    /// 参加の衰退・穏やかな専制判定を既定 Params の具体値で固定する。
    /// </summary>
    public class SoftDespotismRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>後見的国家の力＝面倒見の範囲×行政浸透。片方0なら後見は届かない。</summary>
        [Test]
        public void TutelaryPower_面倒見と浸透の積()
        {
            // 0.8 × 0.5 × 係数1 = 0.4
            Assert.AreEqual(0.4f, SoftDespotismRules.TutelaryPower(0.8f, 0.5f), Eps);
            // 行政浸透0なら後見は届かない
            Assert.AreEqual(0f, SoftDespotismRules.TutelaryPower(1f, 0f), Eps);
        }

        /// <summary>市民の受動化＝後見力×伸び代×受動化速度×dt で飽和的に進む。</summary>
        [Test]
        public void CivicPassivation_後見が市民を受動化する()
        {
            // 受動化0.2・後見力1・dt1：0.2 + 1×(1−0.2)×0.1×1 = 0.28
            Assert.AreEqual(0.28f, SoftDespotismRules.CivicPassivation(0.2f, 1f, 1f), Eps);
            // 後見が無ければ受動化は進まない
            Assert.AreEqual(0.2f, SoftDespotismRules.CivicPassivation(0.2f, 0f, 1f), Eps);
        }

        /// <summary>自律の萎縮＝受動化を増幅して飽和。使わない自由ほど速く錆びる。</summary>
        [Test]
        public void AutonomyAtrophy_受動化が深いほど自律が萎縮する()
        {
            // 既定 atrophyScale=1 → 指数2：1 − (1−0.5)^2 = 0.75
            Assert.AreEqual(0.75f, SoftDespotismRules.AutonomyAtrophy(0.5f), Eps);
            // 受動化0なら萎縮なし
            Assert.AreEqual(0f, SoftDespotismRules.AutonomyAtrophy(0f), Eps);
            // 凸性＝低い受動化でも自律が削れる（0.25 → 1−0.75^2 = 0.4375 > 0.25）
            Assert.Greater(SoftDespotismRules.AutonomyAtrophy(0.25f), 0.25f);
        }

        /// <summary>快適さと自由の取引＝快適×残存自律。穏やかな隷従の心地よさ。</summary>
        [Test]
        public void ComfortForFreedomTrade_快適さと引き換えに自律を手放す()
        {
            // 0.9 × 0.6 = 0.54
            Assert.AreEqual(0.54f, SoftDespotismRules.ComfortForFreedomTrade(0.9f, 0.6f), Eps);
            // 差し出せる自律が無ければ取引は成立しない
            Assert.AreEqual(0f, SoftDespotismRules.ComfortForFreedomTrade(1f, 0f), Eps);
        }

        /// <summary>幼児化＝後見力×依存×伸び代×幼児化速度×dt。成熟した市民でなくなる。</summary>
        [Test]
        public void Infantilization_政府に依存する子供になる()
        {
            // 幼児化0.1・後見力1・依存0.5・dt2：0.1 + 1×0.5×(1−0.1)×0.1×2 = 0.19
            Assert.AreEqual(0.19f, SoftDespotismRules.Infantilization(0.1f, 1f, 0.5f, 2f), Eps);
            // 依存が無ければ幼児化は進まない
            Assert.AreEqual(0.1f, SoftDespotismRules.Infantilization(0.1f, 1f, 0f, 2f), Eps);
        }

        /// <summary>静かな支配＝受動化そのものが支配力に転じる（暴力なき＝CoupRules の対称）。</summary>
        [Test]
        public void SoftControl_暴力でなく受動化で支配する()
        {
            Assert.AreEqual(0.7f, SoftDespotismRules.SoftControl(0.7f), Eps);
            // クランプ
            Assert.AreEqual(1f, SoftDespotismRules.SoftControl(1.5f), Eps);
            Assert.AreEqual(0f, SoftDespotismRules.SoftControl(-0.2f), Eps);
        }

        /// <summary>参加の衰退＝自律萎縮×衰退速度×dt ぶん参加が減る（AssociationRules と逆）。</summary>
        [Test]
        public void ParticipationDecline_政治参加と結社が衰える()
        {
            // 参加0.8・自律萎縮0.5・dt2：0.8 − 0.5×0.1×2 = 0.7
            Assert.AreEqual(0.7f, SoftDespotismRules.ParticipationDecline(0.8f, 0.5f, 2f), Eps);
            // 萎縮が無ければ参加は減らない
            Assert.AreEqual(0.8f, SoftDespotismRules.ParticipationDecline(0.8f, 0f, 2f), Eps);
        }

        /// <summary>穏やかな専制判定＝受動化と自律萎縮が両方とも閾値超えで成立。</summary>
        [Test]
        public void IsSoftDespotism_両輪が揃って穏やかな専制になる()
        {
            // 両方とも閾値0.5超 → true
            Assert.IsTrue(SoftDespotismRules.IsSoftDespotism(0.7f, 0.6f, 0.5f));
            // 自律萎縮だけ足りない → false（受動的だが自律はまだ残る）
            Assert.IsFalse(SoftDespotismRules.IsSoftDespotism(0.7f, 0.4f, 0.5f));
            // 受動化だけ足りない → false
            Assert.IsFalse(SoftDespotismRules.IsSoftDespotism(0.4f, 0.7f, 0.5f));
        }
    }
}
