using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei.Tests
{
    public class BattleWindowInputScopePlayModeTests
    {
        private Scene first;
        private Scene second;
        private GameObject firstRoot;
        private GameObject secondRoot;
        private GameObject cameraObject;
        private GameObject mapObject;
        private GameObject managerObject;
        private GameClock savedClock;
        private float savedTimeScale;

        [SetUp]
        public void SetUp()
        {
            savedClock = StrategySession.Clock;
            savedTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            BattleViewport.Clear();
            if (first.IsValid()) BattleWindowUI.Unregister(first);
            if (second.IsValid()) BattleWindowUI.Unregister(second);
            if (firstRoot != null) Object.DestroyImmediate(firstRoot);
            if (secondRoot != null) Object.DestroyImmediate(secondRoot);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (mapObject != null) Object.DestroyImmediate(mapObject);
            if (managerObject != null) Object.DestroyImmediate(managerObject);
            StrategySession.Clock = savedClock;
            Time.timeScale = savedTimeScale;
        }

        [Test]
        public void WindowedBattleReturn_PreservesUnifiedPauseAndSpeed()
        {
            StrategySession.Clock = new GameClock { paused = true, speed = 3f };
            managerObject = new GameObject("WindowedBattleManager_Qa");
            BattleManager manager = managerObject.AddComponent<BattleManager>();
            manager.SetHandoffContext(new BattleHandoff.State());

            Time.timeScale = 1f;
            manager.RestoreTimeForReturnForQa();
            Assert.AreEqual(0f, Time.timeScale, 0.001f, "会戦窓を閉じた時に戦略の一時停止が解除された");

            StrategySession.Clock.paused = false;
            StrategySession.Clock.speed = 2.5f;
            manager.RestoreTimeForReturnForQa();
            Assert.AreEqual(2.5f, Time.timeScale, 0.001f, "会戦窓を閉じた時に戦略の倍速が1倍へ戻された");
        }

        [Test]
        public void OnlyFocusedWindowAcceptsUiInput_AndClearReturnsControlToStrategy()
        {
            first = SceneManager.CreateScene("BattleInputScopeQaA");
            second = SceneManager.CreateScene("BattleInputScopeQaB");
            firstRoot = RootIn(first, "FirstRoot");
            secondRoot = RootIn(second, "SecondRoot");
            BattleWindowUI.Register(first, firstRoot.GetComponent<RectTransform>());
            BattleWindowUI.Register(second, secondRoot.GetComponent<RectTransform>());

            cameraObject = new GameObject("BattleCamera_Qa", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, first);
            mapObject = RootIn(first, "MapRect");
            BattleViewport.SetActive(first, cameraObject.GetComponent<Camera>(), mapObject.GetComponent<RectTransform>());

            Assert.IsTrue(BattleWindowUI.AcceptsInput(first));
            Assert.IsFalse(BattleWindowUI.AcceptsInput(second), "非フォーカス会戦が同じキー/クリックを受ける");

            BattleViewport.Clear();
            Assert.IsFalse(BattleWindowUI.AcceptsInput(first), "窓外へ戻った後も旧フォーカス会戦が入力を保持する");
            Assert.IsFalse(BattleWindowUI.AcceptsInput(second));
        }

        private static GameObject RootIn(Scene scene, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
    }
}
