using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>一代記に刻む生涯イベントの種別（TKO-6・#2483）。入校〜死去まで、主人公の人生の節目。</summary>
    public enum ChronicleEventKind { 入校, 卒業任官, 配属, 主命拝命, 主命達成, 主命失敗, 昇進, 武勲, 叙勲, 岐路, 死去 }

    /// <summary>一代記の一行（TKO-6）。発生した通算月＋種別＋一言（表示用・文面は LLM 後付け想定）。</summary>
    public class ChronicleEntry
    {
        public int monthIndex;
        public ChronicleEventKind kind;
        public string note;

        public ChronicleEntry() { }
        public ChronicleEntry(int monthIndex, ChronicleEventKind kind, string note)
        {
            this.monthIndex = monthIndex;
            this.kind = kind;
            this.note = note ?? "";
        }
    }

    /// <summary>
    /// 主人公の一代記（TKO-6・太閤立志伝V「立志伝＝一人の生涯の編纂」・#2483・純データ）。生涯イベント（<see cref="ChronicleEntry"/>）を
    /// 発生順に追記する。<b>無制限に増えないよう上限</b>（<see cref="capacity"/>）を設け、超過は<b>古い順</b>に落とす（PERF・終盤ラグ回避）。
    /// 落とした件数は <see cref="droppedCount"/> で可視化（silent truncation 禁止）。追記・整形は <see cref="ProtagonistChronicleRules"/> が窓口。
    /// 列伝#784/殿堂#785 を一人にフォーカスした自伝として Game 層が表示する（`ChronicleObserverOverlay`/執務机UI）。
    /// </summary>
    public class ProtagonistChronicle
    {
        public readonly List<ChronicleEntry> entries = new List<ChronicleEntry>();
        /// <summary>保持上限（超過は古い順に落とす）。</summary>
        public int capacity = 256;
        /// <summary>上限超過で落とした件数（観測用）。</summary>
        public int droppedCount;

        public int Count => entries.Count;

        /// <summary>全消去（戦役リセット用）。</summary>
        public void Clear() { entries.Clear(); droppedCount = 0; }
    }

    /// <summary>
    /// 主人公の一代記の純ロジック（TKO-6・#2483・唯一の窓口）。生涯イベントの追記と有界化・新しい順の取り出しを担う。
    /// 数値は持たず（記録のみ）、イベントの出所は他モジュール（<see cref="ProtagonistCareerRules"/>/<see cref="MeritRecordRules"/>/
    /// <see cref="SovereignMandateRules"/>/<see cref="MonthlyCouncilRules"/>）＝二重実装しない。決定論・test-first。
    /// </summary>
    public static class ProtagonistChronicleRules
    {
        /// <summary>生涯イベントを追記する（発生順）。上限超過なら古い順に落とし <see cref="ProtagonistChronicle.droppedCount"/> を積む。</summary>
        public static void Record(ProtagonistChronicle c, int monthIndex, ChronicleEventKind kind, string note)
        {
            if (c == null) return;
            c.entries.Add(new ChronicleEntry(monthIndex, kind, note));
            int over = c.entries.Count - Mathf.Max(1, c.capacity);
            if (over > 0)
            {
                c.entries.RemoveRange(0, over); // 古い順（先頭）から落とす
                c.droppedCount += over;
            }
        }

        /// <summary>新しい順に最大 <paramref name="n"/> 件を取り出す（表示用＝最近の出来事から遡る）。</summary>
        public static List<ChronicleEntry> Recent(ProtagonistChronicle c, int n)
        {
            var list = new List<ChronicleEntry>();
            if (c == null || n <= 0) return list;
            for (int i = c.entries.Count - 1; i >= 0 && list.Count < n; i--)
                list.Add(c.entries[i]);
            return list;
        }
    }
}
