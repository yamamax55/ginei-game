namespace Ginei
{
    /// <summary>
    /// 徴募政策（兵力をどう集めるか）の純列挙（STSH-3 #2360）。
    /// 強制の度合いが上がるほど兵力供給は増えるが、社会の合意コスト（協力低下）も増える。
    /// <list type="bullet">
    ///   <item><see cref="自発"/>＝志願制。供給は細いが合意コストはほぼ無く正統性が高い。</item>
    ///   <item><see cref="選抜徴兵"/>＝選抜徴兵（くじ・免除あり）。供給と合意コストの中庸。</item>
    ///   <item><see cref="全員徴兵"/>＝全員徴兵（国民皆兵）。供給は最大だが合意コストが最大。</item>
    /// </list>
    /// 既定＝<see cref="自発"/>（後方互換＝従来は強制徴兵なし）。
    /// </summary>
    public enum RecruitmentPolicy
    {
        /// <summary>志願制（自発服役）。供給最小・合意コスト最小・正統性最大。</summary>
        自発 = 0,
        /// <summary>選抜徴兵（くじ・免除あり）。供給・合意コストともに中庸。</summary>
        選抜徴兵 = 1,
        /// <summary>全員徴兵（国民皆兵）。供給最大・合意コスト最大。</summary>
        全員徴兵 = 2,
    }
}
