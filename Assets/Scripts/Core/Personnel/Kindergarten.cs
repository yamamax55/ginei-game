namespace Ginei
{
    /// <summary>
    /// 幼稚園＝就学前教育（#155-157 の土台の最下根・小学校 <see cref="ElementarySchool"/> の下層）。基礎学力以前の<b>就学前の教育</b>で、
    /// 教育チェーン（幼稚園→小学校→中学校→高校→上級学校）の根を更に下へ伸ばす。寄与は最小（最も基礎的）。
    /// 保育（働く親の支援）を担う保育園 <see cref="Nursery"/> とは役割が別（こちらは<b>教育</b>）。解決は <see cref="KindergartenRules"/>。純データ。
    /// </summary>
    [System.Serializable]
    public class Kindergarten
    {
        public int schoolId;
        public Faction faction;
        public string name = "幼稚園";

        /// <summary>就園率（0..1・就学前児童が幼稚園へ通う割合）。</summary>
        public float enrollmentRate = 0.7f;

        /// <summary>教育の質（0..1）。素質の底上げ（寄与は最小）。</summary>
        public float quality = 0.5f;

        public Kindergarten() { }

        public Kindergarten(int schoolId, Faction faction, string name, float enrollmentRate = 0.7f, float quality = 0.5f)
        {
            this.schoolId = schoolId;
            this.faction = faction;
            this.name = string.IsNullOrEmpty(name) ? "幼稚園" : name;
            this.enrollmentRate = enrollmentRate;
            this.quality = quality;
        }
    }
}
