using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 虐殺（ヴェスターラント型）を固定する：恐怖の即効、露出度比例の汚点と敵宣伝素材（隠せば当面無傷）、
    /// 黙認者の隠れた汚点と発覚（証人数で漏れる）、汚点の遅い風化。境界・決定論を担保。
    /// </summary>
    public class AtrocityRulesTests
    {
        private static readonly AtrocityParams P = AtrocityParams.Default;
        // 即時鎮圧0.5/汚点0.4/宣伝0.5/黙認比0.5/減衰0.005

        [Test]
        public void ImmediateSuppression_TerrorWorks()
        {
            Assert.AreEqual(0.5f, AtrocityRules.ImmediateSuppression(1f, P), 1e-5f);
            Assert.AreEqual(0.25f, AtrocityRules.ImmediateSuppression(0.5f, P), 1e-5f);
        }

        [Test]
        public void PerpetratorStain_RequiresExposure()
        {
            // 露出した大虐殺＝最大汚点
            Assert.AreEqual(0.4f, AtrocityRules.PerpetratorStain(1f, 1f, P), 1e-5f);
            // 隠し通せた蛮行は（当面）無傷
            Assert.AreEqual(0f, AtrocityRules.PerpetratorStain(1f, 0f, P), 1e-5f);
            Assert.AreEqual(0.1f, AtrocityRules.PerpetratorStain(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void PropagandaValue_GiftToTheEnemy()
        {
            Assert.AreEqual(0.5f, AtrocityRules.PropagandaValue(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, AtrocityRules.PropagandaValue(1f, 0f, P), 1e-5f);
        }

        [Test]
        public void CondonerHiddenStain_HalfOfPerpetrator()
        {
            // 黙認者＝実行者の満露出汚点×0.5
            Assert.AreEqual(0.2f, AtrocityRules.CondonerHiddenStain(1f, P), 1e-5f);
            Assert.AreEqual(0.1f, AtrocityRules.CondonerHiddenStain(0.5f, P), 1e-5f);
        }

        [Test]
        public void CondonementExposed_LeaksWithWitnesses()
        {
            // 証人ゼロ＝完全な秘密＝発覚率0
            Assert.IsFalse(AtrocityRules.CondonementExposed(0, 0f));
            // 証人7人＝1−0.9^7≈0.5217：roll 0.5 で露見・0.53 でまだ
            Assert.IsTrue(AtrocityRules.CondonementExposed(7, 0.5f));
            Assert.IsFalse(AtrocityRules.CondonementExposed(7, 0.53f));
            // 証人が多いほど漏れやすい（単調増加）
            Assert.IsTrue(AtrocityRules.CondonementExposed(50, 0.9f));
        }

        [Test]
        public void StainTick_FadesSlowly()
        {
            Assert.AreEqual(0.395f, AtrocityRules.StainTick(0.4f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, AtrocityRules.StainTick(0.001f, 1f, P), 1e-5f); // 下限0
        }
    }
}
