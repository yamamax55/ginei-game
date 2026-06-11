using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>恐怖と憎悪の純ロジック（#1140・マキャヴェッリ）の EditMode テスト。</summary>
    public class FearVsHatredRulesTests
    {
        const float Tol = 1e-4f;

        /// <summary>恐れの水準＝規律ある強制力×予測可能性×係数（理由の分かる罰は恐れを生む）。</summary>
        [Test]
        public void FearLevel_DisciplinedAndPredictable()
        {
            // 0.8 * 0.5 * 1.0 = 0.4
            Assert.AreEqual(0.4f, FearVsHatredRules.FearLevel(0.8f, 0.5f), Tol);
            // 予測不能（=0）なら恐れも0
            Assert.AreEqual(0f, FearVsHatredRules.FearLevel(1f, 0f), Tol);
        }

        /// <summary>憎悪の水準＝恣意性×略奪×係数（財産と名誉の略奪が憎悪を生む）。</summary>
        [Test]
        public void HatredLevel_ArbitraryPlunder()
        {
            // 0.7 * 0.6 * 1.0 = 0.42
            Assert.AreEqual(0.42f, FearVsHatredRules.HatredLevel(0.7f, 0.6f), Tol);
            // 略奪しなければ憎悪は0
            Assert.AreEqual(0f, FearVsHatredRules.HatredLevel(1f, 0f), Tol);
        }

        /// <summary>恐れは統治の安定に寄与する（恐れられる君主は侮られない）。</summary>
        [Test]
        public void ControlFromFear_StabilizesRule()
        {
            // 0.5 * 0.6 = 0.3
            Assert.AreEqual(0.3f, FearVsHatredRules.ControlFromFear(0.5f), Tol);
        }

        /// <summary>憎悪は転覆・暗殺リスクを非線形（二乗）に生む＝憎まれる君主は狙われる。</summary>
        [Test]
        public void SubversionFromHatred_NonlinearRisk()
        {
            // 0.6^2 * 1.0 = 0.36
            Assert.AreEqual(0.36f, FearVsHatredRules.SubversionFromHatred(0.6f), Tol);
            // 低い憎悪の転覆リスクは小さい 0.2^2 = 0.04
            Assert.AreEqual(0.04f, FearVsHatredRules.SubversionFromHatred(0.2f), Tol);
        }

        /// <summary>マキャヴェッリの理想＝恐れられるが憎まれない（規律はあるが略奪しない）が最大。</summary>
        [Test]
        public void FearWithoutHatred_IdealZone()
        {
            // 規律1・略奪なし → 恐れ1.0 − 憎悪0 = 1.0
            Assert.AreEqual(1f, FearVsHatredRules.FearWithoutHatred(1f, 0f, 0f), Tol);
            // 規律0.8・恣意0.5・略奪0.8 → 0.8 − (0.5*0.8)=0.8-0.4 = 0.4
            Assert.AreEqual(0.4f, FearVsHatredRules.FearWithoutHatred(0.8f, 0.5f, 0.8f), Tol);
            // 略奪過多で憎悪が恐れを上回ればクランプで0
            Assert.AreEqual(0f, FearVsHatredRules.FearWithoutHatred(0.3f, 1f, 1f), Tol);
        }

        /// <summary>恐怖が残虐さで憎悪へ転じる非線形の境界（やりすぎると恐怖が憎悪になる）。</summary>
        [Test]
        public void CrossingIntoHatred_BrutalityTipsFearToHatred()
        {
            // 残虐さが閾値0.5以下なら転化なし
            Assert.AreEqual(0f, FearVsHatredRules.CrossingIntoHatred(0.8f, 0.5f), Tol);
            // 残虐さ0.75 → 超過0.25/span0.5=0.5、恐れ0.8*0.5 = 0.4
            Assert.AreEqual(0.4f, FearVsHatredRules.CrossingIntoHatred(0.8f, 0.75f), Tol);
            // 残虐さ最大 → 恐れがそのまま憎悪へ 0.8*1.0 = 0.8
            Assert.AreEqual(0.8f, FearVsHatredRules.CrossingIntoHatred(0.8f, 1f), Tol);
        }

        /// <summary>純安全＝恐れの安定−憎悪の転覆リスク（恐れられても憎まれるなの収支）。</summary>
        [Test]
        public void NetSecurity_FearMinusHatred()
        {
            // 0.5 − 0.2 = 0.3（恐れが上回り安全）
            Assert.AreEqual(0.3f, FearVsHatredRules.NetSecurity(0.5f, 0.2f), Tol);
            // 憎悪が恐れを上回れば負（危うい）
            Assert.AreEqual(-0.5f, FearVsHatredRules.NetSecurity(0.2f, 0.7f), Tol);
        }

        /// <summary>憎まれて危険な状態の判定（憎悪が一線を越えたか）。</summary>
        [Test]
        public void IsHated_OverThreshold()
        {
            Assert.IsTrue(FearVsHatredRules.IsHated(0.6f));   // 既定閾値0.5超
            Assert.IsFalse(FearVsHatredRules.IsHated(0.4f));
            Assert.IsFalse(FearVsHatredRules.IsHated(0.5f));  // 等しいは未越え
        }
    }
}
