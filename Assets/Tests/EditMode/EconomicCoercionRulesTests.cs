using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>経済的強制の梯子（#1397）の純ロジックを担保する。</summary>
    public class EconomicCoercionRulesTests
    {
        const float Tol = 1e-4f;

        /// <summary>段階別の圧力＝通商妨害＜経済制裁＜金融封鎖（既定基礎0.3/0.6/0.9・強度1.0）。</summary>
        [Test]
        public void CoercionPressure_段階で強まる()
        {
            float harass = EconomicCoercionRules.CoercionPressure(CoercionStep.通商妨害, 1f);
            float sanction = EconomicCoercionRules.CoercionPressure(CoercionStep.経済制裁, 1f);
            float blockade = EconomicCoercionRules.CoercionPressure(CoercionStep.金融封鎖, 1f);
            Assert.AreEqual(0.3f, harass, Tol);
            Assert.AreEqual(0.6f, sanction, Tol);
            Assert.AreEqual(0.9f, blockade, Tol);
            Assert.Less(harass, sanction);
            Assert.Less(sanction, blockade);
        }

        /// <summary>相手への打撃は交易依存度に比例（圧力0.9×依存0.5＝0.45・自給自足0は無傷）。</summary>
        [Test]
        public void TargetDamage_交易依存に比例()
        {
            Assert.AreEqual(0.45f, EconomicCoercionRules.TargetDamage(0.9f, 0.5f), Tol);
            Assert.AreEqual(0f, EconomicCoercionRules.TargetDamage(0.9f, 0f), Tol);
        }

        /// <summary>自国への跳ね返りは段階が上がるほど大きい（自国依存0.4・係数0.5＝0.06/0.12/0.18）。</summary>
        [Test]
        public void SelfCost_段階で跳ね返りが増す()
        {
            float harass = EconomicCoercionRules.SelfCost(CoercionStep.通商妨害, 0.4f);
            float sanction = EconomicCoercionRules.SelfCost(CoercionStep.経済制裁, 0.4f);
            float blockade = EconomicCoercionRules.SelfCost(CoercionStep.金融封鎖, 0.4f);
            Assert.AreEqual(0.06f, harass, Tol);
            Assert.AreEqual(0.12f, sanction, Tol);
            Assert.AreEqual(0.18f, blockade, Tol);
            Assert.Less(harass, blockade);
        }

        /// <summary>梯子の遷移＝通商妨害→経済制裁→金融封鎖、金融封鎖は最上段で据え置き。</summary>
        [Test]
        public void NextStep_梯子を一段上げる()
        {
            Assert.AreEqual(CoercionStep.経済制裁, EconomicCoercionRules.NextStep(CoercionStep.通商妨害));
            Assert.AreEqual(CoercionStep.金融封鎖, EconomicCoercionRules.NextStep(CoercionStep.経済制裁));
            Assert.AreEqual(CoercionStep.金融封鎖, EconomicCoercionRules.NextStep(CoercionStep.金融封鎖));
        }

        /// <summary>エスカレーション判断＝抵抗が高く効果が無効閾値0.3未満なら昇る・効いていれば昇らない。</summary>
        [Test]
        public void EscalationDecision_効かないと昇る()
        {
            // 抵抗0.8・効果0.2（<0.3）→昇る
            Assert.IsTrue(EconomicCoercionRules.EscalationDecision(0.8f, 0.2f));
            // 効果0.5（>=0.3）＝効いている→昇らない
            Assert.IsFalse(EconomicCoercionRules.EscalationDecision(0.8f, 0.5f));
            // 抵抗0.3（低い）＝相手が屈しつつある→昇らない
            Assert.IsFalse(EconomicCoercionRules.EscalationDecision(0.3f, 0.2f));
        }

        /// <summary>第三国への波及＝強力な段階ほど大きい（統合度0.5・0.15/0.30/0.45）。</summary>
        [Test]
        public void ThirdPartyDisruption_強い段階ほど波及()
        {
            float harass = EconomicCoercionRules.ThirdPartyDisruption(CoercionStep.通商妨害, 0.5f);
            float blockade = EconomicCoercionRules.ThirdPartyDisruption(CoercionStep.金融封鎖, 0.5f);
            Assert.AreEqual(0.15f, harass, Tol);
            Assert.AreEqual(0.45f, blockade, Tol);
            Assert.Less(harass, blockade);
        }

        /// <summary>多国間協調の要件＝強力な段階ほど高い（0.3/0.6/0.9）。</summary>
        [Test]
        public void CoalitionRequirement_強い段階ほど協調が要る()
        {
            Assert.AreEqual(0.3f, EconomicCoercionRules.CoalitionRequirement(CoercionStep.通商妨害), Tol);
            Assert.AreEqual(0.6f, EconomicCoercionRules.CoalitionRequirement(CoercionStep.経済制裁), Tol);
            Assert.AreEqual(0.9f, EconomicCoercionRules.CoalitionRequirement(CoercionStep.金融封鎖), Tol);
        }

        /// <summary>金融封鎖の締め上げ判定＝金融封鎖かつ圧力が閾値0.7以上のときのみ成立。</summary>
        [Test]
        public void IsFinancialStrangulation_金融封鎖で締め上げ()
        {
            // 金融封鎖・圧力0.9（>=0.7）→締め上げ
            Assert.IsTrue(EconomicCoercionRules.IsFinancialStrangulation(CoercionStep.金融封鎖, 0.9f));
            // 金融封鎖でも圧力0.5（<0.7）→締め上げに至らない
            Assert.IsFalse(EconomicCoercionRules.IsFinancialStrangulation(CoercionStep.金融封鎖, 0.5f));
            // 経済制裁では圧力が高くても締め上げにならない
            Assert.IsFalse(EconomicCoercionRules.IsFinancialStrangulation(CoercionStep.経済制裁, 0.9f));
        }
    }
}
