using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>大義名分の競合（KORY-3 #1411・項羽と劉邦）の純ロジックを担保。既定Params具体値で固定。</summary>
    public class MetaLegitimacyRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>代弁の正統性＝威光×敬意×按分。競合不在なら満額、競合がいれば敬意で按分される。</summary>
        [Test]
        public void SpokesmanLegitimacy_按分される()
        {
            // 競合不在：share=1 → 0.8*0.6*1 = 0.48
            Assert.AreEqual(0.48f, MetaLegitimacyRules.SpokesmanLegitimacy(0.8f, 0.6f, 0f), Eps);
            // 競合あり：share=0.6/(0.6+0.4)=0.6 → 0.8*0.6*0.6 = 0.288
            Assert.AreEqual(0.288f, MetaLegitimacyRules.SpokesmanLegitimacy(0.8f, 0.6f, 0.4f), Eps);
            // 敬意で勝るほど取り分が増える（同じ威光・競合でも reverence が高い方が正統）
            float low = MetaLegitimacyRules.SpokesmanLegitimacy(0.8f, 0.3f, 0.5f);
            float high = MetaLegitimacyRules.SpokesmanLegitimacy(0.8f, 0.7f, 0.5f);
            Assert.Less(low, high);
        }

        /// <summary>大義名分のボーナス＝代弁の正統性×0.5（旗印の力）。正統性0で0。</summary>
        [Test]
        public void AuthorityChampioningBonus_旗印の力()
        {
            Assert.AreEqual(0.144f, MetaLegitimacyRules.AuthorityChampioningBonus(0.288f), Eps);
            Assert.AreEqual(0f, MetaLegitimacyRules.AuthorityChampioningBonus(0f), Eps);
            Assert.AreEqual(0.5f, MetaLegitimacyRules.AuthorityChampioningBonus(1f), Eps);
        }

        /// <summary>冒涜のペナルティ＝冒涜度×威光×0.7。威光ある権威ほど害したときの反動が大きい（項羽の義帝弑殺）。</summary>
        [Test]
        public void DesecrationPenalty_義帝弑殺で大義を失う()
        {
            Assert.AreEqual(0.63f, MetaLegitimacyRules.DesecrationPenalty(1f, 0.9f), Eps);
            // 冒涜しなければ0
            Assert.AreEqual(0f, MetaLegitimacyRules.DesecrationPenalty(0f, 0.9f), Eps);
            // 威光なき権威を害しても失うものは小さい
            Assert.Less(MetaLegitimacyRules.DesecrationPenalty(1f, 0.2f),
                        MetaLegitimacyRules.DesecrationPenalty(1f, 0.9f));
        }

        /// <summary>弔い合戦の正統性＝競合の冒涜×自陣の応え方×0.4。競合が害さねば名分は立たない（劉邦の義帝弔い）。</summary>
        [Test]
        public void AvengerLegitimacy_弔い合戦の旗印()
        {
            Assert.AreEqual(0.32f, MetaLegitimacyRules.AvengerLegitimacy(1f, 0.8f), Eps);
            // 競合が冒涜していなければ弔い合戦は成立しない
            Assert.AreEqual(0f, MetaLegitimacyRules.AvengerLegitimacy(0f, 0.8f), Eps);
            // 弔いに応えなければ正統性は得られない
            Assert.AreEqual(0f, MetaLegitimacyRules.AvengerLegitimacy(1f, 0f), Eps);
        }

        /// <summary>代弁権の競合＝拮抗するほど激しく、一方が圧倒すれば決着して低い。</summary>
        [Test]
        public void ClaimContest_拮抗で激化()
        {
            // 拮抗（0.6 vs 0.6）：balance=1, intensity=0.6 → 0.6
            Assert.AreEqual(0.6f, MetaLegitimacyRules.ClaimContest(0.6f, 0.6f), Eps);
            // 一方圧倒（0.9 vs 0.1）：balance=0.2, intensity=0.5 → 0.1
            Assert.AreEqual(0.1f, MetaLegitimacyRules.ClaimContest(0.9f, 0.1f), Eps);
            // 競合不在で0
            Assert.AreEqual(0f, MetaLegitimacyRules.ClaimContest(0.8f, 0f), Eps);
        }

        /// <summary>権威への依存＝代弁正統性×威光×0.6。旗印に頼った正統性の脆さ（傀儡の権威が崩れると共倒れ）。</summary>
        [Test]
        public void AuthorityDependence_依存の脆さ()
        {
            Assert.AreEqual(0.24f, MetaLegitimacyRules.AuthorityDependence(0.5f, 0.8f), Eps);
            // 威光が高いほど依存が深い
            Assert.Less(MetaLegitimacyRules.AuthorityDependence(0.5f, 0.3f),
                        MetaLegitimacyRules.AuthorityDependence(0.5f, 0.9f));
        }

        /// <summary>傀儡掌握の簒奪誘惑＝掌握度×自力×0.5。権威を握り地力が強いほど廃して自ら立つ誘惑（RegencyRulesへ接続）。</summary>
        [Test]
        public void PuppetMasterRisk_簒奪の誘惑()
        {
            Assert.AreEqual(0.36f, MetaLegitimacyRules.PuppetMasterRisk(0.9f, 0.8f), Eps);
            // 自力が弱ければ権威を握っても廃せない
            Assert.AreEqual(0f, MetaLegitimacyRules.PuppetMasterRisk(0.9f, 0f), Eps);
            // 自力が強いほど誘惑が大きい
            Assert.Less(MetaLegitimacyRules.PuppetMasterRisk(0.8f, 0.3f),
                        MetaLegitimacyRules.PuppetMasterRisk(0.8f, 0.9f));
        }

        /// <summary>正統な代弁者判定＝代弁の正統性が閾値0.5以上。</summary>
        [Test]
        public void IsLegitimateChampion_閾値判定()
        {
            Assert.IsFalse(MetaLegitimacyRules.IsLegitimateChampion(0.48f));
            Assert.IsTrue(MetaLegitimacyRules.IsLegitimateChampion(0.6f));
            Assert.IsTrue(MetaLegitimacyRules.IsLegitimateChampion(0.5f));
        }

        /// <summary>MetaAuthority のコンストラクタは全フィールドを 0..1 にクランプする。</summary>
        [Test]
        public void MetaAuthority_クランプ()
        {
            var a = new MetaAuthority(1.5f, -0.2f, 0.7f);
            Assert.AreEqual(1f, a.authorityPrestige, Eps);
            Assert.AreEqual(0f, a.spokesmanClaim, Eps);
            Assert.AreEqual(0.7f, a.reverence, Eps);
        }
    }
}
