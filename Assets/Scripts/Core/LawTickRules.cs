namespace Ginei
{
    /// <summary>法と秩序の1tickの結果（LAW-6・#2126）。犯罪圧力・公共秩序・安定デルタ・抑圧度。</summary>
    public struct LawTickResult
    {
        public float crimePressure; // 犯罪圧力
        public float orderLevel;    // 公共秩序
        public float stabilityDelta;// 安定度#109 への寄与
        public float repression;    // 抑圧度（法の支配なき取締り）
    }

    /// <summary>
    /// 法と秩序の暦境界オーケストレータ（LAW-6・#2126 配線・純ロジック）。惑星の犯罪→秩序→安定デルタと抑圧を1パスで解く薄い窓口。
    /// 各段は DIST/LAW-3/4/5 と `RuleOfLawEffectRules` へ委譲。test-first。
    /// </summary>
    public static class LawTickRules
    {
        public const float StabilityScale = 20f; // 秩序→安定の係数

        /// <summary>
        /// 1tick：失業/貧困/格差→犯罪圧力→（取締りで）公共秩序→安定デルタ、法の支配指数から抑圧度を求める。
        /// </summary>
        public static LawTickResult TickProvince(float ruleOfLawIndex, float unemployment, float poverty, float inequality,
            float enforcement, CrimeRules.CrimeParams cp)
        {
            var r = new LawTickResult();
            r.crimePressure = CrimeRules.CrimePressure(unemployment, poverty, inequality, cp);
            r.orderLevel = LawEnforcementRules.OrderLevel(r.crimePressure, enforcement);
            r.stabilityDelta = LawOrderEffectRules.StabilityDelta(r.orderLevel, StabilityScale);
            r.repression = LawOrderEffectRules.RepressionLevel(enforcement, ruleOfLawIndex);
            return r;
        }
    }
}
