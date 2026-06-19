using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 宇宙要塞防衛（イゼルローン型）の純ロジック検証。装甲×スクリーンの耐久・主砲の制圧範囲・
    /// 駐留艦隊の相乗・攻囲持久・死角・攻撃側損耗・攻略困難さ・持ちこたえ判定を担保する。
    /// </summary>
    public class FortressDefenseRulesTests
    {
        const float Eps = 0.0001f;
        const float EpsPow = 0.001f; // Sqrt/Pow を含む経路

        /// <summary>装甲厚×(1＋スクリーン寄与)。100×(1+0.5×0.5)=125。</summary>
        [Test]
        public void FortressArmor_装甲とスクリーンで耐久()
        {
            float armor = FortressDefenseRules.FortressArmor(100f, 0.5f);
            Assert.AreEqual(125f, armor, EpsPow);
        }

        /// <summary>スクリーン0なら装甲そのまま。スクリーン満タンなら最大上乗せ（×1.5）。</summary>
        [Test]
        public void FortressArmor_スクリーンの寄与は0から最大まで()
        {
            Assert.AreEqual(100f, FortressDefenseRules.FortressArmor(100f, 0f), Eps);    // 上乗せなし
            Assert.AreEqual(150f, FortressDefenseRules.FortressArmor(100f, 1f), EpsPow); // 100×1.5
            Assert.AreEqual(150f, FortressDefenseRules.FortressArmor(100f, 5f), EpsPow); // 1超はClamp01
        }

        /// <summary>主砲の制圧範囲＝射程×門数を飽和正規化で0..1へ。range2×count(3×0.5)=3 → 3/4=0.75。</summary>
        [Test]
        public void MainGunCoverage_射程と門数で制圧範囲()
        {
            float cov = FortressDefenseRules.MainGunCoverage(2f, 3f);
            Assert.AreEqual(0.75f, cov, EpsPow);
        }

        /// <summary>射程か門数が0なら撃てない＝制圧範囲0。多いほど1へ近づくが越えない。</summary>
        [Test]
        public void MainGunCoverage_0門は制圧範囲ゼロで上限は1()
        {
            Assert.AreEqual(0f, FortressDefenseRules.MainGunCoverage(10f, 0f), Eps);
            Assert.AreEqual(0f, FortressDefenseRules.MainGunCoverage(0f, 10f), Eps);
            float huge = FortressDefenseRules.MainGunCoverage(1000f, 1000f);
            Assert.Less(huge, 1f);
            Assert.Greater(huge, 0.99f);
        }

        /// <summary>要塞＋駐留艦隊の相乗＝和＋synergy×幾何平均。100+100+0.3×sqrt(10000)=230。</summary>
        [Test]
        public void GarrisonFleetSynergy_艦隊が要塞砲の傘で相乗()
        {
            float syn = FortressDefenseRules.GarrisonFleetSynergy(100f, 100f);
            Assert.AreEqual(230f, syn, EpsPow);
            // 片方0なら相乗なし＝和そのもの
            Assert.AreEqual(100f, FortressDefenseRules.GarrisonFleetSynergy(100f, 0f), Eps);
        }

        /// <summary>攻囲持久＝装甲×(1＋備蓄寄与)。125×(1+0.4×1)=175。備蓄0でも装甲ぶんは耐える。</summary>
        [Test]
        public void SiegeResistance_備蓄が攻囲への持久を伸ばす()
        {
            Assert.AreEqual(175f, FortressDefenseRules.SiegeResistance(125f, 1f), EpsPow);
            Assert.AreEqual(125f, FortressDefenseRules.SiegeResistance(125f, 0f), Eps);
        }

        /// <summary>死角＝(1−制圧範囲)×構造×係数。(1-0.75)×1×0.5=0.125。制圧範囲が広いほど穴は減る。</summary>
        [Test]
        public void BlindSpot_制圧範囲が広いほど死角が減る()
        {
            Assert.AreEqual(0.125f, FortressDefenseRules.BlindSpot(0.75f, 1f), Eps);
            // 完全制圧（制圧範囲1）なら死角ゼロ
            Assert.AreEqual(0f, FortressDefenseRules.BlindSpot(1f, 1f), Eps);
        }

        /// <summary>攻撃側損耗＝制圧範囲×敵露出×係数。0.75×0.8×0.6=0.36。露出を抑えれば損耗は減る。</summary>
        [Test]
        public void AttackerAttrition_制圧範囲と露出で攻撃側を削る()
        {
            Assert.AreEqual(0.36f, FortressDefenseRules.AttackerAttrition(0.75f, 0.8f), Eps);
            Assert.AreEqual(0f, FortressDefenseRules.AttackerAttrition(0.75f, 0f), Eps); // 露出ゼロは無損耗
        }

        /// <summary>攻略困難さ＝装甲×(1＋損耗)×(1＋相乗正規化)。125×1.36×(1+230/231)≈339.264。</summary>
        [Test]
        public void AssaultDifficulty_装甲損耗相乗で難攻不落()
        {
            float diff = FortressDefenseRules.AssaultDifficulty(125f, 0.36f, 230f);
            Assert.AreEqual(339.264f, diff, 0.01f);
            // 損耗も相乗も無ければ装甲そのまま
            Assert.AreEqual(125f, FortressDefenseRules.AssaultDifficulty(125f, 0f, 0f), Eps);
        }

        /// <summary>持ちこたえ判定＝攻囲持久＋攻略困難さが既定しきい値100以上で保持。</summary>
        [Test]
        public void IsFortressHolding_持久と困難さの和でしきい値判定()
        {
            Assert.IsTrue(FortressDefenseRules.IsFortressHolding(60f, 40f));   // 100≥100
            Assert.IsFalse(FortressDefenseRules.IsFortressHolding(30f, 40f));  // 70<100
            Assert.IsTrue(FortressDefenseRules.IsFortressHolding(0f, 50f, 50f)); // 明示しきい値
        }

        /// <summary>
        /// 物語＝イゼルローン要塞の攻防。厚い装甲＋スクリーンの要塞が、トゥールハンマー型主砲の傘の下に
        /// 駐留艦隊を抱え、満タンの備蓄で攻囲に耐える。攻囲艦隊は身を晒して制圧範囲に削られ、
        /// 攻略困難さは装甲を大きく超え、要塞は持ちこたえる（難攻不落）。
        /// 同じ要塞でも主砲が無く艦隊も備蓄も尽きれば、攻略困難さは装甲どまりで持ちこたえられない。
        /// </summary>
        [Test]
        public void Story_難攻不落の要塞は持ちこたえ裸の要塞は陥落する()
        {
            // --- 万全のイゼルローン ---
            float armor = FortressDefenseRules.FortressArmor(100f, 0.5f);           // 125
            float coverage = FortressDefenseRules.MainGunCoverage(2f, 3f);          // 0.75
            float synergy = FortressDefenseRules.GarrisonFleetSynergy(armor, 100f); // 125+100+0.3×sqrt(12500)
            float resist = FortressDefenseRules.SiegeResistance(armor, 1f);         // 175
            float attrition = FortressDefenseRules.AttackerAttrition(coverage, 0.8f); // 0.36
            float difficulty = FortressDefenseRules.AssaultDifficulty(armor, attrition, synergy);

            Assert.Greater(coverage, 0.5f);          // 主砲が周囲を広く覆う
            Assert.Greater(synergy, armor + 100f);   // 艦隊の傘で和を超える相乗
            Assert.Greater(difficulty, armor);       // 装甲を超える攻略困難さ
            Assert.IsTrue(FortressDefenseRules.IsFortressHolding(resist, difficulty)); // 持ちこたえる

            // --- 主砲喪失・艦隊壊滅・備蓄枯渇の裸の要塞（損耗も相乗も無い） ---
            float bareCoverage = FortressDefenseRules.MainGunCoverage(2f, 0f);      // 0（撃てない）
            float bareResist = FortressDefenseRules.SiegeResistance(armor, 0f);     // 125
            float bareAttrition = FortressDefenseRules.AttackerAttrition(bareCoverage, 0.8f); // 0
            // 艦隊も尽きた裸の要塞＝相乗0を渡す（傘の下に守る艦隊が無い）。
            float bareDifficulty = FortressDefenseRules.AssaultDifficulty(armor, bareAttrition, 0f);

            Assert.AreEqual(0f, bareCoverage, Eps);
            Assert.AreEqual(armor, bareDifficulty, Eps); // 損耗も艦隊相乗も無く装甲どまり
            Assert.IsFalse(FortressDefenseRules.IsFortressHolding(bareResist - 100f, bareDifficulty - 100f)); // 25+25<100
            Assert.Less(bareDifficulty, difficulty);     // 万全時より攻略しやすい
        }
    }
}
