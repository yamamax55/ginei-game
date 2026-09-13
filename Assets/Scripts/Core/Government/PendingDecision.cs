using System.Collections.Generic;

namespace Ginei
{
    /// <summary>決裁の重要度（決裁デスク DESK-2 #1630）。重大だけが時間を止める（アクティブポーズ）。</summary>
    public enum DecisionSeverity { 情報, 通常, 重要, 重大 }

    /// <summary>決裁の出所（DESK-1 #1629）。イベント＝システム発／諮問・建白結果＝目安箱 #1296 発。</summary>
    public enum DecisionSource { イベント, 諮問, 建白結果, システム }

    /// <summary>決裁の状態（DESK-1 #1629）。新着→提示中→（締切超）最小化→（さらに超）自動解決／人が決めれば決裁済。</summary>
    public enum DecisionStatus { 新着, 提示中, 最小化, 自動解決, 決裁済 }

    /// <summary>
    /// 保留中の1件の決裁（決裁デスク DESK-1 #1629）。イベント（<see cref="EventEngine"/>）や目安箱の諮問/裁可（#1296）が
    /// これに写されて右下スタックに積まれる。<b>時間は止めない</b>（重大を除く）。締切（<see cref="elapsed"/> vs
    /// <see cref="DecisionTriageRules.DeadlineFor"/>）を超えると最小化、さらに超えると AI が <see cref="defaultChoiceIndex"/> を
    /// 機械的に採択（自動解決）。効果は <see cref="effectKey"/> 駆動（`PetitionEffects`/`EventChoice` と共有）。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class PendingDecision
    {
        public int id;
        public string title;
        /// <summary>本文（詳細）。カードを展開した時に表示する。空ならタイトル（要約）のみ。</summary>
        public string body = "";
        /// <summary>フレーバー画像のキー（Resources/Flavor/{imageKey}）。空なら既定の flavor_default を使う。</summary>
        public string imageKey = "";
        public DecisionSeverity severity = DecisionSeverity.通常;
        public DecisionSource source = DecisionSource.システム;

        /// <summary>選択肢ラベル。</summary>
        public readonly List<string> choices = new List<string>();
        /// <summary>締切超/AI自動解決で採択する既定の選択（現状維持/最小コスト想定）。</summary>
        public int defaultChoiceIndex;

        /// <summary>採択時に呼ぶ効果（直列化可・`PetitionEffects`/`EventChoice` と同実体）。</summary>
        public string effectKey = "";

        public DecisionStatus status = DecisionStatus.新着;

        /// <summary>提示からの経過 game-秒（<see cref="DecisionTriageRules.Tick"/> が進める）。</summary>
        public float elapsed;

        /// <summary>決まった選択肢（-1=未決）。人 or AI が確定する。</summary>
        public int chosenIndex = -1;

        // ===== 執行（承認と執行成功を分ける・#稟議完成①②⑤）=====

        /// <summary>
        /// 効果を<b>もう適用したか</b>。決裁済/自動解決という「状態」とは別に立てる＝
        /// どの経路（右下カード・決裁ボード・期限切れ・AI）から解決されても<b>効果は一度だけ</b>。
        /// 二重クリック・期限切れと手動決裁の競合・解決済みの再解決は、ここで弾く。
        /// </summary>
        public bool applied;

        /// <summary>執行の結果（<see cref="applied"/> が true のときだけ意味がある）。</summary>
        public PetitionActionOutcome outcome = PetitionActionOutcome.対象外;

        /// <summary>
        /// 執行の結果の1行（実際の変更値・投入艦隊・命令先・外交結果、または実行できない理由）。
        /// 決裁後に画面へ出す＝<b>承認できたことと、実際に効いたことを区別</b>する。
        /// </summary>
        public string resultDetail = "";

        /// <summary>
        /// この決裁の元になった稟議（<see cref="Petition.id"/>）。0＝稟議に紐づかない決裁。
        /// ★決裁カードと稟議の対応を<b>カード自身が持つ</b>ことで、シーン往復で Director の
        /// インスタンス状態が消えても対応を失わない（幽霊カードを作らない）。
        /// </summary>
        public int petitionId;

        /// <summary>
        /// 官僚機構の摩擦（0..1）。執行忠実度＝<see cref="PetitionFlowRules.ExecutionFidelity"/> の材料。
        /// これもカードが持つ＝Director のインスタンス状態に依存せず、保存・復元もできる。
        /// </summary>
        public float friction;

        /// <summary>
        /// 勝敗メーターへ反映済みか（<see cref="DecisionMeterEffects.MarkMeterApplied"/>）。
        /// 効果の適用（<see cref="applied"/>）とは別勘定＝稟議の執行に失敗してもメーターは二重加算しない。
        /// </summary>
        public bool meterApplied;

        /// <summary>まだ効果を適用していないか（＝執行の対象になりうる）。</summary>
        public bool NotYetApplied => !applied;

        // ===== 誰が出して誰が決めるか・何を対象にするか（#67／⑤の表示）=====

        /// <summary>提案者（実在の人物ID・0=機関からの上申で個人に紐づかない）。</summary>
        public int proposerId;
        /// <summary>提案者の表示名。<b>架空の名前を入れない</b>＝実在の人物から取る。</summary>
        public string proposerName = "";

        /// <summary>決裁権者（実在の人物ID・0=箱宛て）。</summary>
        public int deciderId;
        /// <summary>決裁権者の表示名。</summary>
        public string deciderName = "";

        /// <summary>権限の根拠（役職名・所掌・範囲、または権限外の理由）。画面にそのまま出す。</summary>
        public string authorityBasis = "";

        /// <summary>
        /// 提案の時点で名指しした対象（<see cref="PetitionTarget.Encode"/> の直列化文字列）。
        /// 承認後に別の対象へ勝手に振り替えないための固定。空＝勢力全体（名指しなし）。
        /// </summary>
        public string targetKey = "";

        /// <summary>上申中か（自分では決められず、決裁権者の返事を待っている）。</summary>
        public bool escalated;

        /// <summary>名指しした対象（<see cref="targetKey"/> の復号）。</summary>
        public PetitionTarget Target => PetitionTarget.Decode(targetKey);

        /// <summary>名指しの対象を設定する。</summary>
        public void SetTarget(in PetitionTarget target) => targetKey = target.HasTarget ? target.Encode() : "";

        public PendingDecision() { }

        public PendingDecision(int id, string title, DecisionSeverity severity,
            DecisionSource source = DecisionSource.システム, string effectKey = "", int defaultChoiceIndex = 0,
            string body = "")
        {
            this.id = id;
            this.title = title;
            this.severity = severity;
            this.source = source;
            this.effectKey = effectKey ?? "";
            this.defaultChoiceIndex = defaultChoiceIndex;
            this.body = body ?? "";
        }
    }
}
