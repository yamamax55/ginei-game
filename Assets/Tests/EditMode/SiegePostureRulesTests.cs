using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>攻城姿勢（強襲/包囲）のトレードオフと四面楚歌の守備隊降伏（#131 第3段）の純ロジック検証。</summary>
    public class SiegePostureRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>強襲は攻めが速いが被害大、包囲は攻めず被害小・士気崩しが主役（倍率の大小関係）。</summary>
        [Test]
        public void PostureMultipliers_強襲は速攻血多_包囲は無血で士気戦()
        {
            // 強襲＝軌道/侵略/損耗が速い
            Assert.Greater(SiegePostureRules.SuppressMultiplier(SiegePosture.強襲), SiegePostureRules.SuppressMultiplier(SiegePosture.包囲));
            Assert.Greater(SiegePostureRules.InvadeMultiplier(SiegePosture.強襲), SiegePostureRules.InvadeMultiplier(SiegePosture.包囲));
            Assert.Greater(SiegePostureRules.GrindMultiplier(SiegePosture.強襲), SiegePostureRules.GrindMultiplier(SiegePosture.包囲));
            // 包囲は地上を攻めない（侵略/損耗は0）
            Assert.AreEqual(0f, SiegePostureRules.InvadeMultiplier(SiegePosture.包囲), Eps);
            Assert.AreEqual(0f, SiegePostureRules.GrindMultiplier(SiegePosture.包囲), Eps);
            // 強襲は被害大、包囲は被害小
            Assert.Greater(SiegePostureRules.CasualtyMultiplier(SiegePosture.強襲), SiegePostureRules.CasualtyMultiplier(SiegePosture.包囲));
            // 士気崩しは包囲が主役
            Assert.Greater(SiegePostureRules.MoraleErosionMultiplier(SiegePosture.包囲), SiegePostureRules.MoraleErosionMultiplier(SiegePosture.強襲));
        }

        /// <summary>姿勢の切替（強襲↔包囲）。</summary>
        [Test]
        public void Toggle_強襲と包囲を往復()
        {
            Assert.AreEqual(SiegePosture.包囲, SiegePostureRules.Toggle(SiegePosture.強襲));
            Assert.AreEqual(SiegePosture.強襲, SiegePostureRules.Toggle(SiegePosture.包囲));
        }

        /// <summary>四面楚歌＝物理包囲も心理孤立も高いとき士気崩壊が大きく、包囲姿勢ほど強く削れる。</summary>
        [Test]
        public void GarrisonMoraleErosion_四面楚歌で大きく包囲が主役()
        {
            float blockade = SiegePostureRules.GarrisonMoraleErosion(0.9f, 0.9f, SiegePosture.包囲, 1f);
            float assault = SiegePostureRules.GarrisonMoraleErosion(0.9f, 0.9f, SiegePosture.強襲, 1f);
            Assert.Greater(blockade, 0f);
            Assert.Greater(blockade, assault); // 包囲のほうが士気を削る

            // 包囲が緩い（物理包囲も孤立も低い）と崩壊は小さい
            float weak = SiegePostureRules.GarrisonMoraleErosion(0.1f, 0.1f, SiegePosture.包囲, 1f);
            Assert.Less(weak, blockade);

            Assert.AreEqual(0f, SiegePostureRules.GarrisonMoraleErosion(0.9f, 0.9f, SiegePosture.包囲, 0f), Eps); // dt0
        }

        /// <summary>守備隊は士気が閾値以下で降伏（戦わずして崩れる）。</summary>
        [Test]
        public void GarrisonSurrendered_士気閾値割れで降伏()
        {
            Assert.IsTrue(SiegePostureRules.GarrisonSurrendered(0.1f, 0.2f));
            Assert.IsTrue(SiegePostureRules.GarrisonSurrendered(0.2f, 0.2f)); // 閾値ちょうども降伏
            Assert.IsFalse(SiegePostureRules.GarrisonSurrendered(0.5f, 0.2f));
        }

        /// <summary>実効守備隊＝頭数×士気（士気崩壊で守備が実効的に弱る・基準非破壊）。</summary>
        [Test]
        public void EffectiveGarrison_士気で守備が弱る()
        {
            Assert.AreEqual(15000f, GroundInvasionRules.EffectiveGarrison(15000f, 1f), Eps);
            Assert.AreEqual(7500f, GroundInvasionRules.EffectiveGarrison(15000f, 0.5f), Eps);
            Assert.AreEqual(0f, GroundInvasionRules.EffectiveGarrison(15000f, 0f), Eps);
        }
    }
}
