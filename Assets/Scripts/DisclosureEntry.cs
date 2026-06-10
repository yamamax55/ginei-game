using System;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 開示状態（FND-4 #495・秘史開示 #450）。世界の「明らかになった真実」の集合を保持する。
    /// 直列化（FND-2 セーブ）しやすいよう id のリストで持つ。判定・開示は <see cref="DisclosureRules"/> が唯一の窓口。
    /// </summary>
    [Serializable]
    public class DisclosureState
    {
        public List<string> revealedIds = new List<string>();

        public bool IsRevealed(string id) => id != null && revealedIds.Contains(id);

        /// <summary>真実を1つ開示済みにする（既に開示済み/無効idは false）。</summary>
        public bool Reveal(string id)
        {
            if (string.IsNullOrEmpty(id) || revealedIds.Contains(id)) return false;
            revealedIds.Add(id);
            return true;
        }

        public int Count => revealedIds.Count;
    }

    /// <summary>
    /// 1つの開示項目（FND-4 #495・データ駆動の「条件→開示/変化」）。秘史・真相・予言・エンディング条件などを
    /// <b>隠れた事実</b>として定義し、<b>前提（他の開示）</b>と<b>条件（コンテキスト）</b>が揃うと開示される（連鎖開示）。
    /// 開示時に効果（イデオロギー変化・エンディング解放…）を適用できる。世界観EPICはこれを“データ入力”として乗せる。
    /// 評価・連鎖は <see cref="DisclosureLedger"/>。<see cref="EventContext"/> はイベントエンジン #116 と共有。
    /// </summary>
    public class DisclosureEntry
    {
        public string id;
        public string title;
        public string body;      // 開示される真実の本文
        public string category;  // 秘史 / 真相 / 予言 / エンディング 等

        /// <summary>これより先に開示されていなければならない他の項目（連鎖＝秘史Aが秘史Bを解く）。</summary>
        public List<string> prerequisites = new List<string>();

        /// <summary>開示条件（null＝前提さえ揃えば開示可）。コンテキストを見て真なら開示候補。</summary>
        public Func<EventContext, bool> condition;

        /// <summary>開示時の効果（イデオロギー変化・エンディング解放等。任意）。</summary>
        public Action<EventContext> onReveal;

        public DisclosureEntry() { }
        public DisclosureEntry(string id, string title, string body, string category = "秘史")
        {
            this.id = id;
            this.title = title ?? "";
            this.body = body ?? "";
            this.category = category ?? "秘史";
        }

        /// <summary>前提（先行開示）を加える（流れるように定義）。</summary>
        public DisclosureEntry Requires(params string[] ids)
        {
            if (ids != null) prerequisites.AddRange(ids);
            return this;
        }

        /// <summary>開示条件を設定する（流れるように定義）。</summary>
        public DisclosureEntry When(Func<EventContext, bool> cond) { condition = cond; return this; }

        /// <summary>開示時の効果を設定する（流れるように定義）。</summary>
        public DisclosureEntry OnReveal(Action<EventContext> act) { onReveal = act; return this; }
    }
}
