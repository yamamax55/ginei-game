using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 給与・俸給（#1969 WAGE・<see cref="WageRules"/>）を固定する：人物の俸給(WAGE-1)、POPの給与のざっくり集計(WAGE-2)、
    /// 実質賃金と生活水準(WAGE-3)、人件費(WAGE-4)、俸給未払いペナルティ(WAGE-5)。
    /// </summary>
    public class WageTests
    {
        private static PayScale Scale() => PayScale.Default; // 基本俸10/ステップ0.5/能力加給0.3

        // ===== WAGE-1 人物の俸給 =====
        [Test]
        public void PersonSalary_FromRankAndAbility()
        {
            var s = Scale();
            Assert.AreEqual(10f, WageRules.RankBasePay(1, s), 1e-3f);   // tier1＝基本俸
            Assert.AreEqual(30f, WageRules.RankBasePay(5, s), 1e-3f);   // 10×(1+0.5×4)
            Assert.AreEqual(55f, WageRules.RankBasePay(10, s), 1e-3f);  // 10×(1+0.5×9)
            Assert.AreEqual(0f, WageRules.RankBasePay(0, s), 1e-3f);
            Assert.AreEqual(1f, WageRules.AbilityFactor(50f, s), 1e-3f);
            Assert.AreEqual(1.3f, WageRules.AbilityFactor(100f, s), 1e-3f);
            Assert.AreEqual(0.7f, WageRules.AbilityFactor(0f, s), 1e-3f);
            Assert.AreEqual(30f, WageRules.PersonSalary(5, 50f, s), 1e-3f);
            Assert.AreEqual(39f, WageRules.PersonSalary(5, 100f, s), 1e-3f); // 30×1.3
            // 提督データから（階級5・統率50→俸給30）
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.staffOfficers = new AdmiralData[0];
            a.rankTier = 5; a.leadership = 50;
            Assert.AreEqual(30f, WageRules.PersonSalary(a, s), 1e-3f);
            Assert.AreEqual(0f, WageRules.PersonSalary((AdmiralData)null, s), 1e-3f);
        }

        // ===== WAGE-2 POPの給与（ざっくり） =====
        [Test]
        public void PopWages_RoughAggregate()
        {
            Assert.AreEqual(1500f, WageRules.PopWageBill(1000f, 1.5f), 1e-3f);
            // 星系：生産年齢×就業率×賃金率（既存ルールと整合）
            var p = new Province { demographics = new Population(100f, 600f, 100f) };
            float expected = OccupationRules.WorkingAge(p) * OccupationRules.EmploymentRate(p) * 1.5f;
            Assert.AreEqual(expected, WageRules.ProvincePopWages(p, 1.5f), 1e-3f);
            // 勢力合計＝星系群の和
            var p2 = new Province { demographics = new Population(50f, 400f, 50f) };
            float sum = WageRules.ProvincePopWages(p, 1.5f) + WageRules.ProvincePopWages(p2, 1.5f);
            Assert.AreEqual(sum, WageRules.AggregatePopWages(new List<Province> { p, p2 }, 1.5f), 1e-3f);
            Assert.AreEqual(0f, WageRules.AggregatePopWages(null, 1.5f), 1e-4f);
        }

        // ===== WAGE-3 実質賃金・生活水準 =====
        [Test]
        public void RealWage_AndLivingStandard()
        {
            Assert.AreEqual(24f, WageRules.RealWage(30f, 1.25f), 1e-3f);  // 名目30/物価1.25
            Assert.AreEqual(30f, WageRules.RealWage(30f, 0f), 1e-3f);     // 物価0は名目そのまま
            Assert.AreEqual(1.2f, WageRules.WageLivingStandard(24f, 20f), 1e-3f);
            Assert.AreEqual(1f, WageRules.WageLivingStandard(24f, 0f), 1e-3f);
        }

        // ===== WAGE-4 人件費 =====
        [Test]
        public void PayrollCost_SumsRoster()
        {
            var s = Scale();
            AdmiralData Make(int tier, int lead)
            {
                var a = ScriptableObject.CreateInstance<AdmiralData>();
                a.staffOfficers = new AdmiralData[0];
                a.rankTier = tier; a.leadership = lead;
                return a;
            }
            var roster = new List<AdmiralData> { Make(5, 50), Make(5, 50), null }; // 30+30、null は無視
            Assert.AreEqual(60f, WageRules.PayrollCost(roster, s), 1e-3f);
            Assert.AreEqual(0f, WageRules.PayrollCost(null, s), 1e-4f);
        }

        // ===== WAGE-5 未払い =====
        [Test]
        public void ArrearsPenalty_FromUnpaidRatio()
        {
            Assert.AreEqual(0f, WageRules.ArrearsPenalty(0f, 100f), 1e-4f);    // 全額支給＝ペナルティなし
            Assert.AreEqual(0.5f, WageRules.ArrearsPenalty(50f, 100f), 1e-4f); // 半額未払い
            Assert.AreEqual(1f, WageRules.ArrearsPenalty(150f, 100f), 1e-4f);  // 全額未払いで頭打ち
            Assert.AreEqual(0f, WageRules.ArrearsPenalty(50f, 0f), 1e-4f);     // 無給任務は対象外
        }
    }
}
