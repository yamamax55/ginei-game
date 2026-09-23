using UnityEngine;

namespace Ginei
{
    /// <summary>要塞主砲（トールハンマー型）の危険域回避の調整値（#641 EX4-3 ハメ防止）。</summary>
    public readonly struct FortressThreatParams
    {
        /// <summary>射線の半幅に足す安全余裕（被弾幅より広めに避ける）。</summary>
        public readonly float widthMargin;
        /// <summary>横方向（射線から離れる）回避の強さ（大きいほど大きく逸れる）。</summary>
        public readonly float avoidStrength;

        public FortressThreatParams(float widthMargin, float avoidStrength)
        {
            this.widthMargin = Mathf.Max(0f, widthMargin);
            this.avoidStrength = Mathf.Max(0f, avoidStrength);
        }

        /// <summary>既定＝半幅+1.5 の余裕・回避強度2.0（射線から横へ大きく逃げる）。</summary>
        public static FortressThreatParams Default => new FortressThreatParams(1.5f, 2.0f);
    }

    /// <summary>
    /// 要塞主砲（#77 トールハンマー型）の<b>危険域回避</b>の純ロジック（#641 EX4-3＝AIが主砲の射線へ
    /// 漫然と突っ込まない・ハメ防止）。チャージ中は射線（aimDir）が固定され予告線が出る＝その間に
    /// <b>横へ散開</b>すれば回避できる。危険域＝射線方向に minRange..maxRange、半幅(halfWidth+余裕)内、前方。
    /// 危険域にいるなら目標を射線から離れる横方向へ逸らした目的地を返す（移動本体は変えずAI側で目標補正＝
    /// ZOC回避/ブラックホール回避と同方式）。懐（minRange未満）・射程外・後方・射線外は安全＝目標そのまま。
    /// 盤面非依存・実効値パターン（基準値非破壊）・決定論・test-first。
    /// </summary>
    public static class FortressThreatRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 1e-6f;

        /// <summary>
        /// 点（origin→ship のベクトル <paramref name="toShip"/>）が主砲の危険帯にあるか。
        /// 前方（along&gt;0）かつ along∈[minRange, maxRange] かつ 射線からの距離 ≤ halfWidth+widthMargin。
        /// </summary>
        public static bool InDangerZone(Vector2 toShip, Vector2 aimDir, float minRange, float maxRange,
            float halfWidth, float widthMargin)
        {
            Vector2 a = aimDir.sqrMagnitude > Epsilon ? aimDir.normalized : Vector2.up;
            float along = Vector2.Dot(toShip, a);
            if (along <= 0f) return false;                         // 後方＝当たらない
            float lo = Mathf.Max(0f, minRange);
            if (along < lo || along > Mathf.Max(lo, maxRange)) return false; // 懐／射程外＝安全
            float perp = (toShip - a * along).magnitude;
            return perp <= Mathf.Max(0f, halfWidth) + Mathf.Max(0f, widthMargin);
        }

        /// <summary>
        /// 主砲の危険域にいるとき、目標 <paramref name="desired"/> を射線から離れる横方向へ逸らした目的地を返す
        /// （目的地までの距離は保つ）。安全なら <paramref name="desired"/> をそのまま返す。
        /// </summary>
        public static Vector2 SteerClear(Vector2 pos, Vector2 desired, Vector2 origin, Vector2 aimDir,
            float minRange, float maxRange, float halfWidth)
            => SteerClear(pos, desired, origin, aimDir, minRange, maxRange, halfWidth, FortressThreatParams.Default);

        /// <summary><inheritdoc cref="SteerClear(Vector2,Vector2,Vector2,Vector2,float,float,float)"/></summary>
        public static Vector2 SteerClear(Vector2 pos, Vector2 desired, Vector2 origin, Vector2 aimDir,
            float minRange, float maxRange, float halfWidth, FortressThreatParams p)
        {
            Vector2 a = aimDir.sqrMagnitude > Epsilon ? aimDir.normalized : Vector2.up;
            Vector2 toShip = pos - origin;
            if (!InDangerZone(toShip, a, minRange, maxRange, halfWidth, p.widthMargin)) return desired;

            float along = Vector2.Dot(toShip, a);
            Vector2 perpVec = toShip - a * along;     // 射線からのはみ出しベクトル（横方向）
            Vector2 perpDir;
            if (perpVec.sqrMagnitude > Epsilon) perpDir = perpVec.normalized; // 既にいる側へさらに逃がす
            else perpDir = new Vector2(-a.y, a.x);    // 射線上ど真ん中なら左へ（左右どちらでも可）

            Vector2 toDesired = desired - pos;
            float dist = toDesired.magnitude;
            Vector2 dir = dist > Epsilon ? toDesired / dist : a; // 目標が現在地なら前方を既定に
            Vector2 steered = (dir + perpDir * p.avoidStrength).normalized;
            return pos + steered * dist;
        }
    }
}
