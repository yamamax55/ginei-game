using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 突撃（#突撃）の成立判定・純ロジック・test-first。基本は遠距離砲撃で、敵と<b>正面同士でぶつかる</b>ときに
    /// 突撃を決めると突撃側がボーナス（既存 <see cref="ActiveCommand.突撃"/> の自己バフ）を得て、受けた側は混乱する
    /// （<see cref="ConfusionRules"/>）。突撃を仕掛けるかは軍団長・艦隊指揮官の判断（呼び出し側の AI）に委ねる。
    /// </summary>
    public static class ChargeRules
    {
        /// <summary>正面衝突とみなす向きの一致しきい値（cos）。0.5＝前方±60°以内で互いに正対。</summary>
        public const float HeadOnCos = 0.5f;
        /// <summary>突撃が成立する最大間合い（この距離以内で正対していれば突撃可）。</summary>
        public const float ChargeRange = 14f;

        /// <summary>
        /// 自分と敵が「正面同士」か＝自分の前方に敵がいて(<paramref name="selfForward"/>)、かつ敵の前方にも自分がいる。
        /// どちらかが側背面を晒していれば false（正面衝突ではない）。
        /// </summary>
        public static bool IsHeadOn(Vector2 selfPos, Vector2 selfForward, Vector2 enemyPos, Vector2 enemyForward, float cosThreshold = HeadOnCos)
        {
            Vector2 toEnemy = enemyPos - selfPos;
            if (toEnemy.sqrMagnitude < 1e-6f) return true; // 重なり＝正面衝突扱い
            toEnemy = toEnemy.normalized;
            Vector2 sf = selfForward.sqrMagnitude > 1e-6f ? selfForward.normalized : Vector2.up;
            Vector2 ef = enemyForward.sqrMagnitude > 1e-6f ? enemyForward.normalized : Vector2.up;
            bool iFaceEnemy = Vector2.Dot(sf, toEnemy) >= cosThreshold;
            bool enemyFacesMe = Vector2.Dot(ef, -toEnemy) >= cosThreshold;
            return iFaceEnemy && enemyFacesMe;
        }

        /// <summary>突撃が成立する間合いか（正対していて、かつ <paramref name="range"/> 以内）。</summary>
        public static bool InChargeRange(Vector2 selfPos, Vector2 enemyPos, float range = ChargeRange)
            => (enemyPos - selfPos).sqrMagnitude <= range * range;
    }
}
