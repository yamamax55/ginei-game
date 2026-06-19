using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>航路回廊の戦略的支配の純ロジック（CorridorControlRules）の EditMode テスト。</summary>
    public class CorridorControlRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void CorridorValue_NoAlternative_IsHigherThanWithAlternatives()
        {
            // 2*10/(1+0)=20 / 2*10/(1+3)=5
            float noAlt = CorridorControlRules.CorridorValue(2f, 10f, 0f);
            float withAlt = CorridorControlRules.CorridorValue(2f, 10f, 3f);
            Assert.AreEqual(20f, noAlt, Eps);
            Assert.AreEqual(5f, withAlt, Eps);
            Assert.Greater(noAlt, withAlt, "迂回路が無いほど回廊は貴重");
        }

        [Test]
        public void BlockadeStrength_SumsFortressAndPatrol()
        {
            // 8*1 + 4*1 = 12
            Assert.AreEqual(12f, CorridorControlRules.BlockadeStrength(8f, 4f), Eps);
        }

        [Test]
        public void BreakthroughDifficulty_BlockadeRatio()
        {
            // 12/(12+4)=0.75
            Assert.AreEqual(0.75f, CorridorControlRules.BreakthroughDifficulty(12f, 4f), Eps);
            // 両者ゼロは0
            Assert.AreEqual(0f, CorridorControlRules.BreakthroughDifficulty(0f, 0f), Eps);
        }

        [Test]
        public void SupplyLineSecurity_ValueTimesBlockade()
        {
            // 5*12=60
            Assert.AreEqual(60f, CorridorControlRules.SupplyLineSecurity(5f, 12f), Eps);
        }

        [Test]
        public void ChokePointLeverage_ValueTimesBlockade()
        {
            // 20*12=240
            Assert.AreEqual(240f, CorridorControlRules.ChokePointLeverage(20f, 12f), Eps);
        }

        [Test]
        public void TollRevenue_TrafficTimesControl()
        {
            // 10 * (12/13) = 9.230769...
            Assert.AreEqual(10f * (12f / 13f), CorridorControlRules.TollRevenue(10f, 12f), Eps);
            // 封鎖ゼロは支配度0=収入0
            Assert.AreEqual(0f, CorridorControlRules.TollRevenue(10f, 0f), Eps);
        }

        [Test]
        public void BypassPressure_AltTimesBlockade()
        {
            // 3*12=36
            Assert.AreEqual(36f, CorridorControlRules.BypassPressure(3f, 12f), Eps);
            // 迂回路が無ければ圧力ゼロ
            Assert.AreEqual(0f, CorridorControlRules.BypassPressure(0f, 12f), Eps);
        }

        [Test]
        public void IsCorridorSecure_RequiresBlockadeAndThreshold()
        {
            Assert.IsTrue(CorridorControlRules.IsCorridorSecure(12f, 0.75f), "封鎖あり・難度0.75は確保");
            Assert.IsFalse(CorridorControlRules.IsCorridorSecure(0f, 0.9f), "封鎖が無ければ確保不可");
            Assert.IsFalse(CorridorControlRules.IsCorridorSecure(12f, 0.4f), "難度が閾値未満は未確保");
        }

        [Test]
        public void InputsAreClamped_NoNegativeOutputs()
        {
            // 負入力はclampされ非負/0
            Assert.AreEqual(0f, CorridorControlRules.CorridorValue(-5f, -3f, -2f), Eps);
            Assert.AreEqual(0f, CorridorControlRules.BlockadeStrength(-8f, -4f), Eps);
            Assert.AreEqual(0f, CorridorControlRules.SupplyLineSecurity(-5f, -12f), Eps);
        }

        [Test]
        public void Narrative_IserlohnCorridor_SecuredByFortressDeniesEnemySupply()
        {
            // 物語：イゼルローン回廊＝迂回路なしの貴重な隘路を要塞で封じる。
            // 連結性3・交通12・迂回路0 → 価値36
            float p = CorridorControlRules.CorridorValue(3f, 12f, 0f);
            Assert.AreEqual(36f, p, Eps);

            // 要塞火力15・哨戒5 → 封鎖20
            float block = CorridorControlRules.BlockadeStrength(15f, 5f);
            Assert.AreEqual(20f, block, Eps);

            // 攻撃側兵力5 → 突破難度 20/25=0.8 ＝ 既定閾値0.6超で確保
            float diff = CorridorControlRules.BreakthroughDifficulty(block, 5f);
            Assert.AreEqual(0.8f, diff, Eps);
            Assert.IsTrue(CorridorControlRules.IsCorridorSecure(block, diff), "強封鎖の回廊は確保される");

            // 確保した回廊は補給線を安泰にし（価値×封鎖）、戦略的てこも大きい
            float supply = CorridorControlRules.SupplyLineSecurity(p, block);
            float leverage = CorridorControlRules.ChokePointLeverage(p, block);
            Assert.AreEqual(720f, supply, Eps);   // 36*20
            Assert.AreEqual(720f, leverage, Eps); // 36*20
            Assert.Greater(supply, 0f);

            // 迂回路が無いので迂回圧力はゼロ＝敵は回廊を通るしかない
            Assert.AreEqual(0f, CorridorControlRules.BypassPressure(0f, block), Eps);

            // 迂回路があれば封鎖の厳しさが迂回圧力を生む（フェザーン迂回型）
            float bypass = CorridorControlRules.BypassPressure(2f, block);
            Assert.AreEqual(40f, bypass, Eps); // 2*20
            Assert.Greater(bypass, 0f, "迂回路があると封鎖が迂回を促す");
        }
    }
}
