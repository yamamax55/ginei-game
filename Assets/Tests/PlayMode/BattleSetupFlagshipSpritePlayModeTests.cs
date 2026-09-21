using System.Collections;
using System.Reflection;
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
            flagship = Sprite.Create(flagshipTexture, new Rect(0, 0, 2, 2), Vector2.zero);
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
            SpriteRenderer visual = fleet.transform.Find("FlagshipBody").GetComponent<SpriteRenderer>();
            Assert.AreSame(flagship, visual.sprite);
            Assert.IsFalse(body.enabled, "元のTriangleレンダラが重ね描きされる");
            Assert.AreSame(triangle, ring.sprite);
            Assert.AreSame(triangle, marker.sprite);
            Assert.AreSame(triangle, glow.sprite);
            Assert.AreSame(triangle, escort.sprite);

            FleetSpriteProvider.Register(Faction.帝国, null);
            Assert.IsFalse(BattleSetup.ApplyFlagshipSprite(fleet, Faction.帝国));
            Assert.AreSame(flagship, visual.sprite, "未登録時は現在の表示画像を変更しない");
            Object.DestroyImmediate(fleet);
        }

        [Test]
        public void ChildVisualNormalizesSizeAndPivotWithoutScalingRoot()
        {
            GameObject fleet = new GameObject("Fleet");
            SpriteRenderer source = fleet.AddComponent<SpriteRenderer>();
            source.sprite = triangle;
            Vector3 originalRootScale = new Vector3(1f, 1f, 1f);
            fleet.transform.localScale = originalRootScale;
            FleetSpriteProvider.Register(Faction.帝国, flagship);

            Assert.IsTrue(BattleSetup.ApplyFlagshipSprite(fleet, Faction.帝国, 1.2f));

            Transform body = fleet.transform.Find("FlagshipBody");
            Assert.IsNotNull(body);
            Assert.AreEqual(originalRootScale, fleet.transform.localScale, "陣形基準のroot scaleを変更した");
            Assert.AreEqual(1.2f, body.GetComponent<SpriteRenderer>().bounds.size.y, 0.001f);
            Assert.AreEqual(fleet.transform.position.x, body.GetComponent<SpriteRenderer>().bounds.center.x, 0.001f);
            Assert.AreEqual(fleet.transform.position.y, body.GetComponent<SpriteRenderer>().bounds.center.y, 0.001f);
            Object.DestroyImmediate(fleet);
        }

        [Test]
        public void ArtworkFacesTransformUpAndFactionColorKeepsItWhite()
        {
            GameObject fleet = new GameObject("Fleet");
            fleet.transform.rotation = Quaternion.Euler(0f, 0f, 37f);
            SpriteRenderer source = fleet.AddComponent<SpriteRenderer>();
            source.sprite = triangle;
            FleetStrength strength = fleet.AddComponent<FleetStrength>();
            strength.faction = Faction.帝国;
            FactionColor factionColor = fleet.AddComponent<FactionColor>();
            FleetSpriteProvider.Register(Faction.帝国, flagship);

            Assert.IsTrue(BattleSetup.ApplyFlagshipSprite(fleet, Faction.帝国));
            SpriteRenderer visual = fleet.transform.Find("FlagshipBody").GetComponent<SpriteRenderer>();
            factionColor.ApplyColors();

            Assert.Less(Vector3.Angle(fleet.transform.up, visual.transform.up), 0.01f,
                "画像の上方向（艦首）が旗艦Transform.upと一致しない");
            Assert.AreEqual(Color.white, visual.color, "勢力固有画像へ陣営tintが重ねられた");
            Assert.AreNotEqual(Color.white, source.color, "Triangleフォールバックまで無着色になった");
            Object.DestroyImmediate(fleet);
        }

        [UnityTest]
        public IEnumerator ArtworkDamageFlashRestoresWhiteTint()
        {
            GameObject fleet = new GameObject("Fleet");
            SpriteRenderer source = fleet.AddComponent<SpriteRenderer>();
            source.sprite = triangle;
            FleetStrength strength = fleet.AddComponent<FleetStrength>();
            strength.flashDuration = 0.01f;
            FleetSpriteProvider.Register(Faction.帝国, flagship);
            BattleSetup.ApplyFlagshipSprite(fleet, Faction.帝国);
            SpriteRenderer visual = fleet.transform.Find("FlagshipBody").GetComponent<SpriteRenderer>();

            MethodInfo flash = typeof(FleetStrength).GetMethod("Flash", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(flash);
            flash.Invoke(strength, null);
            Assert.AreEqual(strength.flagshipArtworkFlashColor, visual.color, "専用画像の被弾変化が見えない");

            yield return new WaitForSeconds(0.03f);
            Assert.AreEqual(Color.white, visual.color, "被弾後に無着色へ復帰しなかった");
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

            Assert.AreSame(flagship, fleet.transform.Find("FlagshipBody").GetComponent<SpriteRenderer>().sprite);
            Assert.IsFalse(body.enabled);
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
