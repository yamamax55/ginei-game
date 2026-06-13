using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 魔術師の純ロジック（#魔術師・銀河英雄伝説＝ヤン・ウェンリー）。覇王 <see cref="KaiserRules"/>（ラインハルト）の好敵手にして対極。
    /// やる気ゼロの最高知力＝逆転特化の不敗戦術家を再現する：
    /// ①<b>奇跡のヤンの逆転</b>（劣勢＝低兵力/低HP/数的不利であるほど与効果が跳ね上がる＝バーミリオン会戦）、
    /// ②<b>不敗の撤退</b>（負けないための撤退・脱出が必ず成功＝エル・ファシル）、
    /// ③<b>戦術ハック</b>（正面を避け敵の必勝陣形を機能不全にする＝敵陣形効果を削ぐ）、
    /// ④<b>予測の先回り</b>（敵の次手を読み不意打ち/会心を無効化）、
    /// ⑤<b>ヒューベリオン</b>（不敗の象徴＝味方全体の生存率上昇）、
    /// ⑥<b>やる気ゼロ</b>（平時の成長は遅いが、高難度の戦場では知略相関で一気に伸びる）、
    /// ⑦<b>民主主義の羊飼い</b>（シビリアン・コントロールを破らず、理不尽な政治デバフに高い精神耐性）、
    /// ⑧<b>偉大なる教師</b>（弟子〔ユリアン〕への経験伝達・成長限界解放が全キャラ最高＝継承）。
    /// 専用旗艦ヒューベリオンは <see cref="SignatureShipRegistry"/>（ヤン→ヒューベリオン）。ベレー帽・紅茶は flavor。
    /// 数式は係数を返すだけで既存窓口（`CombatModifiers`#106/`FormationDoctrineRules`#5/`DetectionRules`#2180/`GrowthRules`#537-543/`CommandStaffRules`）へ橋渡しする。
    /// 実効値パターン・決定論・test-first。
    /// </summary>
    public static class MagicianRules
    {
        /// <summary>劣勢時の逆転の最大上乗せ（絶体絶命で最大）。</summary>
        public const float ComebackMax = 0.5f;
        /// <summary>敵の必勝陣形を機能不全にする倍率（敵陣形効果に乗る）。</summary>
        public const float FormationDisruption = 0.7f;
        /// <summary>先回りで受ける不意打ち/会心の倍率（半減）。</summary>
        public const float AmbushNegation = 0.5f;
        /// <summary>ヒューベリオン＝味方生存率倍率。</summary>
        public const float AllySurvival = 1.2f;
        /// <summary>高難度の戦場での成長倍率（知略相関で一気に伸びる）。</summary>
        public const float BattleGrowth = 1.5f;
        /// <summary>平時の成長倍率（やる気ゼロで遅い）。</summary>
        public const float PeacetimeGrowth = 0.5f;
        /// <summary>理不尽な政治デバフへの耐性（割合）。</summary>
        public const float PoliticalResistance = 0.7f;
        /// <summary>偉大なる教師＝弟子への経験伝達/成長限界解放の倍率。</summary>
        public const float Mentorship = 2.0f;

        /// <summary>奇跡のヤン＝劣勢 disadvantage(0..1) が大きいほど与効果が跳ね上がる（並は1.0）。</summary>
        public static float ComebackFactor(bool isMagician, float disadvantage)
            => isMagician ? 1f + Mathf.Clamp01(disadvantage) * ComebackMax : 1f;

        /// <summary>負けないための撤退/脱出が必ず成功するか（不敗・エル・ファシル）。</summary>
        public static bool GuaranteesRetreat(bool isMagician) => isMagician;

        /// <summary>敵の必勝陣形を機能不全にする倍率（敵の陣形効果に乗る・並は1.0）。`FormationDoctrineRules`#5 へ。</summary>
        public static float EnemyFormationDisruptionFactor(bool isMagician)
            => isMagician ? FormationDisruption : 1f;

        /// <summary>受ける不意打ち/会心の倍率（先回りで無効化＝半減・並は1.0）。`DetectionRules`#2180 へ。</summary>
        public static float AmbushNegationFactor(bool isMagician)
            => isMagician ? AmbushNegation : 1f;

        /// <summary>ヒューベリオン＝味方全体の生存率倍率（並は1.0）。</summary>
        public static float AllySurvivalFactor(bool isMagician)
            => isMagician ? AllySurvival : 1f;

        /// <summary>成長倍率（やる気ゼロ＝平時は遅いが、戦場では知略相関で一気に伸びる・並は1.0）。`GrowthRules`#537-543 へ。</summary>
        public static float GrowthRateFactor(bool isMagician, bool inBattle)
            => isMagician ? (inBattle ? BattleGrowth : PeacetimeGrowth) : 1f;

        /// <summary>理不尽な政治デバフへの耐性（0..1・並は0）。本国からの無茶振りを耐え抜く。</summary>
        public static float PoliticalDebuffResistance(bool isMagician)
            => isMagician ? PoliticalResistance : 0f;

        /// <summary>シビリアン・コントロールを必ず守るか（民主主義の信念＝反乱・専横を起こさない）。</summary>
        public static bool UpholdsCivilianControl(bool isMagician) => isMagician;

        /// <summary>偉大なる教師＝弟子への経験伝達・成長限界解放の倍率（全キャラ最高・並は1.0）。</summary>
        public static float MentorshipFactor(bool isMagician)
            => isMagician ? Mentorship : 1f;
    }
}
