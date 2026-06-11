using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 反者道之動（<see cref="ReversalRules"/>・#1550 LAOZ-2）の純ロジックを担保する。
    /// 逆U字のピークと両端・反転点・ピーク後の反転・極端の反動・逓減から反転・循環・禍福・ピーク通過判定を、
    /// 既定 <see cref="ReversalParams.Default"/>（鋭さ1・反動0.5・禍福閾値0.8・反転急峻1）の具体値で固定する。
    /// </summary>
    public class ReversalRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>逆U字＝ピークで最大1・両端で0に落ちる（強さ・繁栄は頂点で反転）。</summary>
        [Test]
        public void InvertedU_ピークで最大両端で0()
        {
            Assert.AreEqual(1f, ReversalRules.InvertedU(0.5f, 0.5f), Eps, "ピークで最大1");
            Assert.AreEqual(0f, ReversalRules.InvertedU(0f, 0.5f), Eps, "左端で0");
            Assert.AreEqual(0f, ReversalRules.InvertedU(1f, 0.5f), Eps, "右端で0");
            // ピークと端の中間は0より大・1より小（山なり）。
            float mid = ReversalRules.InvertedU(0.25f, 0.5f);
            Assert.Greater(mid, 0f);
            Assert.Less(mid, 1f);
        }

        /// <summary>逆U字＝非対称ピーク（peak=0.8）でもピークで1・両端で0。</summary>
        [Test]
        public void InvertedU_非対称ピーク()
        {
            Assert.AreEqual(1f, ReversalRules.InvertedU(0.8f, 0.8f), Eps);
            Assert.AreEqual(0f, ReversalRules.InvertedU(0f, 0.8f), Eps);
            Assert.AreEqual(0f, ReversalRules.InvertedU(1f, 0.8f), Eps);
        }

        /// <summary>反転点＝閾値という極を超えたかの判定（境界は到達扱い）。</summary>
        [Test]
        public void TippingPoint_閾値超過で反転()
        {
            Assert.IsTrue(ReversalRules.TippingPoint(0.7f, 0.6f), "超過で反転");
            Assert.IsTrue(ReversalRules.TippingPoint(0.6f, 0.6f), "境界ちょうどは到達");
            Assert.IsFalse(ReversalRules.TippingPoint(0.5f, 0.6f), "未満は未反転");
        }

        /// <summary>ピーク後の反転＝peak以下は0、過ぎた分だけ反る（既定急峻1）。</summary>
        [Test]
        public void ReversalAfterPeak_ピーク後に反る()
        {
            Assert.AreEqual(0f, ReversalRules.ReversalAfterPeak(0.5f, 0.6f), Eps, "ピーク以下は反らない");
            Assert.AreEqual(0f, ReversalRules.ReversalAfterPeak(0.6f, 0.6f), Eps, "ピークちょうどは0");
            // peak=0.6, value=0.8 → (0.8-0.6)/(1-0.6)=0.5、急峻1で0.5。
            Assert.AreEqual(0.5f, ReversalRules.ReversalAfterPeak(0.8f, 0.6f), Eps);
            // 極（value=1）で最大1。
            Assert.AreEqual(1f, ReversalRules.ReversalAfterPeak(1f, 0.6f), Eps);
        }

        /// <summary>極端の反動＝閾値以下は0、超えるほど反動が増す（強さが弱さに・既定反動0.5）。</summary>
        [Test]
        public void ExtremeBacklash_極端ほど反動()
        {
            Assert.AreEqual(0f, ReversalRules.ExtremeBacklash(0.5f, 0.6f), Eps, "閾値以下は反動なし");
            // intensity=0.8, thr=0.6 → excess=(0.8-0.6)/0.4=0.5、×0.5=0.25。
            Assert.AreEqual(0.25f, ReversalRules.ExtremeBacklash(0.8f, 0.6f), Eps);
            // 極（1.0）→ excess=1、×0.5=0.5。
            Assert.AreEqual(0.5f, ReversalRules.ExtremeBacklash(1f, 0.6f), Eps);
        }

        /// <summary>逓減から反転＝最適点で最大の得・両端で失（得が失に転じる）。</summary>
        [Test]
        public void DiminishingThenReversing_最適点で最大両端で失()
        {
            // 最適点でInvertedU=1 → 2*1-1=1（最大の得）。
            Assert.AreEqual(1f, ReversalRules.DiminishingThenReversing(0.5f, 0.5f), Eps);
            // 両端でInvertedU=0 → -1（最大の失）。
            Assert.AreEqual(-1f, ReversalRules.DiminishingThenReversing(0f, 0.5f), Eps);
            Assert.AreEqual(-1f, ReversalRules.DiminishingThenReversing(1f, 0.5f), Eps);
        }

        /// <summary>循環＝0と1が繋がり位相0.5で最大（極まれば始めに還る）。</summary>
        [Test]
        public void CyclicalReturn_循環の円環()
        {
            Assert.AreEqual(0f, ReversalRules.CyclicalReturn(0f), Eps, "位相0で0");
            Assert.AreEqual(1f, ReversalRules.CyclicalReturn(0.5f), Eps, "位相0.5で最大1");
            Assert.AreEqual(0f, ReversalRules.CyclicalReturn(1f), Eps, "位相1で再び0（環が閉じる）");
        }

        /// <summary>禍福は糾える縄＝閾値以下は0、高すぎる幸運ほど反転リスク（福に禍が伏す・既定閾値0.8）。</summary>
        [Test]
        public void FortuneMisfortune_極まる福に禍が伏す()
        {
            Assert.AreEqual(0f, ReversalRules.FortuneMisfortune(0.8f), Eps, "閾値0.8以下はリスク0");
            // fortune=0.9, thr=0.8 → (0.9-0.8)/(1-0.8)=0.5。
            Assert.AreEqual(0.5f, ReversalRules.FortuneMisfortune(0.9f), Eps);
            Assert.AreEqual(1f, ReversalRules.FortuneMisfortune(1f), Eps, "幸運の極で反転リスク最大");
        }

        /// <summary>ピーク通過判定＝盛りを過ぎ衰退局面か（境界ちょうどは未通過）。</summary>
        [Test]
        public void IsPastPeak_反転局面判定()
        {
            Assert.IsTrue(ReversalRules.IsPastPeak(0.7f, 0.6f), "超過で反転局面");
            Assert.IsFalse(ReversalRules.IsPastPeak(0.6f, 0.6f), "境界ちょうどは未通過");
            Assert.IsFalse(ReversalRules.IsPastPeak(0.5f, 0.6f), "未満は盛りの内");
        }
    }
}
