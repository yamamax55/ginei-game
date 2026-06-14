using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 自動プレイテストの不変条件（純ロジック）を固定する：例外=致命／決着しない=警告／瞬間消失=警告／
    /// 全停止=注意／HUD見切れ=注意／Material リーク=注意／敵対ペア無し=注意。
    /// レポートの合否・最高重要度・CI 終了コードの導出も担保する。
    /// </summary>
    public class PlaytestInvariantsTests
    {
        // 正常系の観測（所見が出ない基準）：決着済み・動いた・例外なし・HUD 画面内・リークなし。
        private static PlaytestObservations Healthy()
        {
            return new PlaytestObservations
            {
                scenarioName = "テスト会戦",
                durationSeconds = 30f,
                timeLimitSeconds = 0f,
                resolved = true,
                hadHostilePairAtStart = true,
                screenWidth = 1920,
                screenHeight = 1080,
                leakedMaterialCount = 0,
                sampleIntervalSeconds = 1f,
                aliveFlagshipSamples = new List<int> { 6, 6, 5, 4, 2, 1 },
                totalMovementSamples = new List<float> { 12f, 9f, 4f, 1f },
                hudBounds = new List<UiBounds> { new UiBounds("速度HUD", 1600, 10, 1900, 60) },
            };
        }

        [Test]
        public void Healthy_ProducesNoFindings_AndPasses()
        {
            var r = PlaytestInvariants.Evaluate(Healthy());
            Assert.AreEqual(0, r.findings.Count, "正常な観測では所見が出ないはず");
            Assert.IsTrue(r.Passed);
            Assert.AreEqual(PlaytestSeverity.情報, r.HighestSeverity);
            Assert.AreEqual(0, r.SuggestedExitCode);
            Assert.AreEqual("テスト会戦", r.scenarioName);
            Assert.AreEqual(6, r.sampleCount);
        }

        [Test]
        public void NullObservations_ReturnsEmptyReport()
        {
            var r = PlaytestInvariants.Evaluate(null);
            Assert.AreEqual(0, r.findings.Count);
            Assert.IsTrue(r.Passed);
        }

        [Test]
        public void ErrorLog_IsFatal_PerEntry()
        {
            var obs = Healthy();
            obs.errorLogs.Add("NullReferenceException at FleetStrength.Awake");
            obs.errorLogs.Add("");                 // 空は無視される
            obs.errorLogs.Add("Arial.ttf is no longer a valid built-in font");
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(2, r.CountOf(PlaytestSeverity.致命));
            Assert.AreEqual(PlaytestSeverity.致命, r.HighestSeverity);
            Assert.AreEqual(2, r.SuggestedExitCode);
            Assert.IsFalse(r.Passed);
        }

        [Test]
        public void NotResolved_WithHostiles_IsWarning()
        {
            var obs = Healthy();
            obs.resolved = false;
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(1, r.CountOf(PlaytestSeverity.警告));
            Assert.IsTrue(HasCategory(r, PlaytestCategory.進行));
            Assert.AreEqual(1, r.SuggestedExitCode);
        }

        [Test]
        public void NotResolved_VisualCaptureMode_IsInfoNotWarning_AndPasses()
        {
            var obs = Healthy();
            obs.resolved = false;
            obs.visualCaptureOnly = true; // スクショ撮影目的＝非決着はバグでない（描画速度の制約）
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(0, r.CountOf(PlaytestSeverity.警告), "可視化モードでは非決着を警告にしない");
            Assert.AreEqual(1, r.CountOf(PlaytestSeverity.情報), "情報として記録は残す");
            Assert.IsTrue(r.Passed, "警告以上が無いので PASS");
            Assert.AreEqual(0, r.SuggestedExitCode);
            Assert.IsTrue(HasMessageContaining(r, "可視化キャプチャ"));
        }

        [Test]
        public void NotResolved_VisualCaptureMode_StillFlagsRealException()
        {
            var obs = Healthy();
            obs.resolved = false;
            obs.visualCaptureOnly = true;
            obs.errorLogs.Add("ArgumentNullException: shader"); // 実バグは可視化モードでも致命のまま
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(1, r.CountOf(PlaytestSeverity.致命));
            Assert.IsFalse(r.Passed);
        }

        [Test]
        public void NotResolved_WithoutHostiles_DoesNotWarnResolution()
        {
            var obs = Healthy();
            obs.resolved = false;
            obs.hadHostilePairAtStart = false; // 敵対ペアが無いなら決着不要
            var r = PlaytestInvariants.Evaluate(obs);
            // 決着の警告は出ない（代わりに敵対ペア無しの注意が出る）
            Assert.AreEqual(0, r.CountOf(PlaytestSeverity.警告));
            Assert.AreEqual(1, r.CountOf(PlaytestSeverity.注意));
        }

        [Test]
        public void NoHostilePair_IsCaution()
        {
            var obs = Healthy();
            obs.hadHostilePairAtStart = false;
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(1, r.CountOf(PlaytestSeverity.注意));
        }

        [Test]
        public void SuddenMassLoss_IsWarning()
        {
            var obs = Healthy();
            // 6 隻が一気に 1 隻へ（消失率 ~83% ≥ 80%）
            obs.aliveFlagshipSamples = new List<int> { 6, 6, 1, 1 };
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(1, r.CountOf(PlaytestSeverity.警告));
            Assert.IsTrue(HasMessageContaining(r, "瞬時"));
        }

        [Test]
        public void GradualLoss_IsNotFlaggedAsMassLoss()
        {
            var obs = Healthy();
            obs.aliveFlagshipSamples = new List<int> { 6, 5, 4, 3, 2, 1 }; // 緩やかな減少
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(0, r.CountOf(PlaytestSeverity.警告));
        }

        [Test]
        public void SmallBattleWipe_BelowMinFleets_IsNotMassLoss()
        {
            var obs = Healthy();
            // 少数戦（3隻以下）の全滅は正常とみなす（閾値 MinFleetsForLossCheck=4 未満）
            obs.aliveFlagshipSamples = new List<int> { 3, 0 };
            obs.resolved = true;
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(0, r.CountOf(PlaytestSeverity.警告));
        }

        [Test]
        public void AllStationary_IsCaution()
        {
            var obs = Healthy();
            obs.totalMovementSamples = new List<float> { 0f, 0f, 0.001f, 0f }; // 全て ε 以下
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.IsTrue(HasMessageContaining(r, "移動量がほぼ0"));
            Assert.AreEqual(1, r.CountOf(PlaytestSeverity.注意));
        }

        [Test]
        public void OffscreenHud_IsCaution()
        {
            var obs = Healthy();
            obs.hudBounds = new List<UiBounds>
            {
                new UiBounds("通知パネル", -20, 10, 200, 120),  // 左にはみ出し
                new UiBounds("速度HUD", 1600, 10, 1950, 60),    // 右にはみ出し(>1920)
                new UiBounds("正常", 100, 100, 300, 200),       // 画面内
            };
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(2, r.CountOf(PlaytestSeverity.注意));
            Assert.IsTrue(HasCategory(r, PlaytestCategory.表示));
        }

        [Test]
        public void MaterialLeak_IsCaution()
        {
            var obs = Healthy();
            obs.leakedMaterialCount = 3;
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(1, r.CountOf(PlaytestSeverity.注意));
            Assert.IsTrue(HasCategory(r, PlaytestCategory.リーク));
        }

        [Test]
        public void UnmeasuredLeak_IsIgnored()
        {
            var obs = Healthy();
            obs.leakedMaterialCount = -1; // 未計測
            var r = PlaytestInvariants.Evaluate(obs);
            Assert.AreEqual(0, r.findings.Count);
        }

        private static bool HasCategory(PlaytestReport r, PlaytestCategory c)
        {
            foreach (var f in r.findings) if (f.category == c) return true;
            return false;
        }

        private static bool HasMessageContaining(PlaytestReport r, string sub)
        {
            foreach (var f in r.findings) if (f.message != null && f.message.Contains(sub)) return true;
            return false;
        }
    }
}
