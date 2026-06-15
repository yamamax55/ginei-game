using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 進行中の陳情（稟議）の在庫＝目安箱の単一の保管庫（稟議基盤整備）。<see cref="Petition"/> の状態機械
    /// （<see cref="WorkflowRules"/>）・伝播（<see cref="PetitionFlowRules"/>）・効果（<see cref="PetitionEffects"/>）は
    /// すでに窓口がそろっているが、<b>生きている陳情を誰も保持していなかった</b>＝消費側（GalaxyView/受信箱UI）が
    /// 都度ハンドワイヤする必要があった。ここはその唯一の store＝勢力/箱ごとの照会と有界化を担う。
    /// <see cref="NotificationCenter"/> の有界リング思想に倣い、容量超過は<b>古い決着済みから</b>落とす
    /// （活性・決裁待ちは残す）。打ち切りは <see cref="droppedCount"/> で可視化（silent truncation 禁止＝PERF）。
    /// 状態遷移ロジックは持たず既存窓口へ委譲する（並行新設しない）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class PetitionLedger
    {
        /// <summary>保持中の陳情（投入順）。決着済みは容量超過時に <see cref="Prune"/> で掃く。</summary>
        public readonly List<Petition> items = new List<Petition>();

        /// <summary>活性陳情の目安上限（超過分は古い決着済みから落とす）。</summary>
        public int capacity = 64;

        /// <summary>容量超過で落とした決着済み陳情の累計（観測用＝打ち切りを silent にしない）。</summary>
        public int droppedCount;

        private int seq;

        public int Count => items.Count;

        /// <summary>id 採番（1始まり・単調増加）。投入時に id=0 なら自動付与する。</summary>
        public int NextId() => ++seq;

        /// <summary>
        /// 陳情を投入する（id 未設定なら採番）。投入後に容量超過なら古い決着済みから落とす。
        /// 既に同 id が在席なら二重投入しない（false）。
        /// </summary>
        public bool Add(Petition pet)
        {
            if (pet == null) return false;
            if (pet.id == 0) pet.id = NextId();
            else if (pet.id > seq) seq = pet.id; // 外部採番に追従（採番衝突を防ぐ）
            if (Get(pet.id) != null) return false;
            items.Add(pet);
            Prune();
            return true;
        }

        /// <summary>id で引く（無ければ null）。</summary>
        public Petition Get(int id)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].id == id) return items[i];
            return null;
        }

        /// <summary>在席から外す（履歴に残さず取り除く＝主に test/明示破棄用）。</summary>
        public bool Remove(int id)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null && items[i].id == id) { items.RemoveAt(i); return true; }
            }
            return false;
        }

        /// <summary>進行中（起案/伝播中/決裁待ち/承認/再浮上）の件数。</summary>
        public int ActiveCount()
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
                if (WorkflowRules.IsActive(items[i])) n++;
            return n;
        }

        /// <summary>進行中の陳情を集める。</summary>
        public List<Petition> Active()
        {
            var list = new List<Petition>();
            for (int i = 0; i < items.Count; i++)
                if (WorkflowRules.IsActive(items[i])) list.Add(items[i]);
            return list;
        }

        /// <summary>決裁待ちの陳情を集める（受信箱の決裁トレイ）。任意で箱/地方スコープで絞る。</summary>
        public List<Petition> AwaitingDecision(BoxKind? box = null, string regionKey = null)
        {
            var list = new List<Petition>();
            for (int i = 0; i < items.Count; i++)
            {
                var p = items[i];
                if (p == null || p.status != PetitionStatus.決裁待ち) continue;
                if (box.HasValue && p.box != box.Value) continue;
                if (regionKey != null && p.regionKey != regionKey) continue;
                list.Add(p);
            }
            return list;
        }

        /// <summary>指定勢力の陳情を集める。</summary>
        public List<Petition> ByFaction(Faction faction)
        {
            var list = new List<Petition>();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].faction == faction) list.Add(items[i]);
            return list;
        }

        /// <summary>指定の箱（＋地方スコープ）宛の陳情を集める。</summary>
        public List<Petition> ByBox(BoxKind box, string regionKey = null)
        {
            var list = new List<Petition>();
            for (int i = 0; i < items.Count; i++)
            {
                var p = items[i];
                if (p == null || p.box != box) continue;
                if (regionKey != null && p.regionKey != regionKey) continue;
                list.Add(p);
            }
            return list;
        }

        /// <summary>
        /// 容量超過分を<b>古い決着済み（執行済/却下）から</b>落とす（活性・決裁待ちは残す）。
        /// 落とした件数を <see cref="droppedCount"/> に積む（打ち切りの可視化）。
        /// </summary>
        public void Prune()
        {
            int over = items.Count - capacity;
            for (int i = 0; i < items.Count && over > 0;)
            {
                if (WorkflowRules.IsResolved(items[i])) { items.RemoveAt(i); over--; droppedCount++; }
                else i++;
            }
        }

        /// <summary>全消去（戦役リセット用）。採番・打ち切り計も戻す。</summary>
        public void Clear()
        {
            items.Clear();
            seq = 0;
            droppedCount = 0;
        }
    }
}
