using System.Collections.Generic;

namespace Ginei
{
    /// <summary>軍団への配属／解除ができない理由。<see cref="なし"/>＝操作してよい。</summary>
    public enum CorpsAssignmentRejection
    {
        なし,
        艦隊が無い,
        他勢力の艦隊,
        軍団が無い,
        他勢力の軍団,
        すでにその軍団,
        軍団に属していない,
        交戦中,
        増援航行中,
        軍団旗艦は解除できない,
    }

    /// <summary>
    /// 戦略艦隊の<b>軍団への配属・解除</b>（#軍団編成メニュー）。
    ///
    /// <b>台帳を増やさない</b>のが本モジュールの主旨。戦略側の軍団は
    /// <see cref="StrategicFleet.corpsId"/>／<see cref="StrategicFleet.corpsName"/> が持つ<b>唯一の所属情報</b>で、
    /// ここはその2つを検査つきで書き換えるだけ。並行する所属レジストリを新設しない
    /// （＝戦略・戦術・セーブが同じ所属を見る）。
    ///
    /// <b>二重所属は構造的に起きない</b>：所属は艦隊が1つだけ持つフィールドなので、配属すれば前の軍団からは
    /// 自動的に外れる。他勢力の軍団へは入れない（<see cref="CorpsAssignmentRejection.他勢力の軍団"/>）。
    ///
    /// <b>進行中の行動を勝手に止めない</b>：交戦中・増援航行中の艦隊は編成を変えられない（理由を返すだけで、
    /// 戦闘や航行には触れない）。移動中の艦隊は編成を変えてよい＝所属が変わっても航路はそのまま。
    ///
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CorpsAssignmentRules
    {
        /// <summary>軍団の identity（同じ軍団かを見るキーと、表示名・勢力）。</summary>
        public readonly struct CorpsInfo
        {
            public readonly int corpsId;
            public readonly string corpsName;
            public readonly Faction faction;
            /// <summary>この軍団に属する艦隊数。</summary>
            public readonly int fleetCount;
            /// <summary>この軍団の指揮を担う艦隊のID（<see cref="StrategicFleet.isCorpsFlagship"/>）。無ければ -1。</summary>
            public readonly int flagshipFleetId;

            public CorpsInfo(int corpsId, string corpsName, Faction faction, int fleetCount, int flagshipFleetId)
            {
                this.corpsId = corpsId;
                this.corpsName = corpsName ?? "";
                this.faction = faction;
                this.fleetCount = fleetCount;
                this.flagshipFleetId = flagshipFleetId;
            }

            public bool IsValid => corpsId >= 0;
        }

        // ===== 可否 =====

        /// <summary>
        /// その艦隊を <paramref name="corpsId"/> の軍団へ配属できるか。
        /// <paramref name="corpsFaction"/> は配属先の軍団の勢力（艦隊と一致しなければならない）。
        /// </summary>
        public static CorpsAssignmentRejection CanAssign(StrategicFleet fleet, Faction player,
                                                        int corpsId, Faction corpsFaction)
        {
            CorpsAssignmentRejection basic = CanEdit(fleet, player);
            if (basic != CorpsAssignmentRejection.なし) return basic;

            if (corpsId < 0) return CorpsAssignmentRejection.軍団が無い;
            if (corpsFaction != fleet.faction) return CorpsAssignmentRejection.他勢力の軍団;
            if (fleet.corpsId == corpsId) return CorpsAssignmentRejection.すでにその軍団;

            return CorpsAssignmentRejection.なし;
        }

        /// <summary>その艦隊を軍団から外せるか。</summary>
        public static CorpsAssignmentRejection CanUnassign(StrategicFleet fleet, Faction player)
        {
            CorpsAssignmentRejection basic = CanEdit(fleet, player);
            if (basic != CorpsAssignmentRejection.なし) return basic;

            if (!fleet.HasCorps) return CorpsAssignmentRejection.軍団に属していない;
            // 軍団旗艦＝軍団の指揮を担う艦隊。これを黙って抜くと軍団が指揮官不在になるので、
            // 先に別の艦隊へ指揮を移す（＝旗艦を付け替える）ことを求める。
            if (fleet.isCorpsFlagship) return CorpsAssignmentRejection.軍団旗艦は解除できない;

            return CorpsAssignmentRejection.なし;
        }

        /// <summary>編成を触ってよい状態か（勢力・進行中の行動）。配属/解除に共通の前段。</summary>
        private static CorpsAssignmentRejection CanEdit(StrategicFleet fleet, Faction player)
        {
            if (fleet == null) return CorpsAssignmentRejection.艦隊が無い;
            if (fleet.faction != player) return CorpsAssignmentRejection.他勢力の艦隊;
            // 進行中の行動は中断しない＝編成の変更だけを断る。
            if (fleet.engaged) return CorpsAssignmentRejection.交戦中;
            if (fleet.warpingAsReinforcement) return CorpsAssignmentRejection.増援航行中;
            return CorpsAssignmentRejection.なし;
        }

        /// <summary>理由の日本語1行（UI にそのまま出す）。<see cref="CorpsAssignmentRejection.なし"/>＝空文字。</summary>
        public static string RejectionText(CorpsAssignmentRejection reason)
        {
            switch (reason)
            {
                case CorpsAssignmentRejection.なし: return "";
                case CorpsAssignmentRejection.艦隊が無い: return "艦隊が選択されていません";
                case CorpsAssignmentRejection.他勢力の艦隊: return "自軍の艦隊ではないため編成できません";
                case CorpsAssignmentRejection.軍団が無い: return "配属先の軍団が選ばれていません";
                case CorpsAssignmentRejection.他勢力の軍団: return "他勢力の軍団へは配属できません";
                case CorpsAssignmentRejection.すでにその軍団: return "すでにその軍団に所属しています";
                case CorpsAssignmentRejection.軍団に属していない: return "どの軍団にも所属していません";
                case CorpsAssignmentRejection.交戦中: return "交戦中のため編成を変えられません";
                case CorpsAssignmentRejection.増援航行中: return "増援として航行中のため編成を変えられません";
                case CorpsAssignmentRejection.軍団旗艦は解除できない:
                    return "軍団の指揮を担う艦隊です（先に別の艦隊へ指揮を移してください）";
                default: return "編成を変えられません";
            }
        }

        // ===== 実行 =====

        /// <summary>
        /// 軍団へ配属する。所属は艦隊が1つだけ持つので、<b>前の軍団からは自動的に外れる</b>（二重所属なし）。
        /// 軍団旗艦だった艦隊を別の軍団へ移すと、その旗艦フラグは降りる（元の軍団の指揮を持ったまま移らない）。
        /// 艦艇数・司令官・航路・所在地は<b>一切書き換えない</b>。
        /// </summary>
        public static bool Assign(StrategicFleet fleet, Faction player, in CorpsInfo corps,
                                  out CorpsAssignmentRejection reason)
        {
            reason = CanAssign(fleet, player, corps.corpsId, corps.faction);
            if (reason != CorpsAssignmentRejection.なし) return false;

            fleet.isCorpsFlagship = false;     // 別の軍団の指揮は持ち込まない
            fleet.corpsId = corps.corpsId;
            fleet.corpsName = corps.corpsName;
            return true;
        }

        /// <summary>軍団から外す（独立艦隊にする）。艦艇数・司令官・航路・所在地は書き換えない。</summary>
        public static bool Unassign(StrategicFleet fleet, Faction player, out CorpsAssignmentRejection reason)
        {
            reason = CanUnassign(fleet, player);
            if (reason != CorpsAssignmentRejection.なし) return false;

            fleet.corpsId = -1;
            fleet.corpsName = null;
            fleet.isCorpsFlagship = false;
            return true;
        }

        /// <summary>
        /// その軍団の指揮を <paramref name="fleet"/> へ移す（軍団旗艦の付け替え）。
        /// 同じ軍団の他の艦隊からは旗艦フラグを降ろす＝<b>1軍団に指揮艦隊は1つ</b>。
        /// </summary>
        public static bool SetCorpsFlagship(IReadOnlyList<StrategicFleet> fleets, StrategicFleet fleet,
                                            Faction player, out CorpsAssignmentRejection reason)
        {
            reason = CanEdit(fleet, player);
            if (reason != CorpsAssignmentRejection.なし) return false;
            if (fleet == null || !fleet.HasCorps) { reason = CorpsAssignmentRejection.軍団に属していない; return false; }

            if (fleets != null)
            {
                for (int i = 0; i < fleets.Count; i++)
                {
                    StrategicFleet f = fleets[i];
                    if (f == null || f == fleet) continue;
                    if (f.faction == fleet.faction && f.corpsId == fleet.corpsId) f.isCorpsFlagship = false;
                }
            }
            fleet.isCorpsFlagship = true;
            return true;
        }

        // ===== 照会（軍団編成メニューの一覧） =====

        /// <summary>
        /// その勢力の軍団を、<b>盤面の艦隊から</b>数え上げる（並行台帳を作らない）。
        /// 並びは軍団ID の昇順＝毎回同じ順（決定論）。
        /// </summary>
        public static List<CorpsInfo> CorpsOf(IReadOnlyList<StrategicFleet> fleets, Faction faction)
        {
            var result = new List<CorpsInfo>();
            if (fleets == null) return result;

            var ids = new List<int>();
            var names = new Dictionary<int, string>();
            var counts = new Dictionary<int, int>();
            var flagships = new Dictionary<int, int>();

            for (int i = 0; i < fleets.Count; i++)
            {
                StrategicFleet f = fleets[i];
                if (f == null || f.faction != faction || !f.HasCorps) continue;

                if (!counts.ContainsKey(f.corpsId))
                {
                    ids.Add(f.corpsId);
                    counts[f.corpsId] = 0;
                    flagships[f.corpsId] = -1;
                    names[f.corpsId] = "";
                }
                counts[f.corpsId]++;
                if (!string.IsNullOrEmpty(f.corpsName)) names[f.corpsId] = f.corpsName;
                if (f.isCorpsFlagship) flagships[f.corpsId] = f.id;
            }

            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                int id = ids[i];
                result.Add(new CorpsInfo(id, names[id], faction, counts[id], flagships[id]));
            }
            return result;
        }

        /// <summary>その軍団に属する艦隊（艦隊ID順・決定論）。</summary>
        public static List<StrategicFleet> FleetsIn(IReadOnlyList<StrategicFleet> fleets, Faction faction, int corpsId)
        {
            var result = new List<StrategicFleet>();
            if (fleets == null || corpsId < 0) return result;
            for (int i = 0; i < fleets.Count; i++)
            {
                StrategicFleet f = fleets[i];
                if (f != null && f.faction == faction && f.corpsId == corpsId) result.Add(f);
            }
            result.Sort((a, b) => a.id.CompareTo(b.id));
            return result;
        }

        /// <summary>どの軍団にも属していない自軍艦隊（独立艦隊・艦隊ID順）。一覧から漏らさないために使う。</summary>
        public static List<StrategicFleet> Unassigned(IReadOnlyList<StrategicFleet> fleets, Faction faction)
        {
            var result = new List<StrategicFleet>();
            if (fleets == null) return result;
            for (int i = 0; i < fleets.Count; i++)
            {
                StrategicFleet f = fleets[i];
                if (f != null && f.faction == faction && !f.HasCorps) result.Add(f);
            }
            result.Sort((a, b) => a.id.CompareTo(b.id));
            return result;
        }

        /// <summary>
        /// 新しい軍団に使える ID（既存の最大＋1）。盤面の艦隊から出す＝採番簿を別に持たない。
        /// 勢力をまたいで一意にする（軍団の同一性は ID と勢力の組で見るが、番号が衝突すると読みにくい）。
        /// </summary>
        public static int NextCorpsId(IReadOnlyList<StrategicFleet> fleets)
        {
            int max = -1;
            if (fleets != null)
                for (int i = 0; i < fleets.Count; i++)
                    if (fleets[i] != null && fleets[i].corpsId > max) max = fleets[i].corpsId;
            return max + 1;
        }
    }
}
