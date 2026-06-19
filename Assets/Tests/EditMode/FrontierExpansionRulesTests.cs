using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 辺境拡大 <see cref="FrontierExpansionRules"/> の純ロジック検証（既定 <see cref="FrontierExpansionParams.Default"/> 具体値で期待固定）。
    /// 「広げすぎた版図は補給と統治で自壊する＝過伸長（オーバーストレッチ）」を担保する。
    /// </summary>
    public class FrontierExpansionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>拡張ペースは軍事力×未開拓×経済余剰＝三者そろって初めて広げられる。</summary>
        [Test]
        public void ExpansionRate_NeedsAllThree()
        {
            // 0.8×0.5×0.5×ゲイン1.0 = 0.2
            Assert.AreEqual(0.2f, FrontierExpansionRules.ExpansionRate(0.8f, 0.5f, 0.5f), Eps);
            // 全部満ちれば最大1.0
            Assert.AreEqual(1.0f, FrontierExpansionRules.ExpansionRate(1f, 1f, 1f), Eps);
            // どれかが0なら広げられない（未開拓辺境なし＝広げる先がない）
            Assert.AreEqual(0.0f, FrontierExpansionRules.ExpansionRate(1f, 0f, 1f), Eps);
        }

        /// <summary>補給線の伸長は版図/兵站能力＝能力に等しい版図で半飽和0.5、超えると伸びきる。</summary>
        [Test]
        public void SupplyLineStretch_HalfSaturatesAtCapacity()
        {
            Assert.AreEqual(0.5f, FrontierExpansionRules.SupplyLineStretch(1f, 1f), Eps);   // 比1で0.5
            Assert.AreEqual(0.75f, FrontierExpansionRules.SupplyLineStretch(3f, 1f), Eps);  // 比3で0.75
            // 兵站能力が厚いほど同じ版図でも伸長は小さい
            Assert.Greater(FrontierExpansionRules.SupplyLineStretch(3f, 1f),
                           FrontierExpansionRules.SupplyLineStretch(3f, 3f));
        }

        /// <summary>辺境の防衛コストは国境線×脅威＝長い縁(へり)と高い脅威で守備に資源を食われる。</summary>
        [Test]
        public void FrontierDefenseCost_BorderTimesThreat()
        {
            Assert.AreEqual(0.2f, FrontierExpansionRules.FrontierDefenseCost(0.8f, 0.5f), Eps); // 0.5×0.8×0.5
            Assert.AreEqual(0.0f, FrontierExpansionRules.FrontierDefenseCost(0.8f, 0f), Eps);   // 脅威ゼロなら無料
            Assert.Greater(FrontierExpansionRules.FrontierDefenseCost(1f, 1f),
                           FrontierExpansionRules.FrontierDefenseCost(0.3f, 0.3f));
        }

        /// <summary>拡大の果実は版図×開発度＝広いだけでは富まず、開発が進んで初めて実る。</summary>
        [Test]
        public void TerritorialYield_SizeTimesDevelopment()
        {
            Assert.AreEqual(0.2f, FrontierExpansionRules.TerritorialYield(0.8f, 0.5f), Eps); // 0.5×0.8×0.5
            Assert.AreEqual(0.5f, FrontierExpansionRules.TerritorialYield(1f, 1f), Eps);     // 満版図×満開発
            Assert.AreEqual(0.0f, FrontierExpansionRules.TerritorialYield(1f, 0f), Eps);     // 未開発は実らない
        }

        /// <summary>過伸長は(補給伸長＋防衛コスト)/経済力＝維持負荷が経済力を上回るほど国を食い潰す。</summary>
        [Test]
        public void Overstretch_LoadOverCapacity()
        {
            // load=(0.5+0.5)×1=1.0, ratio=1.0/1=1.0 → 0.5
            Assert.AreEqual(0.5f, FrontierExpansionRules.Overstretch(0.5f, 0.5f, 1f), Eps);
            // 負荷が小さく経済が厚い＝load=0.4, ratio=0.4/2=0.2 → 0.16667
            Assert.AreEqual(0.16667f, FrontierExpansionRules.Overstretch(0.3f, 0.1f, 2f), Eps);
            // 経済力が薄いほど過伸長は深い
            Assert.Greater(FrontierExpansionRules.Overstretch(0.6f, 0.6f, 0.5f),
                           FrontierExpansionRules.Overstretch(0.6f, 0.6f, 3f));
        }

        /// <summary>辺境の統治力低下は版図/統治能力＝統治能力に対し版図が大きいほど辺境を統べきれない。</summary>
        [Test]
        public void GovernanceDecay_SizeOverCapacity()
        {
            Assert.AreEqual(0.5f, FrontierExpansionRules.GovernanceDecay(1f, 1f), Eps);     // 比1で0.5
            Assert.AreEqual(0.66667f, FrontierExpansionRules.GovernanceDecay(2f, 1f), Eps); // 比2で2/3
            // 統治能力が厚いほど同じ版図でも統治は緩まない
            Assert.Greater(FrontierExpansionRules.GovernanceDecay(2f, 1f),
                           FrontierExpansionRules.GovernanceDecay(2f, 4f));
        }

        /// <summary>正味価値は果実−(過伸長＋統治低下)の平均＝正なら実り・負なら広げただけ傷む。</summary>
        [Test]
        public void NetExpansionValue_YieldMinusCosts()
        {
            // cost=(0.5+0.5)/2=0.5, net=0.5-0.5=0.0
            Assert.AreEqual(0.0f, FrontierExpansionRules.NetExpansionValue(0.5f, 0.5f, 0.5f), Eps);
            // 富み・代償少＝cost=0.2, net=0.6
            Assert.AreEqual(0.6f, FrontierExpansionRules.NetExpansionValue(0.8f, 0.2f, 0.2f), Eps);
            // 痩せ版図・大代償＝cost=0.8, net=-0.6（過伸長で傷む）
            Assert.AreEqual(-0.6f, FrontierExpansionRules.NetExpansionValue(0.2f, 0.8f, 0.8f), Eps);
        }

        /// <summary>持続可能判定は正味価値が閾値以上か＝既定閾値0で正なら拡大続行・負なら守勢。</summary>
        [Test]
        public void IsExpansionSustainable_ThresholdGate()
        {
            Assert.IsTrue(FrontierExpansionRules.IsExpansionSustainable(0.3f));   // 正＝持続可能
            Assert.IsFalse(FrontierExpansionRules.IsExpansionSustainable(-0.1f)); // 負＝過伸長
            Assert.IsTrue(FrontierExpansionRules.IsExpansionSustainable(0f));     // 既定閾値0と同値＝可
            // 高い閾値（慎重）なら同じ価値でも持続不可と判断する
            Assert.IsFalse(FrontierExpansionRules.IsExpansionSustainable(0.3f, 0.5f));
        }

        /// <summary>
        /// 物語：勝ちすぎが負ける。強大な軍と豊かな未開拓辺境を持つ勢力が一気に拡張するが、
        /// 兵站・経済・統治の能力を版図が追い越し、補給伸長と防衛コストが経済を食い、辺境の統治が緩む。
        /// 果実は薄く、過伸長と統治低下が勝って正味は負＝この拡大は持続可能でない（守勢へ転じるべき）。
        /// </summary>
        [Test]
        public void Story_OverextendedConquestBecomesUnsustainable()
        {
            // 一気に広げる：強軍×広い未開拓×十分な余剰
            float rate = FrontierExpansionRules.ExpansionRate(0.9f, 0.9f, 0.7f);
            Assert.Greater(rate, 0.5f, "強軍×未開拓×余剰なら拡張ペースは速い");

            // 広げた版図(大)が乏しい兵站/経済/統治能力を追い越す
            float stretch = FrontierExpansionRules.SupplyLineStretch(3f, 1f);       // 比3 → 0.75
            float defense = FrontierExpansionRules.FrontierDefenseCost(0.9f, 0.8f); // 長国境×高脅威
            float over = FrontierExpansionRules.Overstretch(stretch, defense, 1f);  // 経済薄
            float decay = FrontierExpansionRules.GovernanceDecay(3f, 1f);           // 比3 → 0.75
            // 急拡張で開発が追いつかず果実は薄い
            float yield = FrontierExpansionRules.TerritorialYield(0.9f, 0.2f);      // 開発度低

            float net = FrontierExpansionRules.NetExpansionValue(yield, over, decay);
            Assert.Less(net, 0f, "過伸長と統治低下が薄い果実を上回り正味は負");
            Assert.IsFalse(FrontierExpansionRules.IsExpansionSustainable(net),
                "正味が負なら拡大は持続可能でない＝守勢へ転じる");
        }
    }
}
