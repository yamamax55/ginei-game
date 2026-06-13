using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>焦土作戦の進行状態（#1410）の純ロジック検証。既定 Params の具体値で期待値を固定する。</summary>
    public class ScorchedEarthStateRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>焦土は退却が速く破壊に注力するほど時間で広がる（退却0.5・破壊1.0・dt1で0.1進む）。</summary>
        [Test]
        public void ScorchTick_退却と破壊で焦土が広がる()
        {
            // 0.2(scorchRate) * 0.5 * 1.0 * 1 = 0.1
            float next = ScorchedEarthStateRules.ScorchTick(0f, 0.5f, 1f, 1f);
            Assert.AreEqual(0.1f, next, Eps);
            // 退却0・破壊全力でも広がらない
            Assert.AreEqual(0.3f, ScorchedEarthStateRules.ScorchTick(0.3f, 0f, 1f, 1f), Eps);
            // 上限1でクランプ
            Assert.AreEqual(1f, ScorchedEarthStateRules.ScorchTick(0.95f, 1f, 1f, 1f), Eps);
        }

        /// <summary>焼け野原では敵が食えない＝焦土化された範囲ぶん現地調達が無効化される（係数1.0で範囲＝無効化）。</summary>
        [Test]
        public void ForagingDenial_焦土が現地調達を無効化する()
        {
            Assert.AreEqual(0f, ScorchedEarthStateRules.ForagingDenial(0f), Eps);
            Assert.AreEqual(0.6f, ScorchedEarthStateRules.ForagingDenial(0.6f), Eps);
            Assert.AreEqual(1f, ScorchedEarthStateRules.ForagingDenial(1f), Eps);
        }

        /// <summary>デポは破壊努力で空にできるが敵の進撃が速いと間に合わない（進撃0で満額・進撃1で大幅減）。</summary>
        [Test]
        public void DepotDenialTick_敵の進撃が速いと破壊が間に合わない()
        {
            // 進撃0：0.3(rate) * 1.0(effort) * (1-0) * 1 = 0.3
            Assert.AreEqual(0.3f, ScorchedEarthStateRules.DepotDenialTick(0f, 1f, 0f, 1f), Eps);
            // 進撃1：race = 1 - 1*0.8 = 0.2 → 0.3 * 1.0 * 0.2 * 1 = 0.06
            Assert.AreEqual(0.06f, ScorchedEarthStateRules.DepotDenialTick(0f, 1f, 1f, 1f), Eps);
            // dt0は不変
            Assert.AreEqual(0.4f, ScorchedEarthStateRules.DepotDenialTick(0.4f, 1f, 0f, 0f), Eps);
        }

        /// <summary>現地調達もデポも断たれた敵は干上がる＝どちらか断てば窮し両方で締め上げが最大（補給依存で効く）。</summary>
        [Test]
        public void EnemySupplyStrangulation_両方断つと締め上げ最大()
        {
            // 片方のみ：1-(1-0.5)*(1-0) = 0.5、依存1.0 → 0.5
            Assert.AreEqual(0.5f, ScorchedEarthStateRules.EnemySupplyStrangulation(0.5f, 0f, 1f), Eps);
            // 両方0.5：1-(1-0.5)*(1-0.5) = 0.75、依存1.0 → 0.75
            Assert.AreEqual(0.75f, ScorchedEarthStateRules.EnemySupplyStrangulation(0.5f, 0.5f, 1f), Eps);
            // 補給依存が低いと締め上げが効かない：0.75 * 0.4 = 0.3
            Assert.AreEqual(0.3f, ScorchedEarthStateRules.EnemySupplyStrangulation(0.5f, 0.5f, 0.4f), Eps);
        }

        /// <summary>自領を焼く代償は焦土範囲×居住度（係数0.6）＝自国民の窮乏＝RefugeeRules の入力。</summary>
        [Test]
        public void OwnTerritoryCost_自領を焼く代償()
        {
            // 0.5 * 1.0 * 0.6 = 0.3
            Assert.AreEqual(0.3f, ScorchedEarthStateRules.OwnTerritoryCost(0.5f, 1f), Eps);
            // 無人地帯を焼いても代償なし
            Assert.AreEqual(0f, ScorchedEarthStateRules.OwnTerritoryCost(1f, 0f), Eps);
        }

        /// <summary>橋・道路の破壊が敵の進撃を遅らせる＝焦土が時間を稼ぐ（破壊1で1-0.5=0.5倍）。</summary>
        [Test]
        public void AdvanceSlowdown_インフラ破壊が進撃を遅らせる()
        {
            // 破壊0：無傷の道を快進撃
            Assert.AreEqual(1f, ScorchedEarthStateRules.AdvanceSlowdown(0f), Eps);
            // 破壊1：1 - 1*0.5 = 0.5倍
            Assert.AreEqual(0.5f, ScorchedEarthStateRules.AdvanceSlowdown(1f), Eps);
            // 破壊0.4：1 - 0.4*0.5 = 0.8倍
            Assert.AreEqual(0.8f, ScorchedEarthStateRules.AdvanceSlowdown(0.4f), Eps);
        }

        /// <summary>戦後に焦土を復興する＝平時に応じて焦土範囲が取り戻されるが荒廃は時間をかけて残る。</summary>
        [Test]
        public void Reconstruction_戦後に焦土を復興する()
        {
            // 0.5 - 1.0(peace)*0.1(rate)*1 = 0.4
            Assert.AreEqual(0.4f, ScorchedEarthStateRules.Reconstruction(0.5f, 1f, 1f), Eps);
            // 戦時継続（平時0）なら復興は進まない
            Assert.AreEqual(0.5f, ScorchedEarthStateRules.Reconstruction(0.5f, 0f, 1f), Eps);
            // 下限0でクランプ
            Assert.AreEqual(0f, ScorchedEarthStateRules.Reconstruction(0.05f, 1f, 1f), Eps);
        }

        /// <summary>締め上げが閾値以上で敵が干上がった判定＝荒野で敵が補給に窮する。</summary>
        [Test]
        public void IsEnemyStarving_閾値で干上がり判定()
        {
            Assert.IsTrue(ScorchedEarthStateRules.IsEnemyStarving(0.8f, 0.7f));
            Assert.IsFalse(ScorchedEarthStateRules.IsEnemyStarving(0.6f, 0.7f));
            // 境界＝閾値ちょうどで干上がり
            Assert.IsTrue(ScorchedEarthStateRules.IsEnemyStarving(0.7f, 0.7f));
        }

        /// <summary>state struct と既定 Params の具体値を固定する。</summary>
        [Test]
        public void State_と既定Params()
        {
            var s = new ScorchedEarthState(1.5f, -0.2f, 0.5f); // クランプ確認
            Assert.AreEqual(1f, s.scorchedFraction, Eps);
            Assert.AreEqual(0f, s.depotDenial, Eps);
            Assert.AreEqual(0.5f, s.infrastructureDestroyed, Eps);

            var p = ScorchedEarthStateParams.Default;
            Assert.AreEqual(0.2f, p.scorchRate, Eps);
            Assert.AreEqual(1f, p.foragingDenialScale, Eps);
            Assert.AreEqual(0.3f, p.depotDenialRate, Eps);
            Assert.AreEqual(0.8f, p.captureRacePenalty, Eps);
            Assert.AreEqual(0.5f, p.advanceSlowdownScale, Eps);
            Assert.AreEqual(0.6f, p.ownCostScale, Eps);
            Assert.AreEqual(0.1f, p.reconstructionRate, Eps);
        }
    }
}
