using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>陣形の使用可否（軍神専用ゲート・車懸かり）。</summary>
    public class FormationAccessRulesTests
    {
        [Test]
        public void Legendary_Formations_Are_Transcendent_Only()
        {
            Assert.IsTrue(FormationAccessRules.IsTranscendentOnly(Formation.車懸かり)); // 旋回突撃
            Assert.IsTrue(FormationAccessRules.IsTranscendentOnly(Formation.八陣));     // 石兵八陣
        }

        [Test]
        public void Other_Formations_Are_Not_Restricted()
        {
            foreach (Formation f in System.Enum.GetValues(typeof(Formation)))
            {
                if (f == Formation.車懸かり || f == Formation.八陣) continue;
                Assert.IsFalse(FormationAccessRules.IsTranscendentOnly(f), f.ToString());
                // 非軍神でも使える
                Assert.IsTrue(FormationAccessRules.CanUse(f, false), f.ToString());
                Assert.IsTrue(FormationAccessRules.CanUse(f, true), f.ToString());
            }
        }

        [Test]
        public void Legendary_Formations_Require_Transcendent()
        {
            Assert.IsFalse(FormationAccessRules.CanUse(Formation.車懸かり, false)); // 凡将は不可
            Assert.IsTrue(FormationAccessRules.CanUse(Formation.車懸かり, true));    // 軍神のみ可
            Assert.IsFalse(FormationAccessRules.CanUse(Formation.八陣, false));     // 凡将は不可
            Assert.IsTrue(FormationAccessRules.CanUse(Formation.八陣, true));        // 天才のみ可
        }
    }
}
