using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>挟撃／包囲：攻撃元の角度分布から包囲度、被ダメ倍率。</summary>
    public class EnvelopmentRulesTests
    {
        [Test]
        public void Encirclement_NeedsTwoAndSpread()
        {
            Assert.AreEqual(0f, EnvelopmentRules.EncirclementFactor(new List<float> { 0f }), 1e-4f);       // 1隊＝包囲なし
            Assert.AreEqual(0f, EnvelopmentRules.EncirclementFactor(new List<float> { 0f, 10f }), 1e-4f);  // 同方向＝0
        }

        [Test]
        public void Encirclement_PincerIsFull()
        {
            // 前後から挟む（0°と180°）＝完全包囲。
            Assert.AreEqual(1f, EnvelopmentRules.EncirclementFactor(new List<float> { 0f, 180f }), 1e-4f);
            // 0°と90° → (90-60)/(180-60)=0.25
            Assert.AreEqual(0.25f, EnvelopmentRules.EncirclementFactor(new List<float> { 0f, 90f }), 1e-3f);
        }

        [Test]
        public void DamageFactor_RampsWithEncirclement()
        {
            Assert.AreEqual(1f, EnvelopmentRules.DamageFactor(0f), 1e-4f);
            Assert.AreEqual(1.25f, EnvelopmentRules.DamageFactor(1f), 1e-4f);
            Assert.AreEqual(1.125f, EnvelopmentRules.DamageFactor(0.5f), 1e-4f);
        }
    }
}
