using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 王家の教育の純ロジック（#王家教育・帝王学＝史実の傅役/三師三少・アリストテレスの薫陶を参考）。
    /// 王族は既存の教育システム（士官学校#155/科挙#156/大学）を<b>使わない</b>：
    /// ・<b>生まれた瞬間にネームド化</b>（POP→学校の昇格でなく出生で Named）。
    /// ・<b>子供時代と大人時代で能力は別</b>＝子供は素養の未成熟分しか出せず、大人は帝王学の習得度で素養が実現される。
    /// ・<b>家庭教師</b>が帝王学を授け（質＝統率/政務/情報）、<b>元勲</b>がいれば薫陶のボーナス。
    /// 名望ある師に就いた賢君と、放埒に育った暗君を分ける＝教育が王の器を決める。決定論・test-first・後方互換。
    /// </summary>
    public static class RoyalEducationRules
    {
        /// <summary>成人（元服）年齢。これ未満は子供時代。</summary>
        public const int MajorityAge = 16;

        /// <summary>子供時代に発揮できる素養の割合（未成熟＝大人になる前は能力が低い）。</summary>
        public const float ChildMaturity = 0.4f;

        /// <summary>教育ゼロでも大人時代に実現される素養の下限割合（放埒に育っても子供よりはまし）。</summary>
        public const float UneducatedFloor = 0.5f;

        /// <summary>帝王学を授け切るのに要する年数（子供時代いっぱい）。</summary>
        public const float FullTutoringYears = 12f;

        /// <summary>元勲の薫陶ボーナスの係数と上限。</summary>
        public const float GenroBonusScale = 0.3f;
        public const float MaxGenroBonus = 0.25f;

        /// <summary>子供時代か（成人年齢未満）。</summary>
        public static bool IsChild(int age) => age < MajorityAge;

        /// <summary>家庭教師の質（0..1）＝帝王学に資する統率/政務/情報の平均。師が賢いほど良く教える。</summary>
        public static float TutorQuality(Person tutor)
        {
            if (tutor == null) return 0f;
            return Mathf.Clamp01((tutor.leadership + tutor.operation + tutor.intelligence) / 300f);
        }

        /// <summary>元勲の薫陶ボーナス（元勲がいれば教育にプラス・上限あり）。</summary>
        public static float GenroBonus(Person genro)
        {
            if (genro == null) return 0f;
            float quality = (genro.leadership + genro.operation + genro.intelligence) / 300f;
            return Mathf.Clamp(quality * GenroBonusScale, 0f, MaxGenroBonus);
        }

        /// <summary>到達しうる帝王学の上限（師の質＋元勲の薫陶。凡庸な師では頭打ち）。</summary>
        public static float EducationCap(float tutorQuality, float genroBonus)
            => Mathf.Clamp01(Mathf.Clamp01(tutorQuality) + Mathf.Max(0f, genroBonus));

        /// <summary>
        /// 帝王学の習得度を年数ぶん進める（上限＝<see cref="EducationCap"/> で頭打ち＝凡庸な師は長年でも一定まで）。
        /// 子供時代いっぱい（<see cref="FullTutoringYears"/>）で上限へ達する。
        /// </summary>
        public static float AccumulateEducation(float currentEducation, float tutorQuality, float genroBonus, float dtYears)
        {
            float cap = EducationCap(tutorQuality, genroBonus);
            float rate = cap / FullTutoringYears;
            return Mathf.Min(cap, Mathf.Max(0f, currentEducation) + rate * Mathf.Max(0f, dtYears));
        }

        /// <summary>子供時代に発揮する能力（素養の未成熟分）。</summary>
        public static int ChildStat(int potential)
            => Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(potential, 0, 100) * ChildMaturity), 0, 100);

        /// <summary>大人時代に実現される能力（素養を帝王学の習得度で実現＝無教育で下限、満教育で素養そのもの）。</summary>
        public static int AdultStat(int potential, float education)
        {
            float realized = Mathf.Lerp(UneducatedFloor, 1f, Mathf.Clamp01(education));
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(potential, 0, 100) * realized), 0, 100);
        }

        /// <summary>
        /// 王族をネームド化して生む（出生で Named＝既存の学校経路を経ない別格）。
        /// 子供時代の能力（素養の未成熟分）を <paramref name="person"/> に書き、王家フラグと生年を立てる。
        /// </summary>
        public static void BornRoyal(Person person, RoyalUpbringing up)
        {
            if (person == null || up == null) return;
            person.isRoyal = true;
            person.birthYear = up.bornYear;
            up.matured = false;
            up.education = 0f;
            ApplyChildStats(person, up);
        }

        /// <summary>子供時代の能力を素養から書く（未成熟）。</summary>
        public static void ApplyChildStats(Person person, RoyalUpbringing up)
        {
            if (person == null || up == null) return;
            person.leadership   = ChildStat(up.potLeadership);
            person.attack       = ChildStat(up.potAttack);
            person.defense      = ChildStat(up.potDefense);
            person.mobility     = ChildStat(up.potMobility);
            person.operation    = ChildStat(up.potOperation);
            person.intelligence = ChildStat(up.potIntelligence);
        }

        /// <summary>
        /// 帝王学を1年ぶん進める（子供時代のみ・成人後は無効）。家庭教師の質＋元勲の薫陶で習得度が漸増する。
        /// </summary>
        public static void TickEducation(RoyalUpbringing up, Person tutor, Person genro, float dtYears)
        {
            if (up == null || up.matured) return;
            up.education = AccumulateEducation(up.education, TutorQuality(tutor), GenroBonus(genro), dtYears);
        }

        /// <summary>
        /// 成人して大人時代の能力を確定する（一度きり）。帝王学の習得度で素養が実現され、子供時代とは別の能力になる。
        /// 名師＋元勲に育てられた皇子は素養を満たし、放埒に育った皇子は下限止まり＝教育が王の器を分ける。
        /// </summary>
        public static void Mature(Person person, RoyalUpbringing up)
        {
            if (person == null || up == null || up.matured) return;
            float edu = up.education;
            person.leadership   = AdultStat(up.potLeadership, edu);
            person.attack       = AdultStat(up.potAttack, edu);
            person.defense      = AdultStat(up.potDefense, edu);
            person.mobility     = AdultStat(up.potMobility, edu);
            person.operation    = AdultStat(up.potOperation, edu);
            person.intelligence = AdultStat(up.potIntelligence, edu);
            up.matured = true;
        }
    }
}
