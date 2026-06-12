using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>教育・学習塾（#2025・<see cref="CramSchoolRules"/>）：月謝(EDU-1)・講師人件費(EDU-2)・生徒講師比(EDU-3)・利益(EDU-4)。</summary>
    public class CramSchoolTests
    {
        [Test]
        public void Tuition_AndInstructorCost()
        {
            Assert.AreEqual(15000000f, CramSchoolRules.TuitionRevenue(500, 30000f), 1e0f);
            Assert.AreEqual(6000000f, CramSchoolRules.InstructorCost(20, 300000f), 1e0f);
        }

        [Test]
        public void Ratio_AndProfit()
        {
            Assert.AreEqual(25f, CramSchoolRules.StudentToInstructorRatio(500, 20), 1e-3f); // 1講師25生徒
            Assert.AreEqual(5000000f, CramSchoolRules.CramSchoolProfit(15000000f, 6000000f, 4000000f), 1e0f);
        }
    }
}
