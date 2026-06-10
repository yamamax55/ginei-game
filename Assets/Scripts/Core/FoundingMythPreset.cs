using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 建国神話プリセット（ゲーム開始時の「出発点」データ・ScriptableObject）。Issue #490(START-1)。
    ///
    /// スロースタート方針（#489）に沿って、開始時は深いビルドを作らせず、少数のプリセットから1つ選ぶだけにする。
    /// プリセットは「どの勢力で始めるか（<see cref="FactionData"/>）＋世界観の2軸＋ごく軽い初期傾向（味付け）」を束ねる。
    /// イデオロギー/体制/階級そのものは <see cref="FactionData"/>（ideology・legacyFaction・ranks）が唯一の出所で、
    /// ここでは参照するだけ（重複定義しない）。プリセットは“確定ビルド”でなく“種”で、以後イベント等で変化する想定。
    ///
    /// 全プリセットに共通で「授けられた方舟」スタート（LORE-8 #494）を付与する：
    /// 現人類は技術を自力で築いたのではなく、機神が惑星に置いた宇宙船・FTL一式から始まる＝借り物の文明。
    /// これが「思想は未成熟なのに宇宙にいる」理由（END-1 #459）と、ASI/AGIに届かない天井（CAP #474）の裏付けになる。
    ///
    /// アセットは エディタメニュー `Ginei/Create Founding Myth Presets` で生成（`Resources/FoundingMyths/`）。
    /// 後方互換：会戦・既存シナリオは無改変。本データは開始局面（タイトル/シナリオ選択）の意味づけ用。
    /// </summary>
    [CreateAssetMenu(fileName = "NewFoundingMyth", menuName = "Ginei/Founding Myth Preset")]
    public class FoundingMythPreset : ScriptableObject
    {
        [Header("基本情報")]
        [Tooltip("プリセット名（出発点の表示名。例：王党派 ── 神授の帝政）")]
        public string presetName = "建国神話";

        [TextArea(2, 5)]
        [Tooltip("この出発点の建国神話・フレーバー（タイトル/選択画面の説明文）")]
        public string foundingMyth = "";

        [Tooltip("この出発点が建てる勢力定義。ideology・legacyFaction・ranks の出所（重複定義しない）")]
        public FactionData factionData;

        [Header("世界観の2軸（-1〜+1・IDE-3 #470 / 正統性 ENU-4↔SOC-3）")]
        [Range(-1f, 1f)]
        [Tooltip("設計(ID)↔創発(進化論)。-1＝設計され従う世界観／+1＝設計者なき創発を信じる")]
        public float designVsEmergence = 0f;

        [Range(-1f, 1f)]
        [Tooltip("正統性の源泉。-1＝上から(神授・血統)／+1＝下から(契約・合議)")]
        public float authorityAxis = 0f;

        [Header("初期傾向（味付け程度・実効値パターンで読む想定／基準値は非破壊）")]
        [Range(0f, 2f)]
        [Tooltip("拡張・探索への傾き（1.0＝標準）")]
        public float expansionBias = 1f;

        [Range(0f, 2f)]
        [Tooltip("攻撃性への傾き（1.0＝標準）")]
        public float aggressionBias = 1f;

        [Range(0f, 2f)]
        [Tooltip("結束・士気への傾き（1.0＝標準）")]
        public float cohesionBias = 1f;

        [Range(0f, 2f)]
        [Tooltip("規律・統制への傾き（1.0＝標準）")]
        public float disciplineBias = 1f;

        [Range(0f, 2f)]
        [Tooltip("開明・科学/啓蒙への傾き（1.0＝標準）")]
        public float opennessBias = 1f;

        [Header("授けられた方舟スタート（全プリセット共通・LORE-8 #494）")]
        [Tooltip("機神が惑星に置いた宇宙船・FTL一式から始まる（自力発明ではない＝借り物の文明）。全プリセット共通でtrue")]
        public bool bestowedArk = true;

        [Tooltip("開始時に授かる艦隊数（スロースタート：既定1）")]
        public int startingFleets = 1;

        [Tooltip("開始時の本拠星系数（スロースタート：既定1）")]
        public int startingSystems = 1;

        [TextArea(2, 4)]
        [Tooltip("方舟スタートの世界観メモ（開示前は『神々が授けた聖なる船』として神話化される＝LORE-6 #456）")]
        public string arkNote =
            "機神が苗床(惑星)に意図的に置いた宇宙船・FTL一式から始まる。自力で発明していない＝原理を持たない借り物の文明。" +
            "ゆえに自己改良AI(ASI/AGI)には届かない(CAP #474)。文明側は方舟を『神授の聖船』と崇める(LORE-6 #456)。";

        // ───────────── 参照ヘルパ（FactionData を唯一の出所として読むだけ） ─────────────

        /// <summary>この出発点の思想・系統（FactionData.ideology を参照。未割当なら空文字）。</summary>
        public string Ideology => factionData != null ? factionData.ideology : "";

        /// <summary>旧 enum Faction との対応（FactionData.legacyFaction を参照。未割当なら帝国）。</summary>
        public Faction LegacyFaction => factionData != null ? factionData.legacyFaction : Faction.帝国;

        /// <summary>勢力名（FactionData.factionName を参照。未割当なら presetName）。</summary>
        public string FactionName => factionData != null ? factionData.factionName : presetName;
    }
}
