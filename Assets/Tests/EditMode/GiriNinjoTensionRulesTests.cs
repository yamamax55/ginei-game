using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>義理と人情の葛藤（ルース・ベネディクト『菊と刀』・KIKU-3 #1838）の純ロジックテスト。</summary>
    public class GiriNinjoTensionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>葛藤の深さ＝義理と人情が両方強く拮抗するほど深く・一方が圧倒的なら浅い。</summary>
        [Test]
        public void TensionLevel_両方強く拮抗するほど深い()
        {
            // 両方最大で拮抗＝最深1
            Assert.AreEqual(1f, GiriNinjoTensionRules.TensionLevel(1f, 1f), Eps);
            // 義理だけ強い＝迷いなく葛藤なし
            Assert.AreEqual(0f, GiriNinjoTensionRules.TensionLevel(1f, 0f), Eps);
            // 0.8と0.8で拮抗＝0.8*0.8*1=0.64
            Assert.AreEqual(0.64f, GiriNinjoTensionRules.TensionLevel(0.8f, 0.8f), Eps);
            // 同じ積でも偏りがあると拮抗度が下がり葛藤が浅い：1.0と0.5 < 0.7と0.7に近い構図
            Assert.Greater(GiriNinjoTensionRules.TensionLevel(0.7f, 0.7f),
                           GiriNinjoTensionRules.TensionLevel(1f, 0.5f));
        }

        /// <summary>義理の強さ＝人目があるほど勝る（社会的可視性で底上げ）。</summary>
        [Test]
        public void GiriPriority_人目があるほど義理が勝つ()
        {
            // 可視性ゼロ＝義務そのまま
            Assert.AreEqual(0.5f, GiriNinjoTensionRules.GiriPriority(0.5f, 0f), Eps);
            // 義務0.5・可視性0.5＝0.5*1.5=0.75
            Assert.AreEqual(0.75f, GiriNinjoTensionRules.GiriPriority(0.5f, 0.5f), Eps);
            // 義務0.7・可視性1＝0.7*2=1.4→クランプ1
            Assert.AreEqual(1f, GiriNinjoTensionRules.GiriPriority(0.7f, 1f), Eps);
        }

        /// <summary>人情の強さ＝私的な場ほど勝る（私性で底上げ）。</summary>
        [Test]
        public void NinjoPriority_私的な場ほど情が勝る()
        {
            // 私性ゼロ＝絆そのまま
            Assert.AreEqual(0.4f, GiriNinjoTensionRules.NinjoPriority(0.4f, 0f), Eps);
            // 絆0.4・私性0.5＝0.4*1.5=0.6
            Assert.AreEqual(0.6f, GiriNinjoTensionRules.NinjoPriority(0.4f, 0.5f), Eps);
        }

        /// <summary>葛藤の解決＝義理が勝てば正・人情が勝てば負・拮抗で0。</summary>
        [Test]
        public void ResolveConflict_綱引きを符号付き軸へ写す()
        {
            // 拮抗＝板挟み0
            Assert.AreEqual(0f, GiriNinjoTensionRules.ResolveConflict(0.6f, 0.6f), Eps);
            // 義理のみ＝完全に義理(+1)
            Assert.AreEqual(1f, GiriNinjoTensionRules.ResolveConflict(1f, 0f), Eps);
            // 人情のみ＝完全に人情(−1)
            Assert.AreEqual(-1f, GiriNinjoTensionRules.ResolveConflict(0f, 1f), Eps);
            // 両方ゼロ＝板挟み0
            Assert.AreEqual(0f, GiriNinjoTensionRules.ResolveConflict(0f, 0f), Eps);
            // 義理がやや勝つ＝正側
            Assert.Greater(GiriNinjoTensionRules.ResolveConflict(0.7f, 0.5f), 0f);
        }

        /// <summary>選択の代償＝捨てた側が重いほど大きい（既定幅0.8）。</summary>
        [Test]
        public void MoralCostOfChoice_捨てた側が重いほど痛い()
        {
            // 選択1・捨てた側1×0.8＝0.8
            Assert.AreEqual(0.8f, GiriNinjoTensionRules.MoralCostOfChoice(1f, 1f), Eps);
            // 捨てた側が無ければ代償ゼロ（失うものが無い）
            Assert.AreEqual(0f, GiriNinjoTensionRules.MoralCostOfChoice(1f, 0f), Eps);
            // 選択0.5・捨てた側0.5×0.8＝0.2
            Assert.AreEqual(0.2f, GiriNinjoTensionRules.MoralCostOfChoice(0.5f, 0.5f), Eps);
        }

        /// <summary>内面の苦悩＝葛藤×誠実さ（誠実な人ほど苦しむ・既定幅1.0）。</summary>
        [Test]
        public void InnerConflictStress_誠実な人ほど苦しむ()
        {
            // 葛藤0.8・誠実1×1.0＝0.8
            Assert.AreEqual(0.8f, GiriNinjoTensionRules.InnerConflictStress(0.8f, 1f), Eps);
            // 誠実さゼロ＝同じ葛藤でも痛まない
            Assert.AreEqual(0f, GiriNinjoTensionRules.InnerConflictStress(0.8f, 0f), Eps);
            // 誠実な人の方が苦しむ
            Assert.Greater(GiriNinjoTensionRules.InnerConflictStress(0.6f, 0.9f),
                           GiriNinjoTensionRules.InnerConflictStress(0.6f, 0.3f));
        }

        /// <summary>世間の評価＝義理を欠くほど・共同体が義理を重んじるほど非難（負値・既定幅0.8）。</summary>
        [Test]
        public void SocialJudgment_義理を欠くと世間に咎められる()
        {
            // 人情選択1・共同体1×0.8＝−0.8
            Assert.AreEqual(-0.8f, GiriNinjoTensionRules.SocialJudgment(1f, 1f), Eps);
            // 共同体が義理を重んじなければ非難ゼロ
            Assert.AreEqual(0f, GiriNinjoTensionRules.SocialJudgment(1f, 0f), Eps);
            // 義理を欠かなければ非難ゼロ
            Assert.AreEqual(0f, GiriNinjoTensionRules.SocialJudgment(0f, 1f), Eps);
        }

        /// <summary>悲劇の度合い＝深い葛藤かつ両立不能なほど大きい（両立可能なら悲劇にならない）。</summary>
        [Test]
        public void TragicOutcome_両立不能な葛藤が悲劇を生む()
        {
            // 葛藤0.64・両立不能1＝0.64
            Assert.AreEqual(0.64f, GiriNinjoTensionRules.TragicOutcome(0.64f, 1f), Eps);
            // 両立可能（折衷の道あり）＝悲劇ゼロ
            Assert.AreEqual(0f, GiriNinjoTensionRules.TragicOutcome(0.9f, 0f), Eps);
        }

        /// <summary>板挟み判定＝葛藤が閾値超で引き裂かれた状態（既定0.6）。</summary>
        [Test]
        public void IsTorn_閾値超で板挟み()
        {
            Assert.IsTrue(GiriNinjoTensionRules.IsTorn(0.7f));   // 0.7>0.6
            Assert.IsFalse(GiriNinjoTensionRules.IsTorn(0.5f));  // 0.5<=0.6
            Assert.IsFalse(GiriNinjoTensionRules.IsTorn(0.6f));  // 境界は板挟みでない
        }

        /// <summary>
        /// 物語＝義理(主君への恩返しの務め)と人情(友への情愛)が拮抗して板挟みになり、人目があれば義理が勝ち、
        /// 両立不能なら悲劇を生む。深く誠実な人ほど苦悩し、情を取れば世間に咎められる。
        /// </summary>
        [Test]
        public void Story_義理と人情の板挟みが悲劇を生む()
        {
            // 義理も人情も強く拮抗＝深い葛藤
            float tension = GiriNinjoTensionRules.TensionLevel(0.8f, 0.8f);
            Assert.AreEqual(0.64f, tension, Eps);
            Assert.IsTrue(GiriNinjoTensionRules.IsTorn(tension)); // 板挟み

            // 人目の前（高可視性）では義理が・私性が低いので人情はやや劣る
            float giri = GiriNinjoTensionRules.GiriPriority(0.7f, 0.9f);   // 0.7*1.9=1.33→1
            float ninjo = GiriNinjoTensionRules.NinjoPriority(0.8f, 0.1f); // 0.8*1.1=0.88
            Assert.AreEqual(1f, giri, Eps);
            Assert.AreEqual(0.88f, ninjo, Eps);

            // 綱引きは義理側へ倒れる（人目が義理を勝たせる）
            float resolution = GiriNinjoTensionRules.ResolveConflict(giri, ninjo);
            Assert.Greater(resolution, 0f); // 義理を取る

            // 誠実な人ほど苦悩は深い
            Assert.Greater(GiriNinjoTensionRules.InnerConflictStress(tension, 0.9f),
                           GiriNinjoTensionRules.InnerConflictStress(tension, 0.2f));

            // 義理を取ったので情(人情)を裏切る代償が生じる
            float cost = GiriNinjoTensionRules.MoralCostOfChoice(giri, ninjo);
            Assert.Greater(cost, 0f);

            // 仮に人情を取って義理を欠いていたら世間に咎められた（負の評価）
            Assert.Less(GiriNinjoTensionRules.SocialJudgment(0.9f, 0.9f), 0f);

            // 両立不能なら悲劇＝折衷の道がある時より深い
            Assert.Greater(GiriNinjoTensionRules.TragicOutcome(tension, 1f),
                           GiriNinjoTensionRules.TragicOutcome(tension, 0.2f));
        }
    }
}
