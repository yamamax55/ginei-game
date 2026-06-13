using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>テロの原理化（アーレント型 TOTL-2 #1519）の純ロジックの担保。</summary>
    public class TerrorPrincipleRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>道具的恐怖＝脅威×抑圧必要×上限0.8。脅威も必要も無ければ恐怖は要らない。</summary>
        [Test]
        public void InstrumentalTerror_脅威と必要に比例し脅威ゼロでゼロ()
        {
            // 脅威0.5・必要0.5 → 0.5*0.5*0.8 = 0.2
            Assert.AreEqual(0.2f, TerrorPrincipleRules.InstrumentalTerror(0.5f, 0.5f), Eps);
            // 脅威ゼロ＝まだ道具すら要らない
            Assert.AreEqual(0f, TerrorPrincipleRules.InstrumentalTerror(0f, 1f), Eps);
            // 上限＝脅威1・必要1 → 0.8
            Assert.AreEqual(0.8f, TerrorPrincipleRules.InstrumentalTerror(1f, 1f), Eps);
        }

        /// <summary>恐怖の自走＝制度に根付くほど脅威と無関係に育つ。根がなければ自走しない。</summary>
        [Test]
        public void TerrorAutonomy_根付きに応じて自走し根ゼロで不動()
        {
            // 根付き0.5・dt1 → 0.5 + 0.1*0.5*1 = 0.55
            Assert.AreEqual(0.55f, TerrorPrincipleRules.TerrorAutonomy(0.5f, 1f), Eps);
            // 根ゼロ＝自走しない
            Assert.AreEqual(0f, TerrorPrincipleRules.TerrorAutonomy(0f, 10f), Eps);
            // 育つ一方＝tick で単調増加
            Assert.Greater(TerrorPrincipleRules.TerrorAutonomy(0.5f, 1f), 0.5f);
        }

        /// <summary>粛清の自己増殖＝脅威が減る（標的が尽きる）ほど慣性が標的を捏造して加速する。</summary>
        [Test]
        public void SelfPerpetuatingPurge_脅威が減るほど自己増殖が速い()
        {
            // 慣性0.5・脅威0（尽きた）・dt1 → 0.5 + 0.15*0.5*1*1 = 0.575
            float vacuum = TerrorPrincipleRules.SelfPerpetuatingPurge(0.5f, 0f, 1f);
            Assert.AreEqual(0.575f, vacuum, Eps);
            // 脅威が高い間は捏造の余地が小さく増殖が鈍い
            float threatful = TerrorPrincipleRules.SelfPerpetuatingPurge(0.5f, 1f, 1f);
            Assert.AreEqual(0.5f, threatful, Eps);
            // 脅威ゼロの方が高脅威より自己増殖が速い＝脅威がなくても粛清は止まらない
            Assert.Greater(vacuum, threatful);
        }

        /// <summary>無実の標的化＝自走が高く罪の関連性が低いほど誰でも標的。罪が完全に関係するなら無実なし。</summary>
        [Test]
        public void InnocentTargeting_自走高で罪が無関係になり無実が標的化()
        {
            // 自走0.8・罪関連0.25 → 0.8*(1-0.25) = 0.6
            Assert.AreEqual(0.6f, TerrorPrincipleRules.InnocentTargeting(0.8f, 0.25f), Eps);
            // 罪が完全に関係する＝無実の標的化なし
            Assert.AreEqual(0f, TerrorPrincipleRules.InnocentTargeting(0.9f, 1f), Eps);
            // 自走極大＋罪無関係＝誰でも標的
            Assert.AreEqual(1f, TerrorPrincipleRules.InnocentTargeting(1f, 0f), Eps);
        }

        /// <summary>原理への転化＝自走が閾値を超え道具的恐怖を追い越したら手段から目的へ。</summary>
        [Test]
        public void PrincipleTransition_自走が道具を追い越し閾値超で転化()
        {
            // 自走0.7（閾値0.6以上）かつ道具的0.3より上 → 転化
            Assert.IsTrue(TerrorPrincipleRules.PrincipleTransition(0.3f, 0.7f, 0.6f));
            // 自走が閾値未満 → まだ道具
            Assert.IsFalse(TerrorPrincipleRules.PrincipleTransition(0.3f, 0.5f, 0.6f));
            // 道具的恐怖の方が大きい＝まだ手段が主 → 未転化
            Assert.IsFalse(TerrorPrincipleRules.PrincipleTransition(0.8f, 0.7f, 0.6f));
        }

        /// <summary>恒常的不安＝自走が原理化するほど誰も安全でない＝服従の極大化。</summary>
        [Test]
        public void PermanentInsecurity_自走に等しく全員が標的になりうる()
        {
            Assert.AreEqual(0.7f, TerrorPrincipleRules.PermanentInsecurity(0.7f), Eps);
            Assert.AreEqual(1f, TerrorPrincipleRules.PermanentInsecurity(1.5f), Eps); // クランプ
            Assert.AreEqual(0f, TerrorPrincipleRules.PermanentInsecurity(0f), Eps);
        }

        /// <summary>運動の慣性＝止まると死ぬので掌握が強いほど敵を作り続ける（永久機関）。</summary>
        [Test]
        public void MovementMomentumNeed_掌握が強いほど敵生産が増える()
        {
            // 掌握0.5・dt1 → 0.5*(1+0.1*1) = 0.55
            Assert.AreEqual(0.55f, TerrorPrincipleRules.MovementMomentumNeed(0.5f, 1f), Eps);
            // 掌握ゼロ＝敵生産なし
            Assert.AreEqual(0f, TerrorPrincipleRules.MovementMomentumNeed(0f, 5f), Eps);
            // 掌握が強いほど敵生産が多い
            Assert.Greater(TerrorPrincipleRules.MovementMomentumNeed(0.8f, 1f),
                           TerrorPrincipleRules.MovementMomentumNeed(0.4f, 1f));
        }

        /// <summary>恐怖の目的化判定＝自走が既定閾値0.6以上で自己増殖段階に入る。</summary>
        [Test]
        public void IsTerrorPrincipalized_既定閾値0_6で目的化を判定()
        {
            Assert.IsTrue(TerrorPrincipleRules.IsTerrorPrincipalized(0.6f));
            Assert.IsTrue(TerrorPrincipleRules.IsTerrorPrincipalized(0.9f));
            Assert.IsFalse(TerrorPrincipleRules.IsTerrorPrincipalized(0.59f));
            // 明示閾値も使える
            Assert.IsTrue(TerrorPrincipleRules.IsTerrorPrincipalized(0.5f, 0.5f));
        }
    }
}
