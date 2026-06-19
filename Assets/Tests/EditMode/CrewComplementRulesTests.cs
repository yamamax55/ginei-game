using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>乗員定数（艦の人員配置と充足）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class CrewComplementRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 必要乗員はサイズ比例_自動化で省人化され下限が残る()
        {
            // サイズ1・自動化0.5 → 1*100*(1-0.5*0.6)=100*0.7=70。
            Assert.AreEqual(70f, CrewComplementRules.RequiredComplement(1.0f, 0.5f), Eps);
            // サイズ2・自動化0 → 2*100*1=200。
            Assert.AreEqual(200f, CrewComplementRules.RequiredComplement(2.0f, 0.0f), Eps);
            // 自動化1でも省人化幅0.6 → 100*0.4=40（無人にはならない）。
            Assert.AreEqual(40f, CrewComplementRules.RequiredComplement(1.0f, 1.0f), Eps);
        }

        [Test]
        public void 充足率は実乗員割る必要数_過剰は頭打ち_必要0は満員扱い()
        {
            Assert.AreEqual(0.7f, CrewComplementRules.ManningRatio(70f, 100f), Eps);
            // 過剰は1で頭打ち。
            Assert.AreEqual(1f, CrewComplementRules.ManningRatio(150f, 100f), Eps);
            // 必要0（人手不要）は常に満員。
            Assert.AreEqual(1f, CrewComplementRules.ManningRatio(50f, 0f), Eps);
        }

        [Test]
        public void シフト過酷は不足とテンポの積_満員なら過酷ゼロ()
        {
            // (1-0.7)*1.0*1.0=0.3。
            Assert.AreEqual(0.3f, CrewComplementRules.WatchSystemStrain(0.7f, 1.0f), Eps);
            // 満員（充足1）なら過酷さ0。
            Assert.AreEqual(0f, CrewComplementRules.WatchSystemStrain(1.0f, 1.0f), Eps);
        }

        [Test]
        public void 部署効率は充足と練度の積_どちらか欠けると回らない()
        {
            // 0.8*0.75=0.6。
            Assert.AreEqual(0.6f, CrewComplementRules.StationEffectiveness(0.8f, 0.75f), Eps);
            // 練度0（人だけ）なら部署は回らない。
            Assert.AreEqual(0f, CrewComplementRules.StationEffectiveness(0.8f, 0.0f), Eps);
        }

        [Test]
        public void 省人化の効力は自動化と信頼性の積_当てにならぬ自動化は補えない()
        {
            // 0.8*0.5=0.4。
            Assert.AreEqual(0.4f, CrewComplementRules.AutomationOffset(0.8f, 0.5f), Eps);
            // 信頼性0なら故障する自動化＝人手の代わりにならない。
            Assert.AreEqual(0f, CrewComplementRules.AutomationOffset(0.8f, 0.0f), Eps);
        }

        [Test]
        public void 疲労はシフト過酷と任務期間の積_余裕運用は疲れない()
        {
            // 0.4*1.0*0.5=0.2。
            Assert.AreEqual(0.2f, CrewComplementRules.FatigueAccumulation(0.4f, 1.0f), Eps);
            // 過酷さ0なら期間が長くても疲労0。
            Assert.AreEqual(0f, CrewComplementRules.FatigueAccumulation(0.0f, 5.0f), Eps);
        }

        [Test]
        public void 戦闘配置は充足から死傷を引く_死傷が充足を上回れば配置不能()
        {
            // 1.0-0.4=0.6。
            Assert.AreEqual(0.6f, CrewComplementRules.BattleStationsCapacity(1.0f, 0.4f), Eps);
            // 死傷が充足を上回れば0でクランプ。
            Assert.AreEqual(0f, CrewComplementRules.BattleStationsCapacity(0.5f, 0.7f), Eps);
        }

        [Test]
        public void 充足判定はしきい値で足切り()
        {
            // 既定しきい0.8。
            Assert.IsTrue(CrewComplementRules.IsAdequatelyManned(0.8f));
            Assert.IsFalse(CrewComplementRules.IsAdequatelyManned(0.7f));
            // 明示しきい値委譲版。
            Assert.IsTrue(CrewComplementRules.IsAdequatelyManned(0.6f, 0.5f));
        }

        [Test]
        public void 入力はクランプされ負やオーバーフローに安全()
        {
            // 負のサイズ→必要0。
            Assert.AreEqual(0f, CrewComplementRules.RequiredComplement(-5f, 0.5f), Eps);
            // 充足・テンポ・練度等は0..1にクランプされ発散しない。
            Assert.AreEqual(0f, CrewComplementRules.WatchSystemStrain(2.0f, 2.0f), Eps);
            Assert.AreEqual(1f, CrewComplementRules.StationEffectiveness(2.0f, 2.0f), Eps);
            Assert.AreEqual(1f, CrewComplementRules.AutomationOffset(2.0f, 2.0f), Eps);
        }

        [Test]
        public void 物語_自動化巡洋艦が定員割れと損耗を耐え抜く()
        {
            var p = CrewComplementParams.Default;
            // サイズ1.5・自動化0.5の巡洋艦の必要乗員。
            float required = CrewComplementRules.RequiredComplement(1.5f, 0.5f, p);
            // 1.5*100*0.7=105。
            Assert.AreEqual(105f, required, Eps);

            // 実乗員84名（定員割れ）。充足84/105=0.8。
            float manning = CrewComplementRules.ManningRatio(84f, required, p);
            Assert.AreEqual(0.8f, manning, Eps);
            // しきい0.8ちょうどなので辛うじて充足とみなす。
            Assert.IsTrue(CrewComplementRules.IsAdequatelyManned(manning));

            // 高テンポ運用でシフトが過酷化：(1-0.8)*1.0=0.2。
            float strain = CrewComplementRules.WatchSystemStrain(manning, 1.0f, p);
            Assert.AreEqual(0.2f, strain, Eps);
            // 長期任務（期間2.0）で疲労が積もる：0.2*2.0*0.5=0.2。
            float fatigue = CrewComplementRules.FatigueAccumulation(strain, 2.0f, p);
            Assert.AreEqual(0.2f, fatigue, Eps);

            // 被弾で死傷率0.3。戦闘配置維持：0.8-0.3=0.5。
            float battle = CrewComplementRules.BattleStationsCapacity(manning, 0.3f, p);
            Assert.AreEqual(0.5f, battle, Eps);
            // 半数の部署はまだ機能＝壊滅はしていない。
            Assert.Greater(battle, 0f);
        }
    }
}
