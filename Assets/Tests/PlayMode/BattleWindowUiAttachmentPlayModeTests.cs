using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ginei.Tests
{
    public class BattleWindowUiAttachmentPlayModeTests
    {
        [UnityTest]
        public IEnumerator HudCommandAndMinimap_AttachOnlyToTheirBattleWindowRoot()
        {
            Scene battle = SceneManager.CreateScene("BattleUiAttachmentQa");
            GameObject rootObject = new GameObject("BattleUiRoot_Qa", typeof(RectTransform));
            RectTransform root = rootObject.GetComponent<RectTransform>();
            BattleWindowUI.Register(battle, root);

            GameObject commandHost = InScene(battle, "CommandHost");
            GameObject commandPanel = InScene(battle, "CommandPanel", typeof(RectTransform));
            CommandMenu command = commandHost.AddComponent<CommandMenu>();
            command.menuRoot = commandPanel;

            GameObject hudHost = InScene(battle, "HudHost");
            FleetHUDManager hud = hudHost.AddComponent<FleetHUDManager>();
            GameObject minimapHost = InScene(battle, "MinimapHost");
            Minimap minimap = minimapHost.AddComponent<Minimap>();

            yield return null; // StartでHUD/ミニマップの実UIを生成

            command.AttachForTest();
            hud.AttachForTest();
            minimap.AttachForTest();

            Assert.IsTrue(command.WindowAttachedForTest);
            Assert.AreSame(root, command.WindowParentForTest, "コマンドメニューが別窓/全画面へ残った");
            Assert.IsTrue(hud.WindowAttachedForTest);
            Assert.AreSame(root, hud.WindowParentForTest, "艦隊HUDが別窓/全画面へ残った");
            Assert.IsTrue(minimap.WindowAttachedForTest);
            Assert.AreSame(root, minimap.WindowParentForTest, "ミニマップが別窓/全画面へ残った");

            BattleWindowUI.Unregister(battle);
            Object.DestroyImmediate(commandPanel);
            Object.DestroyImmediate(commandHost);
            Object.DestroyImmediate(hudHost);
            Object.DestroyImmediate(minimapHost);
            Object.DestroyImmediate(rootObject);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(battle);
            if (unload != null) while (!unload.isDone) yield return null;
        }

        private static GameObject InScene(Scene scene, string name, params System.Type[] components)
        {
            GameObject go = components != null && components.Length > 0
                ? new GameObject(name, components)
                : new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
    }
}
