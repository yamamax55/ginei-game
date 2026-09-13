using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦術ミニマップの写像（純ロジック・test-first）。
    ///
    /// <b>なぜ Core にあるか</b>：ミニマップの不具合は「原点がずれる」「縦横が独立に伸びる」
    /// 「視界枠が中心しか合わない」の3つで、いずれも<b>座標計算そのもの</b>。
    /// Play を起こさないと確かめられない場所に置くと、直したつもりが直っていない。
    ///
    /// <b>座標系の約束</b>
    /// <list type="bullet">
    ///   <item>ワールドは<b>その会戦の原点を含んだ絶対座標</b>（ウィンドウ化会戦は戦場ごとに原点がずれる）。
    ///   呼び手は艦もカメラも戦場矩形も<b>同じ原点系</b>で渡すこと＝片方だけ原点なしにするのが従来の不具合。</item>
    ///   <item>ミニマップ側は<b>中心が原点</b>のローカル px（右が +x・上が +y）。</item>
    /// </list>
    /// </summary>
    public static class MinimapProjectionRules
    {
        /// <summary>ミニマップ内で実際に絵を描く領域（アスペクトを保つためのレターボックス）。</summary>
        public readonly struct Fit
        {
            /// <summary>描画領域の幅（px）。</summary>
            public readonly float width;
            /// <summary>描画領域の高さ（px）。</summary>
            public readonly float height;

            public Fit(float width, float height)
            {
                this.width = Mathf.Max(0.0001f, width);
                this.height = Mathf.Max(0.0001f, height);
            }
        }

        /// <summary>
        /// 戦場の縦横比を保ったまま、パネルへ内接する描画領域を出す（＝戦場の形が歪まない）。
        /// 戦場が横長ならパネルの上下が余り、縦長なら左右が余る。
        /// 縮退（幅か高さが 0）でも 0 除算しないようクランプする。
        /// </summary>
        public static Fit FitPreserveAspect(Vector2 worldMin, Vector2 worldMax, float panelWidth, float panelHeight)
        {
            float spanX = Mathf.Max(0.0001f, worldMax.x - worldMin.x);
            float spanY = Mathf.Max(0.0001f, worldMax.y - worldMin.y);
            float pw = Mathf.Max(0.0001f, panelWidth);
            float ph = Mathf.Max(0.0001f, panelHeight);

            // パネルに収まる最大の倍率（px / world）。
            float scale = Mathf.Min(pw / spanX, ph / spanY);
            return new Fit(spanX * scale, spanY * scale);
        }

        /// <summary>1ワールド単位あたりのピクセル数（縦横で同じ＝アスペクト保持の帰結）。</summary>
        public static float PixelsPerWorld(Vector2 worldMin, Vector2 worldMax, in Fit fit)
        {
            float spanX = Mathf.Max(0.0001f, worldMax.x - worldMin.x);
            return fit.width / spanX;
        }

        /// <summary>
        /// ワールド座標 → ミニマップのローカル px（中心原点）。
        /// <b>クランプしない</b>＝戦場の外の点は外の位置に出る（呼び手が必要なら間引く）。
        /// 従来は Clamp01 していたため、原点がずれた座標がすべて一辺へ貼り付いていた。
        /// </summary>
        public static Vector2 WorldToLocal(Vector2 world, Vector2 worldMin, Vector2 worldMax, in Fit fit)
        {
            float spanX = Mathf.Max(0.0001f, worldMax.x - worldMin.x);
            float spanY = Mathf.Max(0.0001f, worldMax.y - worldMin.y);
            float u = (world.x - worldMin.x) / spanX;   // 0..1
            float v = (world.y - worldMin.y) / spanY;
            return new Vector2((u - 0.5f) * fit.width, (v - 0.5f) * fit.height);
        }

        /// <summary>
        /// ミニマップのローカル px（中心原点） → ワールド座標。<see cref="WorldToLocal"/> の逆。
        /// クリックした地点へカメラを飛ばすのに使う＝<b>往復して同じ点に戻る</b>ことをテストで固定する。
        /// </summary>
        public static Vector2 LocalToWorld(Vector2 local, Vector2 worldMin, Vector2 worldMax, in Fit fit)
        {
            float spanX = Mathf.Max(0.0001f, worldMax.x - worldMin.x);
            float spanY = Mathf.Max(0.0001f, worldMax.y - worldMin.y);
            float u = local.x / fit.width + 0.5f;
            float v = local.y / fit.height + 0.5f;
            return new Vector2(worldMin.x + u * spanX, worldMin.y + v * spanY);
        }

        /// <summary>その点が戦場矩形の中にあるか（外の艦を描かない判定に使う）。</summary>
        public static bool Contains(Vector2 world, Vector2 worldMin, Vector2 worldMax)
            => world.x >= worldMin.x && world.x <= worldMax.x
            && world.y >= worldMin.y && world.y <= worldMax.y;

        /// <summary>
        /// 視界枠＝<b>カメラの視界と戦場の交差</b>を返す（見えている範囲だけ）。
        /// 交差が無ければ false（枠を出さない）。
        ///
        /// 従来は中心だけを戦場内へ丸め、大きさはカメラの視界そのままだったため、
        /// 端に寄ると枠が戦場の外へはみ出し、引くと枠が全面を覆って動かなくなっていた。
        /// </summary>
        public static bool TryViewportIntersection(Vector2 camCenter, float halfWidth, float halfHeight,
                                                   Vector2 worldMin, Vector2 worldMax,
                                                   out Vector2 viewMin, out Vector2 viewMax)
        {
            float hw = Mathf.Max(0f, halfWidth);
            float hh = Mathf.Max(0f, halfHeight);

            float minX = Mathf.Max(worldMin.x, camCenter.x - hw);
            float maxX = Mathf.Min(worldMax.x, camCenter.x + hw);
            float minY = Mathf.Max(worldMin.y, camCenter.y - hh);
            float maxY = Mathf.Min(worldMax.y, camCenter.y + hh);

            viewMin = new Vector2(minX, minY);
            viewMax = new Vector2(maxX, maxY);
            return maxX > minX && maxY > minY;
        }

        /// <summary>
        /// 戦場矩形を原点ぶん平行移動する（ウィンドウ化会戦の遠方オフセットを足す1行窓口）。
        /// 「境界だけ原点なし・中身は原点あり」という食い違いを作らないために、必ずここを通す。
        /// </summary>
        public static void Offset(ref Vector2 worldMin, ref Vector2 worldMax, Vector2 origin)
        {
            worldMin += origin;
            worldMax += origin;
        }
    }
}
