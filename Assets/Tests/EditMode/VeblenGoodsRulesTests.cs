using NUnit.Framework;
using UnityEngine;
using Ginei;
using VParams = Ginei.VeblenGoodsParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 地位財・顕示的消費（VEBL-1 #1593）を固定する：逆需要（高価格で需要増＝普通財と逆）、
    /// 誇示価値（見られてこそ）、希少プレミアム、スノッブ効果（普及で凡庸化）、最適高価格、
    /// 地位財判定（弾力性が正）、模倣品の威信希釈。既定 Params の具体値で期待を固定。
    /// </summary>
    public class VeblenGoodsRulesTests
    {
        // 逆需要：価格が高いほど需要が増える（普通財と逆＝地位財の核）
        [Test]
        public void VeblenDemand_HigherPrice_IncreasesDemand()
        {
            var p = VParams.Default;
            float low = VeblenGoodsRules.VeblenDemand(0.2f, 0.8f, p);
            float high = VeblenGoodsRules.VeblenDemand(0.8f, 0.8f, p);
            Assert.Greater(high, low); // 高価格ほど需要増（逆需要曲線）
        }

        // 逆需要：地位への感応度が高いほど高価格で需要が伸びる（財力なき層は反応しない＝頭打ち）
        [Test]
        public void VeblenDemand_LowSensitivity_CapsLow()
        {
            var p = VParams.Default;
            float sensitive = VeblenGoodsRules.VeblenDemand(0.9f, 1f, p);
            float poor = VeblenGoodsRules.VeblenDemand(0.9f, 0.1f, p);
            Assert.Greater(sensitive, poor); // 財力（感応度）で頭打ち＝買えない層は需要を出さない
        }

        // 誇示価値：高価格でも人目につかなければ0、見られてこそ価値が出る
        [Test]
        public void ConspicuousValue_RequiresVisibility()
        {
            var p = VParams.Default;
            float unseen = VeblenGoodsRules.ConspicuousValue(1f, 0f, p);
            float seen = VeblenGoodsRules.ConspicuousValue(1f, 1f, p);
            Assert.AreEqual(0f, unseen, 1e-4f); // 誰も見ない＝誇示価値0
            Assert.AreEqual(1f, seen, 1e-4f);   // 公然＝最大
        }

        // 希少プレミアム：希少なほど高い。完全希少で1、希少なしで0
        [Test]
        public void ExclusivityPremium_RisesWithExclusivity()
        {
            var p = VParams.Default;
            float none = VeblenGoodsRules.ExclusivityPremium(0f, p);
            float full = VeblenGoodsRules.ExclusivityPremium(1f, p);
            float mid = VeblenGoodsRules.ExclusivityPremium(0.5f, p);
            Assert.AreEqual(0f, none, 1e-4f);
            Assert.AreEqual(1f, full, 1e-4f);
            Assert.Less(mid, 0.5f); // 非線形（^1.5）＝中間は控えめ、極端な希少が跳ねる
        }

        // スノッブ効果：みなが持つと価値が下がる（普及で地位財でなくなる）
        [Test]
        public void SnobEffect_AdoptionErodesValue()
        {
            var p = VParams.Default; // snobStrength=0.8
            float rare = VeblenGoodsRules.SnobEffect(0f, p);     // 誰も持たない
            float common = VeblenGoodsRules.SnobEffect(1f, p);   // みなが持つ
            Assert.AreEqual(1f, rare, 1e-4f);                    // 希少＝価値満額
            Assert.AreEqual(1f - 0.8f, common, 1e-4f);           // 普及＝0.2へ凡庸化
        }

        // 最適価格：地位財は高くあるべき＝必ず下限以上、感応度×財力で1へ近づく
        [Test]
        public void OptimalVeblenPrice_StaysHigh()
        {
            var p = VParams.Default; // optimalFloor=0.6
            float lowMarket = VeblenGoodsRules.OptimalVeblenPrice(0f, 0f, p);
            float richMarket = VeblenGoodsRules.OptimalVeblenPrice(1f, 1f, p);
            Assert.AreEqual(0.6f, lowMarket, 1e-4f);  // 下限を割らない（地位財は安売りしない）
            Assert.AreEqual(1f, richMarket, 1e-4f);   // 感応・財力が高いと最高値
        }

        // 地位財判定：価格弾力性が正（右上がり）なら地位財、負（右下がり）なら普通財
        [Test]
        public void IsVeblenGood_PositiveElasticity()
        {
            Assert.IsTrue(VeblenGoodsRules.IsVeblenGood(0.3f));   // 値上げで需要増＝Veblen財
            Assert.IsFalse(VeblenGoodsRules.IsVeblenGood(-0.5f)); // 右下がり＝普通財
            Assert.IsFalse(VeblenGoodsRules.IsVeblenGood(0f));    // 無弾力＝地位財ではない
        }

        // 模倣品の希釈：偽物が出回るほど本物の威信が薄まる（誰も持てないが崩れる）
        [Test]
        public void CounterfeitDilution_FakesErodePrestige()
        {
            float genuine = VeblenGoodsRules.CounterfeitDilution(1f, 0f);  // 偽物なし
            float diluted = VeblenGoodsRules.CounterfeitDilution(1f, 0.5f);
            float flooded = VeblenGoodsRules.CounterfeitDilution(1f, 1f);  // 市場が偽物だらけ
            Assert.AreEqual(1f, genuine, 1e-4f);
            Assert.AreEqual(0.5f, diluted, 1e-4f);
            Assert.AreEqual(0f, flooded, 1e-4f); // 威信は0へ
        }
    }
}
