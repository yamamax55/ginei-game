using UnityEngine;

namespace Ginei
{
    /// <summary>経路表示の調整値（ワールド単位）。</summary>
    public readonly struct RouteDisplayParams
    {
        /// <summary>矢じりの長さ（ワールド）。</summary>
        public readonly float arrowHeadLength;
        /// <summary>矢じりの半幅（ワールド）。</summary>
        public readonly float arrowHeadHalfWidth;
        /// <summary>ラベルを線から離す距離（ワールド）。</summary>
        public readonly float labelOffset;

        public RouteDisplayParams(float arrowHeadLength, float arrowHeadHalfWidth, float labelOffset)
        {
            this.arrowHeadLength = Mathf.Max(0f, arrowHeadLength);
            this.arrowHeadHalfWidth = Mathf.Max(0f, arrowHeadHalfWidth);
            this.labelOffset = Mathf.Max(0f, labelOffset);
        }

        /// <summary>既定＝矢じり長0.55・半幅0.28・ラベル離間0.35。</summary>
        public static RouteDisplayParams Default => new RouteDisplayParams(0.55f, 0.28f, 0.35f);
    }

    /// <summary>
    /// 戦略MAPに「どこからどこへ移動中か」を描くための純ロジック。座標は呼び手が渡す＝盤面非依存
    /// （非 MonoBehaviour・決定論・描画しない）。
    ///
    /// 移動命令の入口は艦隊メニュー（<see cref="FleetOrderRules"/>）へ集約し、MAP は表示専用になる。
    /// ここは表示側の幾何（ラベル位置・矢じり）と文字列（方角・経路1行）と同一性キーだけを持つ。
    /// 方角は <see cref="FleetClusterRules.HeadingLabel"/> へ<b>委譲</b>する＝8方位の判定を再実装しない。
    /// </summary>
    public static class FleetRouteDisplayRules
    {
        /// <summary>退化（from==to）とみなす距離の二乗。</summary>
        private const float DegenerateSqr = 1e-8f;

        /// <summary>
        /// 線分 from→to の中点。ラベル位置に使う（線から <see cref="RouteDisplayParams.labelOffset"/> だけ
        /// 法線方向〔進行方向の左手側〕へ逃がす＝線とラベルが重ならない）。from==to なら中点そのもの。
        /// </summary>
        public static Vector2 LabelPosition(Vector2 from, Vector2 to, in RouteDisplayParams p)
        {
            Vector2 mid = new Vector2((from.x + to.x) * 0.5f, (from.y + to.y) * 0.5f);
            Vector2 d = to - from;
            if (d.sqrMagnitude < DegenerateSqr) return mid; // 退化＝逃がす向きが決まらない
            Vector2 dir = d.normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);    // 進行方向の左手側
            return mid + normal * p.labelOffset;
        }

        /// <summary>
        /// 矢じり（三角形）の3頂点。<paramref name="to"/> を先端に、from→to の向きで <paramref name="to"/> の手前に置く。
        /// from==to の退化ケースでは3頂点とも <paramref name="to"/>（＝面積0＝描いても見えない・例外は出さない）。
        /// </summary>
        public static void ArrowHead(Vector2 from, Vector2 to, in RouteDisplayParams p,
                                     out Vector2 tip, out Vector2 left, out Vector2 right)
        {
            tip = to;
            Vector2 d = to - from;
            if (d.sqrMagnitude < DegenerateSqr) { left = to; right = to; return; }
            Vector2 dir = d.normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            Vector2 back = to - dir * p.arrowHeadLength;    // 矢じりの底辺の中心
            left = back + normal * p.arrowHeadHalfWidth;
            right = back - normal * p.arrowHeadHalfWidth;
        }

        /// <summary>
        /// 進行方向の見出し（「北東へ」等・8方位）。方角の判定は <see cref="FleetClusterRules.HeadingLabel"/> へ委譲。
        /// from==to は空文字（向きが無い）。
        /// </summary>
        public static string HeadingText(Vector2 from, Vector2 to)
        {
            string label = FleetClusterRules.HeadingLabel(to - from);
            if (string.IsNullOrEmpty(label)) return "";
            return label + "へ";
        }

        /// <summary>
        /// 経路を一意に指すキー。<b>別々の経路を1本に誤表示しない</b>ための同一性判定に使う
        /// （出発元・現在区間の相手・最終目的地・勢力の4つが全部同じときだけ同じキー）。
        /// 各要素を16bitずつ詰めた決定論の値＝星系IDは 0..65535 を想定（範囲外は下位16bitで折り返す）。
        /// </summary>
        public static long RouteKey(int fromId, int hopToId, int finalId, Faction faction)
        {
            long a = fromId & 0xFFFF;
            long b = hopToId & 0xFFFF;
            long c = finalId & 0xFFFF;
            long f = ((int)faction) & 0xFFFF;
            return (a << 48) | (b << 32) | (c << 16) | f;
        }

        /// <summary>
        /// 「モンブラン → ローガン（経由 3 星系）」のような1行。<paramref name="viaCount"/> が0以下なら括弧を出さない。
        /// 名前が null なら空文字として扱う。
        /// </summary>
        public static string RouteText(string fromName, string finalName, int viaCount)
        {
            string a = fromName ?? "";
            string b = finalName ?? "";
            string head = a + " → " + b;
            if (viaCount <= 0) return head;
            return head + "（経由 " + viaCount + " 星系）";
        }
    }
}
