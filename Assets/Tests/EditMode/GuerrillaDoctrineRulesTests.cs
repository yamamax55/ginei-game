using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// GuerrillaDoctrineRules（遊撃戦ドクトリン・#1396）の EditMode テスト。
    /// 既定 Params の具体値で期待値を固定し、交戦回避の有効性・状況別モード選択・回廊妨害・ヒットアンドラン・
    /// 強敵への消耗・戦力集中の拒否・住民基盤への依存・遊撃戦の構え判定を担保する。
    /// </summary>
    public class GuerrillaDoctrineRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>交戦回避＝機動×地形遮蔽で効く。両輪が揃えば高く、片方欠ければ相乗ぶんが落ちる。</summary>
        [Test]
        public void EvasionEffectiveness_両輪が揃うほど高い()
        {
            // 機動1・遮蔽1＝相加1×0.5＋相乗1×0.5＝1。
            Assert.AreEqual(1f, GuerrillaDoctrineRules.EvasionEffectiveness(1f, 1f), Eps);
            // 機動1・遮蔽0＝相加(1×0.5)×0.5＋相乗(0)×0.5＝0.25＝片方では伸びない。
            Assert.AreEqual(0.25f, GuerrillaDoctrineRules.EvasionEffectiveness(1f, 0f), Eps);
            // 機動0・遮蔽0＝0。
            Assert.AreEqual(0f, GuerrillaDoctrineRules.EvasionEffectiveness(0f, 0f), Eps);
            // 入力はクランプされる。
            Assert.AreEqual(1f, GuerrillaDoctrineRules.EvasionEffectiveness(5f, 5f), Eps);
        }

        /// <summary>状況別モード選択＝十六字訣（敵進めば退き・敵駐すれば擾し・敵疲れれば打つ）。</summary>
        [Test]
        public void SelectModeForSituation_十六字訣に従う()
        {
            // 好機が立てば打つ（敵疲れれば我打つ）。opportunity 0.7 >= 0.6。
            Assert.AreEqual(OperationalMode.奇襲打撃,
                GuerrillaDoctrineRules.SelectModeForSituation(0.5f, 0.5f, 0.7f));
            // 劣勢で好機薄＝退く（敵進めば我退き）。敵0.9−自軍0.3＝0.6 >= 0.3。
            Assert.AreEqual(OperationalMode.交戦回避,
                GuerrillaDoctrineRules.SelectModeForSituation(0.9f, 0.3f, 0.1f));
            // 拮抗で好機薄＝擾乱（敵駐すれば我擾し）。差0、好機0.2。
            Assert.AreEqual(OperationalMode.回廊妨害,
                GuerrillaDoctrineRules.SelectModeForSituation(0.5f, 0.5f, 0.2f));
        }

        /// <summary>回廊妨害＝妨害の激しさ×敵の補給露出。露出が無ければ効かない。</summary>
        [Test]
        public void CorridorHarassment_妨害と露出の積()
        {
            Assert.AreEqual(0.6f, GuerrillaDoctrineRules.CorridorHarassment(0.8f, 0.75f), Eps);
            // 敵が補給線を晒していなければ効かない。
            Assert.AreEqual(0f, GuerrillaDoctrineRules.CorridorHarassment(1f, 0f), Eps);
        }

        /// <summary>ヒットアンドラン＝打撃の窓×退却速度。速く退けねば反撃で捕まる。</summary>
        [Test]
        public void HitAndRun_窓と退却速度の相乗()
        {
            Assert.AreEqual(0.5f, GuerrillaDoctrineRules.HitAndRun(1f, 0.5f), Eps);
            // 退却できねば成果ゼロ（捕まる）。
            Assert.AreEqual(0f, GuerrillaDoctrineRules.HitAndRun(1f, 0f), Eps);
        }

        /// <summary>強敵への消耗＝妨害×回避×レート×dt で時間に比例し飽和する。</summary>
        [Test]
        public void AttritionOnStronger_時間に比例して積む()
        {
            // 妨害0.8×回避0.5×レート0.2×dt2 ＝ 0.16。
            Assert.AreEqual(0.16f, GuerrillaDoctrineRules.AttritionOnStronger(0.8f, 0.5f, 2f), Eps);
            // 回避できねば（自軍が削られる）消耗戦は持続できずゼロ。
            Assert.AreEqual(0f, GuerrillaDoctrineRules.AttritionOnStronger(0.8f, 0f, 2f), Eps);
            // 飽和して1を超えない。
            Assert.AreEqual(1f, GuerrillaDoctrineRules.AttritionOnStronger(1f, 1f, 100f), Eps);
        }

        /// <summary>戦力集中の拒否＝分散度そのもの（分散すれば決戦の的を与えない）。</summary>
        [Test]
        public void ForceConcentrationDenial_分散度を返す()
        {
            Assert.AreEqual(0.7f, GuerrillaDoctrineRules.ForceConcentrationDenial(0.7f), Eps);
            Assert.AreEqual(1f, GuerrillaDoctrineRules.ForceConcentrationDenial(2f), Eps);
            Assert.AreEqual(0f, GuerrillaDoctrineRules.ForceConcentrationDenial(-1f), Eps);
        }

        /// <summary>住民基盤への依存＝支持を下限0.2〜1へ写像（民心が海）。</summary>
        [Test]
        public void PopularBaseReliance_支持を下限から写像()
        {
            // 支持1＝1。
            Assert.AreEqual(1f, GuerrillaDoctrineRules.PopularBaseReliance(1f), Eps);
            // 支持0＝下限0.2（最低限の自力）。
            Assert.AreEqual(0.2f, GuerrillaDoctrineRules.PopularBaseReliance(0f), Eps);
            // 支持0.5＝Lerp(0.2,1,0.5)＝0.6。
            Assert.AreEqual(0.6f, GuerrillaDoctrineRules.PopularBaseReliance(0.5f), Eps);
        }

        /// <summary>遊撃戦の構え判定＝回避×妨害が閾値以上（両者の相乗が要る）。</summary>
        [Test]
        public void IsGuerrillaPosture_回避と妨害の相乗で判定()
        {
            // 回避0.8×妨害0.7＝0.56 >= 閾値0.5＝機能。
            Assert.IsTrue(GuerrillaDoctrineRules.IsGuerrillaPosture(0.8f, 0.7f, 0.5f));
            // 妨害できねば（ただ逃げるだけ）構えは成立しない。
            Assert.IsFalse(GuerrillaDoctrineRules.IsGuerrillaPosture(1f, 0f, 0.5f));
            // 回避できねば（捕まって決戦）構えは成立しない。
            Assert.IsFalse(GuerrillaDoctrineRules.IsGuerrillaPosture(0f, 1f, 0.5f));
        }

        /// <summary>既定 Params の具体値を固定（回帰防止）。</summary>
        [Test]
        public void Default_既定値が固定されている()
        {
            var p = GuerrillaDoctrineParams.Default;
            Assert.AreEqual(0.5f, p.EvasionMobilityWeight, Eps);
            Assert.AreEqual(0.5f, p.EvasionSynergyShare, Eps);
            Assert.AreEqual(0.6f, p.StrikeOpportunityThreshold, Eps);
            Assert.AreEqual(0.3f, p.EvasionDisadvantageThreshold, Eps);
            Assert.AreEqual(0.2f, p.AttritionRate, Eps);
            Assert.AreEqual(0.2f, p.PopularBaseFloor, Eps);
        }
    }
}
