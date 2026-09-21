using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei.Tests
{
    public class NotificationFeedWindowScopePlayModeTests
    {
        private Scene battleScene;
        private GameObject rootObject;
        private GameObject feedObject;
        private GameObject otherFeedObject;

        [TearDown]
        public void TearDown()
        {
            if (feedObject != null) Object.DestroyImmediate(feedObject);
            if (otherFeedObject != null) Object.DestroyImmediate(otherFeedObject);
            if (rootObject != null) Object.DestroyImmediate(rootObject);
            if (battleScene.IsValid()) BattleWindowUI.Unregister(battleScene);
        }

        [Test]
        public void AdditiveBattleFeed_AttachesOnlyItsWindowRoot()
        {
            // 既に別シーンの通知欄があっても、additive Battle 用を別に生成できることを確認する。
            otherFeedObject = new GameObject("NotificationFeed_OtherScene_Qa");
            otherFeedObject.AddComponent<NotificationFeed>();
            battleScene = SceneManager.CreateScene("Battle");
            rootObject = new GameObject("BattleWindowUiRoot_Qa", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(rootObject, battleScene);
            RectTransform root = rootObject.GetComponent<RectTransform>();
            BattleWindowUI.Register(battleScene, root);

            NotificationFeed feed = NotificationFeed.EnsureForSceneForTest(battleScene);
            Assert.IsNotNull(feed, "既存の別シーン通知欄に抑止され、会戦用通知欄が生成されない");
            feedObject = feed.gameObject;
            feed.AttachForTest();

            Assert.IsTrue(feed.WindowAttachedForTest);
            Assert.AreSame(root, feed.WindowParentForTest);
            Assert.AreEqual(battleScene, feed.gameObject.scene);
            Assert.IsTrue(feed.BattleContextForTest);
        }
    }
}
