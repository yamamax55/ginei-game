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
