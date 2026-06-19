using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>攻城砲撃（要塞・拠点への組織的砲撃＝攻める側）の純ロジック EditMode テスト。既定 Params で期待値を固定。</summary>
    public class SiegeBatteryRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 砲撃集中_砲門数と火力統制で決まる()
        {
            // Lerp(0.4, 1, 0.5) = 0.7 → 10門 × 0.7 = 7.0
            Assert.AreEqual(7f, SiegeBatteryRules.BarrageConcentration(10f, 0.5f), Eps);
            // 統制満額なら全門集中、ゼロでも下限0.4ぶんは撃てる
            Assert.AreEqual(10f, SiegeBatteryRules.BarrageConcentration(10f, 1f), Eps);
            Assert.AreEqual(4f, SiegeBatteryRules.BarrageConcentration(10f, 0f), Eps);
            // 負の砲門数は0へ
            Assert.AreEqual(0f, SiegeBatteryRules.BarrageConcentration(-5f, 1f), Eps);
        }

        [Test]
        public void スクリーン飽和_押し合いの比で決まる()
        {
            // 7 / (7 + 3) = 0.7
            Assert.AreEqual(0.7f, SiegeBatteryRules.ScreenSaturation(7f, 3f), Eps);
            // 双方ゼロは飽和なし
            Assert.AreEqual(0f, SiegeBatteryRules.ScreenSaturation(0f, 0f), Eps);
            // スクリーンが無ければ完全飽和
            Assert.AreEqual(1f, SiegeBatteryRules.ScreenSaturation(7f, 0f), Eps);
        }

        [Test]
        public void 累積装甲損傷_集中と飽和と時間の積()
        {
            // 7 × 0.7 × 10 × 0.01 = 0.49
            Assert.AreEqual(0.49f, SiegeBatteryRules.CumulativeArmorDamage(7f, 0.7f, 10f), Eps);
            // 砲撃時間ゼロなら損傷なし
            Assert.AreEqual(0f, SiegeBatteryRules.CumulativeArmorDamage(7f, 0.7f, 0f), Eps);
        }

        [Test]
        public void 砲撃精度_距離で割り引かれる()
        {
            // 0.8 / (1 + 1×1) = 0.4
            Assert.AreEqual(0.4f, SiegeBatteryRules.BarrageAccuracy(0.8f, 1f), Eps);
            // 至近なら照準どおり
            Assert.AreEqual(0.8f, SiegeBatteryRules.BarrageAccuracy(0.8f, 0f), Eps);
            // 遠距離ほど落ちる：0.8 / (1 + 3) = 0.2
            Assert.AreEqual(0.2f, SiegeBatteryRules.BarrageAccuracy(0.8f, 3f), Eps);
        }

        [Test]
        public void 対砲戦損失_反撃と露出の積()
        {
            // 0.6 × 0.4 × 0.5 = 0.12
            Assert.AreEqual(0.12f, SiegeBatteryRules.CounterBatteryLoss(0.6f, 0.4f), Eps);
            // 露出ゼロなら削られない
            Assert.AreEqual(0f, SiegeBatteryRules.CounterBatteryLoss(0.6f, 0f), Eps);
        }

        [Test]
        public void 弾薬消費_集中と時間の積()
        {
            // 7 × 10 × 1 = 70
            Assert.AreEqual(70f, SiegeBatteryRules.AmmunitionExpenditure(7f, 10f), Eps);
            // 撃たなければ消費なし
            Assert.AreEqual(0f, SiegeBatteryRules.AmmunitionExpenditure(7f, 0f), Eps);
        }

        [Test]
        public void 突破口形成_累積損傷と精度の積()
        {
            // 0.49 × 0.4 × 1 = 0.196
            Assert.AreEqual(0.196f, SiegeBatteryRules.BreachFormation(0.49f, 0.4f), Eps);
            // 精度ゼロなら突破口は開かない
            Assert.AreEqual(0f, SiegeBatteryRules.BreachFormation(0.49f, 0f), Eps);
        }

        [Test]
        public void 突破判定_閾値以上で開通()
        {
            // 既定閾値0.5：未満は不成立、以上で成立
            Assert.IsFalse(SiegeBatteryRules.IsBreachAchieved(0.196f));
            Assert.IsTrue(SiegeBatteryRules.IsBreachAchieved(0.6f));
            // 明示閾値版
            Assert.IsTrue(SiegeBatteryRules.IsBreachAchieved(0.196f, 0.1f));
            Assert.IsFalse(SiegeBatteryRules.IsBreachAchieved(0.196f, 0.5f));
        }

        [Test]
        public void 入力クランプ_範囲外でも壊れない()
        {
            // 統制は0..1へクランプ：1.5でも満額10
            Assert.AreEqual(10f, SiegeBatteryRules.BarrageConcentration(10f, 1.5f), Eps);
            // 精度は0..1へクランプ
            Assert.AreEqual(1f, SiegeBatteryRules.BarrageAccuracy(2f, 0f), Eps);
            // 対砲戦損失は0..1へクランプ
            Assert.AreEqual(0.5f, SiegeBatteryRules.CounterBatteryLoss(2f, 2f), Eps);
        }

        [Test]
        public void 物語_長期砲撃でイゼルローン要塞に突破口が開く()
        {
            // 多数の砲門を高い統制で集中する
            float concentration = SiegeBatteryRules.BarrageConcentration(20f, 0.8f);
            Assert.Greater(concentration, 0f);

            // 厚い防御スクリーンを集中砲火で飽和突破する
            float saturation = SiegeBatteryRules.ScreenSaturation(concentration, 5f);
            Assert.Greater(saturation, 0.5f); // 集中が勝りスクリーンを飽和

            // 至近・高照準で長時間撃ち続け装甲を累積で削る
            float accuracy = SiegeBatteryRules.BarrageAccuracy(0.9f, 0.5f);
            float armorDamage = SiegeBatteryRules.CumulativeArmorDamage(concentration, saturation, 40f);

            // 累積損傷×精度が突破口を開く
            float breach = SiegeBatteryRules.BreachFormation(armorDamage, accuracy);
            Assert.IsTrue(SiegeBatteryRules.IsBreachAchieved(breach), "長期砲撃で突破口が開くはず");

            // 一方、長期砲撃は弾薬を大量に食う
            float ammo = SiegeBatteryRules.AmmunitionExpenditure(concentration, 40f);
            Assert.Greater(ammo, 0f);

            // 短時間・薄い砲撃では突破口は開かない（対照）
            float weakConc = SiegeBatteryRules.BarrageConcentration(3f, 0.3f);
            float weakSat = SiegeBatteryRules.ScreenSaturation(weakConc, 5f);
            float weakDamage = SiegeBatteryRules.CumulativeArmorDamage(weakConc, weakSat, 2f);
            float weakBreach = SiegeBatteryRules.BreachFormation(weakDamage, accuracy);
            Assert.IsFalse(SiegeBatteryRules.IsBreachAchieved(weakBreach), "薄い砲撃では突破口は開かない");
        }
    }
}
