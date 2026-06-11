using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 足による投票＝孟子の徳治版（MENC-2 #1566）の純ロジック検証。
    /// 統治の魅力・移住の引力・仁政の流入・苛政の流出・人口移動量・好循環・頭脳流出・過疎化判定を担保。
    /// </summary>
    public class PopulationMigrationRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>統治の魅力＝仁政×（1−重税）×治安。仁政・軽税・治安が揃うほど高い。</summary>
        [Test]
        public void GovernanceAttractiveness_仁政軽税治安で高く重税治安難で削る()
        {
            // 仁政1・無税・治安1＝魅力最大（govern=0.6×1+0.4=1.0）。
            Assert.AreEqual(1.0f, PopulationMigrationRules.GovernanceAttractiveness(1f, 0f, 1f), Eps);
            // 重税1は魅力を殺す（1−1=0）。
            Assert.AreEqual(0f, PopulationMigrationRules.GovernanceAttractiveness(1f, 1f, 1f), Eps);
            // 治安0は無法＝魅力0。
            Assert.AreEqual(0f, PopulationMigrationRules.GovernanceAttractiveness(1f, 0f, 0f), Eps);
            // 仁政0・無税・治安1＝素地のみ（0.6×0+0.4=0.4）。
            Assert.AreEqual(0.4f, PopulationMigrationRules.GovernanceAttractiveness(0f, 0f, 1f), Eps);
        }

        /// <summary>移住の引力＝移住先と出身地の魅力差。魅力の高い側へ流れる。</summary>
        [Test]
        public void MigrationPull_魅力の高い側へ正の引力()
        {
            // 出身0.3・移住先0.8＝出ていきたい（正）。
            Assert.AreEqual(0.5f, PopulationMigrationRules.MigrationPull(0.3f, 0.8f), Eps);
            // 逆＝逆流（負）。
            Assert.AreEqual(-0.5f, PopulationMigrationRules.MigrationPull(0.8f, 0.3f), Eps);
            // 同じ魅力＝動機なし。
            Assert.AreEqual(0f, PopulationMigrationRules.MigrationPull(0.5f, 0.5f), Eps);
        }

        /// <summary>仁政が隣国の苛政から民を吸引する流入＝徳×隣の暴政×流入率×dt。</summary>
        [Test]
        public void BenevolenceInflux_仁政が隣の苛政から民を吸引する()
        {
            // 仁政0.8・隣の苛政1・dt2＝0.8×1×0.1×2=0.16。
            Assert.AreEqual(0.16f, PopulationMigrationRules.BenevolenceInflux(0.8f, 1f, 2f), Eps);
            // 隣が善政（苛政0）なら吸引できない。
            Assert.AreEqual(0f, PopulationMigrationRules.BenevolenceInflux(0.8f, 0f, 2f), Eps);
            // 自国も苛政（仁政0）なら吸引できない。
            Assert.AreEqual(0f, PopulationMigrationRules.BenevolenceInflux(0f, 1f, 2f), Eps);
        }

        /// <summary>苛政が民を流出させる＝苛政×移動の自由×流出率×dt。逃げ道があるほど逃散。</summary>
        [Test]
        public void MisruleExodus_苛政と逃げ道で民が逃散する()
        {
            // 苛政1・自由0.5・dt2＝1×0.5×0.1×2=0.1。
            Assert.AreEqual(0.1f, PopulationMigrationRules.MisruleExodus(1f, 0.5f, 2f), Eps);
            // 善政（苛政0）なら流出しない。
            Assert.AreEqual(0f, PopulationMigrationRules.MisruleExodus(0f, 1f, 2f), Eps);
            // 封鎖（移動の自由0）なら逃げられない。
            Assert.AreEqual(0f, PopulationMigrationRules.MisruleExodus(1f, 0f, 2f), Eps);
        }

        /// <summary>人口移動量＝引力勾配×移動の自由×最大流量率×dt。自由がなければ動けない。</summary>
        [Test]
        public void PopulationFlow_引力勾配と移動の自由で動く()
        {
            // 勾配1・全開1・dt1＝1×1×0.05×1=0.05（流入）。
            Assert.AreEqual(0.05f, PopulationMigrationRules.PopulationFlow(1f, 1f, 1f), Eps);
            // 勾配負＝流出（負）。
            Assert.AreEqual(-0.05f, PopulationMigrationRules.PopulationFlow(-1f, 1f, 1f), Eps);
            // 移動の自由0＝誰も動けない。
            Assert.AreEqual(0f, PopulationMigrationRules.PopulationFlow(1f, 0f, 1f), Eps);
        }

        /// <summary>好循環＝民が集まれば国力が増す。流出（負）なら回らない。</summary>
        [Test]
        public void VirtuousCycleBonus_流入が国力を増し流出は回らない()
        {
            // 流入0.2＝0.2×0.3=0.06。
            Assert.AreEqual(0.06f, PopulationMigrationRules.VirtuousCycleBonus(0.2f), Eps);
            // 流出（負）は好循環を回さない。
            Assert.AreEqual(0f, PopulationMigrationRules.VirtuousCycleBonus(-0.2f), Eps);
        }

        /// <summary>苛政は有能な民（流動性が高い）を先に失う＝頭脳流出。</summary>
        [Test]
        public void BrainDrainFromMisrule_苛政が有能な民を先に失う()
        {
            // 苛政1・才能流動性1＝1×1×0.5=0.5（有能層が先に去る）。
            Assert.AreEqual(0.5f, PopulationMigrationRules.BrainDrainFromMisrule(1f, 1f), Eps);
            // 善政（苛政0）なら有能層も去らない。
            Assert.AreEqual(0f, PopulationMigrationRules.BrainDrainFromMisrule(0f, 1f), Eps);
            // 才能が逃げられない（流動性0）なら頭脳流出なし。
            Assert.AreEqual(0f, PopulationMigrationRules.BrainDrainFromMisrule(1f, 0f), Eps);
        }

        /// <summary>苛政で流出が閾値を超えると過疎化する。</summary>
        [Test]
        public void IsDepopulating_累積流出が閾値超で過疎化()
        {
            // 既定閾値0.3。流出0.4＝過疎化。
            Assert.IsTrue(PopulationMigrationRules.IsDepopulating(0.4f));
            // 流出0.2＝まだ持ちこたえる。
            Assert.IsFalse(PopulationMigrationRules.IsDepopulating(0.2f));
            // 明示閾値も効く。
            Assert.IsTrue(PopulationMigrationRules.IsDepopulating(0.6f, 0.5f));
            Assert.IsFalse(PopulationMigrationRules.IsDepopulating(0.4f, 0.5f));
        }
    }
}
