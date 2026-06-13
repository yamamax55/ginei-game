using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>人民投票的指導者民主主義（ツェーザリズム・WEBR-4 #1533）の純ロジックテスト。</summary>
    public class PlebiscitaryRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>直接の負託＝カリスマ×大衆動員。どちらか欠ければゼロ（積）。</summary>
        [Test]
        public void DirectMandate_カリスマと動員の積()
        {
            Assert.AreEqual(0.4f, PlebiscitaryRules.DirectMandate(0.8f, 0.5f), Eps);
            // 大衆動員ゼロなら直接の負託は生まれない
            Assert.AreEqual(0f, PlebiscitaryRules.DirectMandate(1f, 0f), Eps);
            // カリスマゼロでも同じ
            Assert.AreEqual(0f, PlebiscitaryRules.DirectMandate(0f, 1f), Eps);
        }

        /// <summary>議会の迂回＝直接の負託が強く議会が弱いほど飛び越せる（議会制の空洞化）。</summary>
        [Test]
        public void ParliamentaryBypass_弱い議会ほど飛び越せる()
        {
            Assert.AreEqual(0.28f, PlebiscitaryRules.ParliamentaryBypass(0.4f, 0.3f), Eps);
            // 議会が万全なら迂回できない
            Assert.AreEqual(0f, PlebiscitaryRules.ParliamentaryBypass(0.9f, 1f), Eps);
            // 議会が弱いほど迂回度は上がる
            Assert.Greater(
                PlebiscitaryRules.ParliamentaryBypass(0.6f, 0.2f),
                PlebiscitaryRules.ParliamentaryBypass(0.6f, 0.8f));
        }

        /// <summary>人民投票の正統性＝負託×参加。参加ゼロなら正統性は生まれない（喝采による正統化）。</summary>
        [Test]
        public void PlebiscitaryLegitimacy_負託と参加の積()
        {
            Assert.AreEqual(0.24f, PlebiscitaryRules.PlebiscitaryLegitimacy(0.4f, 0.6f), Eps);
            Assert.AreEqual(0f, PlebiscitaryRules.PlebiscitaryLegitimacy(0.8f, 0f), Eps);
        }

        /// <summary>ツェーザリズムのリスク＝直接動員が強く制度の歯止めが弱いほど独裁へ傾く。</summary>
        [Test]
        public void CaesarismRisk_歯止めが弱いほど高い()
        {
            Assert.AreEqual(0.64f, PlebiscitaryRules.CaesarismRisk(0.8f, 0.2f), Eps);
            // 制度の歯止めが万全ならリスクは消える
            Assert.AreEqual(0f, PlebiscitaryRules.CaesarismRisk(0.9f, 1f), Eps);
            // 歯止めが弱いほどリスクは上がる
            Assert.Greater(
                PlebiscitaryRules.CaesarismRisk(0.7f, 0.1f),
                PlebiscitaryRules.CaesarismRisk(0.7f, 0.6f));
        }

        /// <summary>喝采の力学＝カリスマ×大衆の情動。感情が高ぶるほど討議が喝采に置き換わる。</summary>
        [Test]
        public void AcclamationDynamics_情動が討議を置き換える()
        {
            Assert.AreEqual(0.24f, PlebiscitaryRules.AcclamationDynamics(0.6f, 0.5f), Eps);
            // 情動ゼロなら喝采は起きない
            Assert.AreEqual(0f, PlebiscitaryRules.AcclamationDynamics(1f, 0f), Eps);
        }

        /// <summary>指導者大衆の短絡＝中間団体が薄いほど直結する（媒介の消失）。</summary>
        [Test]
        public void LeaderMassShortCircuit_中間団体が薄いほど短絡()
        {
            Assert.AreEqual(0.54f, PlebiscitaryRules.LeaderMassShortCircuit(0.9f, 0.4f), Eps);
            // 中間団体が厚ければ短絡は起きない
            Assert.AreEqual(0f, PlebiscitaryRules.LeaderMassShortCircuit(0.9f, 1f), Eps);
        }

        /// <summary>正統性の移ろい＝喝采依存の正統性は揮発する（熱が冷めると崩れる）。</summary>
        [Test]
        public void MandateVolatility_正統性に比例して移ろう()
        {
            Assert.AreEqual(0.3f, PlebiscitaryRules.MandateVolatility(0.5f), Eps);
            // 正統性ゼロなら揺らぎもない
            Assert.AreEqual(0f, PlebiscitaryRules.MandateVolatility(0f), Eps);
            // 高い正統性ほど揺らぎ幅も大きい
            Assert.Greater(
                PlebiscitaryRules.MandateVolatility(0.9f),
                PlebiscitaryRules.MandateVolatility(0.3f));
        }

        /// <summary>ツェーザリズム判定＝リスクと議会迂回がともに閾値超でツェーザリズムに陥る。</summary>
        [Test]
        public void IsCaesarist_リスクと迂回がともに閾値超で成立()
        {
            // 0.64>0.5 かつ 0.6>0.5 ＝成立
            Assert.IsTrue(PlebiscitaryRules.IsCaesarist(0.64f, 0.6f, 0.5f));
            // 迂回が閾値以下なら不成立（議会が飛び越せない＝独裁にならない）
            Assert.IsFalse(PlebiscitaryRules.IsCaesarist(0.64f, 0.4f, 0.5f));
            // リスクが閾値以下なら不成立（制度の歯止めが効いている）
            Assert.IsFalse(PlebiscitaryRules.IsCaesarist(0.4f, 0.6f, 0.5f));
        }
    }
}
