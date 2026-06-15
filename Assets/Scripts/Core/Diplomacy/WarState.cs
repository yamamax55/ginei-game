using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>進行中の戦争の状態（DIPLO-3・#2119・純データ）。ターン数・損害・戦況（A優位+）。</summary>
    public class WarState
    {
        public string factionA = "";
        public string factionB = "";
        public int turnsAtWar;   // 開戦からのターン数
        public float casualties; // 損害 0..1（厭戦の素）
        public float warScore;   // 戦況 -1..1（+＝A優位／−＝A劣勢）

        public WarState() { }
        public WarState(string a, string b) { factionA = a; factionB = b; }
    }

    /// <summary>
    /// 進行中の戦争の台帳（DIPLO-3・#2119・static）。無向ペアで戦争状態を保持。Core 純ロジック・test-first。
    /// </summary>
    public static class WarLedger
    {
        static readonly List<WarState> wars = new List<WarState>();

        public static IReadOnlyList<WarState> All => wars;

        static bool Match(WarState w, string a, string b)
            => (w.factionA == a && w.factionB == b) || (w.factionA == b && w.factionB == a);

        public static WarState Get(string a, string b)
        {
            for (int i = 0; i < wars.Count; i++)
                if (Match(wars[i], a, b)) return wars[i];
            return null;
        }

        public static WarState GetOrCreate(string a, string b)
        {
            var w = Get(a, b);
            if (w == null) { w = new WarState(a, b); wars.Add(w); }
            return w;
        }

        public static bool Remove(string a, string b)
        {
            for (int i = 0; i < wars.Count; i++)
                if (Match(wars[i], a, b)) { wars.RemoveAt(i); return true; }
            return false;
        }

        public static void Clear() => wars.Clear();
    }

    /// <summary>
    /// 戦争状態の純ロジック（DIPLO-3・#2119）。ターン進行・厭戦・講和受諾度を `WarGoalRules`#192 に委譲して解く。test-first。
    /// </summary>
    public static class WarStateRules
    {
        /// <summary>戦争を進める（ターン加算）。</summary>
        public static void Tick(WarState w, int turns = 1)
        {
            if (w == null) return;
            w.turnsAtWar += Mathf.Max(0, turns);
        }

        /// <summary>厭戦（戦争疲れ）＝`WarGoalRules.WarWeariness`（ターン×率＋損害）。</summary>
        public static float Weariness(WarState w, WarGoalRules.WarGoalParams p)
            => w == null ? 0f : WarGoalRules.WarWeariness(w.turnsAtWar, w.casualties, p);

        /// <summary>
        /// 指定側の講和受諾度＝`WarGoalRules.PeaceAcceptance`。warScore は A優位なので、A 視点はそのまま、B 視点は符号反転。
        /// </summary>
        public static float PeaceAcceptanceFor(WarState w, bool forA, WarGoalRules.WarGoalParams p)
        {
            if (w == null) return 0f;
            float score = forA ? w.warScore : -w.warScore;
            return WarGoalRules.PeaceAcceptance(score, Weariness(w, p), p);
        }
    }
}
