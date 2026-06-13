using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 通商破壊（L-3 #95・<see cref="CommerceRaidingRules"/>）の境界・エッジケースを固定する。
    /// <see cref="LogisticsLayerTests"/> がカバーしない穴だけを埋める：撃破判定の同値タイ、負値クランプ、
    /// EscortNeeded の下限0、DeliveredSupply の単体、ResolveInterception の null 安全。
    /// </summary>
    public class CommerceRaidingRulesExtraTests
    {
        /// <summary>同値（襲撃＝防御）は厳密 &gt; なので守り切る＝タイは防御側勝ち（境界の定義）。</summary>
        [Test]
        public void ConvoyDestroyed_TieGoesToDefender()
        {
            // 護衛のみで拮抗（既存テストの 100 vs 100 と別経路：自衛と合算してちょうど同値になるタイを固定）
            Assert.IsFalse(CommerceRaidingRules.ConvoyDestroyed(100, escortStrength: 70, convoySelfDefense: 30));
            // ごくわずかでも上回れば撃破される（タイの直上）
            Assert.IsTrue(CommerceRaidingRules.ConvoyDestroyed(100.001f, 70, 30));
        }

        /// <summary>負の入力は0へクランプされる：負の襲撃力は誰も撃破できず、負の護衛/自衛は防御0扱い。</summary>
        [Test]
        public void ConvoyDestroyed_NegativeInputsClampToZero()
        {
            // 負の襲撃力＝攻撃にならない（防御0でも撃破されない＝0 > 0 は偽）
            Assert.IsFalse(CommerceRaidingRules.ConvoyDestroyed(raiderStrength: -50, escortStrength: 0));
            // 負の護衛・自衛は防御0扱い＝わずかな襲撃でも撃破される
            Assert.IsTrue(CommerceRaidingRules.ConvoyDestroyed(10, escortStrength: -100, convoySelfDefense: -100));
        }

        /// <summary>必要護衛は下限0：自衛が襲撃を上回れば護衛ゼロで足りる（負にならない）。</summary>
        [Test]
        public void EscortNeeded_FloorsAtZero_WhenSelfDefenseExceedsRaider()
        {
            Assert.AreEqual(0f, CommerceRaidingRules.EscortNeeded(40, convoySelfDefense: 60), 1e-4f);
            // 負の襲撃力でも0（クランプ）
            Assert.AreEqual(0f, CommerceRaidingRules.EscortNeeded(-30, convoySelfDefense: 0), 1e-4f);
        }

        /// <summary>DeliveredSupply 単体：撃破で0、無事で積荷量、負の積荷は0にクランプ。</summary>
        [Test]
        public void DeliveredSupply_DestroyedZeros_NegativePayloadClamps()
        {
            Assert.AreEqual(0f, CommerceRaidingRules.DeliveredSupply(convoyPayload: 50, destroyed: true), 1e-4f);
            Assert.AreEqual(50f, CommerceRaidingRules.DeliveredSupply(50, destroyed: false), 1e-4f);
            Assert.AreEqual(0f, CommerceRaidingRules.DeliveredSupply(-10, destroyed: false), 1e-4f); // 負の積荷は届かない
        }

        /// <summary>ResolveInterception は frontStock=null でも例外を投げず、届いた量を返す（純計算として使える）。</summary>
        [Test]
        public void ResolveInterception_NullStock_ReturnsDeliveredWithoutThrowing()
        {
            // 護衛十分で到達：null でも届いた量を返す
            float delivered = CommerceRaidingRules.ResolveInterception(null, convoyPayload: 50, raiderStrength: 100, escortStrength: 120);
            Assert.AreEqual(50f, delivered, 1e-4f);

            // 撃破時は0（null でも安全）
            float lost = CommerceRaidingRules.ResolveInterception(null, 50, 100, 10);
            Assert.AreEqual(0f, lost, 1e-4f);
        }

        /// <summary>到達時、前線備蓄は全資源一律で積荷ぶん増える（AddAll の配線を固定）。</summary>
        [Test]
        public void ResolveInterception_DeliveredAddsToAllResources()
        {
            var front = new ResourceStockpile(0, 0, 0);
            float delivered = CommerceRaidingRules.ResolveInterception(front, convoyPayload: 25, raiderStrength: 50, escortStrength: 60);
            Assert.AreEqual(25f, delivered, 1e-4f);
            Assert.AreEqual(25f, front.supplies, 1e-4f);
            Assert.AreEqual(25f, front.ammo, 1e-4f);
            Assert.AreEqual(25f, front.fuel, 1e-4f);
        }
    }
}
