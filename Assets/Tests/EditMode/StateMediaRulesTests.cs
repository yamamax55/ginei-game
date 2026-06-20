using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 国営放送（公式報道機関の運営と効果）の純ロジック EditMode テスト。
    /// 到達範囲・公式信頼度・娯楽懐柔・報道比率・視聴者馴致・動員放送・メッセージ受容・機能判定を
    /// 既定 Params の具体値で固定する。期待値は手計算で検算。
    /// </summary>
    public class StateMediaRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>到達範囲＝インフラ×人口（積）。どちらか欠けると届かない。</summary>
        [Test]
        public void BroadcastReach_NeedsBothInfrastructureAndPopulation()
        {
            // 0.8 * 0.5 * 1 = 0.40
            Assert.AreEqual(0.40f, StateMediaRules.BroadcastReach(0.8f, 0.5f), Eps);
            // 人口0なら到達0。
            Assert.AreEqual(0f, StateMediaRules.BroadcastReach(0.9f, 0f), Eps);
            // インフラ0なら到達0。
            Assert.AreEqual(0f, StateMediaRules.BroadcastReach(0f, 0.9f), Eps);
        }

        /// <summary>公式信頼度＝実績0.6＋透明性0.4の重み付き合成。嘘の前科も不透明さも信頼を削る。</summary>
        [Test]
        public void OfficialCredibility_BlendsTrackRecordAndTransparency()
        {
            // 0.9*0.6 + 0.4*0.4 = 0.54 + 0.16 = 0.70
            Assert.AreEqual(0.70f, StateMediaRules.OfficialCredibility(0.9f, 0.4f), Eps);
            // 両方満点で1、両方0で0。
            Assert.AreEqual(1f, StateMediaRules.OfficialCredibility(1f, 1f), Eps);
            Assert.AreEqual(0f, StateMediaRules.OfficialCredibility(0f, 0f), Eps);
        }

        /// <summary>娯楽懐柔＝娯楽の質×到達（パンとサーカス）。良質な娯楽が広く届くほど不満をそらす。</summary>
        [Test]
        public void EntertainmentPacification_QualityTimesReach()
        {
            // 0.8 * 0.5 * 1 = 0.40
            Assert.AreEqual(0.40f, StateMediaRules.EntertainmentPacification(0.8f, 0.5f), Eps);
            // 質が高くても届かなければ懐柔できない。
            Assert.AreEqual(0f, StateMediaRules.EntertainmentPacification(1f, 0f), Eps);
        }

        /// <summary>報道比率＝報道偏重で+（プロパガンダ過多）、娯楽偏重で-（娯楽過多）、中庸で0。</summary>
        [Test]
        public void NewsToEntertainmentRatio_SignedBalance()
        {
            // 2*0.75-1 = 0.5（報道過多）
            Assert.AreEqual(0.5f, StateMediaRules.NewsToEntertainmentRatio(0.75f), Eps);
            // 2*0.25-1 = -0.5（娯楽過多）
            Assert.AreEqual(-0.5f, StateMediaRules.NewsToEntertainmentRatio(0.25f), Eps);
            // 0.5 で中庸0。
            Assert.AreEqual(0f, StateMediaRules.NewsToEntertainmentRatio(0.5f), Eps);
        }

        /// <summary>視聴者馴致＝到達×娯楽懐柔。広く届く娯楽漬けほど依存が深まる。</summary>
        [Test]
        public void ViewerHabituation_ReachTimesPacification()
        {
            // 0.6 * 0.5 * 1 = 0.30
            Assert.AreEqual(0.30f, StateMediaRules.ViewerHabituation(0.6f, 0.5f), Eps);
        }

        /// <summary>動員放送＝到達×信頼×緊急度。広く・信じられ・切迫しているほど国民を動かす。</summary>
        [Test]
        public void MobilizationBroadcast_ReachCredibilityUrgency()
        {
            // 0.8 * 0.7 * 0.5 * 1 = 0.28
            Assert.AreEqual(0.28f, StateMediaRules.MobilizationBroadcast(0.8f, 0.7f, 0.5f), Eps);
            // 緊急度0なら動員しない。
            Assert.AreEqual(0f, StateMediaRules.MobilizationBroadcast(0.8f, 0.7f, 0f), Eps);
        }

        /// <summary>メッセージ受容＝信頼×馴致を露骨さで割り引く。露骨なプロパガンダは受容を下げる。</summary>
        [Test]
        public void MessageAcceptance_CrudePropagandaDiscountsAcceptance()
        {
            // 露骨さ0：0.8*0.5/1 = 0.40
            Assert.AreEqual(0.40f, StateMediaRules.MessageAcceptance(0.8f, 0.5f, 0f), Eps);
            // 露骨さ1.0：0.8*0.5/(1+1) = 0.20（半減）
            Assert.AreEqual(0.20f, StateMediaRules.MessageAcceptance(0.8f, 0.5f, 1f), Eps);
            // 露骨なほど受容は下がる。
            Assert.Greater(StateMediaRules.MessageAcceptance(0.8f, 0.5f, 0f),
                           StateMediaRules.MessageAcceptance(0.8f, 0.5f, 1f));
        }

        /// <summary>機能判定＝受容が既定閾値0.5を超えるか。</summary>
        [Test]
        public void IsStateMediaEffective_ThresholdGate()
        {
            Assert.IsTrue(StateMediaRules.IsStateMediaEffective(0.6f));
            Assert.IsFalse(StateMediaRules.IsStateMediaEffective(0.5f));
            // 明示閾値版。
            Assert.IsTrue(StateMediaRules.IsStateMediaEffective(0.31f, 0.3f));
            Assert.IsFalse(StateMediaRules.IsStateMediaEffective(0.3f, 0.3f));
        }

        /// <summary>
        /// 物語テスト：パンとサーカス国家。良質な娯楽を全国へ流して国民を懐柔・馴致し、信頼ある公式放送が
        /// メッセージを受容させ国営メディアが機能する。緊急時には動員放送が国民を動かす。
        /// </summary>
        [Test]
        public void Narrative_PanemEtCircensesStateBroadcasting()
        {
            // 全国津々浦々に届く放送網。
            float reach = StateMediaRules.BroadcastReach(0.9f, 0.9f); // 0.81
            Assert.Greater(reach, 0.7f);

            // 良質な娯楽が広く届き国民を懐柔。
            float pacify = StateMediaRules.EntertainmentPacification(0.9f, reach); // 0.729
            Assert.Greater(pacify, 0.6f);

            // 娯楽漬けで視聴が習慣化・依存。
            float habit = StateMediaRules.ViewerHabituation(reach, pacify); // 0.81*0.729 ≈ 0.5905
            Assert.Greater(habit, 0.5f);

            // 実績と透明性に裏打ちされた公式放送は信頼が高い。
            float cred = StateMediaRules.OfficialCredibility(0.9f, 0.8f); // 0.86
            Assert.Greater(cred, 0.8f);

            // 露骨なプロパガンダを控えればメッセージは広く受容される。
            float accept = StateMediaRules.MessageAcceptance(cred, habit, 0.1f); // 0.86*0.5905/1.1 ≈ 0.4616
            Assert.IsTrue(StateMediaRules.IsStateMediaEffective(accept, 0.4f));

            // 緊急時の動員放送は信頼と到達があれば国民を強く動かす。
            float mobilize = StateMediaRules.MobilizationBroadcast(reach, cred, 1f); // 0.81*0.86 ≈ 0.6966
            Assert.Greater(mobilize, 0.6f);
        }
    }
}
