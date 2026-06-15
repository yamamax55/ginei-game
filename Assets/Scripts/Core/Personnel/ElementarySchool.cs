namespace Ginei
{
    /// <summary>
    /// 小学校＝初等教育（#155-157 の土台の根・中学校 <see cref="MiddleSchool"/> の下層）。<b>ほぼ全員が受ける基礎教育</b>で、
    /// 識字・基礎学力の裾野を作る＝教育チェーン（小学校→中学校→高校→上級学校）の根。就学率が高いほど人材の母数が太り、
    /// 質が高いほど素質の底上げ（最も基礎的＝寄与は小）。解決は <see cref="ElementarySchoolRules"/>。勢力の教育政策レイヤー。純データ。
    /// </summary>
    [System.Serializable]
    public class ElementarySchool
    {
        public int schoolId;
        public Faction faction;
        public string name = "小学校";

        /// <summary>就学率（0..1・学齢児童が小学校へ通う割合）。中学校より高い（義務教育＝ほぼ全員）。</summary>
        public float enrollmentRate = 0.95f;

        /// <summary>教育の質（0..1）。基礎学力の底上げ（寄与は最小＝最も基礎的）。</summary>
        public float quality = 0.5f;

        public ElementarySchool() { }

        public ElementarySchool(int schoolId, Faction faction, string name, float enrollmentRate = 0.95f, float quality = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "小学校" : name;
            this.enrollmentRate = enrollmentRate;
            this.quality = quality;
        }
    }
}
