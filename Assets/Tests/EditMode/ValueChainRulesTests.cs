using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// バリューチェーン（#1023）を固定する：森→木→製材→家と各段の付加価値が累積価値に積み上がる・
    /// 連鎖歩留まりは各段の積（1段でも悪いと全体が痩せる）・付加価値の取り分の分布（スマイルカーブ＝川上/川下の儲け）・
    /// 歩留まり律速段・垂直統合で取り込む中間マージン（既定Params maxMarginPerStage=0.30）とクランプを担保。
    /// </summary>
    public class ValueChainRulesTests
    {
        private static readonly ValueChainParams P = ValueChainParams.Default; // maxMarginPerStage=0.30

        // 森→木→製材→家の4段連鎖。
        // 森: 投入(原料賦存)10・付加5・歩留0.9 / 木: 付加10・歩留0.8 / 製材: 付加20・歩留0.5(律速) / 家: 付加40・歩留0.95
        private static ValueChainStage[] ForestToHouse()
            => new[]
            {
                new ValueChainStage(10f, 5f, 0.9f, "森"),
                new ValueChainStage(0f, 10f, 0.8f, "木"),
                new ValueChainStage(0f, 20f, 0.5f, "製材"),
                new ValueChainStage(0f, 40f, 0.95f, "家"),
            };

        [Test]
        public void AddedValue_ReturnsClampedValueAdded()
        {
            Assert.AreEqual(20f, ValueChainRules.AddedValue(new ValueChainStage(0f, 20f, 1f)), 1e-4f);
            // 負はクランプ
            Assert.AreEqual(0f, ValueChainRules.AddedValue(new ValueChainStage(0f, -5f, 1f)), 1e-4f);
            // null は0
            Assert.AreEqual(0f, ValueChainRules.AddedValue(null), 1e-4f);
        }

        [Test]
        public void CumulativeValue_AddsBaseAndAllValueAdded()
        {
            // 起点の原料10 ＋ 付加(5+10+20+40)=75 → 累積85
            Assert.AreEqual(85f, ValueChainRules.CumulativeValue(ForestToHouse()), 1e-4f);
            // 空は0
            Assert.AreEqual(0f, ValueChainRules.CumulativeValue(new ValueChainStage[0]), 1e-4f);
        }

        [Test]
        public void ChainYield_IsProductOfStageYields()
        {
            // 0.9×0.8×0.5×0.95 = 0.342（1段でも悪いと全体が痩せる）
            Assert.AreEqual(0.342f, ValueChainRules.ChainYield(ForestToHouse()), 1e-4f);
            // 段が無ければ無損失1.0
            Assert.AreEqual(1f, ValueChainRules.ChainYield(new ValueChainStage[0]), 1e-4f);
        }

        [Test]
        public void ValueCaptureByStage_DistributesByValueAdded()
        {
            // 付加合計75 → 森5/75・木10/75・製材20/75・家40/75（スマイルカーブ＝最終財の家が最大の取り分）
            float[] share = ValueChainRules.ValueCaptureByStage(ForestToHouse());
            Assert.AreEqual(4, share.Length);
            Assert.AreEqual(5f / 75f, share[0], 1e-4f);
            Assert.AreEqual(10f / 75f, share[1], 1e-4f);
            Assert.AreEqual(20f / 75f, share[2], 1e-4f);
            Assert.AreEqual(40f / 75f, share[3], 1e-4f);
            // 総和は1
            Assert.AreEqual(1f, share[0] + share[1] + share[2] + share[3], 1e-4f);
            // 川下(家)が川上(森)より儲かる
            Assert.Greater(share[3], share[0]);
        }

        [Test]
        public void ValueCaptureByStage_ZeroWhenNoValueAdded()
        {
            float[] share = ValueChainRules.ValueCaptureByStage(new[]
            {
                new ValueChainStage(10f, 0f, 1f),
                new ValueChainStage(0f, 0f, 1f),
            });
            Assert.AreEqual(0f, share[0], 1e-4f);
            Assert.AreEqual(0f, share[1], 1e-4f);
        }

        [Test]
        public void BottleneckStageByYield_FindsWorstYield()
        {
            // 製材(歩留0.5・インデックス2)が律速
            Assert.AreEqual(2, ValueChainRules.BottleneckStageByYield(ForestToHouse()));
            // 空は-1
            Assert.AreEqual(-1, ValueChainRules.BottleneckStageByYield(new ValueChainStage[0]));
        }

        [Test]
        public void VerticalIntegrationGain_CapturesMiddleMargins()
        {
            // 受け渡し3回 × 付加合計75 × マージン0.2 = 45（森から家まで一貫生産で中間業者の取り分を取り込む）
            Assert.AreEqual(45f, ValueChainRules.VerticalIntegrationGain(ForestToHouse(), 0.2f, P), 1e-4f);
            // 既定オーバーロードも同じ
            Assert.AreEqual(45f, ValueChainRules.VerticalIntegrationGain(ForestToHouse(), 0.2f), 1e-4f);
        }

        [Test]
        public void VerticalIntegrationGain_ClampedAndEdgeCases()
        {
            // マージンは maxMarginPerStage=0.30 で頭打ち：0.5指定でも0.30 → 3×75×0.30=67.5
            Assert.AreEqual(67.5f, ValueChainRules.VerticalIntegrationGain(ForestToHouse(), 0.5f, P), 1e-4f);
            // 1段以下は受け渡しが無い＝利得0
            Assert.AreEqual(0f, ValueChainRules.VerticalIntegrationGain(
                new[] { new ValueChainStage(0f, 40f, 1f) }, 0.2f, P), 1e-4f);
        }
    }
}
