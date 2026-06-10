using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>FleetCapRules（配備可能艦数＝指揮容量÷必要容量・階級と二重・#1067）の純ロジックを既定Paramsの具体値で固定。</summary>
    public class FleetCapRulesTests
    {
        /// <summary>指揮容量＝（基礎20＋統率×2）×（1＋tier×0.25）。統率50・tier6＝120×2.5＝300。</summary>
        [Test]
        public void CommandCapacity_統率と階級で決まる()
        {
            Assert.AreEqual(300f, FleetCapRules.CommandCapacity(50f, 6), 1e-4f);
        }

        /// <summary>統率が高く階級も上がるほど容量が増える＝大将ほど大艦隊（統率100・tier8＝220×3.0＝660）。</summary>
        [Test]
        public void CommandCapacity_高統率高階級ほど大きい()
        {
            float low = FleetCapRules.CommandCapacity(50f, 6);
            float high = FleetCapRules.CommandCapacity(100f, 8);
            Assert.AreEqual(660f, high, 1e-4f);
            Assert.Greater(high, low);
        }

        /// <summary>配備可能艦数＝容量÷必要容量＝floor(300/2)=150。大きい艦（capPerShip大）ほど隻数が減る。</summary>
        [Test]
        public void DeployableShips_容量を必要容量で割る()
        {
            Assert.AreEqual(150, FleetCapRules.DeployableShips(300f, 2f));
            Assert.AreEqual(50, FleetCapRules.DeployableShips(300f, 6f)); // 大艦ほど食う
        }

        /// <summary>階級による艦数上限＝tier×10（中将tier7＝70・元帥tier10＝100）。</summary>
        [Test]
        public void RankCapLimit_階級が艦数上限を決める()
        {
            Assert.AreEqual(70, FleetCapRules.RankCapLimit(7));
            Assert.AreEqual(100, FleetCapRules.RankCapLimit(10));
        }

        /// <summary>実効配備数＝容量制約と階級制約のmin。容量150だが階級上限60＝階級が縛る＝60。</summary>
        [Test]
        public void EffectiveDeployable_階級が縛る()
        {
            // CommandCapacity(50,6)=300, DeployableShips(300,2)=150, RankCapLimit(6)=60
            Assert.AreEqual(60, FleetCapRules.EffectiveDeployable(300f, 2f, 6));
        }

        /// <summary>実効配備数＝二重制約のmin。大艦で容量50・階級上限60＝容量が縛る＝50。</summary>
        [Test]
        public void EffectiveDeployable_容量が縛る()
        {
            // DeployableShips(300,6)=50, RankCapLimit(6)=60 → min=50
            Assert.AreEqual(50, FleetCapRules.EffectiveDeployable(300f, 6f, 6));
        }

        /// <summary>配備上限内なら true・超過なら false（指揮しきれない＝統制崩壊）。</summary>
        [Test]
        public void IsWithinCap_上限内外を判定()
        {
            Assert.IsTrue(FleetCapRules.IsWithinCap(60, 70));
            Assert.IsTrue(FleetCapRules.IsWithinCap(70, 70));
            Assert.IsFalse(FleetCapRules.IsWithinCap(72, 70));
        }

        /// <summary>超過ペナルティ＝上限内は1.0・超過1隻ごとに0.1低下（72/70＝2隻超過＝0.8）。下限0でクランプ。</summary>
        [Test]
        public void OverCapacityPenalty_超過で統制低下()
        {
            Assert.AreEqual(1f, FleetCapRules.OverCapacityPenalty(70, 70), 1e-4f);
            Assert.AreEqual(0.8f, FleetCapRules.OverCapacityPenalty(72, 70), 1e-4f);
            Assert.AreEqual(0f, FleetCapRules.OverCapacityPenalty(100, 70), 1e-4f); // 30隻超過→クランプ0
        }
    }
}
