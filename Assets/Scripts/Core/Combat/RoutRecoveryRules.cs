using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 敗走からの<b>立ち直り</b>の決まり（純ロジック・test-first）。
    ///
    /// 設計の意図は「<b>交戦が途切れてしばらく経ったら</b>立ち直り始める」。
    /// ところが「交戦中か」を<b>自分が撃っているか／自分の射界に敵がいるか</b>だけで見ると、
    /// <b>撃たれている側</b>（射界の外から叩かれている・敗走して背を向けている）が
    /// 「非交戦」と判定され、待ち時間を飛ばして<b>次のフレームに立ち直って</b>しまう。
    ///
    /// 実機では、それが
    /// 「敗走 開始（士気 2.0→0.0）→ 次フレームに 敗走 解除（士気 0.0→0.0）」
    /// の反復として出た（回復量 <c>recoveryRate × deltaTime</c> ＝ ごく小さな正値が
    /// <c>士気 &lt;= 0</c> の判定を外すため）。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item><b>被弾も交戦のうち</b>（<see cref="IsCombatContact"/>）＝撃たれている間は待ち時間が始まらない。</item>
    ///   <item>立ち直りは<b>交戦が途切れて待ち時間を満たしてから</b>（<see cref="CanRecover"/>）。</item>
    /// </list>
    /// </summary>
    public static class RoutRecoveryRules
    {
        /// <summary>
        /// いま<b>交戦に触れている</b>か。自分が撃っている／射界に敵がいる、に加えて
        /// <b>このフレームに被弾した</b>ときも交戦とみなす。
        /// ここを落とすと、一方的に叩かれている部隊が「非交戦」になって待ち時間を飛ばす。
        ///
        /// ★<b>製品はこの関数を経由していない</b>：<see cref="FleetMorale"/> は被弾時に
        /// 直接 <c>lastCombatTime</c> を更新することで同じ意味を実現している
        /// （毎フレームの被弾有無を持ち回らずに済むため）。
        /// ここは<b>その取り決めを言葉にして固定する</b>ためのもので、
        /// この関数の単体合格は<b>製品に配線されている証拠にはならない</b>。
        /// 配線は PlayMode（<c>NaturalRecovery_*</c>）が実コンポーネントで確かめる。
        /// </summary>
        public static bool IsCombatContact(bool engaging, bool tookDamage)
            => engaging || tookDamage;

        /// <summary>
        /// 敗走から立ち直り始めてよいか。
        /// 交戦に触れている間は false（＝<paramref name="secondsSinceCombat"/> を数え直す）。
        /// </summary>
        public static bool CanRecover(bool combatContact, float secondsSinceCombat, float recoveryDelay)
        {
            if (combatContact) return false;
            return secondsSinceCombat >= Mathf.Max(0f, recoveryDelay);
        }
    }
}
