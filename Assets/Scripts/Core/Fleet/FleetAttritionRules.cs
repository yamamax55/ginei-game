using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 会戦の損害を、参加した戦略艦隊へ<b>按分</b>して戻す純ロジック。
    ///
    /// 戦術マップは「攻撃側の生き残り合計」しか返さないので、それを参加各隊の
    /// <b>参戦時の兵力比</b>で割り振る。全滅（残存0）なら全隊が0になり、盤面から除かれる。
    ///
    /// <b>窓口を1つにする理由</b>：放置の自動解決（力攻め）と、潜行して戦った結果の書き戻しの
    /// 両方が同じ「残存を按分する」処理を必要とする。別々に書くと片方だけ直したり、
    /// 同じ会戦で二度減らしたりする（実機QA：参戦した援軍が無傷で帰ってきた／本隊が消えた）。
    ///
    /// 純ロジック（非 MonoBehaviour・決定論・test-first）。
    /// </summary>
    public static class FleetAttritionRules
    {
        /// <summary>
        /// 参戦時の兵力 <paramref name="before"/> の各隊へ、残存合計 <paramref name="survivor"/> を按分した
        /// 新しい兵力を返す（入力と同じ並び）。
        ///
        /// ・合計が 0 以下、または <paramref name="survivor"/> が 0 以下なら全隊 0（全滅）。
        /// ・<paramref name="survivor"/> が合計以上なら減らさない（無傷＝そのまま返す）。
        /// ・端数は切り捨てたうえで、<b>余りを大きい隊から1ずつ配る</b>＝合計が必ず survivor に一致する
        ///   （四捨五入だけだと合計がずれて兵力が増減する）。
        /// </summary>
        public static void Distribute(IList<int> before, int survivor, IList<int> results)
        {
            if (results == null) return;
            results.Clear();
            if (before == null || before.Count == 0) return;

            int total = 0;
            for (int i = 0; i < before.Count; i++) total += Mathf.Max(0, before[i]);

            if (total <= 0 || survivor <= 0)
            {
                for (int i = 0; i < before.Count; i++) results.Add(0);
                return;
            }
            if (survivor >= total)
            {
                for (int i = 0; i < before.Count; i++) results.Add(Mathf.Max(0, before[i]));
                return;
            }

            // まず切り捨てで配る。
            int assigned = 0;
            for (int i = 0; i < before.Count; i++)
            {
                int b = Mathf.Max(0, before[i]);
                int v = (int)((long)b * survivor / total);
                results.Add(v);
                assigned += v;
            }

            // 余りを、参戦時の兵力が大きい隊から1ずつ配る（決定論＝同値なら先の隊が先）。
            int remainder = survivor - assigned;
            while (remainder > 0)
            {
                int pick = -1, bestBefore = -1;
                for (int i = 0; i < before.Count; i++)
                {
                    int b = Mathf.Max(0, before[i]);
                    if (b <= 0) continue;
                    // すでに参戦時の兵力に達している隊へは足さない（増やさない）。
                    if (results[i] >= b) continue;
                    if (b > bestBefore) { bestBefore = b; pick = i; }
                }
                if (pick < 0) break;   // 配れる先が無い
                results[pick]++;
                remainder--;
            }
        }

        /// <summary>按分後の合計（検算用）。</summary>
        public static int Total(IList<int> values)
        {
            if (values == null) return 0;
            int t = 0;
            for (int i = 0; i < values.Count; i++) t += Mathf.Max(0, values[i]);
            return t;
        }
    }
}
