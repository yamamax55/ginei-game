namespace Ginei
{
    /// <summary>ネームドの軍務配属区分。学校配属＝在学中（艦隊に出せない）／艦隊配属可＝卒業して任に就ける。</summary>
    public enum MilitaryPosting { 学校配属, 艦隊配属可 }

    /// <summary>
    /// 学校配属ゲートの純ロジック（#SCHOOL-AGE 延長・test-first・唯一の窓口）。
    /// <b>ネームドが学校に在学している間は「学校配属」となり艦隊配属できない</b>＝卒業して初めて艦隊（梯団）に出せる。
    /// 在学判定は (a) 入学〜卒業の年齢窓（<see cref="SchoolAgeRules"/>）または (b) 卒業年がまだ未来か（<see cref="Person.graduationYear"/>）。
    /// 配属窓口（<see cref="FleetRoster.AssignAdmiral"/>／<see cref="OrderOfBattle.AssignCommander"/>）が候補の在学を見て弾く前提
    /// （ネームド↔提督データの紐付けは配線層・本モジュールは read-only な判定のみ）。状態は変えない。
    /// </summary>
    public static class SchoolPostingRules
    {
        /// <summary>
        /// 在学中か＝年齢が入学〜卒業の窓 [EntryAge, GraduationAge) に入る（<paramref name="school"/> の課程）。
        /// 生年未設定（BirthYear≤0）や null は判定不能＝在学でない（後方互換＝従来どおり配属可）。
        /// </summary>
        public static bool IsEnrolled(ICharacter c, int currentYear, SchoolType school)
        {
            if (c == null || c.BirthYear <= 0) return false;
            int age = LifecycleRules.Age(c, currentYear);
            return age >= SchoolAgeRules.EntryAge(school) && age < SchoolAgeRules.GraduationAge(school);
        }

        /// <summary>在学中か＝卒業年がまだ未来（currentYear &lt; graduationYear）。graduationYear≤0（未設定）は在学でない。</summary>
        public static bool IsEnrolledByGraduationYear(int graduationYear, int currentYear)
            => graduationYear > 0 && currentYear < graduationYear;

        /// <summary>在学中か（人物の卒業年で判定＝学生として作られたネームドは卒業年が未来）。</summary>
        public static bool IsEnrolled(Person p, int currentYear)
            => p != null && IsEnrolledByGraduationYear(p.graduationYear, currentYear);

        /// <summary>配属区分（年齢窓版）。在学中＝学校配属／それ以外＝艦隊配属可。</summary>
        public static MilitaryPosting PostingOf(ICharacter c, int currentYear, SchoolType school)
            => IsEnrolled(c, currentYear, school) ? MilitaryPosting.学校配属 : MilitaryPosting.艦隊配属可;

        /// <summary>配属区分（卒業年版）。</summary>
        public static MilitaryPosting PostingOf(Person p, int currentYear)
            => IsEnrolled(p, currentYear) ? MilitaryPosting.学校配属 : MilitaryPosting.艦隊配属可;

        /// <summary>
        /// 艦隊（梯団）に配属できるか＝<b>在学中でなく、かつ就任可能（生存・自由＝<see cref="ICharacter.IsAvailable"/>）</b>。
        /// 学校配属の間は false（在学中は艦隊配属不可）。年齢窓版。
        /// </summary>
        public static bool CanAssignToFleet(ICharacter c, int currentYear, SchoolType school)
            => c != null && c.IsAvailable && !IsEnrolled(c, currentYear, school);

        /// <summary>艦隊配属できるか（卒業年版）。在学中＝不可。</summary>
        public static bool CanAssignToFleet(Person p, int currentYear)
            => p != null && p.IsAvailable && !IsEnrolled(p, currentYear);
    }
}
