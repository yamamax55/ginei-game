using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// プロパガンダ作戦（世論操作キャンペーン）の純ロジック検証。既定 Params 期待値固定。
    /// </summary>
    public class PropagandaCampaignRulesTests
    {
        // MessageReach：反復が逓減で効く（rep=1 で repFactor≈0.8333、×0.8=0.6666）
        [Test]
        public void MessageReach_RepetitionDiminishes()
        {
            float reach = PropagandaCampaignRules.MessageReach(0.8f, 1f);
            Assert.AreEqual(0.6666666f, reach, 1e-3f);
        }

        // 反復ゼロでも媒体浸透ぶんの最低到達（repFactor=0.5・×0.6=0.3）
        [Test]
        public void MessageReach_ZeroRepetition_HasFloor()
        {
            float reach = PropagandaCampaignRules.MessageReach(0.6f, 0f);
            Assert.AreEqual(0.3f, reach, 1e-3f);
        }

        // Credibility：誇張ゼロなら到達×事実性そのまま
        [Test]
        public void Credibility_NoExaggeration_EqualsReachTimesFact()
        {
            float cred = PropagandaCampaignRules.Credibility(0.8f, 1f, 0f);
            Assert.AreEqual(0.8f, cred, 1e-4f);
        }

        // Credibility：誇張が信憑性を割り引く（誇張2.0 → 1/(1+1)=半減）
        [Test]
        public void Credibility_Exaggeration_HalvesAtTwo()
        {
            float cred = PropagandaCampaignRules.Credibility(0.8f, 1f, 2f);
            Assert.AreEqual(0.4f, cred, 1e-4f);
        }

        // Indoctrination：到達×信憑×素地×(1+gain)
        [Test]
        public void Indoctrination_ProductWithGain()
        {
            float indoc = PropagandaCampaignRules.Indoctrination(0.8f, 0.5f, 0.5f);
            Assert.AreEqual(0.36f, indoc, 1e-4f);
        }

        // CounterPropaganda：敵努力/(敵+自信憑)
        [Test]
        public void CounterPropaganda_RatioOfEffortToCredibility()
        {
            float counter = PropagandaCampaignRules.CounterPropaganda(0.6f, 0.2f);
            Assert.AreEqual(0.75f, counter, 1e-4f);
        }

        // 両者ゼロなら打ち消しゼロ（ゼロ除算安全）
        [Test]
        public void CounterPropaganda_BothZero_IsZero()
        {
            float counter = PropagandaCampaignRules.CounterPropaganda(0f, 0f);
            Assert.AreEqual(0f, counter, 1e-4f);
        }

        // OpinionShift：反宣伝が勝てば負へ
        [Test]
        public void OpinionShift_CounterDominates_IsNegative()
        {
            float shift = PropagandaCampaignRules.OpinionShift(0.36f, 0.75f);
            Assert.AreEqual(-0.39f, shift, 1e-4f);
        }

        // SaturationFatigue：過剰な反復×誇張で白ける
        [Test]
        public void SaturationFatigue_OverDoneCampaign()
        {
            float fatigue = PropagandaCampaignRules.SaturationFatigue(4f, 2f);
            Assert.AreEqual(0.4444444f, fatigue, 1e-3f);
        }

        // CampaignEffectiveness：転換−飽和
        [Test]
        public void CampaignEffectiveness_ShiftMinusFatigue()
        {
            float eff = PropagandaCampaignRules.CampaignEffectiveness(0.5f, 0.1f);
            Assert.AreEqual(0.4f, eff, 1e-4f);
        }

        // IsPropagandaEffective：既定閾値 0.1
        [Test]
        public void IsPropagandaEffective_DefaultThreshold()
        {
            Assert.IsTrue(PropagandaCampaignRules.IsPropagandaEffective(0.2f));
            Assert.IsFalse(PropagandaCampaignRules.IsPropagandaEffective(0.05f));
        }

        // 入力 clamp：負・過大入力でも 0..1 / -1..1 に収まる
        [Test]
        public void Inputs_AreClamped()
        {
            float reach = PropagandaCampaignRules.MessageReach(5f, -3f);
            Assert.GreaterOrEqual(reach, 0f);
            Assert.LessOrEqual(reach, 1f);

            float shift = PropagandaCampaignRules.OpinionShift(5f, -5f);
            Assert.LessOrEqual(shift, 1f);
            Assert.GreaterOrEqual(shift, -1f);
        }

        // 物語テスト：事実に基づく節度ある宣伝は効き、誇張を反復で煽りまくると白けて逆効果になる。
        [Test]
        public void Story_HonestCampaignWins_OverHypedCampaignBackfires()
        {
            // 節度ある宣伝：よく届き、事実性が高く誇張は控えめ、受け手の素地が厚く敵の反宣伝は弱い
            float reachHonest = PropagandaCampaignRules.MessageReach(0.95f, 1f);
            float credHonest = PropagandaCampaignRules.Credibility(reachHonest, 0.95f, 0.1f);
            float indocHonest = PropagandaCampaignRules.Indoctrination(reachHonest, credHonest, 0.9f);
            float counterHonest = PropagandaCampaignRules.CounterPropaganda(0.1f, credHonest);
            float shiftHonest = PropagandaCampaignRules.OpinionShift(indocHonest, counterHonest);
            float fatigueHonest = PropagandaCampaignRules.SaturationFatigue(1f, 0.1f);
            float effHonest = PropagandaCampaignRules.CampaignEffectiveness(shiftHonest, fatigueHonest);

            // 過剰宣伝：嘘八百を繰り返し誇張で煽る（事実性低・誇張大・反復過剰）
            float reachHype = PropagandaCampaignRules.MessageReach(0.7f, 10f);
            float credHype = PropagandaCampaignRules.Credibility(reachHype, 0.2f, 5f);
            float indocHype = PropagandaCampaignRules.Indoctrination(reachHype, credHype, 0.6f);
            float counterHype = PropagandaCampaignRules.CounterPropaganda(0.3f, credHype);
            float shiftHype = PropagandaCampaignRules.OpinionShift(indocHype, counterHype);
            float fatigueHype = PropagandaCampaignRules.SaturationFatigue(10f, 5f);
            float effHype = PropagandaCampaignRules.CampaignEffectiveness(shiftHype, fatigueHype);

            // 信憑性は節度ある側が高い
            Assert.Greater(credHonest, credHype);
            // 白けは過剰宣伝側が大きい
            Assert.Greater(fatigueHype, fatigueHonest);
            // 正味効果は節度ある側が勝ち、過剰宣伝は逆効果（負）
            Assert.Greater(effHonest, effHype);
            Assert.Less(effHype, 0f);
            Assert.IsTrue(PropagandaCampaignRules.IsPropagandaEffective(effHonest));
            Assert.IsFalse(PropagandaCampaignRules.IsPropagandaEffective(effHype));
        }
    }
}
