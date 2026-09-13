using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>集約に渡す艦隊1隊ぶんの情報（表示に必要な分だけ・純データ）。</summary>
    public readonly struct FleetMarkerInput
    {
        public readonly int fleetId;
        public readonly Faction faction;
        public readonly Vector2 position;
        /// <summary>抽象兵力（内部の戦闘計算用。<b>プレイヤー向けの表示には使わない</b>）。</summary>
        public readonly int strength;
        /// <summary>艦艇数（隻）。<b>プレイヤーに見せる艦隊規模はこちら</b>。</summary>
        public readonly int ships;
        /// <summary>回廊上を航行中か（停泊中と混ぜない＝別のまとまりとして数える）。</summary>
        public readonly bool moving;
        public readonly bool selected;
        /// <summary>航行中の進行方向（停泊中は zero）。集約すると代表方向として平均を出す。</summary>
        public readonly Vector2 heading;

        public FleetMarkerInput(int fleetId, Faction faction, Vector2 position, int strength,
            bool moving, bool selected, Vector2 heading, int ships = 0)
        {
            this.fleetId = fleetId;
            this.faction = faction;
            this.position = position;
            this.strength = strength;
            this.ships = ships;
            this.moving = moving;
            this.selected = selected;
            this.heading = heading;
        }
    }

    /// <summary>集約された1つのマーカー（同陣営・同じ航行状態・近接した艦隊のまとまり）。</summary>
    public class FleetCluster
    {
        public Faction faction;
        public Vector2 center;
        public bool moving;
        public bool anySelected;
        public Vector2 heading;
        /// <summary>まとまりに含まれる艦隊数。</summary>
        public int FleetCount => fleetIds.Count;
        /// <summary>まとまりの総兵力（内部計算用・表示には使わない）。</summary>
        public int totalStrength;
        /// <summary>まとまりの総艦艇数（隻）。<b>表示はこちら</b>。</summary>
        public int totalShips;
        /// <summary>含まれる艦隊id（id 昇順）。クリックで開く一覧の並びに使う。</summary>
        public readonly List<int> fleetIds = new List<int>();

        /// <summary>単独（集約されていない）か。個別名を出してよい。</summary>
        public bool IsSingle => fleetIds.Count == 1;
    }

    /// <summary>
    /// 戦略マップの艦隊マーカー集約（#戦略MAPの艦艇表示）。同じ場所に多数の艦隊が居ると、
    /// 微小な駒とラベルが折り重なって何隻居るのかも誰の艦隊かも読めなくなる。ここでは
    /// <b>同陣営・同じ航行状態・近接</b>の艦隊を1つのマーカーへまとめ、「艦隊数」と「総兵力」を出す。
    ///
    /// 不変条件：①敵味方は絶対に混ぜない ②1つの艦隊はちょうど1つのまとまりに属する（欠落も二重計上もしない）
    /// ③停泊中と航行中は混ぜない（意味が違う）④同じ入力なら毎回同じ結果（id 昇順で決定論）。
    /// 純ロジック＝座標計算のみ。艦隊の状態や所属には触れない。
    /// </summary>
    public static class FleetClusterRules
    {
        /// <summary>既定の集約半径（ワールド単位）。星系の見た目より小さくして、別の星系のぶんを巻き込まない。</summary>
        public const float DefaultMergeRadius = 0.55f;

        /// <summary>
        /// 艦隊を集約する。<paramref name="outClusters"/> は呼び出し側の再利用バッファ（毎フレームのGCを避ける）。
        /// </summary>
        public static void Build(IList<FleetMarkerInput> fleets, float mergeRadius, List<FleetCluster> outClusters)
        {
            if (outClusters == null) return;
            outClusters.Clear();
            if (fleets == null || fleets.Count == 0) return;

            float r = Mathf.Max(0f, mergeRadius);
            float r2 = r * r;

            // id 昇順で処理＝入力の並びに依らず同じ結果になる（マーカーがちらつかない）。
            var ordered = new List<FleetMarkerInput>(fleets);
            ordered.Sort((a, b) => a.fleetId.CompareTo(b.fleetId));

            for (int i = 0; i < ordered.Count; i++)
            {
                FleetMarkerInput f = ordered[i];
                FleetCluster target = null;

                for (int c = 0; c < outClusters.Count; c++)
                {
                    FleetCluster cl = outClusters[c];
                    if (cl.faction != f.faction) continue;   // 敵味方は混ぜない
                    if (cl.moving != f.moving) continue;     // 停泊中と航行中は混ぜない
                    if ((cl.center - f.position).sqrMagnitude > r2) continue;
                    target = cl;
                    break;
                }

                if (target == null)
                {
                    target = new FleetCluster { faction = f.faction, center = f.position, moving = f.moving };
                    outClusters.Add(target);
                }
                else
                {
                    // 重心を更新（含めた艦隊の平均位置）。
                    int n = target.fleetIds.Count;
                    target.center = (target.center * n + f.position) / (n + 1);
                }

                target.fleetIds.Add(f.fleetId);
                target.totalStrength += Mathf.Max(0, f.strength);
                target.totalShips += Mathf.Max(0, f.ships);   // 表示に使うのはこちら（各艦隊の実隻数の合計）
                target.anySelected |= f.selected;
                if (f.moving) target.heading += f.heading;
            }

            // 代表方向を正規化（航行中のまとまりだけ意味を持つ）。
            for (int c = 0; c < outClusters.Count; c++)
            {
                FleetCluster cl = outClusters[c];
                if (cl.heading.sqrMagnitude > 1e-6f) cl.heading = cl.heading.normalized;
            }
        }

        /// <summary>まとまりの総数が入力の艦隊数と一致するか（欠落・二重計上の検証に使う）。</summary>
        public static int TotalFleets(IList<FleetCluster> clusters)
        {
            int n = 0;
            if (clusters == null) return 0;
            for (int i = 0; i < clusters.Count; i++) n += clusters[i].FleetCount;
            return n;
        }

        /// <summary>まとまりの総兵力（同上）。</summary>
        public static int TotalStrength(IList<FleetCluster> clusters)
        {
            int n = 0;
            if (clusters == null) return 0;
            for (int i = 0; i < clusters.Count; i++) n += clusters[i].totalStrength;
            return n;
        }

        /// <summary>
        /// マーカーに出す短い表示。単独なら艦隊数を出さず規模だけ＝画面の文字数を減らす。
        ///
        /// <b>プレイヤーに見せる艦隊規模は艦艇数（隻）だけ</b>にする（抽象兵力は内部の戦闘計算専用で、
        /// 画面には出さない）。数字は <see cref="FleetCluster.totalShips"/>＝各艦隊の実隻数の合計で、
        /// 兵力の数字に「隻」を付け替えたものではない。
        /// 特殊記号は使わない（フォントに無い字は豆腐になるため・実機報告）。
        /// </summary>
        public static string MarkerLabel(FleetCluster cluster)
        {
            if (cluster == null || cluster.FleetCount == 0) return "";
            if (cluster.IsSingle) return $"{cluster.totalShips:N0}隻";
            return $"{cluster.FleetCount}艦隊 {cluster.totalShips:N0}隻";
        }

        /// <summary>航行中のまとまりの方角（8方位・記号を使わず日本語で出す）。</summary>
        public static string HeadingLabel(Vector2 heading)
        {
            if (heading.sqrMagnitude < 1e-6f) return "";
            float deg = Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg; // -180..180（+x=0度）
            if (deg < 0f) deg += 360f;
            int idx = Mathf.RoundToInt(deg / 45f) % 8;
            switch (idx)
            {
                case 0: return "東";
                case 1: return "北東";
                case 2: return "北";
                case 3: return "北西";
                case 4: return "西";
                case 5: return "南西";
                case 6: return "南";
                default: return "南東";
            }
        }
    }
}
