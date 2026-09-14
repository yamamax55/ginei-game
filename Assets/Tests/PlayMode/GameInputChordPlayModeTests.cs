using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 修飾キーの判定を<b>実 Input System のイベント</b>で確かめる（#107・統治上申 Alt+T／人物名鑑 P／生産観測 Alt+P）。
    ///
    /// テスト専用の仮想キーボードを足し、1フレームに複数の状態イベントを<b>順番どおりに</b>積んでから
    /// 1フレーム進め、<see cref="GameInput.WasPressed"/> を読む。実キーボード・OS には触れない（グローバルフックなし）。
    /// ★これは「Input System に届いたイベントをどう解釈するか」の試験であって、実機のキー入力が
    /// ゲームへ届く保証ではない（届くかは実画面の入力診断で見る）。
    /// 実行中に実キーボードを触るとイベントが混ざりうる（その場合は再実行）。
    /// </summary>
    public class GameInputChordPlayModeTests
    {
        private Keyboard keyboard;
        private InputContext savedContext;
        private InputSettings.EditorInputBehaviorInPlayMode savedEditorBehavior;
        private InputSettings.BackgroundBehavior savedBackground;
        private bool settingsSaved;

        [SetUp]
        public void SetUp()
        {
            savedContext = GameInput.Context;
            GameInput.SetContext(InputContext.戦略);

            // ゲームビューにフォーカスが無くても仮想キーボードのイベントを処理させる（TearDown で元へ戻す）。
            InputSettings s = InputSystem.settings;
            savedEditorBehavior = s.editorInputBehaviorInPlayMode;
            savedBackground = s.backgroundBehavior;
            settingsSaved = true;
            s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;

            KeyChordRecorder.Install(); // Play 開始で自動差し込み済みのはず（冪等）
            keyboard = InputSystem.AddDevice<Keyboard>("GineiChordTestKeyboard");
            keyboard.MakeCurrent();
        }

        [TearDown]
        public void TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            keyboard = null;
            if (settingsSaved)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorBehavior;
                InputSystem.settings.backgroundBehavior = savedBackground;
                settingsSaved = false;
            }
            GameInput.SetContext(savedContext);
        }

        /// <summary>押されているキーの一覧で状態イベントを1件積む（空＝全部離す）。</summary>
        private void Queue(params Key[] pressed) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(pressed));

        private static string Describe()
            => $"統治上申={GameInput.WasPressed(GameAction.統治政策上申)} 人物名鑑={GameInput.WasPressed(GameAction.人物名鑑切替)} " +
               $"生産観測={GameInput.WasPressed(GameAction.生産観測切替)} frame={Time.frameCount} " +
               $"記録係={KeyChordRecorder.IsInstalled} 記録件数={(GameInput.ChordLog != null ? GameInput.ChordLog.Count : -1)}";

        /// <summary>前のテスト・前フレームの押下を持ち越していない、を確かめてから始める。</summary>
        private IEnumerator Settle()
        {
            Queue();
            yield return null;
            yield return null;
            Assert.IsFalse(GameInput.WasPressed(GameAction.統治政策上申), "開始前に押下が残っている: " + Describe());
            Assert.IsFalse(GameInput.WasPressed(GameAction.人物名鑑切替), "開始前に押下が残っている: " + Describe());
            Assert.IsFalse(GameInput.WasPressed(GameAction.生産観測切替), "開始前に押下が残っている: " + Describe());
        }

        [UnityTest]
        public IEnumerator ShortAltT_WithinOneFrame_IsGovernanceProposalOnly()
        {
            yield return Settle();
            Queue(Key.LeftAlt);
            Queue(Key.LeftAlt, Key.T);
            Queue(Key.LeftAlt);
            Queue();
            yield return null;

            Assert.IsTrue(GameInput.WasPressed(GameAction.統治政策上申), "短い Alt+T を取りこぼした: " + Describe());
            Assert.IsFalse(GameInput.WasPressed(GameAction.人物名鑑切替), Describe());
            Assert.IsFalse(GameInput.WasPressed(GameAction.生産観測切替), Describe());

            yield return null;
            Assert.IsFalse(GameInput.WasPressed(GameAction.統治政策上申), "次のフレームに持ち越した: " + Describe());
        }

        [UnityTest]
        public IEnumerator BareP_TapWithinOneFrame_IsPersonOnly()
        {
            yield return Settle();
            Queue(Key.P);
            Queue();
            yield return null;

            Assert.IsTrue(GameInput.WasPressed(GameAction.人物名鑑切替), "短い P を取りこぼした: " + Describe());
            Assert.IsFalse(GameInput.WasPressed(GameAction.生産観測切替), Describe());
            Assert.IsFalse(GameInput.WasPressed(GameAction.統治政策上申), Describe());
        }

        [UnityTest]
        public IEnumerator ShortAltP_WithinOneFrame_IsProductionOnly()
        {
            yield return Settle();
            Queue(Key.LeftAlt);
            Queue(Key.LeftAlt, Key.P);
            Queue(Key.LeftAlt);
            Queue();
            yield return null;

            Assert.IsTrue(GameInput.WasPressed(GameAction.生産観測切替), "短い Alt+P を取りこぼした: " + Describe());
            Assert.IsFalse(GameInput.WasPressed(GameAction.人物名鑑切替), "Alt+P で人物名鑑も出た: " + Describe());
        }

        [UnityTest]
        public IEnumerator AltHeldAcrossFrames_ThenP_IsProduction()
        {
            yield return Settle();
            Queue(Key.LeftAlt);
            yield return null;                    // Alt を前のフレームから押している
            Queue(Key.LeftAlt, Key.P);
            Queue(Key.LeftAlt);
            yield return null;

            Assert.IsTrue(GameInput.WasPressed(GameAction.生産観測切替), Describe());
            Assert.IsFalse(GameInput.WasPressed(GameAction.人物名鑑切替), Describe());

            Queue();
            yield return null;
        }

        /// <summary>
        /// 懸念の再現：前のフレームから押していた Alt を<b>離してから</b> P を押す、が同じフレームに収まる。
        /// フレーム末の状態だけ（Alt は「このフレームに離した」）だと Alt+P に見えるが、押した順では素の P。
        /// </summary>
        [UnityTest]
        public IEnumerator AltReleasedThenBareP_SameFrame_IsPersonNotProduction()
        {
            yield return Settle();
            Queue(Key.LeftAlt);
            yield return null;                    // Alt 押下中
            Queue();                              // Alt↑
            Queue(Key.P);                         // P↓（Alt は離れている）
            Queue();                              // P↑
            yield return null;

            Assert.IsTrue(GameInput.WasPressed(GameAction.人物名鑑切替), "Alt を離した後の P が人物名鑑にならない: " + Describe());
            Assert.IsFalse(GameInput.WasPressed(GameAction.生産観測切替), "Alt を離した後の P を Alt+P と誤解した: " + Describe());

            // フレーム状態だけの代替判定なら誤解していた、を記録として残す（判定の差がこのテストの主題）。
            var releasedThisFrame = new ModifierSample(held: false, releasedThisFrame: true);
            Assert.IsTrue(GameInput.TryGetBinding(GameAction.生産観測切替, out InputBinding production));
            Assert.IsTrue(GameInput.BindingMatches(production, InputContext.戦略, true, ModifierSample.Up, releasedThisFrame));
        }
    }
}
