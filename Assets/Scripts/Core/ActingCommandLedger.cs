using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 会戦中の臨時指揮の台帳（#147 拡張・static）。梯団ポスト（例「帝国/第1軍団」）ごとに、正規（戦闘開始時）の指揮官と
    /// 現在の臨時指揮官を保持。指揮官が戦死/離脱すると臨時指揮官が下位へ移る。<b>戦闘終了で `Clear`＝臨時指揮を解いて正規人事へ戻す</b>。
    /// Core 純ロジック・test-first。
    /// </summary>
    public static class ActingCommandLedger
    {
        class Entry { public string postKey; public int originalId; public int actingId; }
        static readonly List<Entry> entries = new List<Entry>();

        public static int Count => entries.Count;

        static Entry Find(string postKey)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].postKey == postKey) return entries[i];
            return null;
        }

        /// <summary>臨時指揮を記録。初回はその時点の指揮官を「正規」として刻み、以後は actingId のみ更新（正規は固定）。</summary>
        public static void Record(string postKey, int originalId, int actingId)
        {
            if (string.IsNullOrEmpty(postKey)) return;
            var e = Find(postKey);
            if (e == null)
            {
                e = new Entry { postKey = postKey, originalId = originalId };
                entries.Add(e);
            }
            e.actingId = actingId;
        }

        /// <summary>現在の指揮官id（臨時含む）。無ければ -1。</summary>
        public static int ActingFor(string postKey) { var e = Find(postKey); return e != null ? e.actingId : -1; }

        /// <summary>正規（戦闘開始時）の指揮官id。無ければ -1。</summary>
        public static int OriginalFor(string postKey) { var e = Find(postKey); return e != null ? e.originalId : -1; }

        /// <summary>臨時指揮中か＝現在の指揮官が正規と異なる（上官を失い下位が臨時継承）。</summary>
        public static bool IsActing(string postKey)
        {
            var e = Find(postKey);
            return e != null && e.actingId >= 0 && e.actingId != e.originalId;
        }

        /// <summary>戦闘終了で台帳を一掃＝臨時指揮を解いて正規人事（OrderOfBattle）へ戻す。</summary>
        public static void Clear() => entries.Clear();
    }
}
