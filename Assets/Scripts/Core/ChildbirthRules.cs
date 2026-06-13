using System;

namespace Ginei
{
    /// <summary>
    /// 出産の純ロジック（結婚と出産システム基盤・唯一の窓口）。父（男性）と母（女性）から子の <see cref="Person"/> を生む。
    /// 子の能力は<b>両親と相関しつつばらつく</b>＝各能力を <see cref="HeredityRules"/> で個別に遺伝（中間親値＋平均回帰＋乱数）。
    /// </summary>
    /// <remarks>
    /// <b>倫理ガード：優生学NG</b>。<see cref="CanConceive"/> は能力を一切参照しない（能力で出産可否を選別しない）。
    /// 生まれた子を能力で<b>間引く/選別する API は無い</b>＝必ず生まれ、能力は <see cref="HeredityRules"/> の平均回帰＋乱数で決まる
    /// （きょうだいで違い、低能力の親から優れた子も、その逆も起こりうる）。生死・自由・成年・近親（<see cref="PersonMarriageRules.AreCloseKin"/>）のみを見る。
    /// 母集団の出生（POP）は <see cref="PopulationDynamicsRules"/>、家系の政略は <see cref="MarriageRules"/> が別途担う。決定論（roll は供給）・test-first。
    /// </remarks>
    public static class ChildbirthRules
    {
        /// <summary>出産可能年齢（母の上限。生年未設定は不問）。</summary>
        public const int MinChildbearingAge = 16;
        public const int MaxChildbearingAge = 50;

        /// <summary>母（女性）が出産可能な年齢域にあるか（生年未設定 birthYear≤0 は不問）。</summary>
        public static bool MotherInChildbearingAge(Person mother, int currentYear)
        {
            if (mother == null) return false;
            if (mother.BirthYear <= 0) return true;
            int age = LifecycleRules.Age(mother, currentYear);
            return age >= MinChildbearingAge && age <= MaxChildbearingAge;
        }

        /// <summary>
        /// 子を生せるか＝父が男性・母が女性・別人・双方存命自由・母が出産可能年齢・近親でない。
        /// <b>能力/身分は問わない（優生学NG）</b>。結婚は必須としない（基盤として婚外も許容＝呼び出し側が方針を決める）。
        /// </summary>
        public static bool CanConceive(Person father, Person mother, int currentYear)
        {
            if (father == null || mother == null || father.id == mother.id) return false;
            if (father.sex != Sex.男性 || mother.sex != Sex.女性) return false; // 生物学的な出産（婚姻の自由とは別軸）
            if (!father.IsAvailable || !mother.IsAvailable) return false;
            if (!MotherInChildbearingAge(mother, currentYear)) return false;
            if (PersonMarriageRules.AreCloseKin(father, mother)) return false;
            return true;
        }

        /// <summary>
        /// 子を出産する＝両親の各能力を <see cref="HeredityRules.InheritStat"/> で個別に遺伝させた新しい <see cref="Person"/> を返す。
        /// 性別は <paramref name="sexRoll"/>（&lt;0.5 で男性）。能力ごとに <paramref name="roll"/>() を引く（独立な乱数＝きょうだいで散る）。
        /// 勢力は父系（<paramref name="father"/> の faction）。血縁（母父id）を刻む。<see cref="CanConceive"/> を満たさなければ null。
        /// </summary>
        public static Person Conceive(Person father, Person mother, int childId, int birthYear,
            float sexRoll, Func<float> roll, HeredityRules.HeredityParams prm)
        {
            if (!CanConceive(father, mother, birthYear)) return null;
            if (roll == null) roll = () => 0.5f; // 既定＝無ノイズ（期待値）

            var child = new Person(childId, "", father.faction, PersonRole.軍人)
            {
                sex = sexRoll < 0.5f ? Sex.男性 : Sex.女性,
                birthYear = birthYear,
                fatherId = father.id,
                motherId = mother.id,
            };

            // 各能力を独立に遺伝（中間親値＋平均回帰＋乱数）＝相関しつつばらつく。
            child.leadership   = HeredityRules.InheritStat(father.leadership,   mother.leadership,   roll(), prm);
            child.attack       = HeredityRules.InheritStat(father.attack,       mother.attack,       roll(), prm);
            child.defense      = HeredityRules.InheritStat(father.defense,      mother.defense,      roll(), prm);
            child.mobility     = HeredityRules.InheritStat(father.mobility,     mother.mobility,     roll(), prm);
            child.operation    = HeredityRules.InheritStat(father.operation,    mother.operation,    roll(), prm);
            child.intelligence = HeredityRules.InheritStat(father.intelligence, mother.intelligence, roll(), prm);
            // 専門能力（テクノクラート LIFE-7）も同様に遺伝。
            child.research    = HeredityRules.InheritStat(father.research,    mother.research,    roll(), prm);
            child.engineering = HeredityRules.InheritStat(father.engineering, mother.engineering, roll(), prm);
            child.planning    = HeredityRules.InheritStat(father.planning,    mother.planning,    roll(), prm);
            child.production  = HeredityRules.InheritStat(father.production,  mother.production,  roll(), prm);

            return child;
        }

        public static Person Conceive(Person father, Person mother, int childId, int birthYear, float sexRoll, Func<float> roll)
            => Conceive(father, mother, childId, birthYear, sexRoll, roll, HeredityRules.HeredityParams.Default);
    }
}
