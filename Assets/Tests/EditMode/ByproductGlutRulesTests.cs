using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 副産物グルット（連産×市場＝供給過剰で価格暴落・廃棄コストの足枷）の純ロジック検証（#1113）。
    /// 既定 <see cref="ByproductGlutParams.Default"/>（暴落弾力性1・下限0.1・廃棄率0.5・貯蔵軽減0.1・有効利用0.8）で期待値を固定。
    /// </summary>
    public class ByproductGlutRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>従産物供給＝主産物生産×固定比（連れ出される量・負はクランプ）。</summary>
        [Test]
        public void ByproductSupply_主産物に固定比で連れ出される()
        {
            Assert.AreEqual(30f, ByproductGlutRules.ByproductSupply(100f, 0.3f), Eps);
            Assert.AreEqual(0f, ByproductGlutRules.ByproductSupply(-100f, 0.3f), Eps);
            Assert.AreEqual(0f, ByproductGlutRules.ByproductSupply(100f, -0.3f), Eps);
        }

        /// <summary>供給過剰度＝需要を超えた捌けない割合。需要が呑めば0・需要0で全量だぶつけば1。</summary>
        [Test]
        public void GlutSeverity_需要超過分が過剰度()
        {
            // 供給100・需要40＝過剰60／100＝0.6
            Assert.AreEqual(0.6f, ByproductGlutRules.GlutSeverity(100f, 40f), Eps);
            // 需要が供給以上＝過剰なし
            Assert.AreEqual(0f, ByproductGlutRules.GlutSeverity(100f, 120f), Eps);
            // 需要0で供給あり＝全量だぶつき
            Assert.AreEqual(1f, ByproductGlutRules.GlutSeverity(100f, 0f), Eps);
            // 供給なし＝過剰なし
            Assert.AreEqual(0f, ByproductGlutRules.GlutSeverity(0f, 50f), Eps);
        }

        /// <summary>価格暴落＝供給過剰ほど値崩れ。過剰なしで1.0・需要の数倍出れば下限0.1へ張り付く。</summary>
        [Test]
        public void PriceCollapse_供給過剰ほど値崩れ()
        {
            // グルット0＝暴落なし
            Assert.AreEqual(1f, ByproductGlutRules.PriceCollapse(0f), Eps);
            // グルット0.6＝(1-0.6)^1=0.4
            Assert.AreEqual(0.4f, ByproductGlutRules.PriceCollapse(0.6f), Eps);
            // グルット1＝下限0.1へ張り付く（二束三文）
            Assert.AreEqual(0.1f, ByproductGlutRules.PriceCollapse(1f), Eps);
        }

        /// <summary>廃棄コスト＝貯蔵できない従産物は満額・貯蔵できれば軽減（負の価値）。</summary>
        [Test]
        public void DisposalCost_貯蔵不可は満額で重い()
        {
            // グルット0.6・貯蔵不可＝0.6×0.5×1＝0.3
            Assert.AreEqual(0.3f, ByproductGlutRules.DisposalCost(0.6f, false), Eps);
            // 貯蔵可＝0.6×0.5×0.1＝0.03（在庫に積めるぶん安い）
            Assert.AreEqual(0.03f, ByproductGlutRules.DisposalCost(0.6f, true), Eps);
            // 貯蔵不可が貯蔵可より高くつく
            Assert.Greater(ByproductGlutRules.DisposalCost(0.6f, false),
                ByproductGlutRules.DisposalCost(0.6f, true));
        }

        /// <summary>有効利用＝川下産業があれば過剰が価値に転じる（コークス→化学型）。</summary>
        [Test]
        public void ByproductValorization_川下産業が過剰を価値化()
        {
            // グルット0.6・川下0.5＝0.6×0.5×0.8＝0.24
            Assert.AreEqual(0.24f, ByproductGlutRules.ByproductValorization(0.6f, 0.5f), Eps);
            // 川下なし＝価値化なし
            Assert.AreEqual(0f, ByproductGlutRules.ByproductValorization(0.6f, 0f), Eps);
        }

        /// <summary>主産物への足枷＝従産物の廃棄コストが主産物の利幅を蝕む（連産の負債）。</summary>
        [Test]
        public void PrimaryProductionDrag_廃棄コストが採算を削る()
        {
            // 廃棄0.3・利幅1.0＝1-0.3/1=0.7
            Assert.AreEqual(0.7f, ByproductGlutRules.PrimaryProductionDrag(0.3f, 1f), Eps);
            // 廃棄コスト>利幅＝赤字転落で採算ゼロ
            Assert.AreEqual(0f, ByproductGlutRules.PrimaryProductionDrag(0.5f, 0.3f), Eps);
            // 利幅厚いほど足枷が軽い（健全）
            Assert.Greater(ByproductGlutRules.PrimaryProductionDrag(0.3f, 2f),
                ByproductGlutRules.PrimaryProductionDrag(0.3f, 1f));
        }

        /// <summary>連鎖：主産物増産→従産物だぶつき→価格暴落かつ廃棄コストが主産物の足を引っ張る（#1113の核）。</summary>
        [Test]
        public void 連鎖_主産物を作るほど従産物が足を引っ張る()
        {
            // 主産物200を比0.4で出すと従産物80。市場需要は20しかない。
            float supply = ByproductGlutRules.ByproductSupply(200f, 0.4f); // 80
            float glut = ByproductGlutRules.GlutSeverity(supply, 20f);      // (80-20)/80=0.75
            float price = ByproductGlutRules.PriceCollapse(glut);           // 0.25
            float disposal = ByproductGlutRules.DisposalCost(glut, false);  // 0.75×0.5=0.375
            float drag = ByproductGlutRules.PrimaryProductionDrag(disposal, 0.5f); // 1-0.375/0.5=0.25

            Assert.AreEqual(80f, supply, Eps);
            Assert.AreEqual(0.75f, glut, Eps);
            Assert.AreEqual(0.25f, price, Eps);   // 暴落
            Assert.AreEqual(0.375f, disposal, Eps);
            Assert.AreEqual(0.25f, drag, Eps);    // 主産物の採算が大きく削られる＝連産の負債
        }
    }
}
