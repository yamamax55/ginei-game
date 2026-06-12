using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 私有財産と国有財産の分離（<see cref="PropertyRules"/>・<see cref="Ownership"/>）を固定する：利潤の行き先（国有→国庫・私有→資本家）、
    /// 赤字は国有なら国庫の負担、政体の既定（共産＝国有）、国有企業は雇用を守る（解雇を緩める）。
    /// </summary>
    public class PropertyTests
    {
        [Test]
        public void ProfitDestination_SplitsByOwnership()
        {
            // 黒字：国有は国庫へ、私有は資本家へ
            Assert.AreEqual(100f, PropertyRules.ProfitToTreasury(100f, Ownership.国有), 1e-4f);
            Assert.AreEqual(0f, PropertyRules.ProfitToTreasury(100f, Ownership.私有), 1e-4f);
            Assert.AreEqual(100f, PropertyRules.ProfitToPrivate(100f, Ownership.私有), 1e-4f);
            Assert.AreEqual(0f, PropertyRules.ProfitToPrivate(100f, Ownership.国有), 1e-4f);
            // 赤字：国有企業の損失は国庫の負担（負値）、私有は所有者が被る
            Assert.AreEqual(-50f, PropertyRules.ProfitToTreasury(-50f, Ownership.国有), 1e-4f);
            Assert.AreEqual(-50f, PropertyRules.ProfitToPrivate(-50f, Ownership.私有), 1e-4f);
        }

        [Test]
        public void DefaultFor_CommunismIsState()
        {
            Assert.AreEqual(Ownership.国有, PropertyRules.DefaultFor("共産主義"));
            Assert.AreEqual(Ownership.私有, PropertyRules.DefaultFor("民主"));
            Assert.AreEqual(Ownership.私有, PropertyRules.DefaultFor("専制"));
            Assert.AreEqual(Ownership.私有, PropertyRules.DefaultFor(null));
            Assert.IsTrue(PropertyRules.IsState(Ownership.国有));
            Assert.IsFalse(PropertyRules.IsState(Ownership.私有));
        }

        [Test]
        public void StateEnterprise_ProtectsEmployment_OnLoss()
        {
            // 同条件の私有/国有企業を赤字（低価格）で1tick：国有の方が解雇が少なく雇用が残る
            var priv = new Enterprise(Faction.同盟, SystemType.工業, 100f, 1000f, 1f, 1f, "私企業", Ownership.私有);
            var state = new Enterprise(Faction.同盟, SystemType.工業, 100f, 1000f, 1f, 1f, "国営", Ownership.国有);
            EnterpriseRules.Tick(priv, 0.5f, 1000f, 1f);   // 赤字
            EnterpriseRules.Tick(state, 0.5f, 1000f, 1f);
            Assert.Less(priv.employees, 100f);              // 私有は解雇で縮小
            Assert.Less(state.employees, 100f);             // 国有も縮小はするが
            Assert.Greater(state.employees, priv.employees); // 国有の方が雇用を守る
        }

        [Test]
        public void Enterprise_DefaultsToPrivate()
        {
            var e = new Enterprise(Faction.帝国, SystemType.工業, 100f);
            Assert.AreEqual(Ownership.私有, e.ownership); // 既定=私有（後方互換）
        }
    }
}
