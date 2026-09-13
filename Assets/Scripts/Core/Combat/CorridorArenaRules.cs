using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 回廊要塞の<b>戦術マップ</b>の地形（#40 C-7）。イゼルローン型の隘路を「両側が航行不能な岩壁に挟まれた
    /// 一本の水路＋その真ん中に居座る要塞」として表す。
    ///
    /// #40 の「迂回不可」は<b>この戦術マップの話</b>＝要塞の外周を回り込んで反対側へ抜けられないこと。
    /// 戦略銀河グラフのほうは別の通商回廊による迂回を<b>許す</b>（路線を消して銀河を一本道にはしない）。
    ///
    /// 幾何の要：要塞が塞ぐ半径が通路の半幅以上なら、壁と要塞のあいだに隙間が無い＝どう動いても
    /// 横をすり抜けられない（<see cref="LeavesNoGap"/>）。この不変条件をテストで固定する。
    /// </summary>
    public static class CorridorArenaRules
    {
        /// <summary>要塞の実体半径の既定（水路半幅に対する割合）。左右に艦が通れる隙間を必ず残す。</summary>
        public const float DefaultBodyRatio = 0.55f;
        /// <summary>要塞の実体半径の上限（同上）。これを超えると制圧後も通り抜けられなくなる。</summary>
        public const float MaxBodyRatio = 0.75f;

        /// <summary>回廊アリーナの寸法（すべてワールド単位・原点＝アリーナ中心）。</summary>
        public readonly struct CorridorArenaBounds
        {
            /// <summary>水路の半幅。|y| がこれを超えると岩壁＝航行不能。</summary>
            public readonly float channelHalfWidth;
            /// <summary>水路の半長。x の可動範囲は ±これ。</summary>
            public readonly float channelHalfLength;
            /// <summary>要塞の中心 x（守備側＝+x 側の手前に据える）。</summary>
            public readonly float fortressX;
            /// <summary>
            /// 要塞が<b>封鎖する</b>半径。守備が健在なあいだ、敵対する艦はこの手前で止まる。
            /// 水路の半幅以上にすることで「横に隙間が無い＝回り込めない」を作る（<see cref="LeavesNoGap"/>）。
            /// </summary>
            public readonly float fortressRadius;

            /// <summary>
            /// 要塞の<b>実体（船体）</b>の半径。艦がめり込めない物理的な大きさで、封鎖半径より小さい。
            ///
            /// 封鎖半径は水路を覆い切る大きさなので、それを physical な障害物として使い続けると
            /// <b>要塞を制圧したあとも誰も通り抜けられない</b>（占領が永久に成立しない）。
            /// 封鎖が解けたあとは、この小さい実体だけを避けて左右から追い越せるようにする。
            /// </summary>
            public readonly float fortressBodyRadius;
            /// <summary>ここまで到達したら突破成立（守備側の出口）。</summary>
            public readonly float breakthroughX;

            /// <param name="fortressBodyRadius">
            /// 要塞の実体の半径。0以下なら水路半幅の <see cref="DefaultBodyRatio"/> 倍を使う
            /// ＝必ず半幅より小さくなり、制圧後は左右から追い越せる。
            /// </param>
            public CorridorArenaBounds(float channelHalfWidth, float channelHalfLength,
                                       float fortressX, float fortressRadius, float breakthroughX,
                                       float fortressBodyRadius = 0f)
            {
                this.channelHalfWidth = Mathf.Max(0.01f, channelHalfWidth);
                this.channelHalfLength = Mathf.Max(0.01f, channelHalfLength);
                this.fortressX = fortressX;
                this.fortressRadius = Mathf.Max(0f, fortressRadius);
                this.breakthroughX = breakthroughX;
                // 実体は必ず水路の半幅より小さくする（そうでないと制圧後も通り抜けられない）。
                float body = fortressBodyRadius > 0f ? fortressBodyRadius : this.channelHalfWidth * DefaultBodyRatio;
                this.fortressBodyRadius = Mathf.Clamp(body, 0f, this.channelHalfWidth * MaxBodyRatio);
            }

            /// <summary>
            /// 既定＝半幅18・半長60・要塞は +18 に半径20・突破線は +52。
            /// 要塞半径20 ≥ 半幅18 なので、要塞が健在なかぎり横に隙間が無い（<see cref="LeavesNoGap"/>=true）。
            /// </summary>
            public static CorridorArenaBounds Default => new CorridorArenaBounds(18f, 60f, 18f, 20f, 52f);
        }

        /// <summary>
        /// 要塞と岩壁のあいだに隙間が無いか＝<b>外周を回り込めない</b>。
        /// 要塞が塞ぐ半径が水路の半幅以上なら、要塞の断面が水路を覆い切るので横をすり抜ける経路が存在しない。
        /// これが false なら要塞を無視して通り抜けられる＝#40 の前提が崩れている（寸法の設定ミス）。
        /// </summary>
        public static bool LeavesNoGap(in CorridorArenaBounds b) => b.fortressRadius >= b.channelHalfWidth;

        /// <summary>
        /// 要塞を回り込むのに必要な横方向の距離（要塞の断面を外れるための |y|）。
        /// これが水路の半幅より大きければ岩壁にぶつかる＝迂回不能。
        /// </summary>
        public static float RequiredBypassOffset(in CorridorArenaBounds b) => b.fortressRadius;

        /// <summary>
        /// 位置を回廊の地形へ収める。岩壁（|y| ≤ 半幅）と水路の長さ（|x| ≤ 半長）で必ず挟み、
        /// さらに要塞の円内からは押し出す。<paramref name="blockedForThisShip"/> が true（＝要塞に敵対する側で
        /// 要塞が健在）なら、要塞の手前（x ≤ fortressX − 半径）より先へは進ませない＝通り抜け不能。
        /// 要塞が落ちれば blocked=false になり、同じ艦がその場から先へ進める。
        /// </summary>
        public static Vector2 Confine(Vector2 pos, in CorridorArenaBounds b, bool blockedForThisShip)
        {
            float x = Mathf.Clamp(pos.x, -b.channelHalfLength, b.channelHalfLength);
            float y = Mathf.Clamp(pos.y, -b.channelHalfWidth, b.channelHalfWidth); // 岩壁＝航行不能

            if (blockedForThisShip)
            {
                // 要塞の手前で止める。横へ逃げても壁があるので回り込めない（LeavesNoGap の幾何）。
                float stand = b.fortressX - b.fortressRadius;
                if (x > stand) x = stand;
            }

            // 要塞の<b>実体</b>（船体の円）からは味方でも押し出す＝要塞にめり込まない。
            // ここで使うのは封鎖半径ではなく実体半径。封鎖半径は水路を覆い切る大きさなので、
            // それを障害物として使い続けると制圧後も誰も通り抜けられず占領が成立しない。
            Vector2 d = new Vector2(x - b.fortressX, y);
            float dist = d.magnitude;
            if (dist < b.fortressBodyRadius)
            {
                if (dist <= 0.0001f) d = new Vector2(-1f, 0f); else d /= dist;
                Vector2 pushed = new Vector2(b.fortressX, 0f) + d * b.fortressBodyRadius;
                x = Mathf.Clamp(pushed.x, -b.channelHalfLength, b.channelHalfLength);
                y = Mathf.Clamp(pushed.y, -b.channelHalfWidth, b.channelHalfWidth);
            }
            return new Vector2(x, y);
        }

        /// <summary>突破が成立したか＝守備側の出口（breakthroughX）まで到達した。</summary>
        public static bool IsBreakthrough(Vector2 pos, in CorridorArenaBounds b) => pos.x >= b.breakthroughX;

        /// <summary>
        /// 要塞が健在なかぎり突破線へ到達できないか（#40 の中核の不変条件）。
        /// 隙間が無く、かつ要塞の手前の停止線が突破線より手前にあるなら、封鎖側にとって突破は不可能。
        /// </summary>
        public static bool BreakthroughImpossibleWhileHeld(in CorridorArenaBounds b)
            => LeavesNoGap(b) && (b.fortressX - b.fortressRadius) < b.breakthroughX;
    }
}
