using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 重ねたウィンドウを「最前面から1枚ずつ」閉じる中央スタック（#ウィンドウESC）の純ロジックを固定する。
    /// order（z順）最大→同 order は新しい登録、を topmost とし、CloseTopmost が1枚だけ閉じること、
    /// 閉じる窓が無ければ false（＝フォールバックへ）、Unregister で登録が外れること。
    /// </summary>
    public class UIWindowStackTests
    {
        [SetUp]
        public void Clear() => UIWindowStack.Clear();

        [Test]
        public void CloseTopmost_NoWindows_ReturnsFalse()
        {
            Assert.IsFalse(UIWindowStack.AnyOpen);
            Assert.IsFalse(UIWindowStack.CloseTopmost());
        }

        [Test]
        public void CloseTopmost_ClosesHighestOrderFirst()
        {
            bool low = true, high = true;
            UIWindowStack.Register(() => low, () => low = false, 90, "low");
            UIWindowStack.Register(() => high, () => high = false, 1090, "high");

            Assert.IsTrue(UIWindowStack.AnyOpen);
            Assert.IsTrue(UIWindowStack.CloseTopmost()); // 手前（order 1090）を閉じる
            Assert.IsFalse(high);
            Assert.IsTrue(low);                          // 奥はまだ開いている

            Assert.IsTrue(UIWindowStack.CloseTopmost()); // 次に奥（90）を閉じる
            Assert.IsFalse(low);

            Assert.IsFalse(UIWindowStack.CloseTopmost()); // もう閉じる窓は無い
        }

        [Test]
        public void CloseTopmost_SameOrder_ClosesMostRecentlyRegistered()
        {
            bool first = true, second = true;
            UIWindowStack.Register(() => first, () => first = false, 1090, "first");
            UIWindowStack.Register(() => second, () => second = false, 1090, "second");

            Assert.IsTrue(UIWindowStack.CloseTopmost());
            Assert.IsFalse(second); // 同 order は後から登録した方が手前
            Assert.IsTrue(first);
        }

        [Test]
        public void CloseTopmost_SkipsClosedWindows()
        {
            bool open = false, target = true;
            UIWindowStack.Register(() => open, () => open = false, 2000, "closedButHighOrder");
            UIWindowStack.Register(() => target, () => target = false, 90, "openLowOrder");

            // order は閉じている窓の方が高いが、開いている窓だけが対象になる。
            Assert.IsTrue(UIWindowStack.CloseTopmost());
            Assert.IsFalse(target);
        }

        [Test]
        public void Unregister_RemovesEntry()
        {
            bool open = true;
            object token = UIWindowStack.Register(() => open, () => open = false, 100, "w");
            Assert.IsTrue(UIWindowStack.AnyOpen);

            UIWindowStack.Unregister(token);
            Assert.IsFalse(UIWindowStack.AnyOpen);
            Assert.IsFalse(UIWindowStack.CloseTopmost());
        }
    }
}
