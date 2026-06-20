using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦術パラダイムの優位と適応（ENDR-3 #2356）＝純ロジック。
    /// 「既存教義に対しフレームシフト（新機軸）を仕掛けると、敵が未対応のうちは局所優位（>1.0）を得るが、
    ///   敵の情報能力とパラダイムの経過時間に応じて適応が進み、優位は 1.0 へ収束する」というイノベーションの
    /// 賞味期限を係数化する。<see cref="ParadigmAdvantage"/> の戻り値は素の倍率＝そのまま
    /// <see cref="ModifierStack.Mul"/> に積める（ModifierStack/CombatModifiers は本クラスからは変更しない）。
    /// すべて Clamp/null安全・調整値は const。test-first。
    /// </summary>
    public static class TacticalParadigmRules
    {
        /// <summary>フレームシフトが未対応の敵に対して得る優位倍率のピーク（>1.0）。</summary>
        public const float AdvantagePeak = 1.3f;

        /// <summary>優位なし（拮抗・対応済など）の基準倍率。</summary>
        public const float NoAdvantage = 1f;

        /// <summary>適応の進みやすさ＝敵情報能力の寄与重み（0..1のうちこの割合を担う）。</summary>
        public const float AdaptIntelWeight = 0.6f;

        /// <summary>適応の進みやすさ＝パラダイム経過時間の寄与重み（古い手ほど読まれる）。</summary>
        public const float AdaptAgeWeight = 0.4f;

        /// <summary>パラダイム経過時間がこの値（単位は呼び出し側の暦秒/日など任意）で時間寄与が飽和する。</summary>
        public const float AdaptAgeFullScale = 30f;

        /// <summary>新機軸の発見（ひらめき）確率に対する機動（柔軟さ）の寄与重み。</summary>
        public const float DiscoveryMobilityWeight = 0.5f;

        /// <summary>新機軸の発見確率に対する逆境（追い詰められ）の寄与重み＝窮すれば通ず。</summary>
        public const float DiscoveryAdversityWeight = 0.5f;

        /// <summary>能力値（機動など 0..100 想定）を 0..1 へ正規化する基準。</summary>
        public const float StatNormalizer = 100f;

        /// <summary>
        /// 自分と敵のパラダイムの組み合わせから優位倍率を返す。
        /// 自分=フレームシフト済 かつ 敵=正規教義（未対応）のときピーク（<see cref="AdvantagePeak"/>）。
        /// 敵=対応済 なら学習済みのため 1.0。それ以外の組み合わせ（拮抗・自分が未シフト等）は 1.0。
        /// なお敵の適応進捗（<see cref="AdaptationProgress"/>）が 1.0 へ向かうと、呼び出し側は本ピークを
        /// その進捗で 1.0 側へ補間する想定（<see cref="ParadigmAdvantageAdapting"/> 参照）。
        /// </summary>
        public static float ParadigmAdvantage(TacticalParadigm mine, TacticalParadigm enemy)
        {
            // 新機軸を仕掛けており、敵がまだ正規教義（未対応）＝最大優位。
            if (mine == TacticalParadigm.フレームシフト済 && enemy == TacticalParadigm.正規教義)
            {
                return AdvantagePeak;
            }

            // それ以外（敵が対応済／自分が新機軸でない／拮抗）は優位なし。
            return NoAdvantage;
        }

        /// <summary>
        /// 敵の適応進捗（<paramref name="adaptationProgress"/> 0..1）を考慮した優位倍率。
        /// 進捗0でピーク、進捗1で 1.0 へ線形収束する（賞味期限の表現）。
        /// 元の組み合わせが優位なし（1.0）なら進捗に依らず 1.0。
        /// </summary>
        public static float ParadigmAdvantageAdapting(TacticalParadigm mine, TacticalParadigm enemy, float adaptationProgress)
        {
            float peak = ParadigmAdvantage(mine, enemy);
            float p = Mathf.Clamp01(adaptationProgress);
            // peak → 1.0 へ p で補間。
            return Mathf.Lerp(peak, NoAdvantage, p);
        }

        /// <summary>
        /// 敵がこちらのパラダイムに適応した度合い（0..1）。
        /// 敵の情報能力（<paramref name="enemyIntelligence"/> 0..1）が高いほど速く、
        /// パラダイムの経過時間（<paramref name="paradigmAge"/>）が長いほど読まれて速く進む。
        /// 進捗が 1.0 に近づくほど、優位倍率は 1.0 に収束する（<see cref="ParadigmAdvantageAdapting"/> で補間）。
        /// </summary>
        public static float AdaptationProgress(float enemyIntelligence, float paradigmAge)
        {
            float intel = Mathf.Clamp01(enemyIntelligence);
            float ageNorm = Mathf.Clamp01(Mathf.Max(0f, paradigmAge) / AdaptAgeFullScale);
            float progress = intel * AdaptIntelWeight + ageNorm * AdaptAgeWeight;
            return Mathf.Clamp01(progress);
        }

        /// <summary>
        /// 新機軸（フレームシフト）を発見する確率（0..1）。
        /// 提督の機動＝発想の柔軟さ（<paramref name="admiralMobility"/>）と、逆境の度合い
        /// （<paramref name="adversityLevel"/> 0..1＝追い詰められているほど高い）の積的寄与で上がる＝
        /// 「窮すれば通ず」。機動は 0..<see cref="StatNormalizer"/> 想定で正規化する。
        /// </summary>
        public static float DiscoveryChance(float admiralMobility, float adversityLevel)
        {
            float mob = Mathf.Clamp01(Mathf.Max(0f, admiralMobility) / StatNormalizer);
            float adversity = Mathf.Clamp01(adversityLevel);
            float chance = mob * DiscoveryMobilityWeight + adversity * DiscoveryAdversityWeight;
            return Mathf.Clamp01(chance);
        }
    }
}
