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
