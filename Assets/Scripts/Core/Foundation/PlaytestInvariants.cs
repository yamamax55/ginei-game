using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 自動プレイテストの不変条件チェック（純ロジック・test-first）。
    /// MonoBehaviour 殻が集めた <see cref="PlaytestObservations"/> を受け取り、バグ/改善点の所見を <see cref="PlaytestReport"/> に出す。
    /// 「観てる人間が気づく異常」を機械化するのが狙い：例外・会戦が決着しない・艦隊が瞬時に消える・全艦が動かない・
    /// HUD が画面外・Material リーク。判定式・閾値はここに集約し、各所へ直書きしない。
    /// </summary>
    public static class PlaytestInvariants
    {
        /// <summary>瞬間消失とみなす1サンプルあたりの減少率（前サンプル比）。</summary>
        public const float MassLossFraction = 0.8f;
        /// <summary>瞬間消失チェックを始める最小旗艦数（少数戦の正常な全滅と区別）。</summary>
        public const int MinFleetsForLossCheck = 4;
        /// <summary>全停止とみなす総移動量の下限（これ以下のサンプルは「動いていない」）。</summary>
        public const float StallMovementEpsilon = 0.01f;

        /// <summary>観測から所見を導きレポートを組む（純関数）。</summary>
        public static PlaytestReport Evaluate(PlaytestObservations obs)
        {
            var report = new PlaytestReport();
            if (obs == null) return report;

            report.scenarioName = obs.scenarioName ?? "";
            report.durationSeconds = obs.durationSeconds;
            report.sampleCount = obs.aliveFlagshipSamples != null ? obs.aliveFlagshipSamples.Count : 0;

            CheckExceptions(obs, report.findings);
            CheckHostilePresence(obs, report.findings);
            CheckResolution(obs, report.findings);
            CheckSuddenMassLoss(obs, report.findings);
            CheckStall(obs, report.findings);
            CheckOffscreenHud(obs, report.findings);
            CheckMaterialLeak(obs, report.findings);

            return report;
        }

        /// <summary>例外/Error ログは1件ごとに致命（過去の「一瞬で艦隊が消える」級＝フォント例外などを捕捉）。</summary>
        public static void CheckExceptions(PlaytestObservations obs, List<PlaytestFinding> findings)
        {
            if (obs.errorLogs == null) return;
            for (int i = 0; i < obs.errorLogs.Count; i++)
            {
                string msg = obs.errorLogs[i];
                if (string.IsNullOrEmpty(msg)) continue;
                findings.Add(new PlaytestFinding(PlaytestSeverity.致命, PlaytestCategory.例外,
                    "実行時例外/Error: " + msg));
            }
        }

        /// <summary>開始時に敵対ペアが無い＝シナリオ/陣営設定の不備の疑い。</summary>
        public static void CheckHostilePresence(PlaytestObservations obs, List<PlaytestFinding> findings)
        {
            if (!obs.hadHostilePairAtStart)
                findings.Add(new PlaytestFinding(PlaytestSeverity.注意, PlaytestCategory.進行,
                    "開始時に敵対する旗艦ペアが無い（シナリオ/陣営設定の不備の可能性）。"));
        }

        /// <summary>
        /// 敵対ペアがあったのに決着しなかった＝デッドロック/勝敗判定の不備の疑い。
        /// ただし可視化キャプチャ実行（<see cref="PlaytestObservations.visualCaptureOnly"/>）では
        /// ソフトGL描画が遅く時間内決着しないのが常でバグでないため、警告でなく情報に降格する（FAILさせない）。
        /// </summary>
        public static void CheckResolution(PlaytestObservations obs, List<PlaytestFinding> findings)
        {
            if (!(obs.hadHostilePairAtStart && !obs.resolved)) return;
            if (obs.visualCaptureOnly)
                findings.Add(new PlaytestFinding(PlaytestSeverity.情報, PlaytestCategory.進行,
                    "可視化キャプチャ中に会戦が時間内に決着しなかった（描画速度の制約・所見対象外）。", obs.durationSeconds));
            else
                findings.Add(new PlaytestFinding(PlaytestSeverity.警告, PlaytestCategory.進行,
                    "会戦が時間内に決着しなかった（デッドロック・勝敗判定の不備の可能性）。", obs.durationSeconds));
        }

        /// <summary>1サンプルで旗艦が <see cref="MassLossFraction"/> 以上消失＝瞬間消失バグの兆候。</summary>
        public static void CheckSuddenMassLoss(PlaytestObservations obs, List<PlaytestFinding> findings)
        {
            var s = obs.aliveFlagshipSamples;
            if (s == null || s.Count < 2) return;
            float interval = obs.sampleIntervalSeconds > 0f ? obs.sampleIntervalSeconds : 1f;
            for (int i = 1; i < s.Count; i++)
            {
                int prev = s[i - 1];
                int cur = s[i];
                if (prev < MinFleetsForLossCheck) continue;
                if (cur >= prev) continue;
                float lost = (prev - cur) / (float)prev;
                if (lost >= MassLossFraction)
                {
                    findings.Add(new PlaytestFinding(PlaytestSeverity.警告, PlaytestCategory.進行,
                        $"旗艦が瞬時に大量消失（{prev}→{cur}）。一瞬で艦隊が消えるバグの兆候。", i * interval));
                    return; // 最初の1件で十分（多重報告しない）
                }
            }
        }

        /// <summary>会戦が進行しているのに全艦が一度も動いていない＝移動が機能していない疑い。</summary>
        public static void CheckStall(PlaytestObservations obs, List<PlaytestFinding> findings)
        {
            if (!obs.hadHostilePairAtStart) return;
            var m = obs.totalMovementSamples;
            if (m == null || m.Count == 0) return;
            float maxMove = 0f;
            for (int i = 0; i < m.Count; i++) if (m[i] > maxMove) maxMove = m[i];
            if (maxMove <= StallMovementEpsilon)
                findings.Add(new PlaytestFinding(PlaytestSeverity.注意, PlaytestCategory.進行,
                    "会戦中に全艦の移動量がほぼ0（移動・AI が機能していない可能性）。"));
        }

        /// <summary>HUD 要素が画面外/見切れ＝レイアウト不備。</summary>
        public static void CheckOffscreenHud(PlaytestObservations obs, List<PlaytestFinding> findings)
        {
            if (obs.hudBounds == null) return;
            float w = obs.screenWidth, h = obs.screenHeight;
            for (int i = 0; i < obs.hudBounds.Count; i++)
            {
                var b = obs.hudBounds[i];
                if (b.xMin < 0f || b.yMin < 0f || b.xMax > w || b.yMax > h)
                    findings.Add(new PlaytestFinding(PlaytestSeverity.注意, PlaytestCategory.表示,
                        $"HUD要素「{b.label}」が画面外/見切れ（[{b.xMin:0},{b.yMin:0}]-[{b.xMax:0},{b.yMax:0}] / 画面 {obs.screenWidth}x{obs.screenHeight}）。"));
            }
        }

        /// <summary>実行時生成 Material の破棄漏れ＝メモリリーク。</summary>
        public static void CheckMaterialLeak(PlaytestObservations obs, List<PlaytestFinding> findings)
        {
            if (obs.leakedMaterialCount > 0)
                findings.Add(new PlaytestFinding(PlaytestSeverity.注意, PlaytestCategory.リーク,
                    $"実行時生成 Material の破棄漏れ {obs.leakedMaterialCount} 件（OnDestroy で Destroy する規約）。"));
        }
    }
}
