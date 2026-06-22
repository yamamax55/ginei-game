using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 肖像プロンプト生成の不変量（③アート一貫性）。画風アンカーの常在・決定論・
    /// 勢力/アーキタイプの反映・シードの安定/上書きを固定し、一貫性の核を守る。
    /// </summary>
    public class PortraitPromptRulesTests
    {
        private static AdmiralData A(string name = "テスト提督")
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.staffOfficers = new AdmiralData[0];
            a.admiralName = name;
            return a;
        }

        [Test]
        public void Prompt_AlwaysContainsStyleAnchor()
        {
            Assert.IsTrue(PortraitPromptRules.BuildPrompt(A()).Contains(PortraitPromptRules.StylePreamble),
                "全肖像で共通の画風アンカーが含まれる（一貫性の核）");
        }

        [Test]
        public void Prompt_IsDeterministic()
        {
            var a = A("ラインハルト");
            a.isKaiser = true;
            Assert.AreEqual(PortraitPromptRules.BuildPrompt(a), PortraitPromptRules.BuildPrompt(a),
                "同一データは同一プロンプト");
        }

        [Test]
        public void Prompt_ReflectsFaction()
        {
            var imp = A(); imp.faction = Faction.帝国;
            var all = A(); all.faction = Faction.同盟;
            Assert.IsTrue(PortraitPromptRules.BuildPrompt(imp).Contains("imperial"), "帝国は帝国軍服");
            Assert.IsTrue(PortraitPromptRules.BuildPrompt(all).Contains("republican"), "同盟は共和国海軍服");
        }

        [Test]
        public void Prompt_ReflectsFactionName_OverEnum()
        {
            // factionName があれば多勢力の制服を使い、enum 既定より優先する
            var a = A(); a.faction = Faction.帝国; a.factionName = "黎明評議会";
            string p = PortraitPromptRules.BuildPrompt(a);
            Assert.IsTrue(p.Contains("Dawn Council"), "factionName の勢力制服を採用");
            Assert.IsFalse(p.Contains("imperial dress"), "enum 既定（帝国軍服）にフォールバックしない");
        }

        [Test]
        public void Prompt_FallsBackToEnum_WhenFactionNameUnknownOrEmpty()
        {
            var empty = A(); empty.faction = Faction.同盟; empty.factionName = "";
            Assert.IsTrue(PortraitPromptRules.BuildPrompt(empty).Contains("republican"),
                "factionName 空なら enum で解決（後方互換）");
            var unknown = A(); unknown.faction = Faction.帝国; unknown.factionName = "存在しない勢力";
            Assert.IsTrue(PortraitPromptRules.BuildPrompt(unknown).Contains("imperial"),
                "未登録の factionName は enum へフォールバック");
        }

        [Test]
        public void UniformCatalog_AllFactionsRegistered()
        {
            foreach (var name in new[] { "黎明評議会", "碧晶公国", "自由港同位体", "灯心自由市ファロス", "鉄環兵団" })
                Assert.IsTrue(PortraitUniformCatalog.Has(name), name + " の制服が登録されている");
            Assert.IsNull(PortraitUniformCatalog.For(""), "空は null");
            Assert.IsNull(PortraitUniformCatalog.For("未登録"), "未登録は null");
        }

        [Test]
        public void Prompt_ReflectsArchetype()
        {
            var a = A("ラインハルト"); a.isKaiser = true;
            Assert.IsTrue(PortraitPromptRules.BuildPrompt(a).Contains("blond"), "覇王アーキタイプ＝金髪の外見");
        }

        [Test]
        public void Prompt_IncludesAppearanceNote()
        {
            var a = A(); a.appearanceNote = "monocle and scar";
            Assert.IsTrue(PortraitPromptRules.BuildPrompt(a).Contains("monocle and scar"), "外見ヒントが反映される");
        }

        [Test]
        public void Prompt_ExpressionFromStats()
        {
            var a = A(); a.attack = 95; a.intelligence = 10; a.leadership = 10;
            Assert.IsTrue(PortraitPromptRules.BuildPrompt(a).Contains("aggressive"), "高攻撃は攻撃的な表情");
        }

        [Test]
        public void Seed_OverrideRespected()
        {
            var a = A(); a.portraitSeed = 12345;
            Assert.AreEqual(12345, PortraitPromptRules.DeriveSeed(a), "portraitSeed 指定が優先");
        }

        [Test]
        public void Seed_StableAndPositive_WhenUnset()
        {
            var a = A("ヤン・ウェンリー");
            int s1 = PortraitPromptRules.DeriveSeed(a);
            int s2 = PortraitPromptRules.DeriveSeed(a);
            Assert.AreEqual(s1, s2, "未指定でも名前から決定論的（再生成で同一人物）");
            Assert.Greater(s1, 0, "シードは正");
        }

        [Test]
        public void NullSafe()
        {
            Assert.AreEqual(PortraitPromptRules.StylePreamble, PortraitPromptRules.BuildPrompt(null));
            Assert.AreEqual(0, PortraitPromptRules.DeriveSeed(null));
        }
    }
}
