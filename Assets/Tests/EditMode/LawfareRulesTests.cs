using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 法律戦（lawfare）の純ロジック（#1380・限定戦争ULW-2）のEditModeテスト。
    /// 既定 LawfareParams（梃子1.0/収縮重み0.9/解釈0.8/非正当化0.85/逆効果0.7・偽善指数2/支持0.8）で期待値を固定。
    /// </summary>
    public class LawfareRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>法的梃子＝専門性×規範的高み×梃子scale（法に通じ大義名分があるほど強い・どちらか欠ければ痩せる）。</summary>
        [Test]
        public void LegalLeverage_専門性と規範の積()
        {
            // 0.8 × 0.75 × 1.0 = 0.6
            Assert.AreEqual(0.6f, LawfareRules.LegalLeverage(0.8f, 0.75f), Eps);
            // 規範的高みゼロなら梃子は立たない（積＝両方が要る）
            Assert.AreEqual(0f, LawfareRules.LegalLeverage(0.9f, 0f), Eps);
        }

        /// <summary>行動空間の収縮＝梃子×相手の脆弱性×収縮重み（正当な行動を違法に見せかけ手足を縛る）。</summary>
        [Test]
        public void ActionSpaceConstriction_梃子と脆弱性で縛る()
        {
            // 0.6 × 0.7 × 0.9 = 0.378
            Assert.AreEqual(0.378f, LawfareRules.ActionSpaceConstriction(0.6f, 0.7f), Eps);
        }

        /// <summary>条約解釈の優位＝条項の曖昧さ×自国法務×解釈scale（曖昧な条項を自国有利に読み相手を縛る）。</summary>
        [Test]
        public void TreatyInterpretationAdvantage_曖昧さを突く()
        {
            // 0.5 × 0.8 × 0.8 = 0.32
            Assert.AreEqual(0.32f, LawfareRules.TreatyInterpretationAdvantage(0.5f, 0.8f), Eps);
            // 曖昧さゼロ（明文の条約）なら解釈で取れる余地なし
            Assert.AreEqual(0f, LawfareRules.TreatyInterpretationAdvantage(0f, 1f), Eps);
        }

        /// <summary>相手の非正当化＝相手の行動の問題性×法的フレーミング技能×非正当化scale（国際法違反として世論を味方に）。</summary>
        [Test]
        public void DelegitimizeOpponent_法的フレーミング()
        {
            // 0.7 × 0.6 × 0.85 = 0.357
            Assert.AreEqual(0.357f, LawfareRules.DelegitimizeOpponent(0.7f, 0.6f), Eps);
        }

        /// <summary>法律戦の逆効果＝濫用×偽善^2×逆効果scale（ダブルスタンダードが自国の信用を蝕む諸刃）。</summary>
        [Test]
        public void LawfareBacklash_偽善は非線形に効く()
        {
            // 0.8 × (0.5^2) × 0.7 = 0.8 × 0.25 × 0.7 = 0.14
            Assert.AreEqual(0.14f, LawfareRules.LawfareBacklash(0.8f, 0.5f), Eps);
            // 偽善が無ければ濫用しても逆効果は生じない（恣意性が露わでなければ罰されない）
            Assert.AreEqual(0f, LawfareRules.LawfareBacklash(1f, 0f), Eps);
            // 偽善が指数で効く＝偽善1.0は0.5の4倍の逆効果（0.14→0.56）
            Assert.AreEqual(0.56f, LawfareRules.LawfareBacklash(0.8f, 1f), Eps);
        }

        /// <summary>国際支持＝規範的高み×法律戦技能×支持scale（合法性を示して第三国の支持を呼ぶ）。</summary>
        [Test]
        public void InternationalSupport_合法性が味方を呼ぶ()
        {
            // 0.9 × 0.7 × 0.8 = 0.504
            Assert.AreEqual(0.504f, LawfareRules.InternationalSupport(0.9f, 0.7f), Eps);
        }

        /// <summary>非対称の法律戦＝弱者の法務技能×強者の露出×収縮重み（軍事弱者が法を盾に強者を縛る）。</summary>
        [Test]
        public void AsymmetricLawfare_弱者が強者を法で縛る()
        {
            // 0.7 × 0.8 × 0.9 = 0.504
            Assert.AreEqual(0.504f, LawfareRules.AsymmetricLawfare(0.7f, 0.8f), Eps);
            // 強者に法的露出がなければ弱者は縛れない
            Assert.AreEqual(0f, LawfareRules.AsymmetricLawfare(1f, 0f), Eps);
        }

        /// <summary>法律戦の支配判定＝行動空間の収縮が閾値以上（法廷・規範の場で優位を確定）。</summary>
        [Test]
        public void IsLawfareDominant_閾値で支配を判定()
        {
            Assert.IsTrue(LawfareRules.IsLawfareDominant(0.6f, 0.5f));
            Assert.IsTrue(LawfareRules.IsLawfareDominant(0.5f, 0.5f), "閾値ちょうどは支配成立");
            Assert.IsFalse(LawfareRules.IsLawfareDominant(0.4f, 0.5f));
        }

        /// <summary>全入力はクランプされる（範囲外でも 0..1 に丸まり例外を投げない）。</summary>
        [Test]
        public void 入力はクランプされる()
        {
            Assert.AreEqual(1f, LawfareRules.LegalLeverage(2f, 5f), Eps, "上限クランプ＝1.0×1.0×1.0");
            Assert.AreEqual(0f, LawfareRules.ActionSpaceConstriction(-1f, 0.5f), Eps, "負入力は0へ");
        }
    }
}
