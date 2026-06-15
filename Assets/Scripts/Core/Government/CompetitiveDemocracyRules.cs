using UnityEngine;

namespace Ginei
{
    /// <summary>競争的民主主義の調整係数（シュンペーター型・SCHU-6 #1598）。</summary>
    public readonly struct CompetitiveDemocracyParams
    {
        /// <summary>創造的破壊の置換ショックを社会的セーフティが和らげる効き（1で全吸収）。</summary>
        public readonly float safetyCushion;
        /// <summary>置換ショックが扇動の隙へ換わる基礎係数。</summary>
        public readonly float openingScale;
        /// <summary>制度不信が扇動の隙を増幅する率（不信が深いほど隙が開く）。</summary>
        public readonly float distrustAmplifier;
        /// <summary>扇動圧が民主的品質を削る速度（per dt・扇動圧1のとき）。</summary>
        public readonly float erosionRate;

        public CompetitiveDemocracyParams(float safetyCushion, float openingScale, float distrustAmplifier, float erosionRate)
        {
            this.safetyCushion = Mathf.Clamp01(safetyCushion);
            this.openingScale = Mathf.Max(0f, openingScale);
            this.distrustAmplifier = Mathf.Max(0f, distrustAmplifier);
            this.erosionRate = Mathf.Max(0f, erosionRate);
        }

        /// <summary>既定＝セーフティ吸収0.6・隙係数0.7・不信増幅1.0・品質浸食0.05。</summary>
        public static CompetitiveDemocracyParams Default => new CompetitiveDemocracyParams(0.6f, 0.7f, 1f, 0.05f);
    }

    /// <summary>
    /// 競争的民主主義と経済置換の純ロジック（シュンペーター『資本主義・社会主義・民主主義』・SCHU-6 #1598）。
    /// 民主主義は理念でなく<b>票を競う手続き＝指導者を選ぶ競争的市場</b>であり、その健全さは候補の質と競争の開放度で決まる。
    /// 一方、創造的破壊の<b>経済の置換ショック</b>（失業・没落）が不満を生み、制度不信が深いほど扇動政治家に隙を与え、
    /// その隙が競争の健全さを蝕んで民主的品質を劣化させる＝経済の創造的破壊が政治の品質を下げる経路を式に出す。
    /// 分担：扇動家自身の訴求力は <see cref="DemagogueRules"/>、党勢・最小選挙は <see cref="PartyRules"/>、
    /// 置換ショックの出所（創造的破壊そのもの）は同 EPIC SCHU の <c>CreativeDestructionRules</c> が持つ。
    /// ここは<b>置換ショック→扇動の隙→民主的品質の劣化</b>という競争的民主主義の経済起点を担う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CompetitiveDemocracyRules
    {
        /// <summary>
        /// 選挙競争の健全度（0..1）＝候補の質 candidateQuality(0..1)×競争の開放度 contestability(0..1)。
        /// 民主主義＝指導者を選ぶ競争市場であり、質の高い候補が開かれた競争で争うほど健全。
        /// どちらかが欠ければ（無投票・談合）競争は機能しない（積）。
        /// </summary>
        public static float ElectoralCompetition(float candidateQuality, float contestability)
        {
            return Mathf.Clamp01(candidateQuality) * Mathf.Clamp01(contestability);
        }

        /// <summary>
        /// 置換ショック（0..1）＝創造的破壊 creativeDestruction(0..1) のうち、社会的セーフティ socialSafety(0..1)
        /// で和らげきれずに残る失業・没落のショック。セーフティが厚いほど（safetyCushion 倍）ショックは吸収される。
        /// </summary>
        public static float DisplacementShock(float creativeDestruction, float socialSafety, CompetitiveDemocracyParams p)
        {
            float raw = Mathf.Clamp01(creativeDestruction);
            float absorbed = raw * Mathf.Clamp01(socialSafety) * p.safetyCushion;
            return Mathf.Clamp01(raw - absorbed);
        }

        public static float DisplacementShock(float creativeDestruction, float socialSafety)
            => DisplacementShock(creativeDestruction, socialSafety, CompetitiveDemocracyParams.Default);

        /// <summary>
        /// 扇動政治家の隙（0..1）＝置換ショック displacementShock(0..1)×係数×（1＋制度不信(1−trust)×増幅）。
        /// 置換ショックが大きく制度不信が深い（institutionalTrust が低い）ほど、扇動が動員できる隙が広がる。
        /// </summary>
        public static float DemagogueOpening(float displacementShock, float institutionalTrust, CompetitiveDemocracyParams p)
        {
            float shock = Mathf.Clamp01(displacementShock);
            float distrust = 1f - Mathf.Clamp01(institutionalTrust);
            return Mathf.Clamp01(shock * p.openingScale * (1f + distrust * p.distrustAmplifier));
        }

        public static float DemagogueOpening(float displacementShock, float institutionalTrust)
            => DemagogueOpening(displacementShock, institutionalTrust, CompetitiveDemocracyParams.Default);

        /// <summary>
        /// 民主的品質（0..1）＝選挙競争の健全度 electoralCompetition(0..1) を、扇動の隙 demagogueOpening(0..1)
        /// が蝕んだ残り＝competition×(1−opening)。健全な競争があっても扇動の隙が広いほど品質は落ちる。
        /// </summary>
        public static float DemocraticQuality(float electoralCompetition, float demagogueOpening)
        {
            return Mathf.Clamp01(Mathf.Clamp01(electoralCompetition) * (1f - Mathf.Clamp01(demagogueOpening)));
        }

        /// <summary>
        /// 品質劣化（per dt 適用後の品質 0..1）＝扇動圧 demagoguePressure(0..1)×浸食率×dt の分だけ品質が削られる。
        /// 扇動が要職を占め続けるほど、民主的品質は時間とともに静かに下がる。
        /// </summary>
        public static float QualityErosionTick(float quality, float demagoguePressure, float dt, CompetitiveDemocracyParams p)
        {
            float erosion = Mathf.Clamp01(demagoguePressure) * p.erosionRate * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(quality) - erosion);
        }

        public static float QualityErosionTick(float quality, float demagoguePressure, float dt)
            => QualityErosionTick(quality, demagoguePressure, dt, CompetitiveDemocracyParams.Default);

        /// <summary>
        /// 為政者の説明責任（0..1）＝民主的品質 quality(0..1)×透明性 transparency(0..1)。
        /// 品質が高く情報が開かれているほど、選挙で為政者の責任を問える（手続き的説明責任）。
        /// </summary>
        public static float AccountabilityStrength(float quality, float transparency)
        {
            return Mathf.Clamp01(quality) * Mathf.Clamp01(transparency);
        }

        /// <summary>
        /// 民主主義の後退判定＝民主的品質 quality が閾値 threshold を下回ったか（手続きが空洞化＝バックスライディング）。
        /// </summary>
        public static bool IsDemocraticBacksliding(float quality, float threshold)
        {
            return Mathf.Clamp01(quality) < Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// 制度の頑健性（0..1）＝制度信頼 institutionalTrust(0..1) と選挙競争 electoralCompetition(0..1) の積。
        /// 強い制度と機能する競争がそろう国だけが、置換ショックの扇動に耐える（どちらか欠ければ脆い）。
        /// </summary>
        public static float ResilienceFromInstitutions(float institutionalTrust, float electoralCompetition)
        {
            return Mathf.Clamp01(institutionalTrust) * Mathf.Clamp01(electoralCompetition);
        }
    }
}
