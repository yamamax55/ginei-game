using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>多極講和（ウェストファリア型）の協調問題の純ロジック検証（TYW-4 #1427）。</summary>
    public class MultipartyPeaceRulesTests
    {
        const float Eps = 0.0001f;
        static MultipartyPeaceParams P => MultipartyPeaceParams.Default;

        /// <summary>交渉の複雑さ＝当事者数×要求対立の相乗で跳ね上がり、両方ゼロなら0。</summary>
        [Test]
        public void NegotiationComplexity_RisesWithPartiesAndConflict()
        {
            float none = MultipartyPeaceRules.NegotiationComplexity(0f, 0f, P);
            float few = MultipartyPeaceRules.NegotiationComplexity(0.3f, 0.3f, P);
            float many = MultipartyPeaceRules.NegotiationComplexity(1f, 1f, P);

            Assert.AreEqual(0f, none, Eps, "当事者なし・対立なしは複雑さ0");
            Assert.AreEqual(1f, many, Eps, "多者かつ要求全面対立は最大");
            Assert.Less(few, many, "当事者と対立が増えるほど複雑");
            Assert.Greater(few, none);
        }

        /// <summary>包括パッケージの成立見込み＝最弱当事者が律速（誰か一人の不満で崩れる）。</summary>
        [Test]
        public void PackageDealFeasibility_LimitedByWeakestParty()
        {
            float[] all = { 0.9f, 0.8f, 0.85f };
            float[] oneUnhappy = { 0.9f, 0.1f, 0.85f };

            Assert.AreEqual(0.8f, MultipartyPeaceRules.PackageDealFeasibility(all), Eps, "最弱の0.8が律速");
            Assert.AreEqual(0.1f, MultipartyPeaceRules.PackageDealFeasibility(oneUnhappy), Eps, "一人の不満が全体を引き下げる");
        }

        /// <summary>空・null 配列は当事者不在＝成立見込み0で安全。</summary>
        [Test]
        public void PackageDealFeasibility_EmptyAndNullSafe()
        {
            Assert.AreEqual(0f, MultipartyPeaceRules.PackageDealFeasibility(null), Eps);
            Assert.AreEqual(0f, MultipartyPeaceRules.PackageDealFeasibility(new float[0]), Eps);
        }

        /// <summary>ごね得＝戦争継続の選択肢（BATNA）があるほど強気に拒否できる。</summary>
        [Test]
        public void HoldoutPower_StrongerWithAlternative()
        {
            float weak = MultipartyPeaceRules.HoldoutPower(0.2f, 0f, P);
            float strong = MultipartyPeaceRules.HoldoutPower(0.2f, 1f, P);

            Assert.AreEqual(0.2f, weak, Eps, "代替案なしはシェアぶんのみ");
            Assert.AreEqual(0.2f + 1f * P.alternativeWeight, strong, Eps, "BATNAが拒否カードを底上げ");
            Assert.Greater(strong, weak);
        }

        /// <summary>妨害者リスク＝拒否力と妨害の限界利得の積（動機が無ければ妨害しない）。</summary>
        [Test]
        public void SpoilerRisk_NeedsBothPowerAndGain()
        {
            Assert.AreEqual(0f, MultipartyPeaceRules.SpoilerRisk(1f, 0f), Eps, "得が無ければ妨害しない");
            Assert.AreEqual(0f, MultipartyPeaceRules.SpoilerRisk(0f, 1f), Eps, "力が無ければ妨害できない");
            Assert.AreEqual(0.5f, MultipartyPeaceRules.SpoilerRisk(1f, 0.5f), Eps);
            Assert.AreEqual(0.25f, MultipartyPeaceRules.SpoilerRisk(0.5f, 0.5f), Eps);
        }

        /// <summary>膠着検知と包括的和平判定は表裏＝閾値で分かれる。</summary>
        [Test]
        public void StalemateAndComprehensivePeace_AreComplementary()
        {
            float low = 0.3f;
            float high = 0.7f;

            Assert.IsTrue(MultipartyPeaceRules.StalemateDetection(low, 0.5f), "成立見込み低は膠着");
            Assert.IsFalse(MultipartyPeaceRules.StalemateDetection(high, 0.5f));
            Assert.IsTrue(MultipartyPeaceRules.IsComprehensivePeace(high, 0.5f), "成立見込み高は包括和平");
            Assert.IsFalse(MultipartyPeaceRules.IsComprehensivePeace(low, 0.5f));
            // 既定閾値0.5でも整合。
            Assert.IsTrue(MultipartyPeaceRules.StalemateDetection(0.3f));
            Assert.IsTrue(MultipartyPeaceRules.IsComprehensivePeace(0.6f));
        }

        /// <summary>部分合意＝参加者を絞ると最弱が引き上がり、完全合意より成立しやすい。</summary>
        [Test]
        public void PartialAgreement_RaisesFeasibilityByExcludingHoldouts()
        {
            float[] sats = { 0.9f, 0.8f, 0.7f, 0.1f }; // 一人(0.1)が頑なに拒否
            float full = MultipartyPeaceRules.PackageDealFeasibility(sats);
            float partial = MultipartyPeaceRules.PartialAgreement(sats, 0.75f); // 上位3名で手を打つ

            Assert.AreEqual(0.1f, full, Eps, "全員だと最弱0.1が律速");
            Assert.AreEqual(0.7f, partial, Eps, "上位3名なら最弱は0.7");
            Assert.Greater(partial, full, "参加者を絞れば成立しやすい");
            // subset≤0・空は0。
            Assert.AreEqual(0f, MultipartyPeaceRules.PartialAgreement(sats, 0f), Eps);
            Assert.AreEqual(0f, MultipartyPeaceRules.PartialAgreement(null, 1f), Eps);
        }

        /// <summary>仲介者の価値＝複雑な交渉ほど・信頼が高いほど大きい（単純な交渉に仲介は要らない）。</summary>
        [Test]
        public void MediatorValue_MattersMoreInComplexTalks()
        {
            float simple = MultipartyPeaceRules.MediatorValue(0.1f, 1f, P);
            float complexHighTrust = MultipartyPeaceRules.MediatorValue(1f, 1f, P);
            float complexNoTrust = MultipartyPeaceRules.MediatorValue(1f, 0f, P);

            Assert.AreEqual(0.1f * 1f * P.mediatorEffectiveness, simple, Eps);
            Assert.AreEqual(P.mediatorEffectiveness, complexHighTrust, Eps, "複雑×信頼最大で効力ぶん");
            Assert.AreEqual(0f, complexNoTrust, Eps, "信頼ゼロの仲介は無価値");
            Assert.Greater(complexHighTrust, simple, "複雑な交渉ほど仲介が効く");
        }
    }
}
