namespace Ginei
{
    /// <summary>
    /// 中学校＝前期中等教育（#155-157 の土台・高校 <see cref="HighSchool"/> の下層）。若年を教育し、<b>高校への進学母数</b>と
    /// 候補の素質の底上げに効く。中学校→高校→上級学校（士官学校/大学/科挙）と<b>進学率が複利で掛かる</b>＝裾野の教育が
    /// 人材の母数を決める。解決は <see cref="MiddleSchoolRules"/>。勢力の教育政策レイヤー。純データ。
    /// </summary>
    [System.Serializable]
    public class MiddleSchool
    {
        public int schoolId;
        public Faction faction;
        public string name = "中学校";

        /// <summary>進学率（0..1・若年が中学校→高校へ進む割合）。高校より高めが普通（裾野が広い）。</summary>
        public float enrollmentRate = 0.8f;

        /// <summary>教育の質（0..1）。素質の底上げ（高校より寄与は小さい＝より基礎的）。</summary>
        public float quality = 0.5f;

        public MiddleSchool() { }

        public MiddleSchool(int schoolId, Faction faction, string name, float enrollmentRate = 0.8f, float quality = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "中学校" : name;
            this.enrollmentRate = enrollmentRate;
            this.quality = quality;
        }
    }
}
