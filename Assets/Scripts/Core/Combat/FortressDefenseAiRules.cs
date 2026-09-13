using UnityEngine;

namespace Ginei
{
    /// <summary>守備艦隊の役割（#40 回廊要塞戦の守備側AI）。全員で突撃せず持ち場を分ける。</summary>
    public enum FortressDefenseRole
    {
        /// <summary>迎撃＝要塞の門前に横一線で張り、封鎖を割って抜けてきた敵を叩く。</summary>
        迎撃,
        /// <summary>直衛＝要塞に張り付いて離れない。最低1隊は必ずここに残る（要塞を空にしない）。</summary>
        直衛
    }

    /// <summary>
    /// 回廊要塞の守備側AIの調整値（#40）。すべて要塞と水路の寸法に対する比／余白で持ち、
    /// 絶対座標は持たない＝アリーナの寸法を変えても守備の配置が自動で追従する。
    /// ※既存の <see cref="FortressDefenseParams"/>（要塞そのものの防御力モデル＝<see cref="FortressDefenseRules"/>）
    /// とは別物ゆえ Ai を付けて名前を分ける（同名だと衝突する）。
    /// </summary>
    public readonly struct FortressDefenseAiParams
    {
        /// <summary>要塞の砲火が届く半径（＝守備が離れてよい上限）。0 で支援判定を無効（要塞喪失後）。</summary>
        public readonly float supportRadius;
        /// <summary>迎撃線を要塞の実体（封鎖円）から離す距離。</summary>
        public readonly float interceptMargin;
        /// <summary>直衛を要塞の実体から離す距離（迎撃線より内側＝要塞寄りに丸める）。</summary>
        public readonly float guardMargin;
        /// <summary>迎撃線を水路の半幅に対しどれだけ広げるか（0..1）。突破ルートを塞ぐ横の張り。</summary>
        public readonly float spreadRatio;
        /// <summary>直衛の横の張り＝迎撃線の張りに対する割合（0..1・要塞の直近に固まる）。</summary>
        public readonly float guardSpanRatio;
        /// <summary>直衛に回す隊の割合（0..1）。端数は丸め、最低1隊・最大でも全体−1隊。</summary>
        public readonly float guardRatio;
        /// <summary>迎撃の出撃/追撃半径＝要塞の封鎖半径×これ（0..1）。1未満＝封鎖円の内側に踏み込んだ敵だけ迎え撃つ。</summary>
        public readonly float interceptEngageRatio;
        /// <summary>直衛の出撃/追撃半径＝要塞の封鎖半径×これ（0..interceptEngageRatio）。迎撃より必ず短い。</summary>
        public readonly float guardEngageRatio;
        /// <summary>持ち場からこの距離ぶんの余裕を超えて離れたら復帰する（追撃半径に加算）。</summary>
        public readonly float returnSlack;
        /// <summary>追撃半径の下限（支援範囲の上限に食われても最低限は動ける）。</summary>
        public readonly float minPursuitRadius;
        /// <summary>持ち場を突破線の手前で止める余白（守備が自分の出口の外へ出ない）。</summary>
        public readonly float exitStandoff;

        public FortressDefenseAiParams(float supportRadius, float interceptMargin, float guardMargin,
            float spreadRatio, float guardSpanRatio, float guardRatio,
            float interceptEngageRatio, float guardEngageRatio,
            float returnSlack, float minPursuitRadius, float exitStandoff)
        {
            this.supportRadius = Mathf.Max(0f, supportRadius);
            this.interceptMargin = Mathf.Max(0.1f, interceptMargin);
            // 直衛は必ず迎撃線より要塞寄り（逆転させない）。
            this.guardMargin = Mathf.Clamp(guardMargin, 0.1f, this.interceptMargin);
            this.spreadRatio = Mathf.Clamp01(spreadRatio);
            this.guardSpanRatio = Mathf.Clamp01(guardSpanRatio);
            this.guardRatio = Mathf.Clamp01(guardRatio);
            this.interceptEngageRatio = Mathf.Clamp01(interceptEngageRatio);
            // 直衛の出撃半径は迎撃を超えない（直衛が一番遠くまで出て行く事態を作らない）。
            this.guardEngageRatio = Mathf.Clamp(guardEngageRatio, 0f, this.interceptEngageRatio);
            this.returnSlack = Mathf.Max(0f, returnSlack);
            this.minPursuitRadius = Mathf.Max(0f, minPursuitRadius);
            this.exitStandoff = Mathf.Max(0f, exitStandoff);
        }

        /// <summary>
        /// 既定＝支援48（要塞主砲 <see cref="FortressMainCannon"/> の既定 maxRange 相当）／迎撃線は封鎖円＋6／
        /// 直衛は＋3／横の張りは水路半幅の0.7（直衛はその0.35）／直衛は約1/3の隊／
        /// 迎撃の出撃半径＝封鎖半径×0.9・直衛×0.5／復帰余裕6／追撃下限4／出口の手前4。
        /// </summary>
        public static FortressDefenseAiParams Default =>
            new FortressDefenseAiParams(48f, 6f, 3f, 0.7f, 0.35f, 0.34f, 0.9f, 0.5f, 6f, 4f, 4f);
    }

    /// <summary>
    /// 回廊要塞戦（#40）の<b>守備側AI</b>の純ロジック（static・test-first）。「要塞を活用した守備」＝
    /// 要塞の砲火に支えられる位置で戦い、突破ルートを塞ぎ、深追いして要塞を空にしない、を数値で決める。
    ///
    /// 地形の寸法は <see cref="CorridorArenaRules.CorridorArenaBounds"/> をそのまま受け取る
    /// （幾何の出所を二重に持たない）。座標系も同じ＝<b>アリーナ中心が原点</b>のローカル座標で、
    /// Game 層（<see cref="FortressDefenseDirector"/>）がアリーナ位置を足してワールドへ写す。
    ///
    /// 分担：
    /// - 地形そのもの（岩壁・封鎖・突破判定）＝<see cref="CorridorArenaRules"/>。本ルールはその中の<b>居場所</b>を決めるだけで、
    ///   迂回不能の幾何（<see cref="CorridorArenaRules.LeavesNoGap"/>）には一切手を触れない。
    /// - 持ち場から離れない拘束の実行＝<see cref="FleetAI"/> の既存の持ち場（corpsAnchor/corpsLeashRange）と
    ///   スロット（corpsSlotLocal）機構へ配線する（AI の操舵を作り直さない）。
    /// 盤面非依存・plain 引数（float/Vector2）・乱数なし・実効値パターン。
    /// </summary>
    public static class FortressDefenseAiRules
    {
        // ── 要塞の支援範囲 ──

        /// <summary>
        /// 要塞の支援（砲火）半径。近接の砲台群（封鎖半径＋実効射程）と主砲の射程の大きいほうを採る。
        /// 守備艦隊はこの円から出ない＝「要塞に支えられて戦う」の数値表現。
        /// </summary>
        public static float SupportRadius(float fortressRadius, float closeSupportRange, float mainCannonRange)
        {
            float close = Mathf.Max(0f, fortressRadius) + Mathf.Max(0f, closeSupportRange);
            return Mathf.Max(close, Mathf.Max(0f, mainCannonRange));
        }

        // ── 守備側の向き（水路のどちら側が守備側か） ──

        /// <summary>守備側の向き（+1＝突破線が要塞より +x 側／−1＝その逆）。持ち場は必ずこちら側に置く。</summary>
        public static float DefenseSide(in CorridorArenaRules.CorridorArenaBounds b)
            => b.breakthroughX >= b.fortressX ? 1f : -1f;

        /// <summary>
        /// 守備の正面（度・+Y 基準＝<see cref="FleetAI.corpsFacingDeg"/> と同一規約）。要塞の門（攻撃側）を向く。
        /// </summary>
        public static float DefenseFacingDeg(in CorridorArenaRules.CorridorArenaBounds b)
            => DefenseSide(b) > 0f ? 90f : -90f;

        // ── 役割の配分（全員で突撃しない・要塞を空にしない） ──

        /// <summary>
        /// 直衛（要塞に張り付く隊）の数。要塞が健在なら<b>必ず1隊以上</b>残し（要塞を空にしない）、
        /// 2隊以上なら<b>必ず1隊以上を迎撃へ回す</b>（全員が要塞に貼り付いて動かない事態も作らない）。
        /// 要塞が落ちたら0＝直衛の縛りを解く。
        /// </summary>
        public static int GuardCount(int defenderCount, float guardRatio, bool fortressHolds)
        {
            if (defenderCount <= 0) return 0;
            if (!fortressHolds) return 0;      // 守るべき要塞が無い＝縛りを解く
            if (defenderCount == 1) return 1;  // 最後の1隊は要塞から離さない
            int g = Mathf.RoundToInt(defenderCount * Mathf.Clamp01(guardRatio));
            return Mathf.Clamp(g, 1, defenderCount - 1);
        }

        /// <summary>迎撃（門前で迎え撃つ隊）の数＝総数−直衛。</summary>
        public static int InterceptCount(int defenderCount, float guardRatio, bool fortressHolds)
            => Mathf.Max(0, defenderCount) - GuardCount(defenderCount, guardRatio, fortressHolds);

        /// <summary>
        /// index 番目の隊の役割（決定論＝並び順だけで決まる）。前寄りの index が迎撃、後ろが直衛。
        /// 要塞が落ちていれば全隊が迎撃（直衛の縛りが解ける）。
        /// </summary>
        public static FortressDefenseRole RoleFor(int index, int defenderCount, float guardRatio, bool fortressHolds)
        {
            int count = Mathf.Max(1, defenderCount);
            int idx = Mathf.Clamp(index, 0, count - 1);
            int intercepts = InterceptCount(count, guardRatio, fortressHolds);
            return idx < intercepts ? FortressDefenseRole.迎撃 : FortressDefenseRole.直衛;
        }

        // ── 持ち場（要塞の門前／要塞直衛） ──

        /// <summary>持ち場の要塞中心からの距離（＝封鎖円の外＋役割ごとの余白）。要塞にめり込ませない。</summary>
        public static float StationDistance(FortressDefenseRole role,
            in CorridorArenaRules.CorridorArenaBounds b, in FortressDefenseAiParams p)
            => b.fortressRadius + (role == FortressDefenseRole.直衛 ? p.guardMargin : p.interceptMargin);

        /// <summary>
        /// 持ち場の x（役割ごとの縦位置）＝要塞の守備側の門前。<b>自分の出口（突破線）は越えない</b>
        /// ＝守備が突破線の外へ出て背後を空けることが無い。ただし出口が要塞に近すぎる歪んだ寸法では
        /// 地形（要塞にめり込まない）を優先し、封鎖円の縁まで戻す。最後に水路の長さでクランプする。
        /// </summary>
        public static float StationLineX(FortressDefenseRole role,
            in CorridorArenaRules.CorridorArenaBounds b, in FortressDefenseAiParams p)
        {
            float side = DefenseSide(b);
            float x = b.fortressX + side * StationDistance(role, b, p); // 要塞にめり込まない門前
            float exit = b.breakthroughX - side * p.exitStandoff;       // 出口の手前
            float skirt = b.fortressX + side * b.fortressRadius;        // 封鎖円の縁（これ以上は寄れない）
            if (side > 0f && x > exit) x = Mathf.Max(exit, skirt);
            else if (side < 0f && x < exit) x = Mathf.Min(exit, skirt);
            return Mathf.Clamp(x, -b.channelHalfLength, b.channelHalfLength);
        }

        /// <summary>横一線に等間隔で散らす（1隊なら中央）。突破ルート＝水路の横断面を塞ぐための配分。</summary>
        public static float LateralOffset(int index, int count, float halfSpan)
        {
            if (count <= 1) return 0f;
            int idx = Mathf.Clamp(index, 0, count - 1);
            float t = idx / (float)(count - 1);
            return Mathf.Lerp(-halfSpan, halfSpan, t);
        }

        /// <summary>
        /// index 番目の隊の持ち場（アリーナ局所座標）。迎撃は門前の横一線（水路を塞ぐ）、直衛は要塞直近に固まる。
        /// 要塞が落ちていれば全隊が1本の迎撃線に並ぶ（＝突破ルートの最終スクリーン）。
        /// </summary>
        public static Vector2 Station(int index, int defenderCount,
            in CorridorArenaRules.CorridorArenaBounds b, in FortressDefenseAiParams p, bool fortressHolds)
        {
            int count = Mathf.Max(1, defenderCount);
            int idx = Mathf.Clamp(index, 0, count - 1);
            int intercepts = InterceptCount(count, p.guardRatio, fortressHolds);
            FortressDefenseRole role = RoleFor(idx, count, p.guardRatio, fortressHolds);

            float x = StationLineX(role, b, p);
            float span = b.channelHalfWidth * p.spreadRatio;
            float y;
            if (role == FortressDefenseRole.迎撃)
            {
                y = LateralOffset(idx, intercepts, span);
            }
            else
            {
                int guards = count - intercepts;
                y = LateralOffset(idx - intercepts, guards, span * p.guardSpanRatio);
            }
            y = Mathf.Clamp(y, -b.channelHalfWidth, b.channelHalfWidth);
            return new Vector2(x, y);
        }

        /// <summary>
        /// 持ち場を「要塞を原点・守備正面（+Y）を基準」にした局所座標へ写す
        /// （<see cref="FleetAI.corpsSlotLocal"/> の規約＝<see cref="DefenseFacingDeg"/> で回すと元へ戻る）。
        /// </summary>
        public static Vector2 StationLocal(Vector2 station, in CorridorArenaRules.CorridorArenaBounds b)
            => Rotate(station - new Vector2(b.fortressX, 0f), -DefenseFacingDeg(b));

        /// <summary>ベクトルを Z 角(度・+Y 基準)で回す（<see cref="FleetAI"/>／CorpsFormation と同一規約）。</summary>
        public static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // ── 追撃の上限と復帰 ──

        /// <summary>
        /// 持ち場からの追撃上限（＝出撃の引き金にもなる半径）。
        /// 要塞が健在なら「封鎖半径×役割の比」＝<b>封鎖円を割って入ってきた敵だけ迎え撃つ</b>（外で睨み合う敵は要塞に任せる）。
        /// さらに<b>持ち場距離＋追撃 ≤ 支援半径</b>になるよう必ず切り詰める＝要塞の砲火の外へは出ない。
        /// 要塞喪失後は「要塞跡〜突破線」の距離まで＝縛りは解くが突破ルートからは離れない。
        /// </summary>
        public static float PursuitRadius(FortressDefenseRole role,
            in CorridorArenaRules.CorridorArenaBounds b, in FortressDefenseAiParams p, bool fortressHolds)
        {
            if (!fortressHolds)
                return Mathf.Max(p.minPursuitRadius, Mathf.Abs(b.breakthroughX - b.fortressX));

            float ratio = role == FortressDefenseRole.直衛 ? p.guardEngageRatio : p.interceptEngageRatio;
            float raw = b.fortressRadius * ratio;
            float cap = Mathf.Max(0f, p.supportRadius - StationDistance(role, b, p)); // 支援範囲から出さない
            float r = Mathf.Min(raw, cap);
            if (r < p.minPursuitRadius) r = Mathf.Min(p.minPursuitRadius, cap);       // 下限も支援範囲を超えない
            return Mathf.Max(0f, r);
        }

        /// <summary>持ち場に就いた隊が要塞中心から離れうる最大距離（＝持ち場距離＋追撃上限）。支援半径を超えない。</summary>
        public static float MaxDistanceFromFortress(FortressDefenseRole role,
            in CorridorArenaRules.CorridorArenaBounds b, in FortressDefenseAiParams p, bool fortressHolds)
            => StationDistance(role, b, p) + PursuitRadius(role, b, p, fortressHolds);

        /// <summary>
        /// 持ち場へ復帰すべきか。①持ち場から追撃上限＋余裕を超えて離れた（深追い）、
        /// ②要塞の支援範囲の外へ出た（<paramref name="supportRadius"/>≤0 なら支援判定なし＝要塞喪失後）。
        /// どちらかで true＝交戦より優先して戻す。
        /// </summary>
        public static bool ShouldReturnToStation(Vector2 pos, Vector2 station, Vector2 fortressCenter,
            float pursuitRadius, float supportRadius, float returnSlack)
        {
            float limit = Mathf.Max(0f, pursuitRadius) + Mathf.Max(0f, returnSlack);
            if ((pos - station).sqrMagnitude > limit * limit) return true;

            float sup = Mathf.Max(0f, supportRadius);
            if (sup > 0f && (pos - fortressCenter).sqrMagnitude > sup * sup) return true;
            return false;
        }

        /// <summary>移動目標を持ち場からの追撃上限の内側へ丸める（深追いさせない）。上限内ならそのまま。</summary>
        public static Vector2 ClampPursuit(Vector2 desired, Vector2 station, float pursuitRadius)
        {
            float r = Mathf.Max(0f, pursuitRadius);
            Vector2 off = desired - station;
            float d = off.magnitude;
            if (d <= r || d < 1e-5f) return desired;
            return station + off / d * r;
        }

        // ── 地形との整合（AI が迂回不能の幾何を崩さないことの担保） ──

        /// <summary>
        /// 持ち場が地形として成立しているか＝岩壁の内側・水路の長さの内側・要塞の封鎖円の外。
        /// 守備AIが要塞にめり込む位置や壁の外を指さない＝<see cref="CorridorArenaRules.Confine"/> と喧嘩しない。
        /// </summary>
        public static bool IsStationValid(Vector2 station, in CorridorArenaRules.CorridorArenaBounds b)
        {
            const float eps = 1e-4f;
            if (Mathf.Abs(station.y) > b.channelHalfWidth + eps) return false;
            if (Mathf.Abs(station.x) > b.channelHalfLength + eps) return false;
            return (station - new Vector2(b.fortressX, 0f)).magnitude >= b.fortressRadius - eps;
        }

        /// <summary>持ち場が守備側（突破線のある側）に在るか＝守備が攻撃側の入口へ回り込まない（迂回不能を崩さない）。</summary>
        public static bool IsOnDefenderSide(Vector2 station, in CorridorArenaRules.CorridorArenaBounds b)
        {
            float side = DefenseSide(b);
            return (station.x - b.fortressX) * side >= -1e-4f;
        }
    }
}
