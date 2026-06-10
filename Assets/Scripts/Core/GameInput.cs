using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Ginei
{
    /// <summary>入力の有効コンテキスト（シーン/モード）。共通＝どのシーンでも有効（#107）。</summary>
    public enum InputContext { 共通, タイトル, 戦略, 会戦 }

    /// <summary>論理アクション（キーの直読みを置き換える唯一の語彙・#107）。新規入力はここに足す。</summary>
    public enum GameAction
    {
        // 共通（どのシーンでも）
        ヘルプ切替,
        キャンセル,
        // 会戦
        ポーズ,
        倍速等速,
        倍速2倍,
        倍速3倍,
        選択フォーカス,
        リスタート,
        戦略へ復帰,
        カメラ上,
        カメラ下,
        カメラ左,
        カメラ右,
        // グループ選択（#83・Ctrl＋数字＝倍速と衝突しないよう修飾キーで分離）
        グループ選択1,
        グループ選択2,
        グループ選択3,
    }

    /// <summary>1アクションのキー割当（修飾キー・有効コンテキスト付き）。直列化/表示しやすい平データ。</summary>
    public readonly struct InputBinding
    {
        public readonly GameAction action;
        public readonly Key key;
        public readonly bool ctrl;
        public readonly InputContext context;

        public InputBinding(GameAction action, Key key, InputContext context, bool ctrl = false)
        {
            this.action = action;
            this.key = key;
            this.context = context;
            this.ctrl = ctrl;
        }
    }

    /// <summary>
    /// 入力マッピングの一元管理（#107・キーバインドの唯一の窓口）。各クラスは `Keyboard.current` を直読みせず
    /// `GameInput.WasPressed(GameAction)`/`IsHeld(...)` で問い合わせる。キー定義は1か所（<see cref="Bindings"/>）で、
    /// **シーン/モードごとに有効なアクションを絞る**（`SetContext`）＝同じキーがシーン違いで衝突せず、各シーンで何が
    /// 効くか（<see cref="ActionsInContext"/>）が一覧できる＝迷わない。衝突検出（<see cref="FindConflicts"/>）と
    /// 表示名（<see cref="KeyLabel"/>・HUD/ヘルプ用）を提供。キー読み取りは Unity 依存だが、定義/コンテキスト/衝突の
    /// ロジックは純粋＝EditMode テストで担保。将来のリバインドUI（#20/#87）の土台。
    /// </summary>
    public static class GameInput
    {
        // ===== 既定のキー割当（現状のキーをここへ集約＝唯一の出所） =====
        private static readonly InputBinding[] table =
        {
            new InputBinding(GameAction.ヘルプ切替,     Key.H,         InputContext.共通),
            new InputBinding(GameAction.キャンセル,     Key.Escape,    InputContext.共通),
            new InputBinding(GameAction.ポーズ,         Key.Space,     InputContext.会戦),
            new InputBinding(GameAction.倍速等速,       Key.Digit1,    InputContext.会戦),
            new InputBinding(GameAction.倍速2倍,        Key.Digit2,    InputContext.会戦),
            new InputBinding(GameAction.倍速3倍,        Key.Digit3,    InputContext.会戦),
            new InputBinding(GameAction.選択フォーカス, Key.F,         InputContext.会戦),
            new InputBinding(GameAction.リスタート,     Key.R,         InputContext.会戦),
            new InputBinding(GameAction.戦略へ復帰,     Key.Backspace, InputContext.会戦),
            new InputBinding(GameAction.カメラ上,       Key.W,         InputContext.会戦),
            new InputBinding(GameAction.カメラ下,       Key.S,         InputContext.会戦),
            new InputBinding(GameAction.カメラ左,       Key.A,         InputContext.会戦),
            new InputBinding(GameAction.カメラ右,       Key.D,         InputContext.会戦),
            // #83：Ctrl＋数字＝グループ選択。倍速（修飾なし）と修飾キーで分離＝衝突しない。
            new InputBinding(GameAction.グループ選択1,  Key.Digit1,    InputContext.会戦, ctrl: true),
            new InputBinding(GameAction.グループ選択2,  Key.Digit2,    InputContext.会戦, ctrl: true),
            new InputBinding(GameAction.グループ選択3,  Key.Digit3,    InputContext.会戦, ctrl: true),
        };

        /// <summary>全キー割当（読み取り専用・UI/検証用）。</summary>
        public static IReadOnlyList<InputBinding> Bindings => table;

        /// <summary>現在の入力コンテキスト（シーン/モード）。各シーンの管理クラスが <see cref="SetContext"/> で設定。</summary>
        public static InputContext Context { get; private set; } = InputContext.共通;

        /// <summary>入力コンテキストを切り替える（Title/Strategy/Battle の管理クラスが起動時に呼ぶ）。</summary>
        public static void SetContext(InputContext context) => Context = context;

        // ===== 純ロジック（テスト可能・キー読み取りを含まない） =====

        /// <summary>その割当が指定コンテキストで有効か（共通は常に有効）。</summary>
        public static bool IsActiveIn(InputContext bindingContext, InputContext current)
            => bindingContext == InputContext.共通 || bindingContext == current;

        /// <summary>アクションの割当を取得（無ければ false）。</summary>
        public static bool TryGetBinding(GameAction action, out InputBinding binding)
        {
            for (int i = 0; i < table.Length; i++)
                if (table[i].action == action) { binding = table[i]; return true; }
            binding = default;
            return false;
        }

        /// <summary>指定コンテキストで有効なアクション一覧（ヘルプ表示＝「このシーンの操作」用）。</summary>
        public static List<GameAction> ActionsInContext(InputContext context)
        {
            var list = new List<GameAction>();
            for (int i = 0; i < table.Length; i++)
                if (IsActiveIn(table[i].context, context)) list.Add(table[i].action);
            return list;
        }

        /// <summary>
        /// キー衝突の検出（同一の「コンテキスト×キー×修飾」に複数アクションが割当たっていないか）。
        /// 共通割当は全シーンへ展開して判定する＝シーン横断の衝突も拾う。衝突文字列のリスト（空＝衝突なし）。
        /// </summary>
        public static List<string> FindConflicts(IReadOnlyList<InputBinding> bindings)
        {
            var occupied = new Dictionary<string, GameAction>();
            var conflicts = new List<string>();
            if (bindings == null) return conflicts;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding b = bindings[i];
                foreach (InputContext ctx in ConcreteContexts(b.context))
                {
                    string slot = ctx + "|" + b.key + (b.ctrl ? "|Ctrl" : "");
                    if (occupied.TryGetValue(slot, out GameAction other) && other != b.action)
                        conflicts.Add($"{slot}: {other} ⇔ {b.action}");
                    else
                        occupied[slot] = b.action;
                }
            }
            return conflicts;
        }

        /// <summary>既定の割当表の衝突検出（空であるべき）。</summary>
        public static List<string> FindConflicts() => FindConflicts(table);

        /// <summary>アクションの表示用キー名（HUD/ヘルプ用。例「Ctrl+1」「Space」「H」）。</summary>
        public static string KeyLabel(GameAction action)
        {
            if (!TryGetBinding(action, out InputBinding b)) return "";
            return (b.ctrl ? "Ctrl+" : "") + KeyName(b.key);
        }

        private static IEnumerable<InputContext> ConcreteContexts(InputContext c)
        {
            if (c == InputContext.共通)
            {
                yield return InputContext.タイトル;
                yield return InputContext.戦略;
                yield return InputContext.会戦;
            }
            else yield return c;
        }

        private static string KeyName(Key k)
        {
            switch (k)
            {
                case Key.Digit1: return "1";
                case Key.Digit2: return "2";
                case Key.Digit3: return "3";
                case Key.Space: return "Space";
                case Key.Escape: return "Esc";
                case Key.Backspace: return "Backspace";
                default: return k.ToString();
            }
        }

        // ===== 入力読み取り（Unity 依存・null安全・コンテキストで絞る） =====

        /// <summary>このフレームでアクションが押されたか（現在のコンテキストで有効なときのみ・修飾キー一致）。</summary>
        public static bool WasPressed(GameAction action)
        {
            if (!TryGetBinding(action, out InputBinding b)) return false;
            if (!IsActiveIn(b.context, Context)) return false;
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            if (!kb[b.key].wasPressedThisFrame) return false;
            return kb.ctrlKey.isPressed == b.ctrl; // 修飾一致＝Ctrl＋数字と数字を分離（#83）
        }

        /// <summary>アクションのキーが押下中か（カメラパン等の継続入力用）。</summary>
        public static bool IsHeld(GameAction action)
        {
            if (!TryGetBinding(action, out InputBinding b)) return false;
            if (!IsActiveIn(b.context, Context)) return false;
            Keyboard kb = Keyboard.current;
            if (kb == null) return false;
            if (!kb[b.key].isPressed) return false;
            return kb.ctrlKey.isPressed == b.ctrl;
        }
    }
}
