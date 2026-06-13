using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ADM-5 コンディション：疲労・負傷で実効能力低下／休養で回復／戦闘不能判定。</summary>
    public class ConditionRulesTests
    {
        [Test]
        public void ConditionFactor_FatigueAndWound()
        {
            Assert.AreEqual(1.0f, ConditionRules.ConditionFactor(0, 0), 1e-4f);
            Assert.AreEqual(0.8f, ConditionRules.ConditionFactor(100, 0), 1e-4f);   // 疲労最大
            Assert.AreEqual(0.7f, ConditionRules.ConditionFactor(0, 100), 1e-4f);   // 負傷最大
            Assert.AreEqual(0.5f, ConditionRules.ConditionFactor(100, 100), 1e-4f); // 両方最大（下限0.4未満にならず）
            Assert.AreEqual(0.9f, ConditionRules.ConditionFactor(50, 0), 1e-4f);
        }

        [Test]
        public void Fatigue_AddAndRecover()
        {
            Assert.AreEqual(100, ConditionRules.AddFatigue(80, 30));  // クランプ
            Assert.AreEqual(50, ConditionRules.AddFatigue(50, -5));   // 負は0扱い
            Assert.AreEqual(80, ConditionRules.Recover(100, 5f, 4f)); // 5日×4=20回復
            Assert.AreEqual(0, ConditionRules.Recover(10, 5f, 4f));   // 下限0
        }

        [Test]
        public void Wound_HealAndIncapacitation()
        {
            Assert.AreEqual(60, ConditionRules.Heal(100, 10f, 4f));
            Assert.IsTrue(ConditionRules.IsIncapacitated(90));
            Assert.IsFalse(ConditionRules.IsIncapacitated(89));
            Assert.IsTrue(ConditionRules.IsIncapacitated(95, 90));
        }
    }
}
