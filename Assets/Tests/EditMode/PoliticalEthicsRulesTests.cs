using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>心情倫理vs責任倫理（ウェーバー『職業としての政治』・WEBR-2 #1528）の純ロジックテスト。</summary>
    public class PoliticalEthicsRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>倫理の軸＝結果重視が勝てば責任側(+)・動機重視が勝てば心情側(−)・拮抗で0・両0で中立。</summary>
        [Test]
        public void EthicsOrientation_動機と結果の綱引きを軸へ写す()
        {
            // 拮抗＝均衡（0）
            Assert.AreEqual(0f, PoliticalEthicsRules.EthicsOrientation(0.5f, 0.5f), Eps);
            // 結果重視のみ＝完全責任倫理(+1)
            Assert.AreEqual(1f, PoliticalEthicsRules.EthicsOrientation(0f, 1f), Eps);
            // 動機重視のみ＝完全心情倫理(−1)
            Assert.AreEqual(-1f, PoliticalEthicsRules.EthicsOrientation(1f, 0f), Eps);
            // 両方ゼロ＝中立0
            Assert.AreEqual(0f, PoliticalEthicsRules.EthicsOrientation(0f, 0f), Eps);
            // 結果がやや勝つ＝正側
            Assert.Greater(PoliticalEthicsRules.EthicsOrientation(0.4f, 0.6f), 0f);
        }

        /// <summary>類型弁別＝既定閾値0.3で内側は均衡・正は責任倫理・負は心情倫理。</summary>
        [Test]
        public void TypeOf_閾値で三類型を弁別する()
        {
            Assert.AreEqual(PoliticalEthicsType.均衡, PoliticalEthicsRules.TypeOf(0.2f));   // |0.2|<=0.3
            Assert.AreEqual(PoliticalEthicsType.均衡, PoliticalEthicsRules.TypeOf(-0.3f));  // 境界は均衡
            Assert.AreEqual(PoliticalEthicsType.責任倫理, PoliticalEthicsRules.TypeOf(0.6f));
            Assert.AreEqual(PoliticalEthicsType.心情倫理, PoliticalEthicsRules.TypeOf(-0.6f));
        }

        /// <summary>心情の純粋さ＝原則固守が高く妥協が無いほど高い（妥協で割り引く）。</summary>
        [Test]
        public void ConvictionPurity_妥協しないほど純粋()
        {
            // 原則固守1・妥協0＝純粋1
            Assert.AreEqual(1f, PoliticalEthicsRules.ConvictionPurity(1f, 0f), Eps);
            // 原則固守1・妥協0.5＝0.5
            Assert.AreEqual(0.5f, PoliticalEthicsRules.ConvictionPurity(1f, 0.5f), Eps);
            // 妥協1＝純粋0
            Assert.AreEqual(0f, PoliticalEthicsRules.ConvictionPurity(1f, 1f), Eps);
            // 原則固守0.8・妥協0.25＝0.6
            Assert.AreEqual(0.6f, PoliticalEthicsRules.ConvictionPurity(0.8f, 0.25f), Eps);
        }

        /// <summary>責任倫理の強さ＝見通しと説明責任の積（片方欠ければ成立しない）。</summary>
        [Test]
        public void ConsequenceResponsibility_見通しと責任の積()
        {
            Assert.AreEqual(0.72f, PoliticalEthicsRules.ConsequenceResponsibility(0.9f, 0.8f), Eps);
            // 説明責任ゼロ＝結果を見通しても責任倫理は成立しない
            Assert.AreEqual(0f, PoliticalEthicsRules.ConsequenceResponsibility(1f, 0f), Eps);
        }

        /// <summary>原則のコスト＝純粋さが悪い帰結を招くほど大きい＝無責任の罠（既定幅0.8）。</summary>
        [Test]
        public void PrincipleCost_純粋さが災いを招くと高コスト()
        {
            // 純粋1×悪帰結1×0.8＝0.8
            Assert.AreEqual(0.8f, PoliticalEthicsRules.PrincipleCost(1f, 1f), Eps);
            // 悪い帰結が無ければコストゼロ（純粋でも結果が良ければ罠にならない）
            Assert.AreEqual(0f, PoliticalEthicsRules.PrincipleCost(1f, 0f), Eps);
            // 純粋0.5×悪帰結0.5×0.8＝0.2
            Assert.AreEqual(0.2f, PoliticalEthicsRules.PrincipleCost(0.5f, 0.5f), Eps);
        }

        /// <summary>実用の摩耗＝結果を追って原則を捨てる魂の摩耗＝マキャヴェリズムへの堕落（既定幅0.8）。</summary>
        [Test]
        public void PragmaticErosion_原則放棄で魂が摩耗する()
        {
            // 責任1×原則放棄1×0.8＝0.8
            Assert.AreEqual(0.8f, PoliticalEthicsRules.PragmaticErosion(1f, 1f), Eps);
            // 原則を捨てなければ摩耗ゼロ（結果を追っても原則を保てば堕落しない）
            Assert.AreEqual(0f, PoliticalEthicsRules.PragmaticErosion(1f, 0f), Eps);
        }

        /// <summary>成熟した判断＝情熱（心情）と責任感の両方が要る幾何平均（片方欠ければ0）。</summary>
        [Test]
        public void MatureJudgment_情熱と責任の両方が要る()
        {
            // 両方1＝最高1
            Assert.AreEqual(1f, PoliticalEthicsRules.MatureJudgment(1f, 1f), Eps);
            // 責任ゼロ＝成熟しない（純粋な信念だけでは足りない）
            Assert.AreEqual(0f, PoliticalEthicsRules.MatureJudgment(1f, 0f), Eps);
            // 心情ゼロ＝成熟しない（結果計算だけでも足りない）
            Assert.AreEqual(0f, PoliticalEthicsRules.MatureJudgment(0f, 1f), Eps);
            // 0.64×0.81 の幾何平均＝0.72
            Assert.AreEqual(0.72f, PoliticalEthicsRules.MatureJudgment(0.64f, 0.81f), Eps);
            // 偏りより均衡が強い：両0.5 > 一方0.9他方0.1
            Assert.Greater(PoliticalEthicsRules.MatureJudgment(0.5f, 0.5f),
                           PoliticalEthicsRules.MatureJudgment(0.9f, 0.1f));
        }

        /// <summary>状況比例＝危機ほど責任倫理(+1)へ倒し、平時は元の軸を保つ。</summary>
        [Test]
        public void ProportionToReality_危機ほど結果倫理へ倒す()
        {
            // 平時（gravity0）＝元の軸を保つ
            Assert.AreEqual(-0.6f, PoliticalEthicsRules.ProportionToReality(-0.6f, 0f), Eps);
            // 危機極大（gravity1）＝完全に責任倫理(+1)へ
            Assert.AreEqual(1f, PoliticalEthicsRules.ProportionToReality(-0.6f, 1f), Eps);
            // 中間は責任側へ引き寄せられる（元より大きい）
            Assert.Greater(PoliticalEthicsRules.ProportionToReality(-0.6f, 0.5f), -0.6f);
        }

        /// <summary>無責任な理想主義＝純粋さが結果責任を閾値超えて上回ると true（既定差0.4）。</summary>
        [Test]
        public void IsIrresponsibleIdealism_純粋さが責任を上回ると無責任()
        {
            // 純粋1・責任0＝差1.0>0.4＝無責任
            Assert.IsTrue(PoliticalEthicsRules.IsIrresponsibleIdealism(1f, 0f));
            // 純粋0.9・責任0.8＝差0.1<=0.4＝無責任でない（結果に責任を負っている）
            Assert.IsFalse(PoliticalEthicsRules.IsIrresponsibleIdealism(0.9f, 0.8f));
            // 責任が純粋を上回る＝無責任でない
            Assert.IsFalse(PoliticalEthicsRules.IsIrresponsibleIdealism(0.3f, 0.9f));
            // 境界（差ちょうど0.4）は無責任でない（超えてはいない）
            Assert.IsFalse(PoliticalEthicsRules.IsIrresponsibleIdealism(0.5f, 0.1f));
        }
    }
}
