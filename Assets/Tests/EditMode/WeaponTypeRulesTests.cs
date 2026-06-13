using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>武器種適性ルール（#2256）：対象種別ごとの与ダメ倍率と発射間隔係数を検証する。</summary>
    public class WeaponTypeRulesTests
    {
        // ---- TargetAptitude ----

        [Test]
        public void Beam_NeutralForBothTargets()
        {
            Assert.AreEqual(WeaponTypeRules.BeamAptitudeFlagship, WeaponTypeRules.TargetAptitude(WeaponType.ビーム, true),  1e-4f);
            Assert.AreEqual(WeaponTypeRules.BeamAptitudeEscort,   WeaponTypeRules.TargetAptitude(WeaponType.ビーム, false), 1e-4f);
            Assert.AreEqual(1.0f, WeaponTypeRules.TargetAptitude(WeaponType.ビーム, true),  1e-4f); // 中立
            Assert.AreEqual(1.0f, WeaponTypeRules.TargetAptitude(WeaponType.ビーム, false), 1e-4f);
        }

        [Test]
        public void Missile_NeutralForBothTargets()
        {
            Assert.AreEqual(WeaponTypeRules.MissileAptitudeFlagship, WeaponTypeRules.TargetAptitude(WeaponType.ミサイル, true),  1e-4f);
            Assert.AreEqual(WeaponTypeRules.MissileAptitudeEscort,   WeaponTypeRules.TargetAptitude(WeaponType.ミサイル, false), 1e-4f);
            Assert.AreEqual(1.0f, WeaponTypeRules.TargetAptitude(WeaponType.ミサイル, true),  1e-4f);
            Assert.AreEqual(1.0f, WeaponTypeRules.TargetAptitude(WeaponType.ミサイル, false), 1e-4f);
        }

        [Test]
        public void LongRange_FlagshipAdvantageEscortDisadvantage()
        {
            Assert.AreEqual(WeaponTypeRules.LongRangeAptitudeFlagship, WeaponTypeRules.TargetAptitude(WeaponType.長距離砲, true),  1e-4f);
            Assert.AreEqual(WeaponTypeRules.LongRangeAptitudeEscort,   WeaponTypeRules.TargetAptitude(WeaponType.長距離砲, false), 1e-4f);
            Assert.AreEqual(1.25f, WeaponTypeRules.TargetAptitude(WeaponType.長距離砲, true),  1e-4f); // 対旗艦↑
            Assert.AreEqual(0.85f, WeaponTypeRules.TargetAptitude(WeaponType.長距離砲, false), 1e-4f); // 対配下艦↓
        }

        [Test]
        public void AntiSmall_EscortAdvantageFFlagshipDisadvantage()
        {
            Assert.AreEqual(WeaponTypeRules.AntiSmallAptitudeFlagship, WeaponTypeRules.TargetAptitude(WeaponType.対小型, true),  1e-4f);
            Assert.AreEqual(WeaponTypeRules.AntiSmallAptitudeEscort,   WeaponTypeRules.TargetAptitude(WeaponType.対小型, false), 1e-4f);
            Assert.AreEqual(0.70f, WeaponTypeRules.TargetAptitude(WeaponType.対小型, true),  1e-4f); // 対旗艦↓
            Assert.AreEqual(1.40f, WeaponTypeRules.TargetAptitude(WeaponType.対小型, false), 1e-4f); // 対配下艦↑
        }

        [Test]
        public void PointDefense_SubdueForBothTargets()
        {
            Assert.AreEqual(WeaponTypeRules.PointDefenseAptitudeFlagship, WeaponTypeRules.TargetAptitude(WeaponType.点防御, true),  1e-4f);
            Assert.AreEqual(WeaponTypeRules.PointDefenseAptitudeEscort,   WeaponTypeRules.TargetAptitude(WeaponType.点防御, false), 1e-4f);
            Assert.AreEqual(0.80f, WeaponTypeRules.TargetAptitude(WeaponType.点防御, true),  1e-4f); // 両方控えめ
            Assert.AreEqual(0.80f, WeaponTypeRules.TargetAptitude(WeaponType.点防御, false), 1e-4f);
        }

        // ---- FireIntervalFactor ----

        [Test]
        public void FireInterval_LongRangeIsSlow()
        {
            Assert.AreEqual(WeaponTypeRules.LongRangeIntervalFactor, WeaponTypeRules.FireIntervalFactor(WeaponType.長距離砲), 1e-4f);
            Assert.AreEqual(1.50f, WeaponTypeRules.FireIntervalFactor(WeaponType.長距離砲), 1e-4f); // 装填が遅い
        }

        [Test]
        public void FireInterval_AntiSmallIsFast()
        {
            Assert.AreEqual(WeaponTypeRules.AntiSmallIntervalFactor, WeaponTypeRules.FireIntervalFactor(WeaponType.対小型), 1e-4f);
            Assert.AreEqual(0.70f, WeaponTypeRules.FireIntervalFactor(WeaponType.対小型), 1e-4f); // 連射が速い
        }

        [Test]
        public void FireInterval_OthersAreDefault()
        {
            Assert.AreEqual(WeaponTypeRules.DefaultIntervalFactor, WeaponTypeRules.FireIntervalFactor(WeaponType.ビーム),   1e-4f);
            Assert.AreEqual(WeaponTypeRules.DefaultIntervalFactor, WeaponTypeRules.FireIntervalFactor(WeaponType.ミサイル), 1e-4f);
            Assert.AreEqual(WeaponTypeRules.DefaultIntervalFactor, WeaponTypeRules.FireIntervalFactor(WeaponType.点防御),   1e-4f);
            Assert.AreEqual(1.0f, WeaponTypeRules.FireIntervalFactor(WeaponType.ビーム), 1e-4f);
        }
    }
}
