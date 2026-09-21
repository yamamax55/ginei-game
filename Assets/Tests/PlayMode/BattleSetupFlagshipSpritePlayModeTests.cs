using System.Collections;
using Ginei.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Ginei.Tests
{
    public class BattleSetupFlagshipSpritePlayModeTests
    {
        private Texture2D triangleTexture;
        private Texture2D flagshipTexture;
        private Sprite triangle;
        private Sprite flagship;

        [SetUp]
        public void SetUp()
        {
            FleetSpriteProvider.Clear();
            triangleTexture = new Texture2D(2, 2);
            flagshipTexture = new Texture2D(2, 2);
            triangle = Sprite.Create(triangleTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            flagship = Sprite.Create(flagshipTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
        }

        [TearDown]
        public void TearDown()
        {
            FleetSpriteProvider.Clear();
            foreach (Squadron squadron in Object.FindObjectsByType<Squadron>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(squadron.gameObject);
            Object.DestroyImmediate(triangle);
            Object.DestroyImmediate(flagship);
            Object.DestroyImmediate(triangleTexture);
            Object.DestroyImmediate(flagshipTexture);
        }

        [Test]
        public void ApplyChangesOnlyFlagshipBodyAndUnknownKeepsFallback()
        {
            GameObject fleet = new GameObject("Fleet");
            SpriteRenderer body = fleet.AddComponent<SpriteRenderer>();
            body.sprite = triangle;
            SpriteRenderer ring = ChildRenderer(fleet, "SelectionRing", triangle);
            SpriteRenderer marker = ChildRenderer(fleet, "FlagshipMarker", triangle);
            SpriteRenderer glow = ChildRenderer(fleet, "FlagshipMarkerGlow", triangle);
            SpriteRenderer escort = ChildRenderer(fleet, "Escort_0", triangle);
            escort.gameObject.AddComponent<EscortShip>();
            FleetSpriteProvider.Register(Faction.帝国, flagship);

            Assert.IsTrue(BattleSetup.ApplyFlagshipSprite(fleet, Faction.帝国));
            Assert.AreSame(flagship, body.sprite);
            Assert.AreSame(triangle, ring.sprite);
            Assert.AreSame(triangle, marker.sprite);
            Assert.AreSame(triangle, glow.sprite);
            Assert.AreSame(triangle, escort.sprite);

            FleetSpriteProvider.Register(Faction.帝国, null);
            Assert.IsFalse(BattleSetup.ApplyFlagshipSprite(fleet, Faction.帝国));
            Assert.AreSame(flagship, body.sprite, "未登録時は現在のフォールバック画像を変更しない");
            Object.DestroyImmediate(fleet);
        }

        [UnityTest]
        public IEnumerator SquadronKeepsOriginalTriangleForGeneratedEscorts()
        {
            GameObject fleet = new GameObject("Fleet");
            SpriteRenderer body = fleet.AddComponent<SpriteRenderer>();
            body.sprite = triangle;
            fleet.AddComponent<FleetStrength>();
            Squadron squadron = fleet.AddComponent<Squadron>();
            squadron.escortCount = 1;
            FleetSpriteProvider.Register(Faction.帝国, flagship);

            Assert.IsTrue(BattleSetup.ApplyFlagshipSprite(fleet, Faction.帝国));
            yield return null; // Squadron.Start → 配下艦生成

            Assert.AreSame(flagship, body.sprite);
            Transform escort = fleet.transform.Find("Escort_0");
            Assert.IsNotNull(escort);
            Assert.AreSame(triangle, escort.GetComponent<SpriteRenderer>().sprite,
                "旗艦専用画像が配下艦へ複製された");
            Object.DestroyImmediate(fleet);
        }

        private static SpriteRenderer ChildRenderer(GameObject parent, string objectName, Sprite sprite)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(parent.transform, false);
            SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            return renderer;
        }
    }
}
