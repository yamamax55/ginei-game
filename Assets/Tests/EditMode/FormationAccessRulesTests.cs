using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>陣形の使用可否（軍神専用ゲート・車懸かり）。</summary>
    public class FormationAccessRulesTests
    {
        [Test]
        public void Kurumagakari_Is_Transcendent_Only()
        {
            Assert.IsTrue(FormationAccessRules.IsTranscendentOnly(Formation.車懸かり));
        }

        [Test]
        public void Other_Formations_Are_Not_Restricted()
        {
            foreach (Formation f in System.Enum.GetValues(typeof(Formation)))
            {
                if (f == Formation.車懸かり) continue;
                Assert.IsFalse(FormationAccessRules.IsTranscendentOnly(f), f.ToString());
                // 非軍神でも使える
                Assert.IsTrue(FormationAccessRules.CanUse(f, false), f.ToString());
                Assert.IsTrue(FormationAccessRules.CanUse(f, true), f.ToString());
            }
        }

        [Test]
        public void Kurumagakari_Requires_Transcendent()
        {
            Assert.IsFalse(FormationAccessRules.CanUse(Formation.車懸かり, false)); // 凡将は不可
            Assert.IsTrue(FormationAccessRules.CanUse(Formation.車懸かり, true));    // 軍神のみ可
        }
    }
}
