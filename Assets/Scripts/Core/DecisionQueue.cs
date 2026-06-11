using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 非ブロッキング決裁キュー（決裁デスク DESK-1 #1629）。イベント/決裁を<b>時間を止めずに積む</b>スタック。
    /// 右下スタックUI（DESK-5）と AI トリアージ（<see cref="DecisionTriageRules"/>・DESK-2/3）の土台。
    /// 有界（<see cref="NotificationCenter"/> のリングバッファ思想＝溜めすぎない）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class DecisionQueue
    {
        /// <summary>積まれている決裁（新着→最小化→解決まで保持。解決済は <see cref="PruneResolved"/> で掃く）。</summary>
        public readonly List<PendingDecision> items = new List<PendingDecision>();

        /// <summary>活性決裁の目安上限（超過は古い通常案件から自動解決に委ねる想定）。</summary>
        public int capacity = 32;

        public int Count => items.Count;

        public void Enqueue(PendingDecision d)
        {
            if (d != null) items.Add(d);
        }

        private static bool IsResolved(PendingDecision d)
            => d.status == DecisionStatus.決裁済 || d.status == DecisionStatus.自動解決;

        /// <summary>未解決（人/AIの確定待ち）の件数。</summary>
        public int ActiveCount()
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && !IsResolved(items[i])) n++;
            return n;
        }

        /// <summary>最小化中の件数（右下バッジ用）。</summary>
        public int MinimizedCount()
        {
            int n = 0;
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].status == DecisionStatus.最小化) n++;
            return n;
        }

        /// <summary>活性上限を超えているか（溢れ処理のトリガ）。</summary>
        public bool IsOverCapacity => ActiveCount() > capacity;

        public void Minimize(PendingDecision d)
        {
            if (d == null || IsResolved(d)) return;
            d.status = DecisionStatus.最小化;
        }

        public void Restore(PendingDecision d)
        {
            if (d == null) return;
            if (d.status == DecisionStatus.最小化)
            {
                d.status = DecisionStatus.提示中;
                d.elapsed = 0f; // 再提示＝締切をリセット（即・再最小化を防ぐ＝展開がちゃんと効く）
            }
        }

        /// <summary>人が決裁する＝選択を確定し決裁済へ。採択した効果キーを返す（呼び出し側が適用）。</summary>
        public string Resolve(PendingDecision d, int choiceIndex)
        {
            if (d == null) return "";
            d.chosenIndex = choiceIndex;
            d.status = DecisionStatus.決裁済;
            return d.effectKey;
        }

        /// <summary>最前面に出すべき決裁＝未解決のうち重要度が最も高い（同列は経過が長い＝古いものを優先）。無ければ null。</summary>
        public PendingDecision Front()
        {
            PendingDecision best = null;
            for (int i = 0; i < items.Count; i++)
            {
                var d = items[i];
                if (d == null || IsResolved(d)) continue;
                if (best == null
                    || (int)d.severity > (int)best.severity
                    || ((int)d.severity == (int)best.severity && d.elapsed > best.elapsed))
                    best = d;
            }
            return best;
        }

        /// <summary>解決済（決裁済/自動解決）を掃く（履歴を別途残す想定）。</summary>
        public void PruneResolved()
        {
            items.RemoveAll(d => d == null || IsResolved(d));
        }
    }
}
