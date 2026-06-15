using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 配下艦のスロット割当（#69 EMOV-1 / #80 EMOV-2・純ロジック・test-first）。
    /// 「現在位置に近いスロットへ」割り当てて、陣形変更・配下艦戦死時の<b>席替えの交差</b>を防ぐ。
    /// 旗艦の向き=+Y=前方=敵方向。艦種重み（EMOV-2）で戦艦を前面/外周（楯）・駆逐艦を側面（遊撃）へ寄せる。
    /// Squadron（Game）はこれを呼ぶだけ＝割当の数式を Game 側に二重実装しない。
    /// 割当はスロット集合が変わった時だけ呼ぶ想定（毎フレーム呼ばない＝スケーラビリティ規律）。
    /// </summary>
    public static class EscortSlotAssignmentRules
    {
        /// <summary>
        /// 距離のみの安定割当（EMOV-1・配下艦戦死時の再フィット用＝軽量）。
        /// 各メンバを添字順に「現在位置から最も近い未使用スロット」へ割り当てる（O(n×m)・ソート不要）。
        /// 返り値 assignment[member]=slotIndex（割当不能＝-1。member数>slot数のとき余りが-1）。
        /// 決定論（同距離は小さいスロット添字を優先）。
        /// </summary>
        public static int[] Assign(IList<Vector2> positions, IList<Vector2> slots)
        {
            int n = positions != null ? positions.Count : 0;
            int m = slots != null ? slots.Count : 0;
            var assignment = NewUnassigned(n);
            if (n <= 0 || m <= 0) return assignment;

            var slotUsed = new bool[m];
            for (int i = 0; i < n; i++)
            {
                int best = -1;
                float bestCost = float.MaxValue;
                for (int j = 0; j < m; j++)
                {
                    if (slotUsed[j]) continue;
                    float c = (positions[i] - slots[j]).sqrMagnitude;
                    if (c < bestCost) { bestCost = c; best = j; }
                }
                if (best >= 0) { assignment[i] = best; slotUsed[best] = true; }
            }
            return assignment;
        }

        /// <summary>
        /// 距離＋艦種重みの割当（EMOV-2・陣形変更/初回の配置用＝品質重視）。
        /// 全(member,slot)ペアをコスト昇順に見る貪欲割当で、距離に艦種不一致ペナルティ
        /// （classBias）を足す＝戦艦を前面/外周・駆逐艦を側面へ寄せる。
        /// classBias≤0 or classes=null のときは距離のみ（<see cref="Assign(IList&lt;Vector2&gt;,IList&lt;Vector2&gt;)"/>）。
        /// </summary>
        public static int[] AssignWithClass(IList<Vector2> positions, IList<Vector2> slots,
                                            IList<ShipClass> classes, float classBias)
        {
            int n = positions != null ? positions.Count : 0;
            int m = slots != null ? slots.Count : 0;
            if (classBias <= 0f || classes == null) return Assign(positions, slots);

            // スロットごとの好ましい艦種を事前計算（広がりで正規化）。
            float maxR = SlotMaxRadius(slots, m);
            var slotPref = new ShipClass[m];
            for (int j = 0; j < m; j++) slotPref[j] = PreferredClassForSlot(slots[j], maxR);

            return AssignGreedy(n, m, (i, j) =>
            {
                float c = (positions[i] - slots[j]).sqrMagnitude;
                if (i < classes.Count && classes[i] != slotPref[j]) c += classBias;
                return c;
            });
        }

        /// <summary>
        /// コスト関数に基づく貪欲割当（全ペアをコスト昇順に確定）。決定論
        /// （同コストは member→slot の昇順）。member数>slot数のとき余りは-1。
        /// </summary>
        public static int[] AssignGreedy(int memberCount, int slotCount, Func<int, int, float> cost)
        {
            var assignment = NewUnassigned(memberCount);
            if (memberCount <= 0 || slotCount <= 0 || cost == null) return assignment;

            var pairs = new List<Pair>(memberCount * slotCount);
            for (int mi = 0; mi < memberCount; mi++)
                for (int s = 0; s < slotCount; s++)
                    pairs.Add(new Pair(mi, s, cost(mi, s)));
            pairs.Sort(ComparePairs);

            var slotUsed = new bool[slotCount];
            int need = memberCount < slotCount ? memberCount : slotCount;
            int assigned = 0;
            for (int p = 0; p < pairs.Count && assigned < need; p++)
            {
                Pair pr = pairs[p];
                if (assignment[pr.Member] != -1 || slotUsed[pr.Slot]) continue;
                assignment[pr.Member] = pr.Slot;
                slotUsed[pr.Slot] = true;
                assigned++;
            }
            return assignment;
        }

        /// <summary>
        /// スロット位置から好ましい艦種を判定（EMOV-2）。旗艦の向き=+Y=前方=敵方向。
        /// 前方かつ外周＝戦艦（楯）／側面・後方の外周＝駆逐艦（遊撃）／中核＝巡航艦。
        /// maxRadius はスロット集合の最大半径（陣形サイズで正規化＝サイズ非依存）。
        /// </summary>
        public static ShipClass PreferredClassForSlot(Vector2 slot, float maxRadius)
        {
            if (maxRadius < 1e-4f) return ShipClass.巡航艦;
            float radial = slot.magnitude / maxRadius;      // 0..1 中心からの距離
            float forward = slot.y / maxRadius;             // 前方度（+で前方）
            float lateral = Mathf.Abs(slot.x) / maxRadius;  // 側方度

            if (forward > 0.35f && radial > 0.5f) return ShipClass.戦艦;            // 前面/外周＝楯
            if (radial > 0.5f && (forward < -0.15f || lateral > 0.55f))            // 側面/後方の外周＝遊撃
                return ShipClass.駆逐艦;
            return ShipClass.巡航艦;                                               // 中核
        }

        /// <summary>スロット集合の最大半径（中心＝原点からの距離）。</summary>
        public static float SlotMaxRadius(IList<Vector2> slots, int count)
        {
            float maxR = 0f;
            for (int j = 0; j < count; j++)
            {
                float r = slots[j].magnitude;
                if (r > maxR) maxR = r;
            }
            return maxR;
        }

        private static int[] NewUnassigned(int n)
        {
            var a = new int[n < 0 ? 0 : n];
            for (int i = 0; i < a.Length; i++) a[i] = -1;
            return a;
        }

        private readonly struct Pair
        {
            public readonly int Member;
            public readonly int Slot;
            public readonly float Cost;
            public Pair(int member, int slot, float cost) { Member = member; Slot = slot; Cost = cost; }
        }

        private static int ComparePairs(Pair a, Pair b)
        {
            int c = a.Cost.CompareTo(b.Cost);
            if (c != 0) return c;
            c = a.Member.CompareTo(b.Member);
            if (c != 0) return c;
            return a.Slot.CompareTo(b.Slot);
        }
    }
}
