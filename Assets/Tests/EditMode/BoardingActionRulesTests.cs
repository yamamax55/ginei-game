using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class BoardingActionRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void GrappleSuccess_EqualSpeed_IsFifty()
        {
            // 拮抗（速度差なし）＝五分。
            Assert.AreEqual(0.5f, BoardingActionRules.GrappleSuccess(3f, 3f), Eps);
        }

        [Test]
        public void GrappleSuccess_FastApproachLowEvasion_HighChance()
        {
            // 速い接近×低回避＝接舷しやすい。diff=(2-0)*2=4 → 0.5+0.5*(4/5)=0.9
            Assert.AreEqual(0.9f, BoardingActionRules.GrappleSuccess(2f, 0f), Eps);
        }

        [Test]
        public void BoardingForce_QualityRaisesForce()
        {
            // 練度0＝頭数のみ100、練度1＝160。質が戦力に乗る。
            Assert.AreEqual(100f, BoardingActionRules.BoardingForce(100f, 0f), Eps);
            Assert.AreEqual(160f, BoardingActionRules.BoardingForce(100f, 1f), Eps);
        }

        [Test]
        public void ShipboardCombat_Advantage_IsSigned()
        {
            // 乗り込み戦力優勢で +、守備優勢で −。(160-40)/200=0.6
            Assert.AreEqual(0.6f, BoardingActionRules.ShipboardCombat(160f, 40f), PowEps);
            Assert.Less(BoardingActionRules.ShipboardCombat(40f, 160f), 0f);
        }

        [Test]
        public void CaptureProgress_AdvancesOnlyWhenWinning()
        {
            // 白兵優勢のときだけ掌握が進む。0.6*0.2*2=0.24
            Assert.AreEqual(0.24f, BoardingActionRules.CaptureProgress(0.6f, 2f), Eps);
            // 守備優勢（負）なら進まない。
            Assert.AreEqual(0f, BoardingActionRules.CaptureProgress(-0.5f, 5f), Eps);
        }

        [Test]
        public void CaptureVsDestroy_ComparesValue()
        {
            // 拿捕価値が高いと + に振れる。(8-2)/10=0.6
            Assert.AreEqual(0.6f, BoardingActionRules.CaptureVsDestroy(8f, 2f), Eps);
            // 互角は0。
            Assert.AreEqual(0f, BoardingActionRules.CaptureVsDestroy(5f, 5f), Eps);
        }

        [Test]
        public void RepelBoarders_GarrisonAndResolve()
        {
            // 守備兵なしは撃退不能＝0。
            Assert.AreEqual(0f, BoardingActionRules.RepelBoarders(0f, 1f), Eps);
            // 守備兵4×覚悟1 → effective=6 → 6/7。
            Assert.AreEqual(6f / 7f, BoardingActionRules.RepelBoarders(4f, 1f), Eps);
        }

        [Test]
        public void PrizeValue_IntactIsWorthMore()
        {
            // 無傷なほど戦利品価値が高い（健全度×艦種価値）。
            Assert.AreEqual(1000f, BoardingActionRules.PrizeValue(1f, 1000f), Eps);
            Assert.AreEqual(500f, BoardingActionRules.PrizeValue(0.5f, 1000f), Eps);
        }

        [Test]
        public void IsShipCaptured_AtThreshold()
        {
            Assert.IsFalse(BoardingActionRules.IsShipCaptured(0.8f, 1f));
            Assert.IsTrue(BoardingActionRules.IsShipCaptured(1f, 1f));
        }

        [Test]
        public void Story_RosenRitterBoardsAndCaptures()
        {
            // ローゼンリッター＝精鋭陸戦隊が高速接近で接舷し、艦内白兵で制圧、無傷で拿捕する。
            // だが艦内防御兵が強ければ撃退される。
            var p = BoardingActionParams.Default;

            // 接舷成功（速い接近・低回避）。
            float grapple = BoardingActionRules.GrappleSuccess(2.5f, 0.5f, p);
            Assert.Greater(grapple, 0.8f);

            // 精鋭陸戦隊が乗り込む（高練度）。
            float force = BoardingActionRules.BoardingForce(120f, 0.9f, p);
            // 手薄な守備（駆逐艦の少人数）。
            float weakGarrison = 30f;
            float combat = BoardingActionRules.ShipboardCombat(force, weakGarrison, p);
            Assert.Greater(combat, 0f); // 乗り込み側が優勢

            // 数秒かけて掌握が進み、閾値で拿捕成立。
            float progress = 0f;
            for (int i = 0; i < 30; i++)
                progress += BoardingActionRules.CaptureProgress(combat, 1f, p);
            Assert.IsTrue(BoardingActionRules.IsShipCaptured(progress, 1f));

            // 無傷で拿捕した艦は戦利品価値が高い（撃沈より拿捕が優位）。
            float prize = BoardingActionRules.PrizeValue(0.95f, 800f);
            Assert.AreEqual(0.95f * 800f, prize, Eps);
            Assert.Greater(BoardingActionRules.CaptureVsDestroy(prize, 100f), 0f);

            // 一方、要塞級の重武装守備に当たれば白兵で負け、撃退される。
            float strongGarrison = 400f;
            float combatVsFortress = BoardingActionRules.ShipboardCombat(force, strongGarrison, p);
            Assert.Less(combatVsFortress, 0f); // 守備が圧倒
            Assert.AreEqual(0f, BoardingActionRules.CaptureProgress(combatVsFortress, 10f, p), Eps); // 掌握進まず
            float repel = BoardingActionRules.RepelBoarders(strongGarrison, 1f, p);
            Assert.Greater(repel, 0.9f); // 強力に撃退される
        }
    }
}
