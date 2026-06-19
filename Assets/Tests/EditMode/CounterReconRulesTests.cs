using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 対偵察ルール（CounterReconRules）の EditMode テスト。
    /// 敵偵察を狩り→盲目にし→情報を遮断し→自軍の動きを隠す一連の数式を既定 Params で固定する。
    /// </summary>
    public class CounterReconRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void ScoutHunting_RisesWithStrength_FallsWithEvasion()
        {
            // cr=3, evasion=0 → denom=4 → 0.75
            Assert.AreEqual(0.75f, CounterReconRules.ScoutHuntingEffectiveness(3f, 0f), Eps);
            // cr=3, evasion=0.5 → denom=4+2=6 → 0.5（回避で効率低下）
            Assert.AreEqual(0.5f, CounterReconRules.ScoutHuntingEffectiveness(3f, 0.5f), Eps);
            // 対偵察ゼロは狩れない
            Assert.AreEqual(0f, CounterReconRules.ScoutHuntingEffectiveness(0f, 0f), Eps);
        }

        [Test]
        public void EnemyBlinding_ScalesByCeiling()
        {
            // 0.9 * 0.75 = 0.675
            Assert.AreEqual(0.675f, CounterReconRules.EnemyBlinding(0.75f), Eps);
            // 完全には暗闇にならない（上限0.9）
            Assert.AreEqual(0.9f, CounterReconRules.EnemyBlinding(1f), Eps);
            Assert.AreEqual(0f, CounterReconRules.EnemyBlinding(0f), Eps);
        }

        [Test]
        public void IntelStaleness_GrowsWithBlindAndTime()
        {
            // 0.675 * 0.05 * 10 = 0.3375
            Assert.AreEqual(0.3375f, CounterReconRules.EnemyIntelStaleness(0.675f, 10f), Eps);
            // 盲目でなければ古びない
            Assert.AreEqual(0f, CounterReconRules.EnemyIntelStaleness(0f, 100f), Eps);
        }

        [Test]
        public void Concealment_CombinesBlindAndEmcon_WithFloor()
        {
            // 1-(1-0.675)(1-0.2) = 1-0.26 = 0.74
            Assert.AreEqual(0.74f, CounterReconRules.OwnMovementConcealment(0.675f, 0.2f), Eps);
            // 盲目も電波管制も無くても床0.1で隠れる
            Assert.AreEqual(0.1f, CounterReconRules.OwnMovementConcealment(0f, 0f), Eps);
        }

        [Test]
        public void Cost_IsShareOfTotal()
        {
            // 3/12 = 0.25
            Assert.AreEqual(0.25f, CounterReconRules.CounterReconCost(3f, 12f), Eps);
            // 総戦力ゼロは費用ゼロ
            Assert.AreEqual(0f, CounterReconRules.CounterReconCost(3f, 0f), Eps);
        }

        [Test]
        public void Attrition_RisesWhenEnemyScoutsOutnumber()
        {
            // es=4, cr=3 → ratio=4/4=1 → 0.5
            Assert.AreEqual(0.5f, CounterReconRules.ReconScreenAttrition(3f, 4f), Eps);
            // es=8 → ratio=2 → clamp 1.0（削り尽くされる）
            Assert.AreEqual(1f, CounterReconRules.ReconScreenAttrition(3f, 8f), Eps);
        }

        [Test]
        public void InformationDominance_BlendsVisibilityAndBlinding()
        {
            // 0.5*0.675 + 0.5*0.8 = 0.7375
            Assert.AreEqual(0.7375f, CounterReconRules.InformationDominance(0.8f, 0.675f), Eps);
            // 自軍盲目・敵も見える＝優位なし
            Assert.AreEqual(0f, CounterReconRules.InformationDominance(0f, 0f), Eps);
        }

        [Test]
        public void IsEnemyBlinded_ChecksThreshold()
        {
            Assert.IsTrue(CounterReconRules.IsEnemyBlinded(0.675f, 0.5f));
            Assert.IsFalse(CounterReconRules.IsEnemyBlinded(0.4f, 0.5f));
        }

        [Test]
        public void Narrative_HuntScoutsBlindEnemyConcealMovesGainDominanceButPayCost()
        {
            // 総戦力12のうち3を対偵察に投入。敵偵察は回避0.5でしぶとい。
            float totalStrength = 12f;
            float counterRecon = 3f;
            float enemyEvasion = 0.5f;

            // 敵偵察を狩る効率（回避で半減）。
            float hunting = CounterReconRules.ScoutHuntingEffectiveness(counterRecon, enemyEvasion);
            Assert.AreEqual(0.5f, hunting, Eps);

            // 敵を盲目にする＝情報遮断。
            float blinding = CounterReconRules.EnemyBlinding(hunting);
            Assert.AreEqual(0.45f, blinding, Eps); // 0.9*0.5

            // 敵を盲目にしたうえで自軍の動きを隠す（電波管制0.2を併用）。
            float conceal = CounterReconRules.OwnMovementConcealment(blinding, 0.2f);
            Assert.AreEqual(0.56f, conceal, Eps); // 1-(1-0.45)(1-0.2)=1-0.44

            // こちらは見え（own0.8）敵は盲目（0.45）＝情報優位を得る。
            float dominance = CounterReconRules.InformationDominance(0.8f, blinding);
            Assert.AreEqual(0.625f, dominance, Eps); // 0.5*0.45+0.5*0.8

            // だが対偵察に主力の1/4を割いた機会費用を払っている。
            float cost = CounterReconRules.CounterReconCost(counterRecon, totalStrength);
            Assert.AreEqual(0.25f, cost, Eps);

            // この時点ではまだ「盲目」の閾値0.5には届かない（投入不足）。
            Assert.IsFalse(CounterReconRules.IsEnemyBlinded(blinding, 0.5f));
        }
    }
}
