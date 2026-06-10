using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 利権と特許状（#1093 Pillars of the Earth）を固定する：利権の経済価値・授与の忠誠・
    /// 取消の怨恨（非対称＝取り上げは深い恨み）・管轄争いの激化・独占の超過利潤・聖俗の管轄争い。
    /// 境界・クランプ・決定論を担保。既定 Params の具体値で期待値を固定する。
    /// </summary>
    public class CharterRightsRulesTests
    {
        private static CharterRightsParams P => CharterRightsParams.Default;

        // --- CharterValue ---

        [Test]
        public void CharterValue_MarketScalesWithActivity()
        {
            // 市場が栄えるほど市場開設権は高価＝0.9*1.0=0.9／採掘は基準0.7*0.5=0.35
            Assert.AreEqual(0.9f, CharterRightsRules.CharterValue(CharterType.市場開設権, 1f, P), 1e-4f);
            Assert.AreEqual(0.35f, CharterRightsRules.CharterValue(CharterType.資源採掘権, 0.5f, P), 1e-4f);
            // 経済活動ゼロなら価値ゼロ、入力は範囲外もクランプ
            Assert.AreEqual(0f, CharterRightsRules.CharterValue(CharterType.通行税徴収権, 0f, P), 1e-4f);
            Assert.AreEqual(0.6f, CharterRightsRules.CharterValue(CharterType.通行税徴収権, 5f, P), 1e-4f);
        }

        // --- GrantLoyaltyBonus / RevocationResentment（非対称＝利権は怨恨の通貨）---

        [Test]
        public void GrantLoyaltyBonus_ValueAndAmbitionRaiseLoyalty()
        {
            // 旨味のある利権（0.9）×野心家（1.0）＝0.9*0.5*1.4=0.63
            Assert.AreEqual(0.63f, CharterRightsRules.GrantLoyaltyBonus(0.9f, 1f, P), 1e-4f);
            // 野心ゼロなら増幅なし＝0.9*0.5=0.45
            Assert.AreEqual(0.45f, CharterRightsRules.GrantLoyaltyBonus(0.9f, 0f, P), 1e-4f);
        }

        [Test]
        public void RevocationResentment_LongHeldEntrenchedRightDeepensResentment()
        {
            // 長く保有した既得権（保有1.0）の取消は怨恨最大＝0.9*(0.4+0.6)=0.9
            Assert.AreEqual(0.9f, CharterRightsRules.RevocationResentment(0.9f, 1f, P), 1e-4f);
            // 保有が浅ければ怨恨は基礎のみ＝0.9*0.4=0.36
            Assert.AreEqual(0.36f, CharterRightsRules.RevocationResentment(0.9f, 0f, P), 1e-4f);
        }

        [Test]
        public void GrantVersusRevocation_AsymmetryGiveEasyTakeHard()
        {
            // 同じ価値の利権でも、取り上げの怨恨は与える忠誠を上回る＝与えるは易く取り上げるは難し
            float grant = CharterRightsRules.GrantLoyaltyBonus(0.9f, 1f, P);       // 0.63
            float revoke = CharterRightsRules.RevocationResentment(0.9f, 1f, P);   // 0.90
            Assert.Greater(revoke, grant);
        }

        // --- DisputeIntensity（複数主体の争奪が激化）---

        [Test]
        public void DisputeIntensity_MoreClaimantsEscalates()
        {
            // 単独請求は争い無し
            Assert.AreEqual(0f, CharterRightsRules.DisputeIntensity(1, 0.9f, P), 1e-4f);
            // 3者＝rivals2 → 2/(2+2)=0.5、価値1.0で 0.5
            float three = CharterRightsRules.DisputeIntensity(3, 1f, P);
            Assert.AreEqual(0.5f, three, 1e-4f);
            // 5者＝rivals4 → 4/6≒0.6667 ＞ 3者（請求者が増えるほど激化）
            float five = CharterRightsRules.DisputeIntensity(5, 1f, P);
            Assert.AreEqual(0.6667f, five, 1e-3f);
            Assert.Greater(five, three);
        }

        // --- MonopolyRent（排他的特許状の超過利潤）---

        [Test]
        public void MonopolyRent_ExclusivityRaisesRentNonlinear()
        {
            // 完全排他＝0.25 の価値が二乗で効き 1.0*1.0*1.0=1.0
            Assert.AreEqual(1f, CharterRightsRules.MonopolyRent(1f, 1f, P), 1e-4f);
            // 半排他は二乗で 0.25 へ落ちる（非線形）
            Assert.AreEqual(0.25f, CharterRightsRules.MonopolyRent(1f, 0.5f, P), 1e-4f);
            // 排他性ゼロなら超過利潤なし
            Assert.AreEqual(0f, CharterRightsRules.MonopolyRent(1f, 0f, P), 1e-4f);
        }

        // --- JurisdictionConflict（聖俗の管轄争い）---

        [Test]
        public void JurisdictionConflict_BothClaimsMaximize_OneSidedZero()
        {
            // 聖俗双方が満額主張＝最大の管轄争い 1.0
            Assert.AreEqual(1f, CharterRightsRules.JurisdictionConflict(1f, 1f, P), 1e-4f);
            // 拮抗（0.6/0.6）＝0.36*1*1=0.36
            Assert.AreEqual(0.36f, CharterRightsRules.JurisdictionConflict(0.6f, 0.6f, P), 1e-4f);
            // 片方の請求が無ければ管轄は確定＝争わない
            Assert.AreEqual(0f, CharterRightsRules.JurisdictionConflict(1f, 0f, P), 1e-4f);
        }
    }
}
