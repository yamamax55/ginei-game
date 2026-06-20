using System.Collections.Generic;

namespace Ginei
{
    /// <summary>自動プレイテスト所見の重要度（低→高）。CI のゲート判定に使う。</summary>
    public enum PlaytestSeverity { 情報, 注意, 警告, 致命 }

    /// <summary>所見の分類。レポートの色分け・フィルタ用。</summary>
    public enum PlaytestCategory { 例外, 進行, 配置, 表示, リーク, パフォーマンス }

    /// <summary>
    /// 自動プレイテスト1件の所見（純データ）。
    /// 観測（MonoBehaviour 殻＝<see cref="PlaytestObservations"/>）と判定（<see cref="PlaytestInvariants"/>）は分離し、
    /// ここは結果だけを持つ＝Core は timeless でテスト容易。
    /// </summary>
    [System.Serializable]
    public struct PlaytestFinding
    {
        public PlaytestSeverity severity;
        public PlaytestCategory category;
        public string message;
        /// <summary>観測時刻（会戦経過秒）。不明・全体に対する所見は0。</summary>
        public float timeSeconds;

        public PlaytestFinding(PlaytestSeverity severity, PlaytestCategory category, string message, float timeSeconds = 0f)
        {
            this.severity = severity;
            this.category = category;
            this.message = message ?? "";
            this.timeSeconds = timeSeconds;
        }
    }

    /// <summary>画面内収まり判定用の矩形（ピクセル）。Rect は Core 非依存方針のため float で持つ（TestHarness でも検証可）。</summary>
    [System.Serializable]
    public struct UiBounds
    {
        public string label;
        public float xMin, yMin, xMax, yMax;

        public UiBounds(string label, float xMin, float yMin, float xMax, float yMax)
        {
            this.label = label ?? "";
            this.xMin = xMin; this.yMin = yMin; this.xMax = xMax; this.yMax = yMax;
        }
    }

    /// <summary>
    /// 自動プレイテストの観測入力（MonoBehaviour 殻が会戦中に収集して詰める平データ）。
    /// 判定は <see cref="PlaytestInvariants.Evaluate"/> が行う。Unity 型に依存しない（int/float/string/bool/List のみ）。
    /// </summary>
    [System.Serializable]
    public class PlaytestObservations
    {
        public string scenarioName = "";
        /// <summary>実際に回した会戦経過秒。</summary>
        public float durationSeconds;
        /// <summary>シナリオの制限時間（秒）。0以下＝無制限（殲滅戦）。</summary>
        public float timeLimitSeconds;
        /// <summary>勝敗が決着したか（BattleManager の決着）。</summary>
        public bool resolved;
        /// <summary>開始時に敵対する旗艦ペアがあったか。無ければ進行系の不変条件はスキップ。</summary>
        public bool hadHostilePairAtStart = true;
        /// <summary>可視化キャプチャ（スクショ撮影）目的の実行か。true のとき非決着は警告でなく情報に降格する
        /// （CI のソフトGL描画は遅く時間内に決着しないのが常＝バグでないため FAIL させない）。例外等の判定は不変。</summary>
        public bool visualCaptureOnly = false;
        public int screenWidth = 1920;
        public int screenHeight = 1080;
        /// <summary>会戦中に捕捉した例外/Error ログ（各1件＝致命）。</summary>
        public List<string> errorLogs = new List<string>();
        /// <summary>実行時生成 Material の破棄漏れ数。-1＝未計測。</summary>
        public int leakedMaterialCount = -1;
        /// <summary>経過に沿った生存旗艦の総数サンプル（瞬間消失・デッドロック検出）。</summary>
        public List<int> aliveFlagshipSamples = new List<int>();
        /// <summary>各間隔の全艦総移動量サンプル（全停止＝移動破綻の検出）。</summary>
        public List<float> totalMovementSamples = new List<float>();
        /// <summary>HUD 要素の画面内収まり判定用の矩形群。</summary>
        public List<UiBounds> hudBounds = new List<UiBounds>();
        /// <summary>サンプリング間隔（秒）。所見の時刻換算に使う。</summary>
        public float sampleIntervalSeconds = 1f;
    }

    /// <summary>
    /// 自動プレイテストの結果レポート（純データ）。JSON 出力（MonoBehaviour 殻が <c>JsonUtility</c> で書く）＋CI ゲートの入力。
    /// </summary>
    [System.Serializable]
    public class PlaytestReport
    {
        public string scenarioName = "";
        public float durationSeconds;
        public int sampleCount;
        public List<PlaytestFinding> findings = new List<PlaytestFinding>();

        /// <summary>最も高い重要度。所見が無ければ 情報。</summary>
        public PlaytestSeverity HighestSeverity
        {
            get
            {
                PlaytestSeverity max = PlaytestSeverity.情報;
                for (int i = 0; i < findings.Count; i++)
                    if (findings[i].severity > max) max = findings[i].severity;
                return max;
            }
        }

        /// <summary>合格＝警告以上の所見が無い。</summary>
        public bool Passed => HighestSeverity < PlaytestSeverity.警告;

        /// <summary>指定重要度の所見数。</summary>
        public int CountOf(PlaytestSeverity severity)
        {
            int n = 0;
            for (int i = 0; i < findings.Count; i++) if (findings[i].severity == severity) n++;
            return n;
        }

        /// <summary>CI 終了コード推奨値：致命=2／警告=1／それ未満=0。</summary>
        public int SuggestedExitCode
        {
            get
            {
                var s = HighestSeverity;
                if (s >= PlaytestSeverity.致命) return 2;
                if (s >= PlaytestSeverity.警告) return 1;
                return 0;
            }
        }
    }
}
