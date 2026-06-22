using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>具申のタブ分類：区分→タブの対応。</summary>
    public class PetitionTabRulesTests
    {
        [Test]
        public void TabOf_GroupsCategories()
        {
            Assert.AreEqual(PetitionTab.軍事, PetitionTabRules.TabOf(PetitionCategory.予算));
            Assert.AreEqual(PetitionTab.軍事, PetitionTabRules.TabOf(PetitionCategory.編制));
            Assert.AreEqual(PetitionTab.軍事, PetitionTabRules.TabOf(PetitionCategory.作戦));
            Assert.AreEqual(PetitionTab.軍事, PetitionTabRules.TabOf(PetitionCategory.海賊狩り));
            Assert.AreEqual(PetitionTab.人事, PetitionTabRules.TabOf(PetitionCategory.人事));
            Assert.AreEqual(PetitionTab.政治, PetitionTabRules.TabOf(PetitionCategory.政治));
            Assert.AreEqual(PetitionTab.個人, PetitionTabRules.TabOf(PetitionCategory.建白));
        }
    }
}
