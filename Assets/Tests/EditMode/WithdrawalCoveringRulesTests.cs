using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 退却援護（殿〔しんがり〕戦術）の純ロジック <see cref="WithdrawalCoveringRules"/> の EditMode テスト。
    /// 既定 <see cref="WithdrawalCoveringParams.Default"/> で期待値を固定（手計算で検算済み）。
    /// </summary>
    public class WithdrawalCoveringRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void RearguardHoldTime_ScalesWithStrengthRatio()
        {
            // 拮抗（R=P）で 1.0×(100/200)=0.5
            Assert.AreEqual(0.5f, WithdrawalCoveringRules.RearguardHoldTime(100f, 100f), Eps);
            // 殿が厚い（R=300,P=100）で 1.0×(300/400)=0.75
            Assert.AreEqual(0.75f, WithdrawalCoveringRules.RearguardHoldTime(300f, 100f), Eps);
        }

        [Test]
        public void RearguardHoldTime_ZeroRearguardOrEmpty_IsZero()
        {
            // 殿がいない＝食い止められない
            Assert.AreEqual(0f, WithdrawalCoveringRules.RearguardHoldTime(0f, 100f), Eps);
            // 両者ゼロ＝割り算ガードで 0
            Assert.AreEqual(0f, WithdrawalCoveringRules.RearguardHoldTime(0f, 0f), Eps);
        }

        [Test]
        public void MainBodyEscape_FasterAndLongerHoldEscapesMore()
        {
            // 速い本隊（speed=1）：0.5×1.0×(0.5+0.5)=0.5
            Assert.AreEqual(0.5f, WithdrawalCoveringRules.MainBodyEscape(0.5f, 1.0f), Eps);
            // 鈍重な本隊（speed=0）：0.5×1.0×0.5=0.25
            Assert.AreEqual(0.25f, WithdrawalCoveringRules.MainBodyEscape(0.5f, 0.0f), Eps);
        }

        [Test]
        public void RearguardSacrifice_LongerHoldAndHeavierPursuitCostsMore()
        {
            // 粘り切り（hold=1）×重い追撃（pursuer=1）：1.0×0.8×1.0=0.8
            Assert.AreEqual(0.8f, WithdrawalCoveringRules.RearguardSacrifice(1.0f, 1.0f), Eps);
            // 短い足止め×軽い追撃：0.5×0.8×(0.5)=0.2
            Assert.AreEqual(0.2f, WithdrawalCoveringRules.RearguardSacrifice(0.5f, 0.0f), Eps);
        }

        [Test]
        public void OrderlyWithdrawal_DisciplineImprovesOrder()
        {
            // 高練度（disc=1）：0.5×((1-0.5)+0.5×1)=0.5×1.0=0.5
            Assert.AreEqual(0.5f, WithdrawalCoveringRules.OrderlyWithdrawal(0.5f, 1.0f), Eps);
            // 低練度（disc=0）：0.5×0.5=0.25（同じ足止めでも混乱して下がりが鈍る）
            Assert.AreEqual(0.25f, WithdrawalCoveringRules.OrderlyWithdrawal(0.5f, 0.0f), Eps);
        }

        [Test]
        public void LeapfrogCovering_TwoUnitsBeatOne()
        {
            // 互いに援護（0.8,0.8）：avg0.8 + 0.25×(0.8×0.8)=0.8+0.16=0.96
            Assert.AreEqual(0.96f, WithdrawalCoveringRules.LeapfrogCovering(0.8f, 0.8f), Eps);
            // 片方が空（1.0,0.0）：avg0.5 + 0.25×0=0.5（連携が崩れる）
            Assert.AreEqual(0.5f, WithdrawalCoveringRules.LeapfrogCovering(1.0f, 0.0f), Eps);
        }

        [Test]
        public void CoverEffectiveness_TerrainBoostsAndClamps()
        {
            // 要害（terrain=1）：0.6×(1+0.5×1)=0.6×1.5=0.9
            Assert.AreEqual(0.9f, WithdrawalCoveringRules.CoverEffectiveness(0.6f, 1.0f), Eps);
            // 厚い殿＋要害は 0.8×1.5=1.2 → 1.0 にクランプ
            Assert.AreEqual(1.0f, WithdrawalCoveringRules.CoverEffectiveness(0.8f, 1.0f), Eps);
        }

        [Test]
        public void SacrificeWorth_AndCoveredFlags()
        {
            // 救出0.5÷犠牲0.2=2.5 ≥ 1.0 → 見合う
            Assert.AreEqual(2.5f, WithdrawalCoveringRules.SacrificeWorth(0.5f, 0.2f), Eps);
            Assert.IsTrue(WithdrawalCoveringRules.IsSacrificeWorthwhile(0.5f, 0.2f));
            // 救出0.1÷犠牲0.8=0.125 < 1.0 → 見合わない（大きな犠牲でわずかしか逃がせない）
            Assert.AreEqual(0.125f, WithdrawalCoveringRules.SacrificeWorth(0.1f, 0.8f), Eps);
            Assert.IsFalse(WithdrawalCoveringRules.IsSacrificeWorthwhile(0.1f, 0.8f));

            // 離脱閾値（既定0.6）
            Assert.IsTrue(WithdrawalCoveringRules.IsWithdrawalCovered(0.7f));
            Assert.IsFalse(WithdrawalCoveringRules.IsWithdrawalCovered(0.25f));
        }

        [Test]
        public void Story_StrongRearguardLetsMainBodyEscapeButPaysWithBlood()
        {
            // 厚い殿（R=400）が小勢の追撃（P=100）を食い止める＝足止め 1.0×(400/500)=0.8
            float hold = WithdrawalCoveringRules.RearguardHoldTime(400f, 100f);
            Assert.AreEqual(0.8f, hold, Eps);

            // 速い本隊はその間に大半が離脱＝0.8×1.0×1.0=0.8（無事離脱できた）
            float escape = WithdrawalCoveringRules.MainBodyEscape(hold, 1.0f);
            Assert.AreEqual(0.8f, escape, Eps);
            Assert.IsTrue(WithdrawalCoveringRules.IsWithdrawalCovered(escape));

            // だが殿は血を流す＝0.8×0.8×(0.5+0.5×0.25)=0.8×0.8×0.625=0.4
            float sacrifice = WithdrawalCoveringRules.RearguardSacrifice(hold, 0.25f);
            Assert.AreEqual(0.4f, sacrifice, Eps);

            // それでも犠牲は見合う＝0.8÷0.4=2.0 ≥ 1.0（少ない犠牲で本隊を救った）
            float worth = WithdrawalCoveringRules.SacrificeWorth(escape, sacrifice);
            Assert.AreEqual(2.0f, worth, Eps);
            Assert.IsTrue(WithdrawalCoveringRules.IsSacrificeWorthwhile(escape, sacrifice));
        }
    }
}
