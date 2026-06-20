using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>TaxBracketRules（累進課税 内政・財政 #161 拡張）の純ロジック検証。</summary>
    public class TaxBracketRulesTests
    {
        // 0〜100=0%、100〜200=10%、200超=20%
        private static List<TaxBracketRules.Bracket> Brackets() => new List<TaxBracketRules.Bracket>
        {
            new TaxBracketRules.Bracket(0f, 0f),
            new TaxBracketRules.Bracket(100f, 0.10f),
            new TaxBracketRules.Bracket(200f, 0.20f),
        };

        [Test]
        public void TaxFor_BelowFirstThreshold_NoTax()
        {
            Assert.AreEqual(0f, TaxBracketRules.TaxFor(80f, Brackets()), 1e-5f);
        }

        [Test]
        public void TaxFor_MiddleBracket_MarginalOnly()
        {
            // 所得150 → (150-100)*0.10 = 5
            Assert.AreEqual(5f, TaxBracketRules.TaxFor(150f, Brackets()), 1e-5f);
        }

        [Test]
        public void TaxFor_TopBracket_StacksMarginals()
        {
            // 所得300 → (200-100)*0.10 + (300-200)*0.20 = 10 + 20 = 30
            Assert.AreEqual(30f, TaxBracketRules.TaxFor(300f, Brackets()), 1e-5f);
        }

        [Test]
        public void TaxFor_NullOrNonPositive_Zero()
        {
            Assert.AreEqual(0f, TaxBracketRules.TaxFor(150f, null), 1e-5f);
            Assert.AreEqual(0f, TaxBracketRules.TaxFor(-50f, Brackets()), 1e-5f);
        }

        [Test]
        public void TotalRevenue_SumsIndividuals()
        {
            var incomes = new List<float> { 80f, 150f, 300f }; // 0 + 5 + 30 = 35
            Assert.AreEqual(35f, TaxBracketRules.TotalRevenue(incomes, Brackets()), 1e-5f);
        }

        [Test]
        public void EffectiveRate_IsRevenueOverGross()
        {
            var incomes = new List<float> { 80f, 150f, 300f }; // 税35 / 総所得530
            Assert.AreEqual(35f / 530f, TaxBracketRules.EffectiveRate(incomes, Brackets()), 1e-5f);
        }

        [Test]
        public void Progressivity_PositiveForProgressiveSchedule()
        {
            var incomes = new List<float> { 80f, 300f };
            // 高所得実効率(30/300=0.10) - 低所得実効率(0) = 0.10
            Assert.AreEqual(0.10f, TaxBracketRules.Progressivity(incomes, Brackets()), 1e-5f);
        }

        [Test]
        public void Progressivity_FlatTaxNearZero()
        {
            var flat = new List<TaxBracketRules.Bracket> { new TaxBracketRules.Bracket(0f, 0.15f) };
            var incomes = new List<float> { 100f, 500f };
            Assert.AreEqual(0f, TaxBracketRules.Progressivity(incomes, flat), 1e-5f);
        }
    }
}
