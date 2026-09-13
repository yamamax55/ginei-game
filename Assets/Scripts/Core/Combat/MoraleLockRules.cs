using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// <b>不退転</b>（特殊指揮 #2175・<see cref="ActiveCommandSpec.moraleLock"/>）と
    /// 敗走判定の関係を決める純ロジック（test-first）。
    ///
    /// <b>なぜ要るか</b>：不退転は「効果中は敗走しない」という約束だが、
    /// 士気は被弾のたび（＝フレーム中の任意の時点で）減る一方、
    /// 「士気を1へ戻す」処理は<b>1フレームに1回</b>しか走らない。
    /// このずれがあると、被弾してから次の士気更新までのあいだだけ
    /// <c>士気 &lt;= 0</c> になり、その隙に AI・陣形保持・支援要請が
    /// 「敗走した」と読んでしまう＝<b>敗走と解除が1フレーム間隔で反復</b>する。
    ///
    /// そこで「効いているか」を<b>判定と下限の両方</b>へ同じ形で織り込み、
    /// <b>更新順に依存しない</b>ようにする。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>効果中は<b>いつ読んでも</b>敗走していない（<see cref="IsRouted"/>）。</item>
    ///   <item>効果中は士気が<b>下限 <see cref="LockedFloor"/> を割らない</b>（<see cref="Clamp"/>）
    ///         ＝値そのものがばたつかない。</item>
    ///   <item>効果が無いときは<b>従来どおり</b>（下限0・士気0で敗走）＝通常の敗走を弱めない。</item>
    /// </list>
    ///
    /// ※これは<b>敗走</b>だけの決まり。軍団の総退却は兵力比でも起きる
    ///   （<see cref="CorpsRetreatRules.ShouldOrderRetreat"/>）ので、
    ///   不退転で敗走しなくても<b>総退却の判断は従来どおり働く</b>。
    /// </summary>
    public static class MoraleLockRules
    {
        /// <summary>不退転が効いている間の士気の下限（0にしないことで敗走判定に落ちない）。</summary>
        public const float LockedFloor = 1f;

        /// <summary>
        /// <b>有効な</b>敗走状態。士気が尽きていても、不退転が効いていれば敗走ではない。
        /// 敗走を見る側（AI・陣形保持・支援要請・HUD・観測）は<b>必ずこれを通す</b>
        /// ＝どこで読んでも同じ答えになる。
        /// </summary>
        public static bool IsRouted(float morale, bool moraleLock)
            => morale <= 0f && !moraleLock;

        /// <summary>いまの士気の下限（不退転中は <see cref="LockedFloor"/>、平時は0）。</summary>
        public static float Floor(bool moraleLock)
            => moraleLock ? LockedFloor : 0f;

        /// <summary>
        /// 士気を増減したあとの値。下限は <see cref="Floor"/>、上限は <paramref name="maxMorale"/>。
        /// 不退転中は下限を割らないので、<b>被弾の順序に関係なく</b>敗走判定へ落ちない。
        /// </summary>
        public static float Clamp(float morale, float delta, float maxMorale, bool moraleLock)
        {
            float max = Mathf.Max(1f, maxMorale);
            float floor = Mathf.Min(Floor(moraleLock), max);
            return Mathf.Clamp(morale + delta, floor, max);
        }

        /// <summary>
        /// 効果が切れた瞬間に士気をどう扱うか。
        /// <b>切れただけで即敗走にはしない</b>（下限で踏みとどまった状態から再開する）＝
        /// 「効果中は死なずに粘る」という趣旨を、効果終了の1フレームで台無しにしない。
        /// 以後は通常どおり、さらに被弾すれば士気0＝敗走になる。
        /// </summary>
        public static float OnLockExpired(float morale)
            => Mathf.Max(morale, 0f);
    }
}
