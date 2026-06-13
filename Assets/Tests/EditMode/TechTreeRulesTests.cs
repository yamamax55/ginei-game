using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 技術ツリー配線（Almagest・#1065・TechTreeRules）の純ロジックテスト。前提充足での解禁・1つ欠けると解禁不可・
    /// 研究フロンティア・技術の深さ・目標までの総コスト積算・飛び級不可を既定Paramsの具体値で固定。
    /// </summary>
    public class TechTreeRulesTests
    {
        // ツリー：基礎 → 中級（前提=基礎） → 上級（前提=中級+基礎B）。基礎B は独立した基礎技術。
        //   基礎(cost5) ─┐
        //                ├→ 中級(cost10) ─┐
        //   基礎B(cost3) ─┼──────────────┴→ 上級(cost20)
        private static TechNode Kiso() => new TechNode("基礎", null, 5f, 0);
        private static TechNode KisoB() => new TechNode("基礎B", null, 3f, 0);
        private static TechNode Chukyu() => new TechNode("中級", new List<string> { "基礎" }, 10f, 1);
        private static TechNode Jokyu() => new TechNode("上級", new List<string> { "中級", "基礎B" }, 20f, 2);

        private static List<TechNode> AllNodes()
            => new List<TechNode> { Kiso(), KisoB(), Chukyu(), Jokyu() };

        /// <summary>前提を全て習得済みなら解禁可能（中級は基礎を習得後に解禁）。</summary>
        [Test]
        public void IsUnlockable_前提充足で解禁できる()
        {
            var researched = new List<string> { "基礎" };
            Assert.IsTrue(TechTreeRules.IsUnlockable(Chukyu(), researched), "基礎を習得済みなら中級は解禁可能");
        }

        /// <summary>前提が1つでも欠けると解禁できない（上級は中級or基礎Bが欠けると不可）。</summary>
        [Test]
        public void IsUnlockable_前提が1つ欠けると解禁できない()
        {
            // 中級だけ習得、基礎Bが未習得＝上級は解禁できない。
            var researched = new List<string> { "基礎", "中級" };
            Assert.IsFalse(TechTreeRules.IsUnlockable(Jokyu(), researched), "基礎Bが欠けると上級は解禁不可");

            // 両方揃えば解禁可能。
            researched.Add("基礎B");
            Assert.IsTrue(TechTreeRules.IsUnlockable(Jokyu(), researched), "前提が全て揃えば上級は解禁可能");
        }

        /// <summary>基礎技術（前提なし）は最初から解禁可能・習得済みは解禁対象外。</summary>
        [Test]
        public void IsUnlockable_基礎は最初から可能で習得済みは対象外()
        {
            var researched = new List<string>();
            Assert.IsTrue(TechTreeRules.IsUnlockable(Kiso(), researched), "基礎は前提なしで解禁可能");

            researched.Add("基礎");
            Assert.IsFalse(TechTreeRules.IsUnlockable(Kiso(), researched), "習得済みは解禁対象でない");
        }

        /// <summary>前提充足率と不足数（上級は前提2のうち1充足で0.5・不足1）。</summary>
        [Test]
        public void PrerequisitesMet_充足率と不足数()
        {
            var researched = new List<string> { "中級" }; // 上級の前提2のうち1充足
            Assert.AreEqual(0.5f, TechTreeRules.PrerequisitesMet(Jokyu(), researched), 1e-4f, "前提2中1充足=0.5");
            Assert.AreEqual(1, TechTreeRules.PrerequisitesMissing(Jokyu(), researched), "あと基礎Bの1つ");

            Assert.AreEqual(1f, TechTreeRules.PrerequisitesMet(Kiso(), researched), 1e-4f, "基礎は前提なし=1.0");
        }

        /// <summary>今研究できる技術＝前提が揃った最前線（初期は基礎・基礎Bのみ）。</summary>
        [Test]
        public void AvailableTechs_前提が揃った最前線だけ返す()
        {
            var researched = new List<string>();
            var avail = TechTreeRules.AvailableTechs(AllNodes(), researched);

            var ids = new HashSet<string>();
            foreach (var n in avail) ids.Add(n.techId);
            Assert.AreEqual(2, avail.Count, "初期は基礎と基礎Bの2つだけ");
            Assert.IsTrue(ids.Contains("基礎") && ids.Contains("基礎B"), "解禁対象は2つの基礎技術");
            Assert.IsFalse(ids.Contains("中級"), "中級は前提未充足で対象外");
        }

        /// <summary>研究フロンティア＝解禁直前の応用技術（基礎を除く）。基礎習得後は中級が現れる。</summary>
        [Test]
        public void ResearchableFrontier_基礎を除いた解禁直前を返す()
        {
            // 初期＝基礎技術しか研究できない＝フロンティアは空。
            Assert.AreEqual(0, TechTreeRules.ResearchableFrontier(AllNodes(), new List<string>()).Count, "初期は応用フロンティア無し");

            // 基礎を習得すると中級が解禁直前に。
            var frontier = TechTreeRules.ResearchableFrontier(AllNodes(), new List<string> { "基礎" });
            Assert.AreEqual(1, frontier.Count, "中級だけがフロンティア");
            Assert.AreEqual("中級", frontier[0].techId);
        }

        /// <summary>技術の深さ＝基礎から何段か（基礎=1・中級=2・上級=3）。</summary>
        [Test]
        public void TechDepth_基礎から積み上げた段数()
        {
            var all = AllNodes();
            Assert.AreEqual(1, TechTreeRules.TechDepth(Kiso(), all), "基礎=深さ1");
            Assert.AreEqual(2, TechTreeRules.TechDepth(Chukyu(), all), "中級=深さ2");
            Assert.AreEqual(3, TechTreeRules.TechDepth(Jokyu(), all), "上級=中級(2)+1=深さ3");
        }

        /// <summary>目標までの総コスト＝未習得の前提を辿って積算（基礎を飛ばせない＝道のりを払う）。</summary>
        [Test]
        public void TotalCostToReach_未習得前提を辿って積算する()
        {
            var all = AllNodes();
            // 何も習得していない＝上級到達には 上級20+中級10+基礎5+基礎B3 = 38。
            Assert.AreEqual(38f, TechTreeRules.TotalCostToReach(Jokyu(), all, new List<string>()), 1e-4f, "上級20+中級10+基礎5+基礎B3=38");

            // 基礎と中級を習得済みなら 上級20+基礎B3 = 23（習得済みは払わない）。
            Assert.AreEqual(23f, TechTreeRules.TotalCostToReach(Jokyu(), all, new List<string> { "基礎", "中級" }), 1e-4f, "上級20+基礎B3=23");

            // 既に習得済みなら0。
            Assert.AreEqual(0f, TechTreeRules.TotalCostToReach(Jokyu(), all, new List<string> { "上級" }), 1e-4f, "習得済みは道のり0");
        }

        /// <summary>飛び級は不可＝技術は積み上げ（前提を飛ばして解禁できない）。</summary>
        [Test]
        public void Leapfrog_技術は積み上げで飛び級できない()
        {
            Assert.IsFalse(TechTreeRules.Leapfrog(Jokyu(), new List<string>()), "前提未充足でも飛び級不可");
            Assert.IsFalse(TechTreeRules.Leapfrog(Jokyu(), new List<string> { "中級", "基礎B" }), "前提充足でも飛び級ではない");
        }
    }
}
