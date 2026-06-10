using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>RefitPurchaseRules（改装/復元＋建造vs購入・#1068）の純ロジックテスト。</summary>
    public class RefitPurchaseRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>改装コスト＝古い艦体ほど同じ向上幅でも高くつく（船体の限界）。</summary>
        [Test]
        public void RefitCost_OlderHullCostsMore()
        {
            // 若い艦(age0)：0.5*0.4*(1+0)=0.2／老朽艦(age1)：0.5*0.4*(1+1)=0.4。
            float young = RefitPurchaseRules.RefitCost(0f, 0.4f);
            float old = RefitPurchaseRules.RefitCost(1f, 0.4f);
            Assert.AreEqual(0.2f, young, Eps);
            Assert.AreEqual(0.4f, old, Eps);
            Assert.Greater(old, young);
        }

        /// <summary>改装の性能上限＝老朽艦は改装しても新造(1.0)に及ばない（古い器の限界）。</summary>
        [Test]
        public void RefitPerformanceCeiling_DecaysWithAgeAndStaysBelowNewBuild()
        {
            // age0：0.85／age0.5：0.85-0.25=0.6／age1：0.85-0.5=0.35→下限0.4。
            float young = RefitPurchaseRules.RefitPerformanceCeiling(0f);
            float mid = RefitPurchaseRules.RefitPerformanceCeiling(0.5f);
            float old = RefitPurchaseRules.RefitPerformanceCeiling(1f);
            Assert.AreEqual(0.85f, young, Eps);
            Assert.AreEqual(0.6f, mid, Eps);
            Assert.AreEqual(0.4f, old, Eps); // 下限でクランプ
            Assert.Less(young, 1.0f);        // 新造には届かない
        }

        /// <summary>急ぐなら買う＝urgency が高いと短納期の購入が有利になる。</summary>
        [Test]
        public void BuildVsBuyDecision_UrgencyFavorsFastDelivery()
        {
            // 建造費1.0/建造時間10、購入価格1.5/納期2。
            // urgency0：buy 1.5 - build 1.0 = +0.5（建造有利）。
            float relaxed = RefitPurchaseRules.BuildVsBuyDecision(1.0f, 10f, 1.5f, 2f, 0f);
            Assert.AreEqual(0.5f, relaxed, Eps);
            Assert.Greater(relaxed, 0f);

            // urgency1：build 1.0+10*0.6=7.0、buy 1.5+2*0.6=2.7、戻り 2.7-7.0=-4.3（購入有利）。
            float urgent = RefitPurchaseRules.BuildVsBuyDecision(1.0f, 10f, 1.5f, 2f, 1f);
            Assert.AreEqual(-4.3f, urgent, Eps);
            Assert.Less(urgent, 0f);
        }

        /// <summary>安くしたいなら造る＝価格が同等でも納期が同じなら建造費の安い建造が有利。</summary>
        [Test]
        public void BuildVsBuyDecision_CheaperBuildWinsWhenNotUrgent()
        {
            // 建造費0.8、購入価格1.2、時間同条件・非急務。戻り 1.2-0.8=+0.4（建造有利）。
            float v = RefitPurchaseRules.BuildVsBuyDecision(0.8f, 5f, 1.2f, 5f, 0f);
            Assert.AreEqual(0.4f, v, Eps);
            Assert.Greater(v, 0f);
        }

        /// <summary>購入の依存リスク＝他勢力供給に頼るほど握られる（自給で0）。</summary>
        [Test]
        public void PurchaseDependency_RisesWithForeignReliance()
        {
            Assert.AreEqual(0f, RefitPurchaseRules.PurchaseDependency(0f), Eps);   // 完全自給
            Assert.AreEqual(0.35f, RefitPurchaseRules.PurchaseDependency(0.5f), Eps); // 0.5*0.7
            Assert.AreEqual(0.7f, RefitPurchaseRules.PurchaseDependency(1f), Eps);  // 1*0.7
        }

        /// <summary>艦齢が分岐点＝若い艦は改装が得・老朽艦は新造が得。</summary>
        [Test]
        public void RefitVsReplaceValue_AgeIsTheBreakPoint()
        {
            // 改装費0.4、新造費1.0。
            // 若い艦(age0.3<=0.5)：overAge0→改装実効0.4、戻り 1.0-0.4=+0.6（改装有利）。
            float young = RefitPurchaseRules.RefitVsReplaceValue(0.3f, 0.4f, 1.0f);
            Assert.AreEqual(0.6f, young, Eps);
            Assert.Greater(young, 0f);

            // 老朽艦(age1)：overAge0.5→改装実効0.4*(1+0.5*1.0)=0.6、戻り 1.0-0.6=+0.4。
            float old = RefitPurchaseRules.RefitVsReplaceValue(1f, 0.4f, 1.0f);
            Assert.AreEqual(0.4f, old, Eps);
            // 老朽艦ほど改装の優位が縮む（同じ費用なのに得が減る）。
            Assert.Less(old, young);
        }

        /// <summary>老朽艦への高額改装は新造が得になる（分岐の反転）。</summary>
        [Test]
        public void RefitVsReplaceValue_ExpensiveRefitOnOldHullFavorsReplace()
        {
            // 老朽艦(age1)・改装費0.8・新造費1.0：改装実効 0.8*1.5=1.2、戻り 1.0-1.2=-0.2（新造有利）。
            float v = RefitPurchaseRules.RefitVsReplaceValue(1f, 0.8f, 1.0f);
            Assert.AreEqual(-0.2f, v, Eps);
            Assert.Less(v, 0f);
        }

        /// <summary>調達推奨＝急務は購入・予算難で若い艦は改装・それ以外は新造の三択。</summary>
        [Test]
        public void AcquisitionRecommendation_PicksMethodBySituation()
        {
            // 急務（urgency0.8）＝速さを買う＝購入。
            Assert.AreEqual(AcquisitionMethod.購入,
                RefitPurchaseRules.AcquisitionRecommendation(0.8f, 0.9f, 0.2f));
            // 予算難（budget0.3）かつ艦体若い（age0.3）＝改装で延命。
            Assert.AreEqual(AcquisitionMethod.改装,
                RefitPurchaseRules.AcquisitionRecommendation(0.2f, 0.3f, 0.3f));
            // 予算難でも老朽艦（age0.9>0.5）＝改装の限界＝新造。
            Assert.AreEqual(AcquisitionMethod.新造,
                RefitPurchaseRules.AcquisitionRecommendation(0.2f, 0.3f, 0.9f));
            // 予算潤沢＝自由に新造。
            Assert.AreEqual(AcquisitionMethod.新造,
                RefitPurchaseRules.AcquisitionRecommendation(0.2f, 0.9f, 0.3f));
        }
    }
}
