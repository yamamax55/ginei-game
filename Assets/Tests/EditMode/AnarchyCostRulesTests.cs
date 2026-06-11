using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 無政府宙域の自然状態コスト（#1459・ホッブズ）の純ロジックを既定Paramsの具体値で固定する。
    /// 自然状態のコスト・経済の麻痺・隣接の不安定化・主権の空白の悪化・再統合のインセンティブ・
    /// リヴァイアサンの価値・軍閥の割拠・無政府崩壊判定を担保。
    /// </summary>
    public class AnarchyCostRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>自然状態コスト＝無法×0.5＋暴力×0.5（既定比重）。万人の闘争の重さ。</summary>
        [Test]
        public void StateOfNatureCost_無法と暴力を比重で混ぜる()
        {
            // 0.5*0.8 + 0.5*0.4 = 0.6
            Assert.AreEqual(0.6f, AnarchyCostRules.StateOfNatureCost(0.8f, 0.4f), Eps);
            // 無法も暴力もゼロなら自然状態コストもゼロ
            Assert.AreEqual(0f, AnarchyCostRules.StateOfNatureCost(0f, 0f), Eps);
        }

        /// <summary>経済麻痺＝無法度の指数曲線（安全なしでは産業が育たない）。無法ほど止まる。</summary>
        [Test]
        public void EconomicParalysis_無法が深まるほど産業が止まる()
        {
            // pow(0, 1/1.5)=0, pow(1,...)=1
            Assert.AreEqual(0f, AnarchyCostRules.EconomicParalysis(0f), Eps);
            Assert.AreEqual(1f, AnarchyCostRules.EconomicParalysis(1f), Eps);
            // 中間は単調増加（無法が増えれば麻痺も増える）
            Assert.Less(AnarchyCostRules.EconomicParalysis(0.3f), AnarchyCostRules.EconomicParalysis(0.7f));
        }

        /// <summary>隣接の不安定化＝距離減衰で近いほど強い（無法は伝染する）。</summary>
        [Test]
        public void NeighborDestabilization_近いほど強く遠いほど届かない()
        {
            // distance=0（隣接）＝コストそのまま
            Assert.AreEqual(0.6f, AnarchyCostRules.NeighborDestabilization(0.6f, 0f), Eps);
            // distance=0.5＝falloff=1-0.5*2=0 ＝届かない
            Assert.AreEqual(0f, AnarchyCostRules.NeighborDestabilization(0.6f, 0.5f), Eps);
            // 遠隔は近隣より弱い
            Assert.Less(AnarchyCostRules.NeighborDestabilization(0.8f, 0.3f),
                        AnarchyCostRules.NeighborDestabilization(0.8f, 0.1f));
        }

        /// <summary>主権の空白は時間で暴力を深める（リヴァイアサン不在の悪化）。</summary>
        [Test]
        public void SecurityVacuumTick_放置で険悪化していく()
        {
            // 0.5 + 0.5*1.0*0.05*2 = 0.55
            Assert.AreEqual(0.55f, AnarchyCostRules.SecurityVacuumTick(0.5f, 1f, 2f), Eps);
            // 脅威ゼロなら悪化なし（共通の敵がいなければひとまず止まる）
            Assert.AreEqual(0.5f, AnarchyCostRules.SecurityVacuumTick(0.5f, 0f, 10f), Eps);
        }

        /// <summary>再統合の動機＝コスト×回復力（安全のため主権に服する）。</summary>
        [Test]
        public void ReintegrationIncentive_コストが高く担い手がいるほど強い()
        {
            // 0.8 * 0.5 = 0.4
            Assert.AreEqual(0.4f, AnarchyCostRules.ReintegrationIncentive(0.8f, 0.5f), Eps);
            // まとめ上げる強者がいなければ動機は実を結ばない
            Assert.AreEqual(0f, AnarchyCostRules.ReintegrationIncentive(0.9f, 0f), Eps);
        }

        /// <summary>リヴァイアサンの価値＝自然状態コストそのもの（無秩序よりまし）。</summary>
        [Test]
        public void LeviathanValue_自然状態コストに等しい()
        {
            Assert.AreEqual(0.7f, AnarchyCostRules.LeviathanValue(0.7f), Eps);
            // 無法が無ければ秩序を立てる追加価値も無い
            Assert.AreEqual(0f, AnarchyCostRules.LeviathanValue(0f), Eps);
        }

        /// <summary>軍閥の割拠＝空白が閾値を超えてはじめて強者が立つ（主権の自然発生）。</summary>
        [Test]
        public void WarlordEmergence_閾値を超えた空白に強者が割拠する()
        {
            // 閾値0.3未満は強者がいても0
            Assert.AreEqual(0f, AnarchyCostRules.WarlordEmergence(0.2f, 1f), Eps);
            // vac=0.65, opportunity=(0.65-0.3)/0.7=0.5, *power0.8 = 0.4
            Assert.AreEqual(0.4f, AnarchyCostRules.WarlordEmergence(0.65f, 0.8f), Eps);
            // 空白が満タンでも強者がいなければ割拠しない
            Assert.AreEqual(0f, AnarchyCostRules.WarlordEmergence(1f, 0f), Eps);
        }

        /// <summary>無政府崩壊判定＝自然状態コストが閾値以上。生産も交易も止まる水準。</summary>
        [Test]
        public void IsAnarchicCollapse_閾値で万人の闘争に陥ったと判定()
        {
            // 既定閾値0.6
            Assert.IsTrue(AnarchyCostRules.IsAnarchicCollapse(0.6f));
            Assert.IsTrue(AnarchyCostRules.IsAnarchicCollapse(0.9f));
            Assert.IsFalse(AnarchyCostRules.IsAnarchicCollapse(0.59f));
        }

        /// <summary>AnarchyState はコンストラクタで0..1にクランプされる（純データの健全性）。</summary>
        [Test]
        public void AnarchyState_入力をクランプする()
        {
            var s = new AnarchyState(1.5f, -0.2f, 0.4f);
            Assert.AreEqual(1f, s.lawlessness, Eps);
            Assert.AreEqual(0f, s.violence, Eps);
            Assert.AreEqual(0.4f, s.economicCollapse, Eps);
        }
    }
}
