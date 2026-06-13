using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 同盟の負担分担（ただ乗り問題）を固定する：ただ乗り誘因は盟主の供給×（1−自国シェア）で増え、
    /// あるべき貢献は力×脅威の応分、実貢献は誘因ぶん目減り、盟主の肩代わりは不足分、
    /// 強制の軋みは負担増×主権感応度、結束は公平×（1−軋み）。
    /// 既定 Params（ただ乗り0.7/脅威0.5/強制0.8）で期待値固定。
    /// </summary>
    public class BurdenSharingRulesTests
    {
        private static readonly BurdenSharingParams P = BurdenSharingParams.Default;
        // ただ乗り重み0.7・脅威重み0.5・強制重み0.8

        [Test]
        public void FreeRiderIncentive_HegemonProvisionInvitesFreeRiding()
        {
            // 盟主が全供給し自国未払い＝ただ乗り誘因が最大化（0.7×1×1）
            Assert.AreEqual(0.7f, BurdenSharingRules.FreeRiderIncentive(0f, 1f, P), 1e-4f);
            // 既に応分を払う国（share=1）はただ乗りの余地なし＝0
            Assert.AreEqual(0f, BurdenSharingRules.FreeRiderIncentive(1f, 1f, P), 1e-4f);
            // 盟主が供給しなければ（公共財なし）誘因も消える
            Assert.AreEqual(0f, BurdenSharingRules.FreeRiderIncentive(0f, 0f, P), 1e-4f);
        }

        [Test]
        public void FreeRiderIncentive_ScalesWithProvisionAndShare()
        {
            // 0.7×0.8×(1−0.5)=0.28
            Assert.AreEqual(0.28f, BurdenSharingRules.FreeRiderIncentive(0.5f, 0.8f, P), 1e-4f);
            // 入力クランプ（負シェア・過大供給）
            Assert.AreEqual(0.7f, BurdenSharingRules.FreeRiderIncentive(-1f, 2f, P), 1e-4f);
        }

        [Test]
        public void OptimalContribution_ScalesWithPowerAndThreat()
        {
            // 力1×脅威1＝満額の応分負担
            Assert.AreEqual(1f, BurdenSharingRules.OptimalContribution(1f, 1f, P), 1e-4f);
            // 力1×脅威0＝平時の最低水準（1−0.5）＝0.5
            Assert.AreEqual(0.5f, BurdenSharingRules.OptimalContribution(1f, 0f, P), 1e-4f);
            // 力0.6×脅威0.5：0.6×(1−0.5+0.5×0.5)=0.6×0.75=0.45
            Assert.AreEqual(0.45f, BurdenSharingRules.OptimalContribution(0.6f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void ActualContribution_FreeRidingErodesContribution()
        {
            // あるべき0.8をただ乗り誘因0.7が削る＝0.8×0.3=0.24
            Assert.AreEqual(0.24f, BurdenSharingRules.ActualContribution(0.8f, 0.7f), 1e-4f);
            // 誘因0なら規範どおり払う
            Assert.AreEqual(0.8f, BurdenSharingRules.ActualContribution(0.8f, 0f), 1e-4f);
            // 誘因1なら払わない＝公共財はただ乗りされる
            Assert.AreEqual(0f, BurdenSharingRules.ActualContribution(0.8f, 1f), 1e-4f);
        }

        [Test]
        public void HegemonOverpayment_BigPowerCoversTheShortfall()
        {
            float[] contributions = { 0.2f, 0.1f, 0.1f };
            // 総所要1.0に対し同盟国計0.4＝盟主が0.6を肩代わり
            Assert.AreEqual(0.6f, BurdenSharingRules.HegemonOverpayment(contributions, 1f), 1e-4f);
            // 充足していれば盟主の追加負担なし
            Assert.AreEqual(0f, BurdenSharingRules.HegemonOverpayment(new[] { 0.6f, 0.5f }, 1f), 1e-4f);
            // null・負値はクランプ＝所要そのものが肩代わり
            Assert.AreEqual(1f, BurdenSharingRules.HegemonOverpayment(null, 1f), 1e-4f);
        }

        [Test]
        public void CoercionStrain_ForcingFairShareHurtsTheAlliance()
        {
            // 負担増0.5を主権感応度0.5の同盟国へ強制＝0.8×0.5×0.5=0.2
            Assert.AreEqual(0.2f, BurdenSharingRules.CoercionStrain(0.5f, 0.5f, P), 1e-4f);
            // 主権意識の高い国へ全力で迫れば軋み最大＝0.8
            Assert.AreEqual(0.8f, BurdenSharingRules.CoercionStrain(1f, 1f, P), 1e-4f);
            // 強制しなければ軋みなし
            Assert.AreEqual(0f, BurdenSharingRules.CoercionStrain(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void AllianceCohesion_FairnessStrongCoercionBrittle()
        {
            // 公平×無強制＝最も強い結束
            Assert.AreEqual(1f, BurdenSharingRules.AllianceCohesion(1f, 0f), 1e-4f);
            // 公平でも強制0.8の軋みで脆くなる＝1×(1−0.8)=0.2
            Assert.AreEqual(0.2f, BurdenSharingRules.AllianceCohesion(1f, 0.8f), 1e-4f);
            // 不公平な負担は強制なしでも結束を削る＝0.4×1=0.4
            Assert.AreEqual(0.4f, BurdenSharingRules.AllianceCohesion(0.4f, 0f), 1e-4f);
        }
    }
}
