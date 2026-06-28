using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦術要塞の純ロジック（#76/#77）：稼働砲台比→コア被ダメ倍率（シールド低下）・沈黙判定・砲台配置角・主砲の射界帯。
    /// ※回廊の戦略要塞 <see cref="FortressRules"/> とは別系統（こちらは Battle シーンの固定拠点）。
    /// </summary>
    public class FortressBatteryRulesTests
    {
        // ── コア被ダメ倍率（稼働砲台が揃うほど堅く、削れるほど脆い）──

        [Test]
        public void CoreDamage_FullTurrets_IsShielded()
        {
            // 全砲台稼働＝シールド満＝既定 0.25 倍（コアは固い）。
            Assert.AreEqual(0.25f, FortressBatteryRules.CoreDamageMultiplier(8, 8), 1e-4f);
        }

        [Test]
        public void CoreDamage_AtOrBelowExposedThreshold_IsFull()
        {
            // 既定 exposedTurretRatio=0.3。稼働比がここ以下でシールド消失＝等倍（1.0）。
            Assert.AreEqual(1.0f, FortressBatteryRules.CoreDamageMultiplier(3, 10), 1e-4f); // 0.3＝閾値
            Assert.AreEqual(1.0f, FortressBatteryRules.CoreDamageMultiplier(1, 10), 1e-4f); // 0.1＜閾値
            Assert.AreEqual(1.0f, FortressBatteryRules.CoreDamageMultiplier(0, 10), 1e-4f); // 全砲台沈黙
        }

        [Test]
        public void CoreDamage_Midpoint_Interpolates()
        {
            // 稼働比 0.65 → shield=(0.65-0.3)/0.7=0.5 → Lerp(1.0,0.25,0.5)=0.625。
            Assert.AreEqual(0.625f, FortressBatteryRules.CoreDamageMultiplier(65, 100), 1e-4f);
        }

        [Test]
        public void CoreDamage_NoTurrets_IsFull()
        {
            // 砲台を持たない要塞は素のコア被ダメ（倍率1.0）。0除算しない。
            Assert.AreEqual(1.0f, FortressBatteryRules.CoreDamageMultiplier(0, 0), 1e-4f);
            Assert.AreEqual(1.0f, FortressBatteryRules.CoreDamageMultiplier(5, 0), 1e-4f);
        }

        [Test]
        public void CoreDamage_MonotonicDecreasingInActiveTurrets()
        {
            // 砲台が多いほど被ダメ倍率は小さい（単調）。
            float few = FortressBatteryRules.CoreDamageMultiplier(4, 10);
            float mid = FortressBatteryRules.CoreDamageMultiplier(7, 10);
            float many = FortressBatteryRules.CoreDamageMultiplier(10, 10);
            Assert.Less(many, mid);
            Assert.Less(mid, few);
        }

        [Test]
        public void CoreDamage_CustomParams()
        {
            // シールド最小倍率0.5・閾値0.5。稼働比0.75 → shield=(0.75-0.5)/0.5=0.5 → Lerp(1,0.5,0.5)=0.75。
            var p = new FortressBatteryParams(0.5f, 0.5f);
            Assert.AreEqual(0.75f, FortressBatteryRules.CoreDamageMultiplier(75, 100, p), 1e-4f);
            Assert.AreEqual(0.5f, FortressBatteryRules.CoreDamageMultiplier(100, 100, p), 1e-4f);
            Assert.AreEqual(1.0f, FortressBatteryRules.CoreDamageMultiplier(50, 100, p), 1e-4f);
        }

        // ── 沈黙判定（全砲台破壊で制圧＝攻略のもう一つの達成）──

        [Test]
        public void IsSilenced_OnlyWhenNoActiveTurrets()
        {
            Assert.IsTrue(FortressBatteryRules.IsSilenced(0));
            Assert.IsTrue(FortressBatteryRules.IsSilenced(-1)); // 念のため負も沈黙扱い
            Assert.IsFalse(FortressBatteryRules.IsSilenced(1));
        }

        // ── 砲台の外周配置角（等間隔・度）──

        [Test]
        public void TurretAngle_EvenlySpaced()
        {
            Assert.AreEqual(0f, FortressBatteryRules.TurretAngleDeg(0, 4), 1e-4f);
            Assert.AreEqual(90f, FortressBatteryRules.TurretAngleDeg(1, 4), 1e-4f);
            Assert.AreEqual(180f, FortressBatteryRules.TurretAngleDeg(2, 4), 1e-4f);
            Assert.AreEqual(270f, FortressBatteryRules.TurretAngleDeg(3, 4), 1e-4f);
        }

        [Test]
        public void TurretAngle_ZeroCount_IsSafe()
        {
            Assert.AreEqual(0f, FortressBatteryRules.TurretAngleDeg(0, 0), 1e-4f);
        }

        // ── 主砲の射界帯（#77：最小射程あり＝懐に潜れば撃てない）──

        [Test]
        public void InFiringBand_BetweenMinAndMax()
        {
            // 最小5・最大40。懐(5未満)は不可、帯内は可、射程外も不可。
            Assert.IsFalse(FortressBatteryRules.InFiringBand(3f, 5f, 40f)); // 懐＝撃てない
            Assert.IsTrue(FortressBatteryRules.InFiringBand(5f, 5f, 40f));  // 最小射程ちょうど
            Assert.IsTrue(FortressBatteryRules.InFiringBand(20f, 5f, 40f)); // 帯内
            Assert.IsTrue(FortressBatteryRules.InFiringBand(40f, 5f, 40f)); // 最大射程ちょうど
            Assert.IsFalse(FortressBatteryRules.InFiringBand(45f, 5f, 40f)); // 射程外
        }
    }
}
