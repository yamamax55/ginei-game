using System.Collections.Generic;

namespace Ginei
{
    /// <summary>決裁1件の記録（稟議基盤整備・人物の決裁履歴）。誰が・どの陳情を・承認/却下したかの最小スナップショット。
    /// 文面（WHY）でなく決定（WHAT）だけを持つ＝決定論・直列化可。<see cref="seq"/> は採番（大きいほど新しい）。</summary>
    public readonly struct PersonDecisionRecord
    {
        public readonly long seq;
        public readonly int petitionId;
        public readonly string title;
        public readonly bool approved;   // true=承認 / false=却下
        public readonly string effectKey;
        public readonly Faction faction;

        public PersonDecisionRecord(long seq, int petitionId, string title, bool approved, string effectKey, Faction faction)
        {
            this.seq = seq;
            this.petitionId = petitionId;
            this.title = title ?? "";
            this.approved = approved;
            this.effectKey = effectKey ?? "";
            this.faction = faction;
        }
    }

    /// <summary>
    /// 人物ごとの「決裁した内容」の履歴台帳（稟議基盤整備）。<see cref="PersonRingiRules.Adjudicate"/> が決裁を確定した瞬間に
    /// 記録し、人物詳細画面（人事オーバーレイ）がその人物の履歴を読む＝「自分のところに来た稟議を決裁した記録を残す」。
    /// 人物につき<b>最新 <see cref="Capacity"/>(=20) 件</b>を新しい順に保持し、超過は古いものから捨てる
    /// （<see cref="NotificationCenter"/> の有界リング思想＝無制限増加・終盤ラグを防ぐ・PERF）。観測専用の表示在庫。
    /// 静的な単一窓口（<see cref="FleetRoster"/>/<see cref="NotificationCenter"/> と同型）。test-first。
    /// </summary>
    public static class PersonDecisionLedger
    {
        /// <summary>人物ごとに残す上限（最新からこの件数）。</summary>
        public const int Capacity = 20;

        private static readonly Dictionary<int, List<PersonDecisionRecord>> byPerson
            = new Dictionary<int, List<PersonDecisionRecord>>();
        private static long nextSeq = 1;

        private static readonly IReadOnlyList<PersonDecisionRecord> Empty = new List<PersonDecisionRecord>();

        /// <summary>決裁を記録する（先頭＝最新へ。<see cref="Capacity"/> 超過は最古を1件捨てる）。personId=0 は記録しない。</summary>
        public static void Record(int personId, int petitionId, string title, bool approved, string effectKey, Faction faction)
        {
            if (personId == 0) return;
            if (!byPerson.TryGetValue(personId, out var list))
            {
                list = new List<PersonDecisionRecord>(Capacity);
                byPerson[personId] = list;
            }
            list.Insert(0, new PersonDecisionRecord(nextSeq++, petitionId, title, approved, effectKey, faction));
            if (list.Count > Capacity) list.RemoveAt(list.Count - 1); // 最古を捨てる
        }

        /// <summary>陳情から決裁を記録する（便宜）。</summary>
        public static void Record(int personId, Petition pet, bool approved)
        {
            if (pet == null) return;
            Record(personId, pet.id, pet.title, approved, pet.effectKey, pet.faction);
        }

        /// <summary>その人物の決裁履歴（新しい順・最大 <see cref="Capacity"/> 件）。未記録は空。</summary>
        public static IReadOnlyList<PersonDecisionRecord> Recent(int personId)
            => byPerson.TryGetValue(personId, out var list) ? list : Empty;

        /// <summary>その人物の保持件数。</summary>
        public static int Count(int personId)
            => byPerson.TryGetValue(personId, out var list) ? list.Count : 0;

        /// <summary>全消去（戦役リセット用）。採番も戻す。</summary>
        public static void Clear()
        {
            byPerson.Clear();
            nextSeq = 1;
        }
    }
}
