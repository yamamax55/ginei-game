using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 対砲戦：発砲量×探知で敵砲を逆探知→射撃管制で命中→持続射撃で火力を削ぐ。
    /// 砲を狙うと艦体への直接打撃が減る（機会費用）／撃ち返すと自分も露呈する。既定 Params で期待値固定。
    /// </summary>
    public class CounterBatteryRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow を含む箇所のみ緩める

        [Test]
        public void BatteryLocalization_VolumeAndSensorSaturateAndCap()
        {
            // 発砲量100・探知1.0 → 100/(100+100)=0.5
            Assert.AreEqual(0.5f, CounterBatteryRules.BatteryLocalization(100f, 1f), Eps);
            // 発砲量900・探知1.0 → 0.9（上限ぎりぎり）
            Assert.AreEqual(0.9f, CounterBatteryRules.BatteryLocalization(900f, 1f), Eps);
            // 上限 maxLocalization=0.9 を超えない
            Assert.AreEqual(0.9f, CounterBatteryRules.BatteryLocalization(100000f, 1f), Eps);
            // 探知ゼロ＝逆探知できない
            Assert.AreEqual(0f, CounterBatteryRules.BatteryLocalization(900f, 0f), Eps);
        }

        [Test]
        public void CounterFireAccuracy_LocalizationTimesFireControl()
        {
            // 0.5 × 1.0 × accuracyScale(0.8) = 0.4
            Assert.AreEqual(0.4f, CounterBatteryRules.CounterFireAccuracy(0.5f, 1f), Eps);
            // 位置不明なら当たらない
            Assert.AreEqual(0f, CounterBatteryRules.CounterFireAccuracy(0f, 1f), Eps);
        }

        [Test]
        public void FirepowerSuppression_AccumulatesWithSustainedFire()
        {
            // 命中0.8・持続1.0 → 1^1.5=1 → 0.8×1×0.7 = 0.56
            Assert.AreEqual(0.56f, CounterBatteryRules.FirepowerSuppression(0.8f, 1f), Eps);
            // 命中0.8・持続0.25 → 0.25^1.5=0.125 → 0.8×0.125×0.7 = 0.07（一瞬では崩せない）
            Assert.AreEqual(0.07f, CounterBatteryRules.FirepowerSuppression(0.8f, 0.25f), PowEps);
            Assert.Greater(
                CounterBatteryRules.FirepowerSuppression(0.8f, 1f),
                CounterBatteryRules.FirepowerSuppression(0.8f, 0.25f)); // 撃ち続けるほど効く
        }

        [Test]
        public void EnemyFirepowerAfter_ReducesBySuppression_BaseNonDestructive()
        {
            // 基準100に抑制0.56 → 100×(1-0.56)=44
            Assert.AreEqual(44f, CounterBatteryRules.EnemyFirepowerAfter(100f, 0.56f), Eps);
            // 抑制ゼロ＝基準のまま（非破壊）
            Assert.AreEqual(100f, CounterBatteryRules.EnemyFirepowerAfter(100f, 0f), Eps);
        }

        [Test]
        public void CounterBatteryDuel_SignedAdvantage()
        {
            Assert.AreEqual(0.5f, CounterBatteryRules.CounterBatteryDuel(0.8f, 0.3f), Eps);   // 優勢
            Assert.AreEqual(-0.5f, CounterBatteryRules.CounterBatteryDuel(0.3f, 0.8f), Eps);  // 劣勢
            Assert.AreEqual(0f, CounterBatteryRules.CounterBatteryDuel(0.5f, 0.5f), Eps);     // 拮抗
        }

        [Test]
        public void OpportunityCost_BatteriesVsHullsTradeoff()
        {
            Assert.AreEqual(0.5f, CounterBatteryRules.OpportunityCost(0.7f, 0.2f), Eps);   // 砲狙いへ寄せる
            Assert.AreEqual(-0.5f, CounterBatteryRules.OpportunityCost(0.2f, 0.7f), Eps);  // 艦体直撃へ寄せる
        }

        [Test]
        public void ReturnFireRisk_AndSuppressionThreshold()
        {
            // 0.9 × 0.5 × returnFireScale(0.6) = 0.27（撃ち返すと露呈）
            Assert.AreEqual(0.27f, CounterBatteryRules.ReturnFireRisk(0.9f, 0.5f), Eps);
            // 制圧判定（既定しきい 0.6）
            Assert.IsFalse(CounterBatteryRules.IsBatterySuppressed(0.56f));
            Assert.IsTrue(CounterBatteryRules.IsBatterySuppressed(0.7f));
        }

        [Test]
        public void Narrative_LocalizeFromFireThenSuppressButLoseHullDamage()
        {
            // 敵が激しく撃つ（発砲量900）＋良好な探知で敵砲を逆探知
            float loc = CounterBatteryRules.BatteryLocalization(900f, 1f);
            Assert.AreEqual(0.9f, loc, Eps);

            // 優れた射撃管制で撃ち返す＝対砲射撃の命中
            float acc = CounterBatteryRules.CounterFireAccuracy(loc, 1f);
            Assert.AreEqual(0.72f, acc, Eps); // 0.9×1×0.8

            // 撃ち続けて敵火力を削ぐ
            float sup = CounterBatteryRules.FirepowerSuppression(acc, 1f);
            Assert.AreEqual(0.504f, sup, PowEps); // 0.72×1×0.7

            // 削いだぶん受ける被害が減る（敵の残存火力が落ちる）
            float after = CounterBatteryRules.EnemyFirepowerAfter(100f, sup);
            Assert.Less(after, 100f);
            Assert.AreEqual(49.6f, after, PowEps);

            // だが砲狙いに寄せたぶん、艦体への直接打撃は減る（機会費用は正＝砲狙い側）
            float cost = CounterBatteryRules.OpportunityCost(0.9f, 0.1f);
            Assert.Greater(cost, 0f);
        }
    }
}
