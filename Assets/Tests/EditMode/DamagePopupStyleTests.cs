using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ダメージ表示スタイル（#744）の仕様を固定する。
    /// 側背面でも「側背面!」等の文字は出さず、色と大きさだけで区別する＝連呼を排した引き算表現。
    /// </summary>
    public class DamagePopupStyleTests
    {
        [Test]
        public void Flank_ShowsOnlyNumber_NoWord()
        {
            var s = DamagePopup.GetStyle(150, isFlank: true);
            Assert.AreEqual("150", s.text);
            StringAssert.DoesNotContain("側背面", s.text);
        }

        [Test]
        public void Normal_IsWhiteNumber()
        {
            var s = DamagePopup.GetStyle(150, isFlank: false);
            Assert.AreEqual("150", s.text);
            Assert.AreEqual(Color.white, s.color);
        }

        [Test]
        public void Flank_DiffersByColorAndSize()
        {
            var f = DamagePopup.GetStyle(99, isFlank: true);
            var n = DamagePopup.GetStyle(99, isFlank: false);
            Assert.AreNotEqual(Color.white, f.color); // 色で区別（濃赤橙）
            Assert.Greater(f.fontSize, n.fontSize);    // 大きさで区別
        }
    }
}
