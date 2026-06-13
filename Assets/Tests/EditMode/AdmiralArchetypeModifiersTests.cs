using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>archetype会戦修正子の合成：与効果/被ダメ/士気・無印は1.0（後方互換）。</summary>
    public class AdmiralArchetypeModifiersTests
    {
        private static AdmiralData A() { var a = ScriptableObject.CreateInstance<AdmiralData>(); a.staffOfficers = new AdmiralData[0]; return a; }

        [Test]
        public void NoArchetype_IsNeutral()
        {
            Assert.AreEqual(1f, AdmiralArchetypeModifiers.AttackFactor(null, 1f), 1e-4f);
            var plain = A();
            Assert.AreEqual(1f, AdmiralArchetypeModifiers.AttackFactor(plain, 1f), 1e-4f);
            Assert.AreEqual(1f, AdmiralArchetypeModifiers.AttackFactor(plain, 0f), 1e-4f);
            Assert.AreEqual(1f, AdmiralArchetypeModifiers.DamageTakenFactor(plain), 1e-4f);
            Assert.AreEqual(1f, AdmiralArchetypeModifiers.MoraleFactor(plain), 1e-4f);
        }

        [Test]
        public void AttackFactor_ComposesValorDeathChargeAndComeback()
        {
            var yukimura = A(); yukimura.isPeerlessWarrior = true;
            Assert.AreEqual(1.15f, AdmiralArchetypeModifiers.AttackFactor(yukimura, 1f), 1e-4f);    // 満身＝武勇のみ
            Assert.AreEqual(1.725f, AdmiralArchetypeModifiers.AttackFactor(yukimura, 0f), 1e-4f);   // 1.15×1.5（決死）

            var zhangfei = A(); zhangfei.isFierceGeneral = true;
            Assert.AreEqual(1.2f, AdmiralArchetypeModifiers.AttackFactor(zhangfei, 1f), 1e-4f);     // 猪突

            var yang = A(); yang.isMagician = true;
            Assert.AreEqual(1.5f, AdmiralArchetypeModifiers.AttackFactor(yang, 0f), 1e-4f);         // 絶体絶命の逆転
            Assert.AreEqual(1.0f, AdmiralArchetypeModifiers.AttackFactor(yang, 1f), 1e-4f);

            var both = A(); both.isPeerlessWarrior = true; both.isFierceGeneral = true;
            Assert.AreEqual(1.38f, AdmiralArchetypeModifiers.AttackFactor(both, 1f), 1e-4f);        // 1.15×1.2
        }

        [Test]
        public void DamageTaken_And_Morale()
        {
            var zhangfei = A(); zhangfei.isFierceGeneral = true;
            Assert.AreEqual(1.25f, AdmiralArchetypeModifiers.DamageTakenFactor(zhangfei), 1e-4f);   // 猪突の隙

            var kircheis = A(); kircheis.isRightHand = true;
            Assert.AreEqual(0.85f, AdmiralArchetypeModifiers.DamageTakenFactor(kircheis), 1e-4f);   // 端正な陣形

            var kaiser = A(); kaiser.isKaiser = true;
            Assert.AreEqual(1.3f, AdmiralArchetypeModifiers.MoraleFactor(kaiser), 1e-4f);           // 黄金の獅子
        }
    }
}
