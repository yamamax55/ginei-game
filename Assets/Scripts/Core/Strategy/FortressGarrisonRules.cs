using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 要塞へ駐留できない理由。<see cref="なし"/>＝駐留させてよい。
    /// <see cref="FleetOrderRules"/>（移動命令）と同じ作法＝理由を列挙で返し
    /// <see cref="FortressGarrisonRules.RejectionText"/> で日本語1行へ変換する。
    /// </summary>
    public enum GarrisonRejection
    {
        なし,
        艦隊が無い,
        要塞が無い,
        他勢力の艦隊,
        敵の要塞,
        回廊上にいる,
        交戦中,
        増援航行中,
        すでに駐留中,
        他の要塞に駐留中,
        要塞から遠い,
        満員,
    }

    /// <summary>要塞の駐留艦隊の調整値。</summary>
    public readonly struct FortressGarrisonParams
    {
        /// <summary>1つの要塞に駐留できる艦隊数の上限（無制限リストを作らない＝スケーラビリティ規律）。</summary>
        public readonly int maxFleets;

        /// <summary>友軍（＝敵対しない他勢力）の要塞にも駐留できるか。false＝自勢力の要塞のみ。</summary>
        public readonly bool allowFriendly;

        /// <summary>
        /// 占領時に、新しい所有者に敵対する駐留艦隊を駐留名簿から外すか。
        /// true でも<b>艦隊そのものは消さず・勢力も変えない</b>（外すだけ）。
        /// </summary>
        public readonly bool releaseHostileOnCapture;

        public FortressGarrisonParams(int maxFleets, bool allowFriendly, bool releaseHostileOnCapture)
        {
            this.maxFleets = Mathf.Max(1, maxFleets);
            this.allowFriendly = allowFriendly;
            this.releaseHostileOnCapture = releaseHostileOnCapture;
        }

        /// <summary>既定＝1要塞に4艦隊まで・友軍の要塞にも駐留可・占領で敵の駐留は名簿から外す。</summary>
        public static FortressGarrisonParams Default => new FortressGarrisonParams(4, true, true);
    }

    /// <summary>
    /// 回廊要塞の<b>駐留艦隊</b>の純ロジック（#40 系）。
    ///
    /// <b>要塞施設と駐留艦隊を分ける</b>のが本モジュールの主旨。
    /// <list type="bullet">
    ///   <item><see cref="Fortress.garrisonStrength"/>＝要塞<b>施設</b>そのものの守備値（砲台・要塞兵・旧セーブの守備力）。
    ///   封鎖判定（<see cref="FortressRules.BlocksPassage"/>）や力攻め（<see cref="StrategyRules.AssaultFortress"/>）は
    ///   従来どおりこれを見る＝<b>ここでは触らない</b>。</item>
    ///   <item><see cref="Fortress.garrisonFleetIds"/>＝盤面に<b>実在する戦略艦隊</b>の名簿。ID・艦艇数
    ///   （<see cref="StrategicFleet.Ships"/>）・指揮官・所属は艦隊側に在り、要塞側はIDを参照するだけ
    ///   ＝匿名の守備艦隊を毎戦闘で生成しない・占領で敵艦隊を味方へ複製しない。</item>
    /// </list>
    ///
    /// <b>二重計上しない</b>：名簿はID集合として扱い（同じIDを二度入れない）、盤面全体では
    /// <see cref="FindGarrison(GalaxyMap,int)"/> が「1艦隊は高々1つの要塞にしか駐留しない」を保証する
    /// （別要塞に駐留中なら <see cref="GarrisonRejection.他の要塞に駐留中"/> で拒否）。星系側の一覧は
    /// <see cref="ExcludeGarrisoned"/> で駐留艦を除いてから数える。
    ///
    /// <b>駐留中は動かない</b>：駐留できるのは星系に停泊している艦隊だけ（回廊上は拒否）で、駐留中は
    /// その星系に留まる。移動命令の側は <see cref="IsGarrisoned(GalaxyMap,StrategicFleet)"/> を見て
    /// 「出撃してから動く」（<see cref="Sortie(Fortress,StrategicFleet)"/>）ようにする＝航路移動と競合させない。
    ///
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FortressGarrisonRules
    {
        // ===== 名簿へのアクセス（null 安全） =====

        /// <summary>駐留名簿を取り出す（null なら生成して要塞へ差し戻す＝旧セーブ対策）。</summary>
        private static List<int> Ids(Fortress f)
        {
            if (f == null) return null;
            if (f.garrisonFleetIds == null) f.garrisonFleetIds = new List<int>();
            return f.garrisonFleetIds;
        }

        /// <summary>駐留艦隊のID一覧（読み取り専用・null 要塞は空）。</summary>
        public static IReadOnlyList<int> GarrisonFleetIds(Fortress f)
        {
            List<int> ids = Ids(f);
            return ids ?? EmptyIds;
        }

        private static readonly List<int> EmptyIds = new List<int>();

        /// <summary>駐留している艦隊の数（部隊数）。</summary>
        public static int GarrisonFleetCount(Fortress f)
        {
            List<int> ids = Ids(f);
            return ids == null ? 0 : ids.Count;
        }

        /// <summary>この要塞に指定IDの艦隊が駐留しているか。</summary>
        public static bool IsGarrisonedIn(Fortress f, int fleetId)
        {
            List<int> ids = Ids(f);
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++) if (ids[i] == fleetId) return true;
            return false;
        }

        /// <summary><inheritdoc cref="IsGarrisonedIn(Fortress,int)"/></summary>
        public static bool IsGarrisonedIn(Fortress f, StrategicFleet fleet)
            => fleet != null && IsGarrisonedIn(f, fleet.id);

        /// <summary>実在の艦隊が駐留しているか（＝旧セーブの守備値だけの要塞と区別する）。</summary>
        public static bool HasFleetGarrison(Fortress f) => GarrisonFleetCount(f) > 0;

        // ===== 可否判定 =====

        /// <summary><inheritdoc cref="CanGarrison(GalaxyMap,Fortress,StrategicFleet,Faction,FortressGarrisonParams)"/></summary>
        public static GarrisonRejection CanGarrison(Fortress f, StrategicFleet fleet, Faction player)
            => CanGarrison(null, f, fleet, player, FortressGarrisonParams.Default);

        /// <summary><inheritdoc cref="CanGarrison(GalaxyMap,Fortress,StrategicFleet,Faction,FortressGarrisonParams)"/></summary>
        public static GarrisonRejection CanGarrison(Fortress f, StrategicFleet fleet, Faction player, FortressGarrisonParams p)
            => CanGarrison(null, f, fleet, player, p);

        /// <summary><inheritdoc cref="CanGarrison(GalaxyMap,Fortress,StrategicFleet,Faction,FortressGarrisonParams)"/></summary>
        public static GarrisonRejection CanGarrison(GalaxyMap map, Fortress f, StrategicFleet fleet, Faction player)
            => CanGarrison(map, f, fleet, player, FortressGarrisonParams.Default);

        /// <summary>
        /// この艦隊をこの要塞へ駐留させられるか。<paramref name="player"/> は操作している勢力。
        ///
        /// <paramref name="map"/> を渡すと盤面まで見る＝「別の要塞に駐留していないか」
        /// （<see cref="GarrisonRejection.他の要塞に駐留中"/>）と「要塞のある回廊の端に停泊しているか」
        /// （<see cref="GarrisonRejection.要塞から遠い"/>）も判定する。null なら要塞と艦隊だけで判定する。
        ///
        /// 敵対判定は <see cref="FactionRelations.IsHostile(FactionData,Faction,FactionData,Faction)"/> へ委譲＝
        /// ここで「勢力が違えば敵」と直書きしない（外交状態＝<see cref="DiplomacyRules"/> が効く）。
        /// </summary>
        public static GarrisonRejection CanGarrison(GalaxyMap map, Fortress f, StrategicFleet fleet,
                                                    Faction player, FortressGarrisonParams p)
        {
            if (fleet == null) return GarrisonRejection.艦隊が無い;
            if (f == null) return GarrisonRejection.要塞が無い;

            // 他勢力の駒は動かせない（操作勢力の艦隊だけ）。
            if (fleet.faction != player) return GarrisonRejection.他勢力の艦隊;

            // 自軍／友軍の要塞だけ＝敵対する要塞へは入れない。友軍不可設定なら自勢力の要塞のみ。
            if (FactionRelations.IsHostile(null, fleet.faction, null, f.owner)) return GarrisonRejection.敵の要塞;
            if (!p.allowFriendly && fleet.faction != f.owner) return GarrisonRejection.敵の要塞;

            // 盤面の進行から外れている／固着している艦隊は駐留させられない。
            if (fleet.warpingAsReinforcement) return GarrisonRejection.増援航行中;
            if (fleet.engaged) return GarrisonRejection.交戦中;
            if (fleet.IsOnCorridor) return GarrisonRejection.回廊上にいる; // 停泊中の艦隊だけが入港できる

            if (IsGarrisonedIn(f, fleet.id)) return GarrisonRejection.すでに駐留中;

            if (map != null)
            {
                // 1艦隊は高々1つの要塞にしか駐留しない（二重計上の防止）。
                if (FindGarrison(map, fleet.id) != null) return GarrisonRejection.他の要塞に駐留中;

                // 要塞のある回廊の端に停泊していること（遠くの星系からは入港できない）。
                Corridor c = FindCorridorOf(map, f);
                if (c != null && !c.Connects(fleet.currentSystemId)) return GarrisonRejection.要塞から遠い;
            }

            if (GarrisonFleetCount(f) >= p.maxFleets) return GarrisonRejection.満員;

            return GarrisonRejection.なし;
        }

        /// <summary>理由の日本語1行（UI にそのまま出す）。<see cref="GarrisonRejection.なし"/>＝空文字。</summary>
        public static string RejectionText(GarrisonRejection reason)
        {
            switch (reason)
            {
                case GarrisonRejection.なし: return "";
                case GarrisonRejection.艦隊が無い: return "艦隊が選択されていません";
                case GarrisonRejection.要塞が無い: return "そこに要塞がありません";
                case GarrisonRejection.他勢力の艦隊: return "自軍の艦隊ではないため命令できません";
                case GarrisonRejection.敵の要塞: return "自軍・友軍の要塞ではないため駐留できません";
                case GarrisonRejection.回廊上にいる: return "航行中のため駐留できません（星系に停泊してから）";
                case GarrisonRejection.交戦中: return "交戦中のため駐留できません";
                case GarrisonRejection.増援航行中: return "増援として航行中のため駐留できません";
                case GarrisonRejection.すでに駐留中: return "すでにこの要塞に駐留しています";
                case GarrisonRejection.他の要塞に駐留中: return "別の要塞に駐留中です（先に出撃させてください）";
                case GarrisonRejection.要塞から遠い: return "要塞の回廊に接する星系に停泊していません";
                case GarrisonRejection.満員: return "この要塞の受け入れ上限に達しています";
                default: return "駐留できません";
            }
        }

        /// <summary>
        /// 理由の<b>短縮形</b>（一覧の狭い列に収めるための8文字以内）。全桁を出せない列で長文を省略記号で
        /// 落とすと理由が読めなくなるため（実機QAで艦艇数が読めなかったのと同じ失敗）、行にはこちらを出し、
        /// 発令結果の1行には <see cref="RejectionText"/> の全文を出す。
        /// </summary>
        public static string ShortRejectionText(GarrisonRejection reason)
        {
            switch (reason)
            {
                case GarrisonRejection.なし: return "";
                case GarrisonRejection.艦隊が無い: return "艦隊未選択";
                case GarrisonRejection.要塞が無い: return "要塞なし";
                case GarrisonRejection.他勢力の艦隊: return "自軍でない";
                case GarrisonRejection.敵の要塞: return "敵の要塞";
                case GarrisonRejection.回廊上にいる: return "航行中";
                case GarrisonRejection.交戦中: return "交戦中";
                case GarrisonRejection.増援航行中: return "増援航行中";
                case GarrisonRejection.すでに駐留中: return "駐留済み";
                case GarrisonRejection.他の要塞に駐留中: return "別要塞に駐留";
                case GarrisonRejection.要塞から遠い: return "回廊外";
                case GarrisonRejection.満員: return "満員";
                default: return "不可";
            }
        }

        // ===== 駐留させる／出撃させる =====

        /// <summary><inheritdoc cref="Garrison(GalaxyMap,Fortress,StrategicFleet,Faction,FortressGarrisonParams,out GarrisonRejection)"/></summary>
        public static bool Garrison(Fortress f, StrategicFleet fleet, Faction player)
            => Garrison(null, f, fleet, player, FortressGarrisonParams.Default, out _);

        /// <summary><inheritdoc cref="Garrison(GalaxyMap,Fortress,StrategicFleet,Faction,FortressGarrisonParams,out GarrisonRejection)"/></summary>
        public static bool Garrison(Fortress f, StrategicFleet fleet, Faction player, out GarrisonRejection reason)
            => Garrison(null, f, fleet, player, FortressGarrisonParams.Default, out reason);

        /// <summary><inheritdoc cref="Garrison(GalaxyMap,Fortress,StrategicFleet,Faction,FortressGarrisonParams,out GarrisonRejection)"/></summary>
        public static bool Garrison(GalaxyMap map, Fortress f, StrategicFleet fleet, Faction player, out GarrisonRejection reason)
            => Garrison(map, f, fleet, player, FortressGarrisonParams.Default, out reason);

        /// <summary>
        /// 艦隊を要塞へ駐留させる。<see cref="CanGarrison(GalaxyMap,Fortress,StrategicFleet,Faction,FortressGarrisonParams)"/>
        /// が <see cref="GarrisonRejection.なし"/> のときだけ名簿へ加える（同じIDは二度入らない）。
        ///
        /// 艦隊側は<b>何も書き換えない</b>＝勢力・艦艇数・指揮官はそのまま、停泊している星系にそのまま留まる。
        /// 「駐留中は動かない」は移動命令側が <see cref="IsGarrisoned(GalaxyMap,StrategicFleet)"/> を見て担保する。
        /// </summary>
        public static bool Garrison(GalaxyMap map, Fortress f, StrategicFleet fleet, Faction player,
                                    FortressGarrisonParams p, out GarrisonRejection reason)
        {
            reason = CanGarrison(map, f, fleet, player, p);
            if (reason != GarrisonRejection.なし) return false;
            Ids(f).Add(fleet.id);
            return true;
        }

        /// <summary>駐留している艦隊を出撃（離脱）させる＝名簿から外す。居なければ false。</summary>
        public static bool Sortie(Fortress f, int fleetId)
        {
            List<int> ids = Ids(f);
            if (ids == null) return false;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != fleetId) continue;
                ids.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary><inheritdoc cref="Sortie(Fortress,int)"/></summary>
        public static bool Sortie(Fortress f, StrategicFleet fleet)
            => fleet != null && Sortie(f, fleet.id);

        /// <summary>
        /// 盤面上のどの要塞に駐留していても出撃させる（移動命令の直前に呼ぶ想定）。
        /// 出撃させた要塞を <paramref name="from"/> で返す。駐留していなければ false。
        /// </summary>
        public static bool SortieFrom(GalaxyMap map, StrategicFleet fleet, out Fortress from)
        {
            from = null;
            if (map == null || fleet == null) return false;
            from = FindGarrison(map, fleet.id);
            if (from == null) return false;
            return Sortie(from, fleet.id);
        }

        /// <summary><inheritdoc cref="SortieFrom(GalaxyMap,StrategicFleet,out Fortress)"/></summary>
        public static bool SortieFrom(GalaxyMap map, StrategicFleet fleet) => SortieFrom(map, fleet, out _);

        // ===== 照会（どこに駐留しているか） =====

        /// <summary>この要塞が据えられている回廊を探す（見つからなければ null）。</summary>
        public static Corridor FindCorridorOf(GalaxyMap map, Fortress f)
        {
            if (map == null || f == null || map.corridors == null) return null;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c != null && ReferenceEquals(c.fortress, f)) return c;
            }
            return null;
        }

        /// <summary>盤面上でこの艦隊IDが駐留している要塞（駐留していなければ null）。</summary>
        public static Fortress FindGarrison(GalaxyMap map, int fleetId) => FindGarrison(map, fleetId, out _);

        /// <summary><inheritdoc cref="FindGarrison(GalaxyMap,int)"/> 据えられている回廊も返す。</summary>
        public static Fortress FindGarrison(GalaxyMap map, int fleetId, out Corridor corridor)
        {
            corridor = null;
            if (map == null || map.corridors == null) return null;
            for (int i = 0; i < map.corridors.Count; i++)
            {
                Corridor c = map.corridors[i];
                if (c == null || c.fortress == null) continue;
                if (!IsGarrisonedIn(c.fortress, fleetId)) continue;
                corridor = c;
                return c.fortress;
            }
            return null;
        }

        /// <summary>この艦隊が（盤面上のどこかの要塞に）駐留中か。駐留中は通常の航路移動をさせない。</summary>
        public static bool IsGarrisoned(GalaxyMap map, StrategicFleet fleet)
            => fleet != null && FindGarrison(map, fleet.id) != null;

        /// <summary><inheritdoc cref="IsGarrisoned(GalaxyMap,StrategicFleet)"/></summary>
        public static bool IsGarrisoned(GalaxyMap map, int fleetId) => FindGarrison(map, fleetId) != null;

        /// <summary>
        /// 星系の艦隊一覧から<b>要塞に駐留中の艦隊を除く</b>（二重計上の防止）。
        /// 駐留艦は星系の停泊艦としては数えない＝戦略MAPのバッジ・戦力集計はこれを通してから数える。
        /// 元のリストは変更せず新しいリストを返す（null は空リスト）。
        /// </summary>
        public static List<StrategicFleet> ExcludeGarrisoned(GalaxyMap map, List<StrategicFleet> fleets)
        {
            var result = new List<StrategicFleet>();
            if (fleets == null) return result;
            for (int i = 0; i < fleets.Count; i++)
            {
                StrategicFleet fl = fleets[i];
                if (fl == null) continue;
                if (IsGarrisoned(map, fl)) continue;
                result.Add(fl);
            }
            return result;
        }

        // ===== 集計（表示・要塞戦への引き渡し） =====

        /// <summary>
        /// 駐留している艦隊の実体を解決して並べる（解決できないIDは飛ばす）。
        /// <paramref name="resolve"/> は艦隊ID→艦隊（<see cref="StrategicFleetRegistry.GetFleet"/> 等）。
        /// </summary>
        public static List<StrategicFleet> GarrisonFleets(Fortress f, System.Func<int, StrategicFleet> resolve)
        {
            var result = new List<StrategicFleet>();
            List<int> ids = Ids(f);
            if (ids == null || resolve == null) return result;
            for (int i = 0; i < ids.Count; i++)
            {
                StrategicFleet fl = resolve(ids[i]);
                if (fl != null) result.Add(fl);
            }
            return result;
        }

        /// <summary><inheritdoc cref="GarrisonFleets(Fortress,System.Func{int,StrategicFleet})"/></summary>
        public static List<StrategicFleet> GarrisonFleets(Fortress f, StrategicFleetRegistry registry)
            => GarrisonFleets(f, registry == null ? (System.Func<int, StrategicFleet>)null : registry.GetFleet);

        /// <summary>
        /// 駐留艦隊の<b>合計艦艇数（隻）</b>＝各艦隊の <see cref="StrategicFleet.Ships"/> の和（表示用）。
        /// <see cref="Fortress.garrisonStrength"/>（施設の守備値）は<b>混ぜない</b>＝別物として別々に見せる。
        /// </summary>
        public static int TotalGarrisonShips(Fortress f, System.Func<int, StrategicFleet> resolve)
        {
            List<int> ids = Ids(f);
            if (ids == null || resolve == null) return 0;
            long total = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                StrategicFleet fl = resolve(ids[i]);
                if (fl == null) continue;
                total += Mathf.Max(0, fl.Ships);
            }
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary><inheritdoc cref="TotalGarrisonShips(Fortress,System.Func{int,StrategicFleet})"/></summary>
        public static int TotalGarrisonShips(Fortress f, StrategicFleetRegistry registry)
            => TotalGarrisonShips(f, registry == null ? (System.Func<int, StrategicFleet>)null : registry.GetFleet);

        /// <summary>
        /// 駐留艦隊の合計兵力（抽象兵力 <see cref="StrategicFleet.strength"/> の和）。要塞戦の守備側戦力として渡す用。
        /// こちらも <see cref="Fortress.garrisonStrength"/> とは別物＝加算しない。
        /// </summary>
        public static int TotalGarrisonStrength(Fortress f, System.Func<int, StrategicFleet> resolve)
        {
            List<int> ids = Ids(f);
            if (ids == null || resolve == null) return 0;
            long total = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                StrategicFleet fl = resolve(ids[i]);
                if (fl == null) continue;
                total += Mathf.Max(0, fl.strength);
            }
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }

        /// <summary><inheritdoc cref="TotalGarrisonStrength(Fortress,System.Func{int,StrategicFleet})"/></summary>
        public static int TotalGarrisonStrength(Fortress f, StrategicFleetRegistry registry)
            => TotalGarrisonStrength(f, registry == null ? (System.Func<int, StrategicFleet>)null : registry.GetFleet);

        // ===== 保守（消えた艦隊の掃除） =====

        /// <summary>
        /// 解決できなくなった（撃滅・盤面から除去された）艦隊IDを名簿から取り除く。取り除いた数を返す。
        /// <paramref name="resolve"/> が null なら何もしない（0）＝解決手段が無いのに名簿を空にしない。
        /// </summary>
        public static int PruneMissing(Fortress f, System.Func<int, StrategicFleet> resolve)
        {
            List<int> ids = Ids(f);
            if (ids == null || resolve == null) return 0;
            int removed = 0;
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                if (resolve(ids[i]) != null) continue;
                ids.RemoveAt(i);
                removed++;
            }
            return removed;
        }

        // ===== 占領時の扱い =====

        /// <summary>駐留名簿を空にし、外した艦隊IDを返す（艦隊そのものは消さない・勢力も変えない）。</summary>
        public static List<int> ClearGarrison(Fortress f)
        {
            var released = new List<int>();
            List<int> ids = Ids(f);
            if (ids == null) return released;
            for (int i = 0; i < ids.Count; i++) released.Add(ids[i]);
            ids.Clear();
            return released;
        }

        /// <summary><inheritdoc cref="OnCaptured(Fortress,Faction,System.Func{int,StrategicFleet},FortressGarrisonParams)"/></summary>
        public static List<int> OnCaptured(Fortress f, Faction newOwner, System.Func<int, StrategicFleet> resolve)
            => OnCaptured(f, newOwner, resolve, FortressGarrisonParams.Default);

        /// <summary>
        /// 要塞が <paramref name="newOwner"/> に落とされたときの駐留名簿の始末。外した艦隊IDを返す。
        ///
        /// <b>敵の駐留艦隊を味方へ変換・複製しない</b>のが要点＝新しい所有者に敵対する艦隊は名簿から
        /// <b>外すだけ</b>で、<see cref="StrategicFleet.faction"/> も艦艇数も一切書き換えない
        /// （その艦隊が撤退したのか壊滅したのかは <see cref="StrategyRules.AssaultFortress"/> 等
        /// 会戦解決の側が決める）。新所有者に敵対しない艦隊（＝元から友軍）はそのまま残る。
        ///
        /// <paramref name="resolve"/> が null＝勢力を判定できないときは<b>全員外す</b>
        /// （敵艦隊が新所有者の駐留として居座るより安全側に倒す）。
        /// 所有者の書き換え（<see cref="Fortress.owner"/>）と守備値の更新はここでは行わない
        /// （<see cref="StrategyRules.AssaultFortress"/>／<see cref="FortressBlockadeRules.Regarrison"/> が担う）。
        /// </summary>
        public static List<int> OnCaptured(Fortress f, Faction newOwner, System.Func<int, StrategicFleet> resolve,
                                           FortressGarrisonParams p)
        {
            var released = new List<int>();
            List<int> ids = Ids(f);
            if (ids == null) return released;
            if (!p.releaseHostileOnCapture) return released;   // 名簿をそのまま残す設定

            for (int i = ids.Count - 1; i >= 0; i--)
            {
                int id = ids[i];
                StrategicFleet fl = resolve != null ? resolve(id) : null;
                // 解決できない＝素性が分からない艦隊も外す（安全側）。
                bool hostile = fl == null || FactionRelations.IsHostile(null, fl.faction, null, newOwner);
                if (!hostile) continue;
                ids.RemoveAt(i);
                released.Add(id);
            }
            released.Reverse();   // 名簿の並び順で返す（決定論）
            return released;
        }

        // ===== 旧セーブ互換（施設の守備値と駐留艦隊は別勘定） =====

        /// <summary>
        /// 要塞<b>施設</b>の守備値（<see cref="Fortress.garrisonStrength"/>）。
        /// 駐留艦隊とは別勘定＝表示でも足し合わせない。封鎖・力攻めの判定は従来どおりこちらを使う。
        /// </summary>
        public static float FacilityGarrisonStrength(Fortress f) => f == null ? 0f : Mathf.Max(0f, f.garrisonStrength);

        /// <summary>
        /// 旧セーブ由来の要塞か＝施設の守備値だけがあり、駐留艦隊の名簿が空。
        /// この場合も<b>守備値を艦隊へ変換しない</b>（勝手に艦隊を生やさない）。施設の値として残り、
        /// 封鎖（<see cref="FortressRules.BlocksPassage"/>）は従来どおり効く＝初期守備戦力が黙って消えない。
        /// </summary>
        public static bool IsLegacyGarrisonOnly(Fortress f)
            => f != null && f.garrisonStrength > 0f && GarrisonFleetCount(f) == 0;

        /// <summary>
        /// 要塞詳細の守備表示1行。施設の守備値と駐留艦隊を<b>並べて</b>出す（足し合わせない）。
        /// 例：「守備力 1000　駐留 2部隊 8,000隻」／駐留なしなら「守備力 1000　駐留なし」。
        /// </summary>
        public static string GarrisonSummaryText(Fortress f, System.Func<int, StrategicFleet> resolve)
        {
            if (f == null) return "";
            int facility = Mathf.RoundToInt(FacilityGarrisonStrength(f));
            int count = GarrisonFleetCount(f);
            if (count == 0) return $"守備力 {facility}　駐留なし";
            // 桁区切りはロケールに依らせない（表示を決定論に保つ）。
            string ships = TotalGarrisonShips(f, resolve).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            return $"守備力 {facility}　駐留 {count}部隊 {ships}隻";
        }

        /// <summary>
        /// 駐留だけの<b>短縮表示</b>（一覧の狭い列用）。例：「2隊 8,000隻」／駐留なしなら「駐留なし」。
        /// 施設の守備値は含めない＝<see cref="GarrisonSummaryText"/> と足し合わせて読ませない。
        /// </summary>
        public static string GarrisonCompactText(Fortress f, System.Func<int, StrategicFleet> resolve)
        {
            if (f == null) return "";
            int count = GarrisonFleetCount(f);
            if (count == 0) return "駐留なし";
            string ships = TotalGarrisonShips(f, resolve).ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
            return $"{count}隊 {ships}隻";
        }
    }
}
