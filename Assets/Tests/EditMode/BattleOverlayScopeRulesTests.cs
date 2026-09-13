using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 会戦専用オーバーレイの帰属シーン判定（BattleOverlayScopeRules）の仕様を固定する（test-first）。
    /// ウィンドウ化会戦（Battle を additive ロード・アクティブは Strategy のまま）で
    /// 会戦用オブジェクトが戦略側に取り残されて動き続ける不具合の再発防止。
    /// </summary>
    public class BattleOverlayScopeRulesTests
    {
        // ───────── ShouldHost（生成してよいシーンか） ─────────

        [Test]
        public void ShouldHost_BattleScene_IsTrue()
        {
            Assert.IsTrue(BattleOverlayScopeRules.ShouldHost("Battle"));
        }

        [Test]
        public void ShouldHost_StrategyScene_IsFalse()
        {
            Assert.IsFalse(BattleOverlayScopeRules.ShouldHost("Strategy"));
        }

        [Test]
        public void ShouldHost_TitleAndResult_IsFalse()
        {
            Assert.IsFalse(BattleOverlayScopeRules.ShouldHost("Title"));
            Assert.IsFalse(BattleOverlayScopeRules.ShouldHost("Result"));
        }

        [Test]
        public void ShouldHost_NullOrEmpty_IsFalse()
        {
            Assert.IsFalse(BattleOverlayScopeRules.ShouldHost(null));
            Assert.IsFalse(BattleOverlayScopeRules.ShouldHost(""));
        }

        [Test]
        public void BattleSceneName_IsBattle()
        {
            // シーン名は Build Settings / SceneLoader と一致させる（変えない）。
            Assert.AreEqual("Battle", BattleOverlayScopeRules.BattleSceneName);
        }

        // ───────── NeedsSceneMove（生成先を移すか） ─────────

        [Test]
        public void NeedsSceneMove_WindowedBattle_IsTrue()
        {
            // ウィンドウ化会戦＝Battle は有効だがアクティブではない（アクティブは Strategy）。
            Assert.IsTrue(BattleOverlayScopeRules.NeedsSceneMove(true, false));
        }

        [Test]
        public void NeedsSceneMove_FullscreenBattle_IsFalse()
        {
            // フルスクリーン会戦＝Battle がアクティブシーン＝移動不要（後方互換）。
            Assert.IsFalse(BattleOverlayScopeRules.NeedsSceneMove(true, true));
        }

        [Test]
        public void NeedsSceneMove_InvalidScene_IsFalse()
        {
            // 無効なシーンハンドルへは移さない（MoveGameObjectToScene が例外を投げる）。
            Assert.IsFalse(BattleOverlayScopeRules.NeedsSceneMove(false, false));
            Assert.IsFalse(BattleOverlayScopeRules.NeedsSceneMove(false, true));
        }

        // ───────── ShouldRun（動作を続けてよいか） ─────────

        [Test]
        public void ShouldRun_LoadedBattleScene_IsTrue()
        {
            Assert.IsTrue(BattleOverlayScopeRules.ShouldRun("Battle", true));
        }

        [Test]
        public void ShouldRun_UnloadedBattleScene_IsFalse()
        {
            // 会戦シーンが畳まれたあとは動かない（決裁カードを生み続けない）。
            Assert.IsFalse(BattleOverlayScopeRules.ShouldRun("Battle", false));
        }

        [Test]
        public void ShouldRun_StrategyScene_IsFalse()
        {
            // 戦略側に取り残された個体は動かない（本不具合の核心）。
            Assert.IsFalse(BattleOverlayScopeRules.ShouldRun("Strategy", true));
        }

        [Test]
        public void ShouldRun_NullSceneName_IsFalse()
        {
            Assert.IsFalse(BattleOverlayScopeRules.ShouldRun(null, true));
        }
    }
}
