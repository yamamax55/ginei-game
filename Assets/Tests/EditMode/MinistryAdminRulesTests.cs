using System.Collections.Generic;
using NUnit.Framework;
using Ginei;
using MP = Ginei.MinistryAdminRules.MinistryParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 省庁ツリーの行政効率→内政寄与（配下官僚の文才×充足率×朝廷の権威で減衰）を固定する。
    /// </summary>
    public class MinistryAdminRulesTests
    {
        private static (List<Ministry> tree, Ministry top) BuildTree()
        {
            var tree = new List<Ministry>();
            var top = new Ministry(1, "太政官", OfficeDomain.内政) { staffSlots = 2 };
            var minbu = new Ministry(2, "民部省", OfficeDomain.内政) { staffSlots = 2 };
            tree.Add(top); tree.Add(minbu);
            MinistryRules.AttachChild(tree, 1, 2);
            return (tree, top);
        }

        private static System.Func<int, Person> Lookup(Dictionary<int, Person> d) => id => d.TryGetValue(id, out var p) ? p : null;

        [Test]
        public void SlotsUnder_SumsTreeCapacity()
        {
            var (tree, top) = BuildTree();
            Assert.AreEqual(4, MinistryAdminRules.SlotsUnder(top, tree)); // 太政官2 + 民部省2
        }

        [Test]
        public void StaffingEfficiency_ZeroWhenUnstaffed()
        {
            var (tree, top) = BuildTree();
            Assert.AreEqual(0f, MinistryAdminRules.StaffingEfficiency(top, tree, _ => null), 1e-4f);
            Assert.AreEqual(0f, MinistryAdminRules.AdministrativeBonus(top, tree, _ => null, 1f, MP.Default), 1e-4f);
        }

        [Test]
        public void StaffingEfficiency_AbilityTimesFill()
        {
            var (tree, top) = BuildTree();
            var people = new Dictionary<int, Person>
            {
                { 11, new Person(11, "官", Faction.帝国, PersonRole.文民){ operation=80, intelligence=80 } },
                { 12, new Person(12, "官", Faction.帝国, PersonRole.文民){ operation=80, intelligence=80 } },
            };
            MinistryRules.AssignOfficial(tree, 2, 11);
            MinistryRules.AssignOfficial(tree, 2, 12);
            // 平均文才0.8 × 充足率(2/4=0.5) = 0.4
            Assert.AreEqual(0.4f, MinistryAdminRules.StaffingEfficiency(top, tree, Lookup(people)), 1e-3f);
            // 権威1・上限8 → 3.2／権威0 → 0（名実の乖離）
            Assert.AreEqual(3.2f, MinistryAdminRules.AdministrativeBonus(top, tree, Lookup(people), 1f, MP.Default), 1e-3f);
            Assert.AreEqual(0f, MinistryAdminRules.AdministrativeBonus(top, tree, Lookup(people), 0f, MP.Default), 1e-4f);
        }

        [Test]
        public void AdministrativeBonus_RisesWithCapableStaffAndAuthority()
        {
            var (tree, top) = BuildTree();
            var weak = new Dictionary<int, Person> { { 11, new Person(11, "凡", Faction.帝国, PersonRole.文民){ operation=20, intelligence=20 } } };
            MinistryRules.AssignOfficial(tree, 2, 11);
            float low = MinistryAdminRules.AdministrativeBonus(top, tree, Lookup(weak), 0.4f, MP.Default);

            var (tree2, top2) = BuildTree();
            var strong = new Dictionary<int, Person>
            {
                { 21, new Person(21, "俊", Faction.帝国, PersonRole.文民){ operation=90, intelligence=90 } },
                { 22, new Person(22, "俊", Faction.帝国, PersonRole.文民){ operation=90, intelligence=90 } },
            };
            MinistryRules.AssignOfficial(tree2, 2, 21);
            MinistryRules.AssignOfficial(tree2, 2, 22);
            float high = MinistryAdminRules.AdministrativeBonus(top2, tree2, Lookup(strong), 0.9f, MP.Default);

            Assert.Greater(high, low);
        }
    }
}
