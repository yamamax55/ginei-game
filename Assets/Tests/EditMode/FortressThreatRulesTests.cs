using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 要塞主砲の危険域回避（#641 EX4-3 ハメ防止）：InDangerZone（危険帯判定）と SteerClear（横へ散開する目標補正）。
    /// 既定 FortressThreatParams（半幅余裕1.5・回避強度2.0）。
    /// </summary>
    public class FortressThreatRulesTests
    {
        // ── 危険帯判定 ──

        [Test]
        public void InDanger_OnTheLine_WithinBand()
        {
            // aim=+X、minRange8/maxRange48/halfWidth1.6。射線上 along=20 の点は危険。
            Assert.IsTrue(FortressThreatRules.InDangerZone(new Vector2(20f, 0f), Vector2.right, 8f, 48f, 1.6f, 1.5f));
        }

        [Test]
        public void Safe_InsideMinRange_Pocket()
        {
            // 懐（along=5 < minRange8）＝撃たれない＝安全。
            Assert.IsFalse(FortressThreatRules.InDangerZone(new Vector2(5f, 0f), Vector2.right, 8f, 48f, 1.6f, 1.5f));
        }

        [Test]
        public void Safe_BeyondMaxRange_AndBehind_AndOffLine()
        {
            Assert.IsFalse(FortressThreatRules.InDangerZone(new Vector2(60f, 0f), Vector2.right, 8f, 48f, 1.6f, 1.5f)); // 射程外
            Assert.IsFalse(FortressThreatRules.InDangerZone(new Vector2(-20f, 0f), Vector2.right, 8f, 48f, 1.6f, 1.5f)); // 後方
            Assert.IsFalse(FortressThreatRules.InDangerZone(new Vector2(20f, 5f), Vector2.right, 8f, 48f, 1.6f, 1.5f)); // 横に5（>1.6+1.5）
        }

        [Test]
        public void InDanger_WithinWidthMargin()
        {
            // perp=3.0 は halfWidth1.6+margin1.5=3.1 内＝まだ危険（余裕ぶんも避ける）。
            Assert.IsTrue(FortressThreatRules.InDangerZone(new Vector2(20f, 3.0f), Vector2.right, 8f, 48f, 1.6f, 1.5f));
        }

        // ── 目標補正（横へ散開）──

        [Test]
        public void SteerClear_PushesLaterally_WhenInDanger()
        {
            // 要塞 origin=0、aim=+X。艦 pos=(20, 0.5)（射線上やや上）、目標は前方 (40,0.5)。
            // 危険域なので横（+Y側＝今いる側）へ逸れる＝結果の y が元の目標より上にずれる。
            Vector2 origin = Vector2.zero;
            Vector2 pos = new Vector2(20f, 0.5f);
            Vector2 desired = new Vector2(40f, 0.5f);
            Vector2 outDest = FortressThreatRules.SteerClear(pos, desired, origin, Vector2.right, 8f, 48f, 1.6f);
            Assert.AreNotEqual(desired, outDest);          // 目標が補正された
            Assert.Greater(outDest.y, desired.y);          // 射線（y=0）から離れる向き（+Y）へ
            // 目的地までの距離は保たれる（移動量を勝手に変えない）。
            Assert.AreEqual((desired - pos).magnitude, (outDest - pos).magnitude, 1e-3f);
        }

        [Test]
        public void SteerClear_NoChange_WhenSafe()
        {
            // 懐（along=5）＝安全＝目標そのまま。
            Vector2 origin = Vector2.zero;
            Vector2 pos = new Vector2(5f, 0f);
            Vector2 desired = new Vector2(2f, 0f); // さらに懐へ
            Vector2 outDest = FortressThreatRules.SteerClear(pos, desired, origin, Vector2.right, 8f, 48f, 1.6f);
            Assert.AreEqual(desired, outDest);
        }

        [Test]
        public void SteerClear_DeadCenter_PicksASide()
        {
            // 射線ど真ん中（perp=0）でも例外なく横へ逸れる（左 perpDir=(-ay,ax)=(0,1)）。
            Vector2 origin = Vector2.zero;
            Vector2 pos = new Vector2(20f, 0f);
            Vector2 desired = new Vector2(40f, 0f);
            Vector2 outDest = FortressThreatRules.SteerClear(pos, desired, origin, Vector2.right, 8f, 48f, 1.6f);
            Assert.AreNotEqual(desired.y, outDest.y); // y方向に逸れた
        }

        [Test]
        public void SteerClear_ZeroAim_NoThrow()
        {
            Vector2 outDest = FortressThreatRules.SteerClear(
                new Vector2(20f, 0f), new Vector2(40f, 0f), Vector2.zero, Vector2.zero, 8f, 48f, 1.6f);
            Assert.IsFalse(float.IsNaN(outDest.x) || float.IsNaN(outDest.y));
        }
    }
}
