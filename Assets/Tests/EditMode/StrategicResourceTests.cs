using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 希少資源（戦略資源 #178）を固定する：定義の一表（種類×用途×希少度）、惑星の偏在産出（鉱床のある惑星だけ・安定度比例）、
    /// 備蓄の加減・消費（非負・部分消費しない）、星系集約、用途ゲート。すべて純ロジック。
    /// </summary>
    public class StrategicResourceTests
    {
        [Test]
        public void Info_Table_DefinesUseAndRarity()
        {
            Assert.AreEqual(4, StrategicResourceRules.All.Length); // 少数に絞る（タイクン回避）
            Assert.AreEqual(StrategicResourceUse.建艦, StrategicResourceRules.UseOf(StrategicResourceType.レアメタル));
            Assert.AreEqual(StrategicResourceUse.特殊兵器, StrategicResourceRules.UseOf(StrategicResourceType.反応物質));
            Assert.AreEqual(StrategicResourceUse.研究, StrategicResourceRules.UseOf(StrategicResourceType.超伝導体));
            Assert.AreEqual(StrategicResourceUse.改造, StrategicResourceRules.UseOf(StrategicResourceType.希少結晶));
            // 希少度は0..1で、結晶が最も稀
            foreach (var t in StrategicResourceRules.All)
                Assert.That(StrategicResourceRules.Rarity(t), Is.InRange(0f, 1f));
            Assert.Greater(StrategicResourceRules.Rarity(StrategicResourceType.希少結晶),
                           StrategicResourceRules.Rarity(StrategicResourceType.レアメタル));
        }

        [Test]
        public void ProvinceRate_OnlyWithDeposit_ScaledByAbundanceAndStability()
        {
            // 鉱床なし＝0（偏在＝大半の惑星は産出しない）
            var bare = new Province(1, "民主", 100f) { stability = GovernanceRules.MaxStability };
            Assert.IsFalse(bare.hasStrategicResource);
            Assert.AreEqual(0f, StrategicResourceRules.ProvinceRate(bare), 1e-6f);

            // 鉱床あり・豊富さ1・安定MAX → 基本率そのまま
            var rich = new Province(2, "専制", 100f)
            {
                hasStrategicResource = true,
                strategicResource = StrategicResourceType.レアメタル,
                strategicAbundance = 1f,
                stability = GovernanceRules.MaxStability,
            };
            float expected = StrategicResourceRules.Info(StrategicResourceType.レアメタル).baseRate;
            Assert.AreEqual(expected, StrategicResourceRules.ProvinceRate(rich), 1e-4f);

            // 豊富さ半分 → 半減
            rich.strategicAbundance = 0.5f;
            Assert.AreEqual(expected * 0.5f, StrategicResourceRules.ProvinceRate(rich), 1e-4f);
        }

        [Test]
        public void Stockpile_AddConsume_FloorsAndNoPartial()
        {
            var s = new StrategicResourceStockpile();
            Assert.IsTrue(s.IsEmpty);
            s.Add(StrategicResourceType.反応物質, 5f);
            Assert.AreEqual(5f, s.Get(StrategicResourceType.反応物質), 1e-4f);
            Assert.IsFalse(s.TryConsume(StrategicResourceType.反応物質, 10f)); // 不足＝部分消費しない
            Assert.AreEqual(5f, s.Get(StrategicResourceType.反応物質), 1e-4f);
            Assert.IsTrue(s.TryConsume(StrategicResourceType.反応物質, 3f));
            Assert.AreEqual(2f, s.Get(StrategicResourceType.反応物質), 1e-4f);
            s.Add(StrategicResourceType.反応物質, -100f); // 下限0
            Assert.AreEqual(0f, s.Get(StrategicResourceType.反応物質), 1e-4f);
        }

        [Test]
        public void ProduceFromSystem_AggregatesOnlyDepositPlanets()
        {
            var s = new StrategicResourceStockpile();
            var planets = new List<Province>
            {
                new Province(3, "民主", 100f) { stability = GovernanceRules.MaxStability }, // 鉱床なし＝寄与しない
                new Province(3, "民主", 100f)
                {
                    hasStrategicResource = true, strategicResource = StrategicResourceType.超伝導体,
                    strategicAbundance = 1f, stability = GovernanceRules.MaxStability,
                },
            };
            StrategicResourceRules.ProduceFromSystem(s, planets, 1f);
            Assert.AreEqual(StrategicResourceRules.Info(StrategicResourceType.超伝導体).baseRate,
                            s.Get(StrategicResourceType.超伝導体), 1e-4f);
            Assert.AreEqual(0f, s.Get(StrategicResourceType.レアメタル), 1e-6f); // 鉱床のない種は出ない
        }

        [Test]
        public void CanAfford_GatesByStock()
        {
            var s = new StrategicResourceStockpile();
            s.Add(StrategicResourceType.希少結晶, 2f);
            Assert.IsTrue(StrategicResourceRules.CanAfford(s, StrategicResourceType.希少結晶, 2f));   // ちょうどで可
            Assert.IsFalse(StrategicResourceRules.CanAfford(s, StrategicResourceType.希少結晶, 3f));  // 不足
            Assert.IsFalse(StrategicResourceRules.CanAfford(s, StrategicResourceType.レアメタル, 1f)); // 未保有
        }
    }
}
