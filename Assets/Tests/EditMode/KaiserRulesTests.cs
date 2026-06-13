using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>覇王（ラインハルト）：各個撃破/攻勢インフレ/カリスマ/部下増幅/門閥特効/暴走/好敵手/持久戦弱。</summary>
    public class KaiserRulesTests
    {
        [Test]
        public void DefeatInDetail_And_OffensiveEscalation()
        {
            Assert.AreEqual(1.4f, KaiserRules.DefeatInDetailFactor(true, 0f), 1e-4f);   // 敵完全分散（アスターテ）
            Assert.AreEqual(1.0f, KaiserRules.DefeatInDetailFactor(true, 1f), 1e-4f);   // 敵集結
            Assert.AreEqual(1.2f, KaiserRules.DefeatInDetailFactor(true, 0.5f), 1e-4f);
            Assert.AreEqual(1.0f, KaiserRules.DefeatInDetailFactor(false, 0f), 1e-4f);

            Assert.AreEqual(1.0f, KaiserRules.OffensiveEscalationFactor(true, 0f), 1e-4f);
            Assert.AreEqual(1.3f, KaiserRules.OffensiveEscalationFactor(true, 1f), 1e-4f); // 攻勢インフレ
            Assert.AreEqual(1.15f, KaiserRules.OffensiveEscalationFactor(true, 0.5f), 1e-4f);
        }

        [Test]
        public void Charisma_Subordinate_AntiEstablishment()
        {
            Assert.AreEqual(1.3f, KaiserRules.CharismaMoraleFactor(true), 1e-4f);
            Assert.AreEqual(1.0f, KaiserRules.CharismaMoraleFactor(false), 1e-4f);

            Assert.AreEqual(1.3f, KaiserRules.SubordinateAmplificationFactor(true, 100), 1e-4f); // 若き天才を引き上げる
            Assert.AreEqual(1.15f, KaiserRules.SubordinateAmplificationFactor(true, 50), 1e-4f);
            Assert.AreEqual(1.0f, KaiserRules.SubordinateAmplificationFactor(false, 100), 1e-4f);

            Assert.AreEqual(1.25f, KaiserRules.AntiEstablishmentBonus(true, true), 1e-4f); // 門閥貴族特効
            Assert.AreEqual(1.0f, KaiserRules.AntiEstablishmentBonus(true, false), 1e-4f);
            Assert.AreEqual(1.0f, KaiserRules.AntiEstablishmentBonus(false, true), 1e-4f);
        }

        [Test]
        public void Berserk_Rival_Attrition()
        {
            // キルヒアイス喪失で暴走（攻↑↑守↓↓）。
            Assert.IsTrue(KaiserRules.IsBerserk(true, true));
            Assert.IsFalse(KaiserRules.IsBerserk(true, false));  // 相棒健在＝完璧なバランス
            Assert.IsFalse(KaiserRules.IsBerserk(false, true));
            Assert.AreEqual(1.5f, KaiserRules.BerserkAttackFactor(true), 1e-4f);
            Assert.AreEqual(1.0f, KaiserRules.BerserkAttackFactor(false), 1e-4f);
            Assert.AreEqual(1.4f, KaiserRules.BerserkDamageTakenFactor(true), 1e-4f);
            Assert.AreEqual(1.0f, KaiserRules.BerserkDamageTakenFactor(false), 1e-4f);

            // 好敵手がいて輝く（ヤン）・いないと退屈で不調。
            Assert.AreEqual(1.15f, KaiserRules.RivalPresenceFactor(true, true), 1e-4f);
            Assert.AreEqual(0.9f, KaiserRules.RivalPresenceFactor(true, false), 1e-4f);
            Assert.AreEqual(1.0f, KaiserRules.RivalPresenceFactor(false, true), 1e-4f);

            // 短期決戦無敵だが持久戦に弱い。
            Assert.AreEqual(0.75f, KaiserRules.AttritionPenaltyFactor(true, true), 1e-4f);
            Assert.AreEqual(1.0f, KaiserRules.AttritionPenaltyFactor(true, false), 1e-4f);
            Assert.AreEqual(1.0f, KaiserRules.AttritionPenaltyFactor(false, true), 1e-4f);
        }
    }
}
