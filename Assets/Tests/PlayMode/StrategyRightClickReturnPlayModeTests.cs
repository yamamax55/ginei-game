using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class StrategyRightClickReturnPlayModeTests
    {
        private GameObject root;
        private GalaxyView view;

        [SetUp]
        public void SetUp()
        {
            UIWindowStack.Clear();
            root = new GameObject("strategy-right-click-return-test");
            root.SetActive(false); // GalaxyView.Startの戦役構築を動かさず、入力経路だけを検証する。
            view = root.AddComponent<GalaxyView>();
        }

        [TearDown]
        public void TearDown()
        {
            UIWindowStack.Clear();
            if (root != null) Object.DestroyImmediate(root);
        }

        [Test]
        public void UiRightClick_ClosesTopmostWindowAndConsumesClick()
        {
            bool open = true;
            UIWindowStack.Register(() => open, () => open = false, 100, "test-window");

            bool consumed = InvokeRoute(modalOpen: false, pointerOverUi: true);

            Assert.IsTrue(consumed);
            Assert.IsFalse(open, "UI上の右クリックで最前面ウィンドウが閉じていない");
        }

        [Test]
        public void MapRightClick_DoesNotCloseWindowOrConsumeFleetMenuInput()
        {
            bool open = true;
            UIWindowStack.Register(() => open, () => open = false, 100, "test-window");

            bool consumed = InvokeRoute(modalOpen: false, pointerOverUi: false);

            Assert.IsFalse(consumed);
            Assert.IsTrue(open, "盤面右クリックがUIの万能撤回として処理された");
        }

        private bool InvokeRoute(bool modalOpen, bool pointerOverUi)
        {
            MethodInfo method = typeof(GalaxyView).GetMethod(
                "HandleStrategyRightClickReturn",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(bool), typeof(bool) },
                null);
            Assert.IsNotNull(method);
            return (bool)method.Invoke(view, new object[] { modalOpen, pointerOverUi });
        }
    }
}
