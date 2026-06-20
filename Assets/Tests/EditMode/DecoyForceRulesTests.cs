using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>囮部隊・陽動（偽の主攻で敵の注意・戦力を引きつけ主攻を手薄にする）純ロジックの EditMode テスト（既定 Params 期待値固定）。</summary>
    public class DecoyForceRulesTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void 説得力は兵力見栄えと行動派手さの混合()
        {
            // strengthLook=clamp01(50/100)=0.5, behaviorLook=0.8, lerp(0.5,0.8,0.5)=0.65。
            Assert.AreEqual(0.65f, DecoyForceRules.Plausibility(50f, 0.8f), Eps);
            // 兵力ゼロ・行動ゼロは説得力ゼロ。
            Assert.AreEqual(0f, DecoyForceRules.Plausibility(0f, 0f), Eps);
            // 基準兵力を超えても見栄えは上限1.0：lerp(1,0.2,0.5)=0.6。
            Assert.AreEqual(0.6f, DecoyForceRules.Plausibility(200f, 0.2f), Eps);
        }

        [Test]
        public void 慎重な敵ほど囮へ戦力を割く()
        {
            // bite=lerp(0.5,1,0.5)=0.75, 0.65*0.75=0.4875。
            Assert.AreEqual(0.4875f, DecoyForceRules.EnemyForcesDrawn(0.65f, 0.5f), Eps);
            // 慎重さゼロでも MinBite=0.5 は食いつく：0.8*0.5=0.4。
            Assert.AreEqual(0.4f, DecoyForceRules.EnemyForcesDrawn(0.8f, 0f), Eps);
            // 説得力満点＆最も慎重なら全力で食いつく上限1.0。
            Assert.AreEqual(1f, DecoyForceRules.EnemyForcesDrawn(1f, 1f), Eps);
        }

        [Test]
        public void 主攻正面の手薄化は割合かける敵総兵力()
        {
            // 0.4875*1000=487.5。
            Assert.AreEqual(487.5f, DecoyForceRules.MainEffortRelief(0.4875f, 1000f), Eps);
            // 食いつき割合に比例。
            Assert.AreEqual(400f, DecoyForceRules.MainEffortRelief(0.4f, 1000f), Eps);
            // 敵総兵力が無ければ手薄化ゼロ。
            Assert.AreEqual(0f, DecoyForceRules.MainEffortRelief(0.5f, 0f), Eps);
        }

        [Test]
        public void 囮損害は敵反応の半分_囮兵力を超えない()
        {
            // 80*0.5=40, ≤50 → 40。
            Assert.AreEqual(40f, DecoyForceRules.DecoyAttrition(50f, 80f), Eps);
            // 200*0.5=100 だが囮兵力50で頭打ち。
            Assert.AreEqual(50f, DecoyForceRules.DecoyAttrition(50f, 200f), Eps);
            // 敵反応が無ければ損害ゼロ。
            Assert.AreEqual(0f, DecoyForceRules.DecoyAttrition(50f, 0f), Eps);
        }

        [Test]
        public void 見破りは説得力の薄さと敵情報力に比例()
        {
            // (1-0.65)*0.6=0.35*0.6=0.21。
            Assert.AreEqual(0.21f, DecoyForceRules.Detection(0.65f, 0.6f), Eps);
            // 本物らしいほど見破られにくい：(1-0.9)*0.6=0.06。
            Assert.AreEqual(0.06f, DecoyForceRules.Detection(0.9f, 0.6f), Eps);
            // 薄い囮＆鋭い敵情報力なら高リスク：(1-0.2)*1.0=0.8。
            Assert.AreEqual(0.8f, DecoyForceRules.Detection(0.2f, 1f), Eps);
        }

        [Test]
        public void 見破られると本隊の意図が露見_秘匿で抑える()
        {
            // 0.21*(1-0.5)=0.105。
            Assert.AreEqual(0.105f, DecoyForceRules.BackfireOnDetection(0.21f, 0.5f), Eps);
            // 秘匿ゼロなら見破りがそのまま露見。
            Assert.AreEqual(0.8f, DecoyForceRules.BackfireOnDetection(0.8f, 0f), Eps);
            // 完全秘匿なら見破られても露見ゼロ。
            Assert.AreEqual(0f, DecoyForceRules.BackfireOnDetection(0.21f, 1f), Eps);
        }

        [Test]
        public void 正味価値は手薄化から損害と露見を引く()
        {
            // 487.5 - 40 - 0.105*100 = 487.5-40-10.5 = 437。
            Assert.AreEqual(437f, DecoyForceRules.DecoyNetValue(487.5f, 40f, 0.105f), Eps);
            // 手薄化が小さく損害・露見が勝てば負：50-40-0.5*100=-40。
            Assert.AreEqual(-40f, DecoyForceRules.DecoyNetValue(50f, 40f, 0.5f), Eps);
        }

        [Test]
        public void 陽動成功は敵食いつきが閾値以上()
        {
            // 既定閾値0.3。0.4875≥0.3→成功。
            Assert.IsTrue(DecoyForceRules.IsDeceptionSuccessful(0.4875f));
            // 食いつき不足は失敗。
            Assert.IsFalse(DecoyForceRules.IsDeceptionSuccessful(0.2f));
            // ちょうど閾値は成功。
            Assert.IsTrue(DecoyForceRules.IsDeceptionSuccessful(0.3f));
        }

        [Test]
        public void 物語_説得力ある囮が敵戦力を引きつけ主攻を手薄にするが見破られると意図が露見()
        {
            // 兵力50・派手な動き0.8の囮は説得力0.65。
            float plaus = DecoyForceRules.Plausibility(50f, 0.8f);
            Assert.AreEqual(0.65f, plaus, Eps);

            // 慎重な敵(0.5)は囮へ割合0.4875を割き、総兵力1000のうち487.5が別方向へ釣り出される。
            float drawn = DecoyForceRules.EnemyForcesDrawn(plaus, 0.5f);
            Assert.AreEqual(0.4875f, drawn, Eps);
            float relief = DecoyForceRules.MainEffortRelief(drawn, 1000f);
            Assert.AreEqual(487.5f, relief, Eps);
            Assert.IsTrue(DecoyForceRules.IsDeceptionSuccessful(drawn));

            // 囮は敵反応80で損害40を被るが、本隊の秘匿(0.5)が高く露見はわずか。
            float attrition = DecoyForceRules.DecoyAttrition(50f, 80f);
            Assert.AreEqual(40f, attrition, Eps);
            float detect = DecoyForceRules.Detection(plaus, 0.6f);
            Assert.AreEqual(0.21f, detect, Eps);
            float backfire = DecoyForceRules.BackfireOnDetection(detect, 0.5f);
            Assert.AreEqual(0.105f, backfire, Eps);
            // 手薄化が代償を大きく上回り陽動は得：437。
            float net = DecoyForceRules.DecoyNetValue(relief, attrition, backfire);
            Assert.AreEqual(437f, net, Eps);
            Assert.Greater(net, 0f);

            // だが囮が薄っぺら(説得力0.2)で敵情報力が鋭い(1.0)と見破られ(0.8)、秘匿ゼロでは本隊の意図が露見(0.8)し陽動は破綻。
            float thinDetect = DecoyForceRules.Detection(0.2f, 1f);
            Assert.AreEqual(0.8f, thinDetect, Eps);
            float thinBackfire = DecoyForceRules.BackfireOnDetection(thinDetect, 0f);
            Assert.AreEqual(0.8f, thinBackfire, Eps);
            // わずかな手薄化に対し損害＋大露見で正味は負：50-40-0.8*100=-70。
            float thinNet = DecoyForceRules.DecoyNetValue(50f, 40f, thinBackfire);
            Assert.AreEqual(-70f, thinNet, Eps);
            Assert.Less(thinNet, 0f);
        }
    }
}
