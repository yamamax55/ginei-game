using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// Game層スモーク（DEV効率化C）。dotnet TestHarness では検証できない
    /// 「Game層が実機でコンパイル＆起動するか」を最小で担保する＝配線を増やしても
    /// 沈黙クラッシュ（Unity が旧アセンブリを保持）に気づける安全網。GameCI（unity-test.yml・
    /// testMode:all）でのみ走る。例外が1つでも出れば PlayMode ランナーが自動失敗させる。
    /// </summary>
    public class GameSmokeTests
    {
        [Test]
        public void Singletons_AutoCreate_WithoutError()
        {
            // シーン間共有の核（自動生成シングルトン）が手置きなしで立ち上がる。
            Assert.IsNotNull(GameSettings.Instance, "GameSettings が自動生成されない");
            Assert.IsNotNull(AudioManager.Instance, "AudioManager が自動生成されない");
        }

        [UnityTest]
        public IEnumerator BattleScene_Boots_WithoutErrors()
        {
            if (!Application.CanStreamedLevelBeLoaded("Battle"))
            {
                Assert.Ignore("Battle シーンが Build Settings に無いためスキップ");
                yield break;
            }

            yield return SceneManager.LoadSceneAsync("Battle", LoadSceneMode.Single);
            // 数フレーム進めて BattleSetup(Awake) / BattleManager(Start) の初期化を通す。
            for (int i = 0; i < 10; i++) yield return null;

            var setup = Object.FindAnyObjectByType<BattleSetup>();
            var manager = Object.FindAnyObjectByType<BattleManager>();
            Assert.IsTrue(setup != null || manager != null,
                "Battle シーンの起動後に BattleSetup/BattleManager が見つからない（会戦が成立していない）");
            // ここまで例外/エラーログが出ていれば PlayMode テストランナーが自動的に fail させる
            // ＝Game層の沈黙クラッシュ（dotnet では見えない）を検出する。
        }
    }
}
