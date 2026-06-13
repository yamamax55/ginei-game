using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>観見二つの目（観の目・見の目）の純ロジック（#1387 五輪書）のテスト。</summary>
    public class BattlePerceptionRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>知覚半径＝情報能力×センサーで戦場視界が広がる（既定 base10/max30）。</summary>
        [Test]
        public void PerceptionRadius_情報能力とセンサーで広がる()
        {
            // 情報0/センサー0＝基礎半径のみ＝10。
            Assert.AreEqual(10f, BattlePerceptionRules.PerceptionRadius(0f, 0f), Eps);
            // 情報1/センサー1＝base10 + max30×1×1 = 40。
            Assert.AreEqual(40f, BattlePerceptionRules.PerceptionRadius(1f, 1f), Eps);
            // 情報0.5/センサー1＝10 + 30×0.5 = 25。
            Assert.AreEqual(25f, BattlePerceptionRules.PerceptionRadius(0.5f, 1f), Eps);
            // センサー0なら情報が高くても基礎半径止まり（見る目があっても眼がなければ届かない）。
            Assert.AreEqual(10f, BattlePerceptionRules.PerceptionRadius(1f, 0f), Eps);
        }

        /// <summary>観の目＝情報能力(主)＋経験(重み0.35)で大局を見る。</summary>
        [Test]
        public void KanNoMe_情報能力と経験で大局を見る()
        {
            // 情報0.8/経験0.4＝0.8×0.65 + 0.4×0.35 = 0.52 + 0.14 = 0.66。
            Assert.AreEqual(0.66f, BattlePerceptionRules.KanNoMe(0.8f, 0.4f), Eps);
            // 情報も経験も満点なら観の目も満点。
            Assert.AreEqual(1f, BattlePerceptionRules.KanNoMe(1f, 1f), Eps);
            // 経験が情報を底上げする（情報0.4でも経験1.0なら 0.4×0.65 + 0.35 = 0.61）。
            Assert.AreEqual(0.61f, BattlePerceptionRules.KanNoMe(0.4f, 1f), Eps);
        }

        /// <summary>見の目＝表面注目そのもの（クランプのみ）。</summary>
        [Test]
        public void KenNoMe_表面注目をそのまま返す()
        {
            Assert.AreEqual(0.7f, BattlePerceptionRules.KenNoMe(0.7f), Eps);
            Assert.AreEqual(1f, BattlePerceptionRules.KenNoMe(1.5f), Eps);
            Assert.AreEqual(0f, BattlePerceptionRules.KenNoMe(-0.2f), Eps);
        }

        /// <summary>大局の明晰さ＝観を強く見を弱く（見の超過で曇る）。</summary>
        [Test]
        public void BigPictureClarity_観を強く見を弱く()
        {
            // 観0.8/見0.3＝見が観を下回る＝罰ゼロ＝観の目0.8がそのまま明晰さ。
            Assert.AreEqual(0.8f, BattlePerceptionRules.BigPictureClarity(0.8f, 0.3f), Eps);
            // 観0.4/見0.9＝見が観を0.5上回る＝0.4 - 0.5 = 0（目先に惑わされ大局を失う）。
            Assert.AreEqual(0f, BattlePerceptionRules.BigPictureClarity(0.4f, 0.9f), Eps);
            // 観0.6/見0.8＝超過0.2＝0.6 - 0.2 = 0.4。
            Assert.AreEqual(0.4f, BattlePerceptionRules.BigPictureClarity(0.6f, 0.8f), Eps);
        }

        /// <summary>意図の読み＝観の目で欺瞞を見破る（観が欺瞞を上回れば惑わされない）。</summary>
        [Test]
        public void IntentReading_観の目で欺瞞に耐える()
        {
            // 観0.9/欺瞞0.3＝観が上回る＝罰ゼロ＝0.9（敵の陽動を見抜く）。
            Assert.AreEqual(0.9f, BattlePerceptionRules.IntentReading(0.9f, 0.3f), Eps);
            // 観0.3/欺瞞0.9＝欺瞞が観を0.6上回る＝0.3 - 0.6 = 0（まんまと欺かれる）。
            Assert.AreEqual(0f, BattlePerceptionRules.IntentReading(0.3f, 0.9f), Eps);
            // 観0.5/欺瞞0.7＝超過0.2＝0.5 - 0.2 = 0.3。
            Assert.AreEqual(0.3f, BattlePerceptionRules.IntentReading(0.5f, 0.7f), Eps);
        }

        /// <summary>状況把握＝知覚の広さ×情報の質（広くても粗ければ不完全）。</summary>
        [Test]
        public void SituationalAwareness_広さと情報の質の積()
        {
            // 既定 reach=base10+max30=40。半径40/質1＝coverage1×1 = 1。
            Assert.AreEqual(1f, BattlePerceptionRules.SituationalAwareness(40f, 1f), Eps);
            // 半径40/質0.5＝1×0.5 = 0.5（全戦場を視界に収めても情報が粗いと把握は半分）。
            Assert.AreEqual(0.5f, BattlePerceptionRules.SituationalAwareness(40f, 0.5f), Eps);
            // 半径20/質1＝coverage0.5×1 = 0.5。
            Assert.AreEqual(0.5f, BattlePerceptionRules.SituationalAwareness(20f, 1f), Eps);
        }

        /// <summary>視野狭窄リスク＝見が観を上回る超過ぶん（目先への没入）。</summary>
        [Test]
        public void TunnelVisionRisk_見が観を上回ると狭窄()
        {
            // 見0.9/観0.4＝超過0.5＝リスク0.5。
            Assert.AreEqual(0.5f, BattlePerceptionRules.TunnelVisionRisk(0.9f, 0.4f), Eps);
            // 見0.3/観0.8＝観が上回る＝リスクゼロ（大局を見ているので狭窄しない）。
            Assert.AreEqual(0f, BattlePerceptionRules.TunnelVisionRisk(0.3f, 0.8f), Eps);
        }

        /// <summary>俯瞰の掌握＝観の目×状況把握が既定しきい値0.6以上で全体掌握。</summary>
        [Test]
        public void IsCommandingView_観の目と状況把握の両輪()
        {
            // 観0.9×把握0.8 = 0.72 ≥ 0.6＝掌握。
            Assert.IsTrue(BattlePerceptionRules.IsCommandingView(0.9f, 0.8f));
            // 観0.9×把握0.5 = 0.45 < 0.6＝未掌握（観の目だけでは届かない）。
            Assert.IsFalse(BattlePerceptionRules.IsCommandingView(0.9f, 0.5f));
            // 観0.5×把握0.5 = 0.25 < 0.6＝未掌握（状況把握だけでも届かない＝両輪）。
            Assert.IsFalse(BattlePerceptionRules.IsCommandingView(0.5f, 0.5f));
        }
    }
}
