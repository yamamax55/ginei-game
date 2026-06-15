using UnityEngine;

namespace Ginei
{
    /// <summary>会戦の士気波及イベント（#2176）。撃墜/敗走は近傍の味方に衝撃、捨てがまり成功は近傍の味方に高揚。</summary>
    public enum MoraleEvent { 旗艦撃墜, 敗走, 捨てがまり成功 }

    /// <summary>
    /// 士気の連鎖崩壊／高揚（panic propagation・#2176）の純ロジック。局所の出来事を戦線全体の流れへ波及させる。
    /// 旗艦撃墜・敗走は近傍の味方士気を削り（パニック）、撃墜は敵を高揚させる（elation）。捨てがまり成功は近傍の味方を奮い立たせる。
    /// ランチェスター集中（局所優勢）と相乗して「各個撃破の快感／総崩れの恐怖」を生む。距離減衰・上限クランプ・test-first。
    /// 符号（味方=負/敵=正 等）は呼び出し側が `FactionRelations` で決め、ここは衝撃の大きさと減衰のみを返す。
    /// </summary>
    public static class MoraleShockRules
    {
        // 事象の基準衝撃量（士気ポイント・正の大きさ）。
        public const float Mag旗艦撃墜 = 18f;
        public const float Mag敗走 = 10f;
        public const float Mag捨てがまり成功 = 12f;

        /// <summary>事象の基準衝撃量。</summary>
        public static float Magnitude(MoraleEvent ev)
        {
            switch (ev)
            {
                case MoraleEvent.旗艦撃墜:      return Mag旗艦撃墜;
                case MoraleEvent.敗走:          return Mag敗走;
                case MoraleEvent.捨てがまり成功: return Mag捨てがまり成功;
                default:                        return 0f;
            }
        }

        /// <summary>距離減衰（0..1）。半径内で線形に減衰、半径外は0、半径0以下は1（中心）。</summary>
        public static float Falloff(float distance, float radius)
        {
            if (radius <= 0f) return 1f;
            float d = Mathf.Max(0f, distance);
            if (d >= radius) return 0f;
            return 1f - d / radius;
        }

        /// <summary>半径内のある距離での衝撃量（正の大きさ＝事象基準×減衰）。符号は呼び出し側で決める。</summary>
        public static float ShockAt(MoraleEvent ev, float distance, float radius)
            => Magnitude(ev) * Falloff(distance, radius);
    }
}
