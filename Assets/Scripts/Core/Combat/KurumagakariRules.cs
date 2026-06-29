using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 車懸かり（旋回突撃）の運動ロジック（#軍神・Core純ロジック・test-first）。
    /// 史実（俗説＝江戸期軍学者の解釈）＝部隊を絶え間なく入れ替え、疲れた前線を後退させ
    /// 新手を前へ回す＝陣がぐるぐると旋回し続けて常に新手が敵に当たる。上杉謙信（軍神）の戦法。
    /// 配下艦の陣形スロットを時間とともに旗艦中心に旋回させ、旗艦周りを巡回させる（運動の核）。
    /// `Squadron` がこの窓口を消費する（旋回角の更新＋スロット回転を二重実装しない）。
    /// </summary>
    public static class KurumagakariRules
    {
        /// <summary>既定の旋回速度（度/秒）。12秒で一周＝絶え間ない旋回を表す。</summary>
        public const float DefaultRotationSpeedDeg = 30f;

        /// <summary>旋回角を dt ぶん進める（0..360 にラップ・フレームレート非依存・timeScale 追従）。</summary>
        public static float AdvanceAngle(float angleDeg, float rotationSpeedDeg, float dt)
        {
            if (dt <= 0f) return Wrap(angleDeg);
            return Wrap(angleDeg + rotationSpeedDeg * dt);
        }

        /// <summary>ローカルスロット（旗艦中心の相対座標）を旋回角ぶん回す＝渦巻きが回転して巡回運動になる。</summary>
        public static Vector2 RotateLocalSlot(Vector2 localSlot, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(
                localSlot.x * cos - localSlot.y * sin,
                localSlot.x * sin + localSlot.y * cos);
        }

        /// <summary>角度を 0..360 にラップ（負値も正に畳む）。</summary>
        private static float Wrap(float deg)
        {
            deg %= 360f;
            if (deg < 0f) deg += 360f;
            return deg;
        }
    }
}
