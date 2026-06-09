using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 入力マッピング一元管理（#107）の純ロジックを固定する：既定割当に衝突が無いこと、衝突検出が機能すること、
    /// Ctrl修飾で倍速とグループ選択が分離されること（#83）、コンテキストでアクションが絞られること、表示名。
    /// キー読み取り（Keyboard.current）はテスト対象外（Play で検証）。
    /// </summary>
    public class GameInputTests
    {
        [Test]
        public void Defaults_HaveNoConflicts()
        {
            List<string> conflicts = GameInput.FindConflicts();
            Assert.AreEqual(0, conflicts.Count, "既定キー割当に衝突: " + string.Join(", ", conflicts));
        }

        [Test]
        public void FindConflicts_DetectsSameKeySameContext()
        {
            var bindings = new List<InputBinding>
            {
                new InputBinding(GameAction.リスタート, Key.R, InputContext.会戦),
                new InputBinding(GameAction.ポーズ,     Key.R, InputContext.会戦), // 同じ R を会戦で二重
            };
            Assert.AreEqual(1, GameInput.FindConflicts(bindings).Count);
        }

        [Test]
        public void SpeedAndGroupSelect_DoNotConflict_DueToCtrl()
        {
            // 倍速等速(1)とグループ選択1(Ctrl+1)は同じ Digit1 だが修飾キーで分離＝衝突しない（#83）
            var bindings = new List<InputBinding>
            {
                new InputBinding(GameAction.倍速等速,      Key.Digit1, InputContext.会戦, ctrl: false),
                new InputBinding(GameAction.グループ選択1, Key.Digit1, InputContext.会戦, ctrl: true),
            };
            Assert.AreEqual(0, GameInput.FindConflicts(bindings).Count);
        }

        [Test]
        public void FindConflicts_CatchesCommonVsSceneOverlap()
        {
            // 共通の H と、会戦の別アクションに H を割り当てると会戦コンテキストで衝突
            var bindings = new List<InputBinding>
            {
                new InputBinding(GameAction.ヘルプ切替, Key.H, InputContext.共通),
                new InputBinding(GameAction.ポーズ,     Key.H, InputContext.会戦),
            };
            Assert.GreaterOrEqual(GameInput.FindConflicts(bindings).Count, 1);
        }

        [Test]
        public void ActionsInContext_ScopesByScene()
        {
            List<GameAction> battle = GameInput.ActionsInContext(InputContext.会戦);
            List<GameAction> title = GameInput.ActionsInContext(InputContext.タイトル);

            Assert.Contains(GameAction.倍速等速, battle);       // 会戦専用
            Assert.Contains(GameAction.ヘルプ切替, battle);     // 共通は会戦でも有効
            Assert.Contains(GameAction.ヘルプ切替, title);      // 共通はタイトルでも有効
            Assert.IsFalse(title.Contains(GameAction.倍速等速)); // 会戦専用はタイトルで無効
        }

        [Test]
        public void IsActiveIn_CommonAlwaysActive()
        {
            Assert.IsTrue(GameInput.IsActiveIn(InputContext.共通, InputContext.タイトル));
            Assert.IsTrue(GameInput.IsActiveIn(InputContext.会戦, InputContext.会戦));
            Assert.IsFalse(GameInput.IsActiveIn(InputContext.会戦, InputContext.タイトル));
        }

        [Test]
        public void KeyLabel_FormatsModifierAndDigits()
        {
            Assert.AreEqual("H", GameInput.KeyLabel(GameAction.ヘルプ切替));
            Assert.AreEqual("1", GameInput.KeyLabel(GameAction.倍速等速));
            Assert.AreEqual("Ctrl+1", GameInput.KeyLabel(GameAction.グループ選択1));
            Assert.AreEqual("Esc", GameInput.KeyLabel(GameAction.キャンセル));
        }

        [Test]
        public void TryGetBinding_ResolvesAction()
        {
            Assert.IsTrue(GameInput.TryGetBinding(GameAction.リスタート, out InputBinding b));
            Assert.AreEqual(Key.R, b.key);
            Assert.AreEqual(InputContext.会戦, b.context);
        }
    }
}
