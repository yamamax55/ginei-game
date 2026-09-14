using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Ginei
{
    /// <summary>
    /// キーボードの入力イベントを<b>届いた順に</b>見て、押下とその瞬間の修飾（Ctrl/Alt）を
    /// <see cref="GameInput.ChordLog"/>（Core の <see cref="KeyChordLog"/>）へ積む記録係（#107）。
    ///
    /// フレーム末の状態では「Alt を離す→P を押す」が1フレームに収まると Alt+P と区別できないため、
    /// Input System がこのアプリへ渡したイベントを1件ずつ読む。<b>OS 全体のフックではない</b>
    /// （Input System が受け取らなかった入力は、ここにも来ない）。記録するだけで入力を消費・改変しない。
    /// Play 開始時に自動で差し込む（手配線不要）。
    /// </summary>
    public static class KeyChordRecorder
    {
        private static KeyChordLog log;
        private static bool installed;
        private static bool warnedDrop;
        // 購読と解除で同じデリゲートを使う（メソッドグループを都度変換しない）。
        private static readonly System.Action<InputEventPtr, InputDevice> Handler = OnEvent;

        /// <summary>差し込み済みか。</summary>
        public static bool IsInstalled => installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // ドメインを再読込しない Play 開始でも前回の購読を持ち越さない。
            Uninstall();
            warnedDrop = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInstall() => Install();

        /// <summary>記録を差し込む（冪等）。</summary>
        public static void Install()
        {
            if (installed) return;
            log = new KeyChordLog();
            GameInput.ChordLog = log;
            InputSystem.onEvent += Handler;
            Application.quitting -= Uninstall;
            Application.quitting += Uninstall;
            installed = true;
        }

        /// <summary>記録を外す（自分が差し込んだ記録だけを外す）。</summary>
        public static void Uninstall()
        {
            if (!installed) return;
            InputSystem.onEvent -= Handler;
            if (GameInput.ChordLog == log) GameInput.ChordLog = null;
            log = null;
            installed = false;
        }

        private static void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (log == null || !(device is Keyboard kb)) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;
            if (eventPtr.handled) return;
            // エディタ用の更新（ゲームビュー外の入力）はゲームの Update が見る状態に入らない＝記録もしない。
            if (InputState.currentUpdateType == InputUpdateType.Editor) return;

            // 修飾はこのイベントを適用した後の値（同じイベントで Alt と T が下がっていれば Alt+T）。
            bool alt = PressedAfter(kb.leftAltKey, eventPtr) || PressedAfter(kb.rightAltKey, eventPtr);
            bool ctrl = PressedAfter(kb.leftCtrlKey, eventPtr) || PressedAfter(kb.rightCtrlKey, eventPtr);

            int frame = Time.frameCount;
            var keys = kb.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                KeyControl k = keys[i];
                if (k == null) continue;
                // onEvent はイベントを状態へ書き込む前に呼ばれる＝isPressed は「このイベントの直前」。
                if (k.isPressed || !PressedAfter(k, eventPtr)) continue;
                if (!log.Record(k.keyCode, ctrl, alt, frame) && !warnedDrop)
                {
                    warnedDrop = true;
                    Debug.LogWarning($"[KeyChordRecorder] 1フレームの押下が上限 {log.Capacity} 件を超えたため、以降の押下記録を捨てました（累計 {log.DroppedCount} 件）。");
                }
            }
        }

        /// <summary>このイベントを適用した後に押されているか（イベントに含まれない部分は現在の値）。</summary>
        private static bool PressedAfter(ButtonControl control, InputEventPtr eventPtr)
        {
            if (control == null) return false;
            return control.ReadValueFromEvent(eventPtr, out float value)
                ? control.IsValueConsideredPressed(value)
                : control.isPressed;
        }
    }
}
