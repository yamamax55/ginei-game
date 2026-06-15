using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 大戦術家の純ロジック（#大戦術家・史実＝ハンニバル・バルカ）。前線最強の戦術家にして、勝利を活かせぬ悲劇の将を再現する：
    /// ①<b>包囲殲滅</b>（カンナエ＝中央をあえて凹ませ両翼で挟撃。`EnvelopmentRules`#2178 を増幅）、
    /// ②<b>戦場の霧の支配</b>（伏兵・夜襲・地形・偵察。`DetectionRules`#2180 の不意打ちを増幅）、
    /// ③<b>心理戦</b>（敵将の性格を突き挑発しミスを誘う。短気/傲慢ほど効く）、
    /// ④<b>アルプス越え・戦象</b>（山岳/雪原の移動ペナルティを無効化）、
    /// ⑤<b>ローマへの宿命</b>（指定した宿敵勢力への特効）、
    /// ⑥<b>多国籍傭兵の結束</b>（種族/属性がバラバラなほど結束が増す＝16年無反乱の統率＝通常の混成ペナルティを反転）、
    /// ⑦<b>戦争に勝つが勝利を活かせぬ</b>（内政/兵站/政治が極端に苦手＝戦略フェーズの不利）、
    /// ⑧<b>スキピオの因縁</b>（戦術を研究され尽くすと包囲が破られる＝ザマの敗北）。
    /// 数式は係数を返すだけで既存窓口（#2178/#2180/CDR-1 性格/兵站#94/外交#189）へ橋渡しする。実効値パターン・決定論・test-first。
    /// </summary>
    public static class GrandTacticianRules
    {
        /// <summary>包囲殲滅（カンナエ）の与効果倍率。研究され尽くすと無効化（ザマ）。</summary>
        public const float EnvelopmentMastery = 1.4f;
        /// <summary>戦場の霧（伏兵/夜襲）の与効果倍率。</summary>
        public const float AmbushMastery = 1.3f;
        /// <summary>宿敵勢力（ローマ）への特効倍率。</summary>
        public const float NemesisBonus = 1.25f;
        /// <summary>多国籍傭兵の結束の最大上乗せ（多様なほど強い）。</summary>
        public const float DiverseCohesionMax = 0.3f;
        /// <summary>通常の指揮官の混成ペナルティ（多様なほど結束↓）。</summary>
        public const float NormalDiversityPenalty = 0.15f;
        /// <summary>内政/兵站が苦手＝戦略フェーズの倍率（勝利を活かせない）。</summary>
        public const float StrategicWeakness = 0.7f;

        /// <summary>包囲殲滅（カンナエ）の与効果倍率。研究され尽くした敵（スキピオ）には効かない＝1.0（ザマ）。並は1.0。</summary>
        public static float EnvelopmentMasteryFactor(bool isGrandTactician, bool studiedByEnemy)
            => (isGrandTactician && !studiedByEnemy) ? EnvelopmentMastery : 1f;

        /// <summary>戦場の霧（伏兵・夜襲・地形）の与効果倍率（並は1.0）。`DetectionRules`#2180 の不意打ちに乗る。</summary>
        public static float AmbushMasteryFactor(bool isGrandTactician)
            => isGrandTactician ? AmbushMastery : 1f;

        /// <summary>山岳/雪原などの移動ペナルティを無効化するか（アルプス越え・戦象）。</summary>
        public static bool IgnoresTerrainPenalty(bool isGrandTactician)
            => isGrandTactician;

        /// <summary>
        /// 心理戦＝敵将を挑発しミスを誘う度合い（0..）。激情＝最も乗りやすく、果敢も乗る。冷静/慎重/堅実は乗りにくい。並は0。
        /// </summary>
        public static float ProvocationFactor(bool isGrandTactician, CommanderPersonality enemyPersonality)
        {
            if (!isGrandTactician) return 0f;
            switch (enemyPersonality)
            {
                case CommanderPersonality.激情: return 0.4f; // 短気＝一番乗る
                case CommanderPersonality.果敢: return 0.3f; // 猪突
                default: return 0.1f;                         // 冷静/慎重/堅実は乗りにくい
            }
        }

        /// <summary>宿敵勢力（ローマ）への特効倍率（指定の宿敵相手のみ・並は1.0）。</summary>
        public static float NemesisFactionBonus(bool isGrandTactician, bool vsSwornEnemy)
            => (isGrandTactician && vsSwornEnemy) ? NemesisBonus : 1f;

        /// <summary>
        /// 多国籍編成の結束倍率。大戦術家は多様（diversity 0..1）なほど結束が増す（16年無反乱）。
        /// 並の指揮官は逆に多様なほど結束が下がる（混成ペナルティ）。
        /// </summary>
        public static float DiverseForceCohesionFactor(bool isGrandTactician, float diversity)
        {
            float d = Mathf.Clamp01(diversity);
            return isGrandTactician ? 1f + d * DiverseCohesionMax : 1f - d * NormalDiversityPenalty;
        }

        /// <summary>内政/兵站/政治の倍率（戦争に勝つが勝利を活かせない＝戦略フェーズの不利・並は1.0）。</summary>
        public static float StrategicWeaknessFactor(bool isGrandTactician)
            => isGrandTactician ? StrategicWeakness : 1f;
    }
}
