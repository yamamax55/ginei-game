using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>地上侵攻の二者化（#131・ドメイン・ダウン後の地上戦）の純ロジック検証。
    /// 守備隊が上回れば停滞、攻撃側の純優勢ぶんだけ侵略が進む、守備隊は削られ対空砲火で艦も傷つける。</summary>
    public class GroundInvasionRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>純優勢＝攻撃側地上戦力−守備隊（劣勢は0）。</summary>
        [Test]
        public void NetAdvantage_攻撃側が上回るぶんだけ()
        {
            Assert.AreEqual(5000f, GroundInvasionRules.NetAdvantage(20000f, 15000f), Eps);
            Assert.AreEqual(0f, GroundInvasionRules.NetAdvantage(10000f, 15000f), Eps); // 劣勢は0
            Assert.AreEqual(0f, GroundInvasionRules.NetAdvantage(-5f, 0f), Eps);        // 負はクランプ
        }

        /// <summary>守備隊が攻撃側以上なら侵攻は停滞（占領できない）。</summary>
        [Test]
        public void DefendersHolding_守備が上回れば停滞()
        {
            Assert.IsTrue(GroundInvasionRules.DefendersHolding(10000f, 15000f));  // 守備優勢
            Assert.IsTrue(GroundInvasionRules.DefendersHolding(15000f, 15000f));  // 拮抗も停滞
            Assert.IsFalse(GroundInvasionRules.DefendersHolding(20000f, 15000f)); // 攻撃優勢で進む
        }

        /// <summary>侵略速度係数＝純優勢÷基準（守備が上回れば0・上限でクランプ）。</summary>
        [Test]
        public void InvasionRateFactor_純優勢に比例し上限でクランプ()
        {
            var p = new GroundInvasionParams(0.01f, 0.001f, 10000f, 3f);
            Assert.AreEqual(0f, GroundInvasionRules.InvasionRateFactor(8000f, 10000f, p), Eps);   // 劣勢＝停滞
            Assert.AreEqual(0.5f, GroundInvasionRules.InvasionRateFactor(15000f, 10000f, p), Eps); // 優勢5000/基準10000
            Assert.AreEqual(3f, GroundInvasionRules.InvasionRateFactor(100000f, 10000f, p), Eps);  // 上限3でクランプ
        }

        /// <summary>守備隊の損耗＝攻撃側地上戦力×係数×dt（残量を超えない）。</summary>
        [Test]
        public void GarrisonLosses_攻撃規模と時間で削られ残量で下げ止まる()
        {
            var p = new GroundInvasionParams(0.01f, 0.001f, 10000f, 3f);
            Assert.AreEqual(200f, GroundInvasionRules.GarrisonLosses(20000f, 15000f, p, 1f), Eps); // 20000×0.01×1
            Assert.AreEqual(15000f, GroundInvasionRules.GarrisonLosses(20000f, 15000f, p, 1000f), Eps); // 残量で頭打ち
            Assert.AreEqual(0f, GroundInvasionRules.GarrisonLosses(20000f, 15000f, p, 0f), Eps); // dt0
        }

        /// <summary>守備隊の対空砲火＝残存守備隊×係数（削られるほど弱まる・0で止む）。</summary>
        [Test]
        public void AttackerCasualtyRate_守備隊に比例して艦を傷つける()
        {
            var p = new GroundInvasionParams(0.01f, 0.001f, 10000f, 3f);
            Assert.AreEqual(15f, GroundInvasionRules.AttackerCasualtyRate(15000f, p), Eps); // 15000×0.001
            Assert.AreEqual(0f, GroundInvasionRules.AttackerCasualtyRate(0f, p), Eps);      // 守備全滅＝砲火なし
        }
    }
}
