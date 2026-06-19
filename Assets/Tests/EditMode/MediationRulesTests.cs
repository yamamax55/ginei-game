using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 第三者調停を固定する：中立性×威信で信頼度、信頼×窮状で受容、技量÷(1+隔たり)で歩み寄り案、案×疲弊で収束、
    /// 利害×長期化で漁夫の利、力×履行保証で保証人、受容×収束−私利で成否(-1..1)、閾値で機能判定。境界・クランプ・物語を担保。
    /// </summary>
    public class MediationRulesTests
    {
        private static readonly MediationParams P = MediationParams.Default; // 尺度1/受容下限0.3/隔たり重み1/疲弊下限0.3/私利重み1/私利罰1/閾値0.25

        [Test]
        public void MediatorCredibility_NeutralityTimesPrestige()
        {
            Assert.AreEqual(0.4f, MediationRules.MediatorCredibility(0.8f, 0.5f, P), 1e-4f);
            // 中立が欠ければ信頼は崩れる
            Assert.AreEqual(0f, MediationRules.MediatorCredibility(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void Acceptability_FloorAndDesperationScale()
        {
            // 窮状0でも下限0.3ぶんは受け入れる素地：0.8×0.3=0.24
            Assert.AreEqual(0.24f, MediationRules.Acceptability(0.8f, 0f, P), 1e-4f);
            // 窮状満タン：0.8×1=0.8
            Assert.AreEqual(0.8f, MediationRules.Acceptability(0.8f, 1f, P), 1e-4f);
            // 信頼満タン・窮状半分：1×(0.3+0.7×0.5)=0.65
            Assert.AreEqual(0.65f, MediationRules.Acceptability(1f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void BridgingProposal_GapDividesSkill()
        {
            // 隔たり最大：0.8/(1+1)=0.4
            Assert.AreEqual(0.4f, MediationRules.BridgingProposal(0.8f, 1f, P), 1e-4f);
            // 隔たり無し：そのまま
            Assert.AreEqual(1f, MediationRules.BridgingProposal(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void PartyConvergence_ExhaustionFloorAndScale()
        {
            // 疲弊0でも下限0.3：0.8×0.3=0.24
            Assert.AreEqual(0.24f, MediationRules.PartyConvergence(0.8f, 0f, P), 1e-4f);
            // 疲弊満タン：0.5×1=0.5
            Assert.AreEqual(0.5f, MediationRules.PartyConvergence(0.5f, 1f, P), 1e-4f);
        }

        [Test]
        public void MediatorSelfInterest_StakeTimesProlongation()
        {
            // 0.6×0.5×1=0.3
            Assert.AreEqual(0.3f, MediationRules.MediatorSelfInterest(0.6f, 0.5f, P), 1e-4f);
            // 利害無しなら誘惑は無い
            Assert.AreEqual(0f, MediationRules.MediatorSelfInterest(0f, 1f, P), 1e-4f);
        }

        [Test]
        public void GuarantorRole_PowerTimesCommitment()
        {
            Assert.AreEqual(0.4f, MediationRules.GuarantorRole(0.8f, 0.5f, P), 1e-4f);
            // 履行保証が無ければ保証人になれない
            Assert.AreEqual(0f, MediationRules.GuarantorRole(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void MediationSuccess_ConvergenceMinusSelfInterest_ClampedSigned()
        {
            // 受容×収束−私利：0.8×0.5−0.3=0.1
            Assert.AreEqual(0.1f, MediationRules.MediationSuccess(0.8f, 0.5f, 0.3f, P), 1e-4f);
            // 私利が勝てば負へ：0.2×0.2−1.0=−0.96
            Assert.AreEqual(-0.96f, MediationRules.MediationSuccess(0.2f, 0.2f, 1f, P), 1e-4f);
            // 下限−1へクランプ（受容0・私利満タン）
            Assert.AreEqual(-1f, MediationRules.MediationSuccess(0f, 0f, 1f, P), 1e-4f);
        }

        [Test]
        public void IsMediationEffective_ThresholdGate()
        {
            Assert.IsTrue(MediationRules.IsMediationEffective(0.54f));   // 既定閾値0.25以上
            Assert.IsFalse(MediationRules.IsMediationEffective(0.1f));   // 閾値未満
            Assert.IsTrue(MediationRules.IsMediationEffective(0.25f));   // 閾値ちょうどは機能
        }

        [Test]
        public void DefaultDelegation_MatchesParamVersion()
        {
            Assert.AreEqual(MediationRules.MediatorCredibility(0.7f, 0.6f, P),
                            MediationRules.MediatorCredibility(0.7f, 0.6f), 1e-4f);
            Assert.AreEqual(MediationRules.Acceptability(0.5f, 0.5f, P),
                            MediationRules.Acceptability(0.5f, 0.5f), 1e-4f);
            Assert.AreEqual(MediationRules.MediationSuccess(0.6f, 0.6f, 0.2f, P),
                            MediationRules.MediationSuccess(0.6f, 0.6f, 0.2f), 1e-4f);
        }

        [Test]
        public void Story_FezzanBrokersPeaceUntilGreedTilts()
        {
            // 中立で名高い商業国家が仲裁：信頼0.9×0.8=0.72
            float cred = MediationRules.MediatorCredibility(0.9f, 0.8f, P);
            Assert.AreEqual(0.72f, cred, 1e-4f);

            // 長き戦に疲れ窮した両者は調停を受け入れる
            float acc = MediationRules.Acceptability(cred, 0.9f, P);   // 0.72×(0.3+0.7×0.9)=0.72×0.93=0.6696
            Assert.AreEqual(0.6696f, acc, 1e-4f);

            // 巧みな調停者が立場の隔たりを埋め、疲弊した両軍は歩み寄る
            float bridge = MediationRules.BridgingProposal(0.9f, 0.4f, P);   // 0.9/1.4=0.642857
            float conv = MediationRules.PartyConvergence(bridge, 0.9f, P);   // 0.642857×(0.3+0.7×0.9)=0.642857×0.93=0.597857
            Assert.AreEqual(0.597857f, conv, 1e-3f);

            // 私利が薄ければ調停は成功＝平和が成る
            float honest = MediationRules.MediationSuccess(acc, conv, 0.1f, P); // 0.6696×0.597857−0.1=0.400324−0.1=0.300324
            Assert.AreEqual(0.300324f, honest, 1e-3f);
            Assert.IsTrue(MediationRules.IsMediationEffective(honest));

            // だが調停者が紛争長期化で漁夫の利を貪れば、公平が曲がり成立は崩れる
            float greed = MediationRules.MediatorSelfInterest(0.9f, 0.8f, P); // 0.72
            float corrupt = MediationRules.MediationSuccess(acc, conv, greed, P); // 0.400324−0.72=−0.319676
            Assert.Less(corrupt, honest);
            Assert.IsFalse(MediationRules.IsMediationEffective(corrupt));

            // 力ある保証人として履行を担保できる
            Assert.AreEqual(0.72f, MediationRules.GuarantorRole(0.8f, 0.9f, P), 1e-4f);
        }
    }
}
