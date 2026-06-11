using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>DivideRules（分割統治・カエサル Divide et Impera・GAL-2 #1346）の EditMode テスト。既定Paramsで期待値を固定。</summary>
    public class DivideRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>懐柔効果＝利益供与×（1＋不満×0.5）。標的の不満が大きいほど和解に乗る。</summary>
        [Test]
        public void SelectiveReconciliationAppeal_既定値で確定する()
        {
            // 0.6 × (1 + 0.4×0.5) = 0.6 × 1.2 = 0.72
            Assert.AreEqual(0.72f, DivideRules.SelectiveReconciliationAppeal(0.6f, 0.4f), Eps);
            // 不満が大きいほど効く
            float low = DivideRules.SelectiveReconciliationAppeal(0.5f, 0f);
            float high = DivideRules.SelectiveReconciliationAppeal(0.5f, 1f);
            Assert.Greater(high, low);
        }

        /// <summary>離反誘発＝懐柔×（1＋亀裂×1.0）。連合の亀裂が大きいほど割りやすい。</summary>
        [Test]
        public void DefectionInducement_亀裂が大きいほど割りやすい()
        {
            // 0.5 × (1 + 0.4×1.0) = 0.5 × 1.4 = 0.7
            Assert.AreEqual(0.7f, DivideRules.DefectionInducement(0.5f, 0.4f), Eps);
            // 一枚岩（亀裂0）は割れにくい
            float monolith = DivideRules.DefectionInducement(0.5f, 0f);
            float cracked = DivideRules.DefectionInducement(0.5f, 0.6f);
            Assert.Greater(cracked, monolith);
            Assert.AreEqual(0.5f, monolith, Eps);
        }

        /// <summary>分断度＝離反誘発×（1−結束）。固い結束ほど割れにくい。</summary>
        [Test]
        public void CoalitionFragmentation_結束が防壁になる()
        {
            // 0.7 × (1 − 0.3) = 0.49
            Assert.AreEqual(0.49f, DivideRules.CoalitionFragmentation(0.7f, 0.3f), Eps);
            // 烏合の衆（結束0）は誘発がそのまま分断に直結
            Assert.AreEqual(0.7f, DivideRules.CoalitionFragmentation(0.7f, 0f), Eps);
            // 完全結束（1）は割れない
            Assert.AreEqual(0f, DivideRules.CoalitionFragmentation(0.7f, 1f), Eps);
        }

        /// <summary>強硬派の孤立＝分断度×0.8。割れるほど残党が孤立し各個撃破の前提になる。</summary>
        [Test]
        public void IsolationOfHoldouts_分断ほど残党が孤立する()
        {
            // 0.5 × 0.8 = 0.4
            Assert.AreEqual(0.4f, DivideRules.IsolationOfHoldouts(0.5f), Eps);
            Assert.AreEqual(0f, DivideRules.IsolationOfHoldouts(0f), Eps);
            Assert.Greater(DivideRules.IsolationOfHoldouts(0.9f), DivideRules.IsolationOfHoldouts(0.3f));
        }

        /// <summary>買収コスト＝基準100×戦力×（1−不満）。強く満ち足りた部族ほど高くつく。</summary>
        [Test]
        public void BribeCost_強く不満の小さい部族ほど高くつく()
        {
            // 100 × 0.8 × (1 − 0.25) = 100 × 0.8 × 0.75 = 60
            Assert.AreEqual(60f, DivideRules.BribeCost(0.8f, 0.25f), Eps);
            // 不満の大きい弱小部族は安く引き抜ける
            float satedStrong = DivideRules.BribeCost(0.9f, 0.1f);
            float angryWeak = DivideRules.BribeCost(0.2f, 0.9f);
            Assert.Greater(satedStrong, angryWeak);
        }

        /// <summary>逆結束リスク＝露骨さ×透明性×1.5。見え透いた引き抜きは連合を逆に結束させる。</summary>
        [Test]
        public void BacklashRisk_見え透いた工作は逆効果()
        {
            // 0.4 × 0.5 × 1.5 = 0.30
            Assert.AreEqual(0.30f, DivideRules.BacklashRisk(0.4f, 0.5f), Eps);
            // 隠密（透明性0）なら逆効果なし
            Assert.AreEqual(0f, DivideRules.BacklashRisk(0.9f, 0f), Eps);
        }

        /// <summary>正味効果＝分断度×（1−露見リスク）。露見ぶん分断が目減りする。</summary>
        [Test]
        public void DivideEffectiveness_露見で目減りする()
        {
            // 0.6 × (1 − 0.3) = 0.42
            Assert.AreEqual(0.42f, DivideRules.DivideEffectiveness(0.6f, 0.3f), Eps);
            // 完全露見なら帳消し
            Assert.AreEqual(0f, DivideRules.DivideEffectiveness(0.6f, 1f), Eps);
        }

        /// <summary>物語：亀裂のある連合は選択的和解で割れ強硬派が孤立するが、工作露見で逆に結束する。</summary>
        [Test]
        public void 亀裂ある連合は選択的和解で割れるが露見で逆結束する()
        {
            // 不満を抱える部族に厚い利益供与（カエサルが一部族に特権）
            float appeal = DivideRules.SelectiveReconciliationAppeal(0.7f, 0.6f); // 0.7×1.3 = 0.91
            Assert.AreEqual(0.91f, appeal, Eps);

            // 亀裂の大きい連合（0.8）ほど離反が誘発される（クランプで上限1.0）
            float defect = DivideRules.DefectionInducement(appeal, 0.8f);          // 0.91×1.8 = 1.638 → 1.0
            Assert.AreEqual(1f, defect, Eps);

            // 緩い結束（0.3）の連合は割れる
            float frag = DivideRules.CoalitionFragmentation(defect, 0.3f);         // 1.0×0.7 = 0.7
            Assert.AreEqual(0.7f, frag, Eps);
            Assert.IsTrue(DivideRules.IsCoalitionDivided(frag)); // 0.7 > 0.5

            // 割れた残党＝強硬派が孤立し各個撃破の前提になる
            float isolation = DivideRules.IsolationOfHoldouts(frag);               // 0.7×0.8 = 0.56
            Assert.AreEqual(0.56f, isolation, Eps);

            // 隠密に進めれば正味の分断が残る
            float quietBacklash = DivideRules.BacklashRisk(appeal, 0.1f);          // 0.91×0.1×1.5 = 0.1365
            float quietEffect = DivideRules.DivideEffectiveness(frag, quietBacklash);
            Assert.Greater(quietEffect, 0.5f);

            // だが工作が露見すると（透明性0.9）逆に連合が結束し、正味の分断は帳消しになる
            float exposedBacklash = DivideRules.BacklashRisk(appeal, 0.9f);        // 0.91×0.9×1.5 = 1.2285 → 1.0
            Assert.AreEqual(1f, exposedBacklash, Eps);
            float exposedEffect = DivideRules.DivideEffectiveness(frag, exposedBacklash);
            Assert.AreEqual(0f, exposedEffect, Eps);
        }
    }
}
