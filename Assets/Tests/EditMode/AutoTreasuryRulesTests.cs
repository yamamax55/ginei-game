using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>AutoTreasuryRules（自律財務運用 #1014）の純ロジックを既定Paramsで担保する。</summary>
    public class AutoTreasuryRulesTests
    {
        private const float Tol = 1e-4f;

        /// <summary>準備金割れ＋債務余地あり＝自動で起債を選ぶ（最優先の資金繰り対処）。</summary>
        [Test]
        public void DecideAction_準備金割れで余地ありなら起債()
        {
            // 準備金40<下限50、債務比率0.3<上限1.0＝余地あり。
            var act = AutoTreasuryRules.DecideAction(40f, 50f, 0.3f, 1.0f, 0.02f, 1f);
            Assert.AreEqual(TreasuryAction.起債, act);
        }

        /// <summary>準備金割れだが債務余地が尽きている＝起債不能で準備金取崩に落ちる。</summary>
        [Test]
        public void DecideAction_準備金割れで余地なしなら取崩()
        {
            // 準備金40<下限50、債務比率1.0=上限1.0＝余地ゼロ。
            var act = AutoTreasuryRules.DecideAction(40f, 50f, 1.0f, 1.0f, 0.02f, 1f);
            Assert.AreEqual(TreasuryAction.準備金取崩, act);
        }

        /// <summary>準備金は足り、債務上限に接近（余地15%以下）＝起債に頼れず緊縮支出を選ぶ。</summary>
        [Test]
        public void DecideAction_債務上限接近なら緊縮()
        {
            // 準備金100>=下限50。債務比率0.9/上限1.0＝余地0.1（10%）<=既定austerityHeadroom0.15。
            var act = AutoTreasuryRules.DecideAction(100f, 50f, 0.9f, 1.0f, 0.02f, 1f);
            Assert.AreEqual(TreasuryAction.緊縮支出, act);
        }

        /// <summary>準備金十分・余地も十分＝平時はタッチレスで静観（何もしない）。dt<=0も無作動。</summary>
        [Test]
        public void DecideAction_平時は静観_dt無効は無作動()
        {
            Assert.AreEqual(TreasuryAction.何もしない,
                AutoTreasuryRules.DecideAction(100f, 50f, 0.3f, 1.0f, 0.02f, 1f));
            Assert.AreEqual(TreasuryAction.何もしない,
                AutoTreasuryRules.DecideAction(40f, 50f, 0.3f, 1.0f, 0.02f, 0f));
        }

        /// <summary>自動起債額＝不足を埋めるが債務余地の範囲を超えない。</summary>
        [Test]
        public void BondIssuanceAmount_余地で頭打ち()
        {
            Assert.AreEqual(30f, AutoTreasuryRules.BondIssuanceAmount(30f, 50f), Tol); // 不足<余地＝不足ぶん
            Assert.AreEqual(20f, AutoTreasuryRules.BondIssuanceAmount(80f, 20f), Tol); // 余地<不足＝余地ぶん
            Assert.AreEqual(0f, AutoTreasuryRules.BondIssuanceAmount(-5f, 50f), Tol);  // 負はクランプ
        }

        /// <summary>借換の利得＝金利低下幅×債務。金利が下がっていなければ0。</summary>
        [Test]
        public void RefinanceBenefit_低下幅と判定()
        {
            // (0.05-0.03)*1000 = 20。
            Assert.AreEqual(20f, AutoTreasuryRules.RefinanceBenefit(0.05f, 0.03f, 1000f), Tol);
            Assert.AreEqual(0f, AutoTreasuryRules.RefinanceBenefit(0.03f, 0.05f, 1000f), Tol); // 上昇＝利得なし
            // 既定thresholdは0.5%＝0.02の低下は借換に値する。
            Assert.IsTrue(AutoTreasuryRules.ShouldRefinance(0.05f, 0.03f, AutoTreasuryParams.Default));
            Assert.IsFalse(AutoTreasuryRules.ShouldRefinance(0.05f, 0.048f, AutoTreasuryParams.Default));
        }

        /// <summary>緊縮の深さ＝上限に近いほど深い。余地15%で0・余地ゼロで最大0.5。</summary>
        [Test]
        public void AusterityDepth_上限接近で深まる()
        {
            // 余地ratio=0.15（=austerityHeadroom）＝まだ緊縮不要＝0。
            Assert.AreEqual(0f, AutoTreasuryRules.AusterityDepth(0.85f, 1.0f), Tol);
            // 余地ゼロ（比率=上限）＝最大深さ0.5。
            Assert.AreEqual(0.5f, AutoTreasuryRules.AusterityDepth(1.0f, 1.0f), Tol);
            // 中間：余地ratio=0.075（headroomの半分）＝深さ0.25。
            Assert.AreEqual(0.25f, AutoTreasuryRules.AusterityDepth(0.925f, 1.0f), Tol);
        }

        /// <summary>支払能力の経路＝準備金＋債務余地で見込み赤字を吸収できるか（詰みの早期判定）。</summary>
        [Test]
        public void IsSolventPath_吸収可否で詰みを判定()
        {
            Assert.IsTrue(AutoTreasuryRules.IsSolventPath(100f, 0f, 0f));     // 黒字（赤字0）は常に可
            Assert.IsTrue(AutoTreasuryRules.IsSolventPath(60f, 100f, 50f));   // 60+50>=100＝吸収可
            Assert.IsFalse(AutoTreasuryRules.IsSolventPath(30f, 100f, 50f));  // 30+50<100＝詰み
        }

        /// <summary>自動運用の信頼度＝平時は満点・激動で下限へ低下（完全自動の限界）。</summary>
        [Test]
        public void TouchlessConfidence_平時満点_激動で下限()
        {
            Assert.AreEqual(1f, AutoTreasuryRules.TouchlessConfidence(0.2f), Tol);  // freeZone内＝満点
            Assert.AreEqual(0.2f, AutoTreasuryRules.TouchlessConfidence(1f), Tol);  // 最大変動＝下限0.2
            // 中間：変動0.6＝(0.6-0.2)/(1-0.2)=0.5 → Lerp(1,0.2,0.5)=0.6。
            Assert.AreEqual(0.6f, AutoTreasuryRules.TouchlessConfidence(0.6f), Tol);
        }
    }
}
