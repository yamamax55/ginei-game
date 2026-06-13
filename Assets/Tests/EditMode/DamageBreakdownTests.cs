using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>ダメージ内訳＋総合倍率クランプ（#2252）。</summary>
    public class DamageBreakdownTests
    {
        [Test]
        public void Clamp_BoundsTotal()
        {
            Assert.AreEqual(DamageClampRules.MaxTotal, DamageClampRules.Clamp(10f), 1e-4f); // 上限
            Assert.AreEqual(DamageClampRules.MinTotal, DamageClampRules.Clamp(0.01f), 1e-4f); // 下限
            Assert.AreEqual(DamageClampRules.MinTotal, DamageClampRules.Clamp(-3f), 1e-4f);   // 負は0→下限
            Assert.AreEqual(1.5f, DamageClampRules.Clamp(1.5f), 1e-4f);                       // 範囲内は素通し
        }

        [Test]
        public void Breakdown_ProductMatchesAndSkipsUnity()
        {
            var b = new DamageBreakdown();
            b.Reset(100);
            b.Add("攻撃", 1.2f);
            b.Add("士気", 0.9f);
            b.Add("陣形", 1f);   // 等倍は省かれる
            Assert.AreEqual(2, b.entries.Count);
            Assert.AreEqual(1.2f * 0.9f, b.RawMultiplier, 1e-4f);
            Assert.AreEqual(108, b.Result); // 100×1.08
            Assert.IsFalse(b.WasClamped);
        }

        [Test]
        public void Breakdown_ClampReflectedInResult()
        {
            var b = new DamageBreakdown();
            b.Reset(100);
            b.Add("攻撃", 1.5f);
            b.Add("集中", 2.0f);
            b.Add("不意打ち", 1.3f);
            b.Add("包囲", 1.25f); // 生=4.875 ＜ 上限5 → クランプ無し
            Assert.IsFalse(b.WasClamped);

            b.Add("一斉砲撃", 1.5f); // 生=7.31 ＞ 上限5 → クランプ
            Assert.IsTrue(b.WasClamped);
            Assert.AreEqual(DamageClampRules.MaxTotal, b.ClampedMultiplier, 1e-4f);
            Assert.AreEqual(500, b.Result); // 100×5
        }
    }
}
