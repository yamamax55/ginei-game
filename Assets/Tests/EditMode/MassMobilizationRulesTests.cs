using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>大衆動員（MassMobilizationRules）＝政治運動への参加の純ロジック検証。</summary>
    public class MassMobilizationRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        /// <summary>動員の訴求＝大義の切迫×指導者の魅力（どちらか欠ければ届かない）。</summary>
        [Test]
        public void MobilizationAppeal_NeedsBothUrgencyAndCharisma()
        {
            // 切迫0.8・魅力0.5 → 0.4
            Assert.AreEqual(0.4f, MassMobilizationRules.MobilizationAppeal(0.8f, 0.5f), Eps);
            // 指導者の魅力が無ければ訴求は0
            Assert.AreEqual(0f, MassMobilizationRules.MobilizationAppeal(1f, 0f), Eps);
            // 切迫が無ければ訴求は0
            Assert.AreEqual(0f, MassMobilizationRules.MobilizationAppeal(0f, 1f), Eps);
        }

        /// <summary>参加の閾値＝個人のリスク÷(1+効力感)＝高いほど動かない、効力感が下げる。</summary>
        [Test]
        public void ParticipationThreshold_FallsWithEfficacy()
        {
            // リスク0.6・効力感0 → 0.6/1 = 0.6
            Assert.AreEqual(0.6f, MassMobilizationRules.ParticipationThreshold(0.6f, 0f), Eps);
            // リスク0.6・効力感1.0 → 0.6/2 = 0.3
            Assert.AreEqual(0.3f, MassMobilizationRules.ParticipationThreshold(0.6f, 1f), Eps);
            // 効力感が高いほど閾値が下がる（参加しやすくなる）
            Assert.Less(MassMobilizationRules.ParticipationThreshold(0.5f, 0.9f),
                MassMobilizationRules.ParticipationThreshold(0.5f, 0.1f));
        }

        /// <summary>同調効果＝(参加者/人口)^0.5＝初期の少数参加者の動きが大きく効く（人が動くと動く）。</summary>
        [Test]
        public void BandwagonEffect_NonlinearInParticipationRatio()
        {
            // 参加0.25・人口1.0 → 0.25^0.5 = 0.5
            Assert.AreEqual(0.5f, MassMobilizationRules.BandwagonEffect(0.25f, 1f), PowEps);
            // 参加0.36・人口1.0 → 0.36^0.5 = 0.6
            Assert.AreEqual(0.6f, MassMobilizationRules.BandwagonEffect(0.36f, 1f), PowEps);
            // 総人口0なら同調なし（ゼロ除算回避）
            Assert.AreEqual(0f, MassMobilizationRules.BandwagonEffect(0.5f, 0f), Eps);
            // 参加比率が高いほど同調が強い（単調増）
            Assert.Greater(MassMobilizationRules.BandwagonEffect(0.8f, 1f),
                MassMobilizationRules.BandwagonEffect(0.2f, 1f));
        }

        /// <summary>臨界量＝同調×0.6＋訴求×0.4＝人の動きと大義が揃うと臨界点へ近づく。</summary>
        [Test]
        public void CriticalMass_WeightsBandwagonAndAppeal()
        {
            // 同調0.5・訴求0.5 → 0.5×0.6 + 0.5×0.4 = 0.5
            Assert.AreEqual(0.5f, MassMobilizationRules.CriticalMass(0.5f, 0.5f), Eps);
            // 同調1.0・訴求0 → 0.6
            Assert.AreEqual(0.6f, MassMobilizationRules.CriticalMass(1f, 0f), Eps);
            // 同調0・訴求1.0 → 0.4
            Assert.AreEqual(0.4f, MassMobilizationRules.CriticalMass(0f, 1f), Eps);
        }

        /// <summary>フリーライダー問題＝公共財×(1−個人の貢献感)＝みんなやるから自分は要らないと降りる。</summary>
        [Test]
        public void FreeRiderProblem_RisesWithGoodAndFallsWithContribution()
        {
            // 公共財0.8・貢献感0.25 → 0.8×0.75 = 0.6
            Assert.AreEqual(0.6f, MassMobilizationRules.FreeRiderProblem(0.8f, 0.25f), Eps);
            // 自分の貢献が必須（貢献感1.0）ならただ乗りは消える
            Assert.AreEqual(0f, MassMobilizationRules.FreeRiderProblem(1f, 1f), Eps);
            // 公共財が大きいほどただ乗りが増える（単調増）
            Assert.Greater(MassMobilizationRules.FreeRiderProblem(0.9f, 0.3f),
                MassMobilizationRules.FreeRiderProblem(0.3f, 0.3f));
        }

        /// <summary>ネットワーク増幅＝1＋紐帯×情報流通×(上限−1)＝口コミで動員が伝播し増幅（≥1.0・上限2.0）。</summary>
        [Test]
        public void NetworkAmplification_AmplifiesWithTiesAndFlow()
        {
            // 紐帯0なら増幅なし＝1.0
            Assert.AreEqual(1f, MassMobilizationRules.NetworkAmplification(0f, 1f), Eps);
            // 紐帯0.5・情報0.8 → 1 + 0.5×0.8×(2-1) = 1.4
            Assert.AreEqual(1.4f, MassMobilizationRules.NetworkAmplification(0.5f, 0.8f), Eps);
            // 紐帯も情報も最大で上限2.0
            Assert.AreEqual(2f, MassMobilizationRules.NetworkAmplification(1f, 1f), Eps);
        }

        /// <summary>抑圧の抑止＝弾圧×0.8×(1−訴求)＝弾圧が動員を抑え込むが強い大義には効きにくい。</summary>
        [Test]
        public void RepressionDeterrence_WeakenedByStrongAppeal()
        {
            // 弾圧1.0・訴求0 → 1×0.8×1 = 0.8
            Assert.AreEqual(0.8f, MassMobilizationRules.RepressionDeterrence(1f, 0f), Eps);
            // 弾圧1.0・訴求0.5 → 1×0.8×0.5 = 0.4
            Assert.AreEqual(0.4f, MassMobilizationRules.RepressionDeterrence(1f, 0.5f), Eps);
            // 大義が強いほど弾圧の抑止が効きにくい（単調減）
            Assert.Less(MassMobilizationRules.RepressionDeterrence(1f, 0.9f),
                MassMobilizationRules.RepressionDeterrence(1f, 0.1f));
        }

        /// <summary>動員規模＝臨界×(1＋増幅寄与)−抑止−ただ乗り（0..1）。</summary>
        [Test]
        public void MobilizationScale_CombinesAllForces()
        {
            // 臨界0.5・増幅2.0・抑止0.1・ただ乗り0.1
            // amplified = 0.5×(1+(2-1)×0.3) = 0.5×1.3 = 0.65 → 0.65-0.1-0.1 = 0.45
            Assert.AreEqual(0.45f, MassMobilizationRules.MobilizationScale(0.5f, 2f, 0.1f, 0.1f), Eps);
            // 抑止とただ乗りが規模を上回れば0でクランプ
            Assert.AreEqual(0f, MassMobilizationRules.MobilizationScale(0.3f, 1f, 0.5f, 0.5f), Eps);
            // 抑止が強いほど規模が小さい（単調減）
            Assert.Less(MassMobilizationRules.MobilizationScale(0.6f, 1.5f, 0.4f, 0.1f),
                MassMobilizationRules.MobilizationScale(0.6f, 1.5f, 0.05f, 0.1f));
        }

        /// <summary>雪だるま判定＝動員規模が閾値（既定0.5）超で運動が立ち上がる。</summary>
        [Test]
        public void IsSnowballing_AboveThreshold()
        {
            Assert.IsTrue(MassMobilizationRules.IsSnowballing(0.6f));
            Assert.IsFalse(MassMobilizationRules.IsSnowballing(0.4f));
            // 閾値ちょうどは超でない
            Assert.IsFalse(MassMobilizationRules.IsSnowballing(0.5f));
        }

        /// <summary>
        /// 物語：弾圧下の都市で、切迫した大義と魅力ある指導者が訴える。
        /// 当初は少数の参加者だが、密な社会的紐帯と情報の流通が口コミで動員を増幅し、
        /// 同調効果が臨界量を押し上げ、運動は雪だるま式に立ち上がる。
        /// </summary>
        [Test]
        public void Narrative_RepressedCityIgnitesIntoSnowballingMovement()
        {
            var p = MassMobilizationParams.Default;

            // 切迫した大義(0.9)＋魅力ある指導者(0.85)＝高い訴求
            float appeal = MassMobilizationRules.MobilizationAppeal(0.9f, 0.85f, p);
            Assert.Greater(appeal, 0.7f);

            // 当初は人口の一部(0.4)が参加 → 同調効果が立ち上がる
            float bandwagon = MassMobilizationRules.BandwagonEffect(0.4f, 1f, p);
            Assert.Greater(bandwagon, 0.5f);

            // 同調×訴求で臨界量へ
            float critical = MassMobilizationRules.CriticalMass(bandwagon, appeal, p);
            Assert.Greater(critical, 0.5f);

            // 密な紐帯(0.9)＋情報流通(0.8)が口コミで増幅
            float network = MassMobilizationRules.NetworkAmplification(0.9f, 0.8f, p);
            Assert.Greater(network, 1.5f);

            // 強い弾圧(0.9)でも大義が強いので抑止は限定的
            float deterrence = MassMobilizationRules.RepressionDeterrence(0.9f, appeal, p);
            Assert.Less(deterrence, 0.3f);

            // 公共財は魅力的だが参加が必須と感じられ(貢献感0.85)ただ乗りは小さい
            float freeRider = MassMobilizationRules.FreeRiderProblem(0.8f, 0.85f, p);
            Assert.Less(freeRider, 0.3f);

            // 総合＝運動は雪だるま式に成立
            float scale = MassMobilizationRules.MobilizationScale(critical, network, deterrence, freeRider, p);
            Assert.Greater(scale, 0.5f);
            Assert.IsTrue(MassMobilizationRules.IsSnowballing(scale, p.snowballThreshold));

            // 対照：弾圧が圧倒的で大義も弱ければ運動は立ち上がらない
            float weakAppeal = MassMobilizationRules.MobilizationAppeal(0.2f, 0.2f, p);
            float weakCritical = MassMobilizationRules.CriticalMass(
                MassMobilizationRules.BandwagonEffect(0.05f, 1f, p), weakAppeal, p);
            float heavyDeterrence = MassMobilizationRules.RepressionDeterrence(1f, weakAppeal, p);
            float weakScale = MassMobilizationRules.MobilizationScale(
                weakCritical, MassMobilizationRules.NetworkAmplification(0.2f, 0.2f, p), heavyDeterrence, 0.4f, p);
            Assert.IsFalse(MassMobilizationRules.IsSnowballing(weakScale, p.snowballThreshold));
            Assert.Less(weakScale, scale);
        }
    }
}
