using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 神権国家（宗教国家・#172-175）の統治係数の純ロジックを既定 Params で固定。
    /// 同信仰=正統性で安定+／異端=安定−／戦闘性の弾圧・寛容で penalty 緩和／神権成立判定／決定論を担保。
    /// </summary>
    public class TheocracyRulesTests
    {
        const float Tol = 1e-4f;
        static readonly TheocracyParams P = TheocracyParams.Default;

        static ReligionDef Def(string name, float milit, float tol)
            => new ReligionDef(name, 0.5f, milit, 0.5f, tol, "", ReligionFamily.アブラハム系);

        [Test]
        public void Legitimacy_同信仰で正のボーナス_信仰強いほど大()
        {
            float low = TheocracyRules.LegitimacyBonus("カトリック", "カトリック", 0.3f, P);
            float high = TheocracyRules.LegitimacyBonus("カトリック", "カトリック", 1f, P);
            Assert.Greater(low, 0f);
            Assert.Greater(high, low);
            Assert.AreEqual(P.legitimacyBonus, high, Tol);
        }

        [Test]
        public void Legitimacy_異信仰はゼロ()
        {
            Assert.AreEqual(0f, TheocracyRules.LegitimacyBonus("カトリック", "イスラム教", 1f, P), Tol);
            Assert.AreEqual(0f, TheocracyRules.LegitimacyBonus("", "", 1f, P), Tol);
        }

        [Test]
        public void Heresy_異端で負のpenalty()
        {
            ReligionDef cath = Def("カトリック", 0.5f, 0.5f);
            float pen = TheocracyRules.HeresyPenalty(cath, "イスラム教", 1f, P);
            Assert.Less(pen, 0f);
        }

        [Test]
        public void Heresy_同信仰と無信仰はゼロ()
        {
            ReligionDef cath = Def("カトリック", 0.5f, 0.5f);
            Assert.AreEqual(0f, TheocracyRules.HeresyPenalty(cath, "カトリック", 1f, P), Tol);
            Assert.AreEqual(0f, TheocracyRules.HeresyPenalty(cath, "", 1f, P), Tol);
        }

        [Test]
        public void Heresy_戦闘的な国教ほどpenaltyを抑える()
        {
            ReligionDef militant = Def("カトリック", 1f, 0f);
            ReligionDef mild = Def("カトリック", 0f, 0f);
            float penM = TheocracyRules.HeresyPenalty(militant, "イスラム教", 1f, P);
            float penL = TheocracyRules.HeresyPenalty(mild, "イスラム教", 1f, P);
            // 戦闘的＝弾圧で不満を抑える＝penalty が 0 に近い（絶対値が小さい）。
            Assert.Greater(penM, penL);
        }

        [Test]
        public void Heresy_寛容な国教ほどpenaltyを抑える()
        {
            ReligionDef tolerant = Def("カトリック", 0f, 1f);
            ReligionDef harsh = Def("カトリック", 0f, 0f);
            float penT = TheocracyRules.HeresyPenalty(tolerant, "イスラム教", 1f, P);
            float penH = TheocracyRules.HeresyPenalty(harsh, "イスラム教", 1f, P);
            Assert.Greater(penT, penH);
        }

        [Test]
        public void StabilityModifier_同信仰で正_異信仰で負()
        {
            ReligionDef cath = Def("カトリック", 0.5f, 0.5f);
            float same = TheocracyRules.StabilityModifier(cath, "カトリック", 0.9f, P);
            float diff = TheocracyRules.StabilityModifier(cath, "イスラム教", 0.9f, P);
            Assert.Greater(same, 0f);
            Assert.Less(diff, 0f);
        }

        [Test]
        public void StabilityModifier_国教なしはゼロ()
        {
            ReligionDef none = ReligionCatalog.None;
            Assert.AreEqual(0f, TheocracyRules.StabilityModifier(none, "カトリック", 1f, P), Tol);
        }

        [Test]
        public void StabilityModifier_名前オーバーロードはカタログ解決と一致()
        {
            float byName = TheocracyRules.StabilityModifier("カトリック", "カトリック", 0.8f, P);
            float byDef = TheocracyRules.StabilityModifier(ReligionCatalog.For("カトリック"), "カトリック", 0.8f, P);
            Assert.AreEqual(byDef, byName, Tol);
        }

        [Test]
        public void StabilityModifier_世俗国家はゼロ()
        {
            Assert.AreEqual(0f, TheocracyRules.StabilityModifier("", "カトリック", 1f, P), Tol);
        }

        [Test]
        public void IsTheocratic_国教ありで浸透していれば成立()
        {
            Assert.IsTrue(TheocracyRules.IsTheocratic("カトリック", 0.8f));
            Assert.IsFalse(TheocracyRules.IsTheocratic("カトリック", 0.2f));
            Assert.IsFalse(TheocracyRules.IsTheocratic("", 1f));
        }

        [Test]
        public void 決定論_同入力は同出力()
        {
            float a = TheocracyRules.StabilityModifier("カトリック", "イスラム教", 0.7f, P);
            float b = TheocracyRules.StabilityModifier("カトリック", "イスラム教", 0.7f, P);
            Assert.AreEqual(a, b, Tol);
        }
    }
}
