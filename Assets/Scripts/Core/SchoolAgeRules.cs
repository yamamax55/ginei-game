namespace Ginei
{
    /// <summary>
    /// 学校の種別（教育チェーン＋軍学校＋登用試験）。<see cref="SchoolAgeRules"/> の入学/卒業年齢の引き。
    /// </summary>
    public enum SchoolType
    {
        保育園, 幼稚園, 小学校, 中学校, 高校,   // 一般教育チェーン（就学前〜後期中等）
        高専, 短大, 専門学校, 大学,            // 高等教育
        幼年学校, 士官学校, 陸軍大学校,         // 軍（将校）の学校
        新兵訓練, 下士官学校,                  // 軍（兵・下士官）の教育（RECRUIT/NCOEDU）
        科挙                                  // 文官登用試験（年齢制限なし）
    }

    /// <summary>
    /// 各学校の入学年齢・修業年限・卒業年齢の<b>史実ベースの単一窓口</b>（教育チェーン #155-157／軍学校／科挙）。
    /// 従来は各 *Rules に <c>GraduationAge</c> 定数が散在していた（士官学校22/大学24/高専・短大・専門20）。ここへ集約し、
    /// <b>入学年齢を史実に即して精緻化</b>する（現代日本の学制＋旧日本軍の学校）。卒業＝入学＋修業（科挙等の特例を除く）。
    /// 史実の要点：<b>陸軍大学校は現役将校が約28歳で選抜入校し約31歳で卒業</b>（青年学校でない）／陸軍幼年学校は約13歳入校／
    /// 科挙は<b>年齢制限なし</b>（童試は少年から、進士登用は平均30代）。純ロジック・test-first・状態は変えない。
    /// </summary>
    public static class SchoolAgeRules
    {
        /// <summary>入学年齢（史実ベース）。</summary>
        public static int EntryAge(SchoolType s)
        {
            switch (s)
            {
                case SchoolType.保育園:     return 0;   // 0歳〜（保育）
                case SchoolType.幼稚園:     return 3;   // 満3歳入園
                case SchoolType.小学校:     return 6;
                case SchoolType.中学校:     return 12;
                case SchoolType.高校:       return 15;
                case SchoolType.高専:       return 15;  // 中学から5年制
                case SchoolType.短大:       return 18;  // 高校卒後
                case SchoolType.専門学校:   return 18;  // 高校卒後
                case SchoolType.大学:       return 18;  // 高校卒後（学部）
                case SchoolType.幼年学校:   return 13;  // 陸軍幼年学校＝高等小学校卒程度
                case SchoolType.士官学校:   return 16;  // 予科入校相当
                case SchoolType.陸軍大学校: return 28;  // 現役将校（中尉〜大尉）が選抜入校
                case SchoolType.新兵訓練:   return 18;  // 入隊（史実の徴兵年齢は20、現代は18）
                case SchoolType.下士官学校: return 24;  // 経験ある兵から（PMEは在職中）
                case SchoolType.科挙:       return 15;  // 童試の典型（年齢制限なし＝下記 IsAgeCapped）
                default:                    return 18;
            }
        }

        /// <summary>卒業（修了/登用）年齢（史実ベース）。</summary>
        public static int GraduationAge(SchoolType s)
        {
            switch (s)
            {
                case SchoolType.保育園:     return 6;
                case SchoolType.幼稚園:     return 6;
                case SchoolType.小学校:     return 12;
                case SchoolType.中学校:     return 15;
                case SchoolType.高校:       return 18;
                case SchoolType.高専:       return 20;  // 5年制
                case SchoolType.短大:       return 20;  // 2年制
                case SchoolType.専門学校:   return 20;  // 2年制
                case SchoolType.大学:       return 22;  // 学部4年（旧24から史実精緻化）
                case SchoolType.幼年学校:   return 16;
                case SchoolType.士官学校:   return 22;  // 予科＋本科
                case SchoolType.陸軍大学校: return 31;  // 約3年＝最古参の参謀（旧22から史実精緻化）
                case SchoolType.新兵訓練:   return 18;  // 短期（月単位は RecruitTrainingRules.TrainingMonths）
                case SchoolType.下士官学校: return 24;
                case SchoolType.科挙:       return 30;  // 進士登用の典型年齢（年齢制限なし）
                default:                    return 22;
            }
        }

        /// <summary>修業年限＝卒業年齢−入学年齢（科挙は受験を重ねる長い道）。</summary>
        public static int DurationYears(SchoolType s) => GraduationAge(s) - EntryAge(s);

        /// <summary>入学/卒業に上限年齢の制約があるか。科挙のみ<b>年齢制限なし</b>（false）＝何歳でも受験できる。</summary>
        public static bool IsAgeCapped(SchoolType s) => s != SchoolType.科挙;

        /// <summary>
        /// 軍学歴（<see cref="MilitaryDegree"/>）→卒業年齢。<b>大学校卒（参謀）は約31歳・士官学校卒は22歳・幼年学校卒/退校は約16歳</b>
        /// ＝同一会戦年でも到達学歴が高いほど年長になる（<see cref="MilitaryAcademyRules"/> の生年逆算が学歴別に精緻化される）。
        /// </summary>
        public static int GraduationAgeForDegree(MilitaryDegree degree)
        {
            switch (degree)
            {
                case MilitaryDegree.大学校卒:   return GraduationAge(SchoolType.陸軍大学校); // 31
                case MilitaryDegree.士官学校卒: return GraduationAge(SchoolType.士官学校);   // 22
                case MilitaryDegree.幼年学校卒: return GraduationAge(SchoolType.幼年学校);   // 16
                default:                        return GraduationAge(SchoolType.幼年学校);   // 退校＝若くして去る
            }
        }
    }
}
