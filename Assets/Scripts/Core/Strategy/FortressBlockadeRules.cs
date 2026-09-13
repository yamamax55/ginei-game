using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 回廊要塞＝固定拠点としての<b>通行の可否</b>を決める純ロジック（#40 C-7）。
    ///
    /// イゼルローン型（要塞あり＝<see cref="CorridorType.要衝"/>）は、健在な要塞に敵対する勢力の艦隊を
    /// 回廊の途中（<see cref="StandoffFraction"/>）で足止めし、<b>撃破/制圧するまで反対側へ抜けさせない</b>。
    /// フェザーン型（要塞なしの通商回廊）は誰でも素通りできる＝両者の差はこの窓口だけで表現する。
    ///
    /// 迂回不可は「グラフに裏エッジを持たせない」方針（<see cref="Corridor"/>）と地形壁で担保する。
    /// その担保が実際に効いているかを <see cref="HasBypass"/>／<see cref="IsCutEdge"/> で検査できるようにし、
    /// 「封鎖したつもりが隣の回廊で回り込める」設計事故を検出する。
    ///
    /// 封鎖の可否そのものは <see cref="StrategyRules.IsFortressBlocked"/>、要塞の攻防値は
    /// <see cref="FortressRules"/> が唯一の窓口＝ここでは<b>再実装せず委譲</b>し、グラフと移動の意味づけだけを持つ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FortressBlockadeRules
    {
        /// <summary>
        /// 封鎖された回廊で敵対艦隊が前進できる上限（回廊長に対する割合）。
        /// 0＝入口で止まる／1＝素通り。要塞の手前で足を止めさせつつ、盤面では「回廊上で対峙している」と
        /// 見えるように中ほどで止める。
        /// </summary>
        public const float StandoffFraction = 0.45f;

        /// <summary>要塞に守られた隘路か（イゼルローン型）＝要塞が据えられている回廊。</summary>
        public static bool IsFortifiedChoke(Corridor c) => c != null && c.fortress != null;

        /// <summary>
        /// 要塞のない通商回廊か（フェザーン型）＝固定拠点が無く、誰にとっても素通りできる回廊。
        /// 型が <see cref="CorridorType.要衝"/> でも要塞が無ければ（＝陥落後も含め）自由通行になる。
        /// </summary>
        public static bool IsCommerceCorridor(Corridor c) => c != null && c.fortress == null;

        /// <summary>この回廊が viewer にとって封鎖されているか（<see cref="StrategyRules.IsFortressBlocked"/> へ委譲）。</summary>
        public static bool Blocks(Corridor c, Faction viewer) => StrategyRules.IsFortressBlocked(c, viewer);

        /// <summary>fromId→toId の回廊が viewer にとって封鎖されているか。回廊が無ければ false。</summary>
        public static bool Blocks(GalaxyMap map, int fromId, int toId, Faction viewer)
        {
            if (map == null) return false;
            return Blocks(map.GetCorridor(fromId, toId), viewer);
        }

        /// <summary>
        /// viewer の艦隊がこの回廊上で進める上限割合。封鎖されていなければ 1（＝反対側まで通れる）、
        /// 封鎖されていれば <see cref="StandoffFraction"/>（＝要塞の手前で足止め）。
        /// </summary>
        public static float MaxAdvanceFraction(Corridor c, Faction viewer)
            => Blocks(c, viewer) ? StandoffFraction : 1f;

        /// <summary>
        /// 要塞を陥落させた側が守備を置き直す（＝再占領後にまた封鎖できるようにする）。
        /// <see cref="StrategyRules.AssaultFortress"/> は陥落時に守備0・<see cref="Fortress.controlsCorridor"/>=false
        /// にするので、そのままでは二度と封鎖できない。所有勢力が守備を入れ直したときだけ封鎖が復活する
        /// ＝「落とした側が守りを固めれば、今度は元の持ち主が通れなくなる」を表す。
        /// garrison が 0 以下なら守備を置かない（封鎖は復活しない）。
        /// </summary>
        public static void Regarrison(Fortress f, Faction owner, float garrison, float shieldIntegrity = 1f)
        {
            if (f == null) return;
            f.owner = owner;
            f.garrisonStrength = Mathf.Max(0f, garrison);
            f.shieldIntegrity = Mathf.Clamp01(shieldIntegrity);
            f.controlsCorridor = f.garrisonStrength > 0f;
        }

        /// <summary>
        /// この回廊を通らずに両端（aId↔bId）を行き来できる経路があるか＝<b>局所的な迂回路</b>の有無。
        /// viewer にとって封鎖された他の回廊も通れないものとして数える（要塞の裏に別の要塞があっても迂回にならない）。
        /// true なら「要塞を無視して回り込める」＝#40 の前提（迂回不可）が崩れている。
        /// </summary>
        public static bool HasBypass(GalaxyMap map, Corridor c, Faction viewer)
            => HasBypass(map, c, viewer, out _);

        /// <summary><inheritdoc cref="HasBypass(GalaxyMap,Corridor,Faction)"/> 見つかった迂回路も返す。</summary>
        public static bool HasBypass(GalaxyMap map, Corridor c, Faction viewer, out List<int> bypass)
        {
            bypass = null;
            if (map == null || c == null) return false;
            if (map.GetSystem(c.aId) == null || map.GetSystem(c.bId) == null) return false;

            // 幅優先で aId→bId を探す。ただし当該回廊そのものと、viewer に封鎖された回廊は使わない。
            var prev = new Dictionary<int, int>();
            var seen = new HashSet<int> { c.aId };
            var queue = new Queue<int>();
            queue.Enqueue(c.aId);

            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                List<int> neighbors = map.Neighbors(u);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int v = neighbors[i];
                    if (seen.Contains(v)) continue;
                    Corridor e = map.GetCorridor(u, v);
                    if (e == null || ReferenceEquals(e, c)) continue; // 当該回廊は使わない
                    if (Blocks(e, viewer)) continue;                  // 別の要塞も迂回路にはならない
                    seen.Add(v);
                    prev[v] = u;
                    if (v == c.bId)
                    {
                        bypass = Reconstruct(prev, c.aId, c.bId);
                        return true;
                    }
                    queue.Enqueue(v);
                }
            }
            return false;
        }

        /// <summary>
        /// この回廊が橋（cut edge）か＝取り除くと両端が<b>まったく</b>行き来できなくなる。
        /// 封鎖の有無に依らないグラフだけの性質＝「裏エッジが無い」ことの直接の検査。
        /// 要塞を据える回廊はこれが true であることが望ましい（そうでなければ封鎖しても意味が薄い）。
        /// </summary>
        public static bool IsCutEdge(GalaxyMap map, Corridor c)
        {
            if (map == null || c == null) return false;
            if (map.GetSystem(c.aId) == null || map.GetSystem(c.bId) == null) return false;

            var seen = new HashSet<int> { c.aId };
            var queue = new Queue<int>();
            queue.Enqueue(c.aId);
            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                List<int> neighbors = map.Neighbors(u);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int v = neighbors[i];
                    if (seen.Contains(v)) continue;
                    Corridor e = map.GetCorridor(u, v);
                    if (e == null || ReferenceEquals(e, c)) continue;
                    if (v == c.bId) return false; // 別ルートで到達できた＝橋ではない
                    seen.Add(v);
                    queue.Enqueue(v);
                }
            }
            return true;
        }

        /// <summary>
        /// マップ上の要塞回廊のうち、<b>局所的に迂回できてしまう</b>ものを列挙する（設計事故の検出）。
        /// 空なら「要塞は迂回不可」＝#40 の前提が成立している。
        /// </summary>
        public static List<Corridor> FindBypassableFortresses(GalaxyMap map, Faction viewer)
        {
            var result = new List<Corridor>();
            if (map == null || map.corridors == null) return result;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (!IsFortifiedChoke(c)) continue;
                if (!Blocks(c, viewer)) continue;      // そもそも封鎖していない要塞は対象外
                if (HasBypass(map, c, viewer)) result.Add(c);
            }
            return result;
        }

        private static List<int> Reconstruct(Dictionary<int, int> prev, int startId, int goalId)
        {
            var rev = new List<int> { goalId };
            int cur = goalId;
            while (cur != startId)
            {
                if (!prev.TryGetValue(cur, out int p)) return new List<int>();
                cur = p;
                rev.Add(cur);
            }
            rev.Reverse();
            return rev;
        }
    }
}
