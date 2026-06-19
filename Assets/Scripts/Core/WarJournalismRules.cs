using UnityEngine;

namespace Ginei
{
    /// <summary>戦争報道（戦況報道が銃後の世論に与える影響）の調整係数。</summary>
    public readonly struct WarJournalismParams
    {
        /// <summary>戦勝報道の盛り上げスケール（戦果×誇張1で生む高揚の最大値）。</summary>
        public readonly float hypeScale;
        /// <summary>誇大報道の発覚反動スケール（誇張×乖離1で生む反動の最大値）。</summary>
        public readonly float backlashScale;
        /// <summary>敗北報道の銃後士気への打撃スケール（深刻さ1で生む打撃の最大値）。</summary>
        public readonly float defeatImpactScale;
        /// <summary>報道の自由度が敗北を増幅する強さ（自由ほど敗報が銃後に響く・0..1）。</summary>
        public readonly float freedomDefeatAmplify;
        /// <summary>犠牲が戦争支持を削る強さ（犠牲1で削る支持の最大値）。</summary>
        public readonly float casualtyWeight;

        public WarJournalismParams(float hypeScale, float backlashScale, float defeatImpactScale,
                                   float freedomDefeatAmplify, float casualtyWeight)
        {
            this.hypeScale = Mathf.Clamp01(hypeScale);
            this.backlashScale = Mathf.Clamp01(backlashScale);
            this.defeatImpactScale = Mathf.Clamp01(defeatImpactScale);
            this.freedomDefeatAmplify = Mathf.Clamp01(freedomDefeatAmplify);
            this.casualtyWeight = Mathf.Clamp01(casualtyWeight);
        }

        /// <summary>既定＝高揚0.7・反動0.6・敗北打撃0.8・自由増幅0.5・犠牲重み0.5。</summary>
        public static WarJournalismParams Default
            => new WarJournalismParams(0.7f, 0.6f, 0.8f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 戦争報道の純ロジック（戦況の報じ方が銃後＝ホームフロントの世論に与える影響）。
    /// 戦況をどう伝えるか（勝報/敗報）×報道の自由度、従軍記者の現地報道、検閲と実態の乖離、
    /// 戦果の誇張とその発覚反動（嘘がばれる）、敗北報道の士気打撃、報道による厭戦/主戦の世論形成。
    /// <para>
    /// 分担：<c>MoraleContagionRules</c>（前線部隊間の士気伝染）が<b>戦場</b>を扱うのに対し、
    /// こちらは<b>銃後の世論</b>（戦勝の高揚−敗北の打撃−誇大報道の反動−犠牲の重み）を扱う。
    /// 符号付き世論は -1（厭戦）..1（主戦）。盤面非依存の plain 引数・実効値パターン。
    /// </para>
    /// 乱数は持たない（決定論）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WarJournalismRules
    {
        /// <summary>
        /// 戦争報道の内容（-1..1）＝戦況 battleOutcome(-1 大敗..1 大勝) を報道の自由度で色づける。
        /// 報道が不自由（検閲下）ほど勝報に偏り敗報を薄める（min を 1−自由度 で底上げ）、
        /// 自由ほど戦況そのままを伝える。自由度 reportingFreedom(0..1)。
        /// </summary>
        public static float WarReporting(float battleOutcome, float reportingFreedom, WarJournalismParams p)
        {
            float outcome = Mathf.Clamp(battleOutcome, -1f, 1f);
            float freedom = Mathf.Clamp01(reportingFreedom);
            // 検閲下（freedom 低）は戦況を勝ち寄りに歪める＝下限を 1 へ引き上げる量で持ち上げる。
            float spun = Mathf.Lerp(1f, outcome, freedom);
            return Mathf.Clamp(spun, -1f, 1f);
        }

        public static float WarReporting(float battleOutcome, float reportingFreedom)
            => WarReporting(battleOutcome, reportingFreedom, WarJournalismParams.Default);

        /// <summary>
        /// 戦勝報道の盛り上げ（0..hypeScale）＝実際の戦果 actualVictory(0..1) を誇張 propagandaSpin(0..1) で増幅。
        /// 戦果が無ければ盛り上げも無い（actualVictory=0 で 0）。誇張ほど戦果以上に沸かせる。
        /// </summary>
        public static float VictoryHype(float actualVictory, float propagandaSpin, WarJournalismParams p)
        {
            float victory = Mathf.Clamp01(actualVictory);
            float spin = Mathf.Clamp01(propagandaSpin);
            // 戦果に誇張ぶんを上乗せ（1+spin 倍まで）。戦果0なら盛り上がらない。
            return Mathf.Clamp01(victory * (1f + spin)) * p.hypeScale;
        }

        public static float VictoryHype(float actualVictory, float propagandaSpin)
            => VictoryHype(actualVictory, propagandaSpin, WarJournalismParams.Default);

        /// <summary>
        /// 報道と実態の乖離（0..1）＝報じた戦況 reportedSituation(-1..1) と実際 actualSituation(-1..1) の差の半分。
        /// 検閲で勝ちを盛り実態が負なら大きく開く。0=報道が実態どおり。
        /// </summary>
        public static float CensorshipGap(float reportedSituation, float actualSituation, WarJournalismParams p)
        {
            float reported = Mathf.Clamp(reportedSituation, -1f, 1f);
            float actual = Mathf.Clamp(actualSituation, -1f, 1f);
            // 差の範囲は -2..2 ＝半分にして 0..1 へ。
            return Mathf.Clamp01(Mathf.Abs(reported - actual) * 0.5f);
        }

        public static float CensorshipGap(float reportedSituation, float actualSituation)
            => CensorshipGap(reportedSituation, actualSituation, WarJournalismParams.Default);

        /// <summary>
        /// 誇大報道の発覚反動（0..backlashScale）＝盛り上げ victoryHype(0..1)×乖離 censorshipGap(0..1)。
        /// 盛り上げと実態との開きが同時に大きいときだけ嘘がばれて世論が裏返る（積＝どちらか小なら反動も小）。
        /// </summary>
        public static float HypeBacklash(float victoryHype, float censorshipGap, WarJournalismParams p)
        {
            float hype = Mathf.Clamp01(victoryHype);
            float gap = Mathf.Clamp01(censorshipGap);
            return hype * gap * p.backlashScale;
        }

        public static float HypeBacklash(float victoryHype, float censorshipGap)
            => HypeBacklash(victoryHype, censorshipGap, WarJournalismParams.Default);

        /// <summary>
        /// 敗北報道の銃後士気打撃（0..defeatImpactScale）＝敗北の深刻さ defeatSeverity(0..1)×係数を、
        /// 報道の自由度 reportingFreedom(0..1) が増幅する（自由ほど敗報が銃後に響く＝検閲は打撃を和らげる）。
        /// </summary>
        public static float DefeatMoraleImpact(float defeatSeverity, float reportingFreedom, WarJournalismParams p)
        {
            float severity = Mathf.Clamp01(defeatSeverity);
            float freedom = Mathf.Clamp01(reportingFreedom);
            float amplify = 1f + freedom * p.freedomDefeatAmplify;
            return Mathf.Clamp01(severity * p.defeatImpactScale * amplify);
        }

        public static float DefeatMoraleImpact(float defeatSeverity, float reportingFreedom)
            => DefeatMoraleImpact(defeatSeverity, reportingFreedom, WarJournalismParams.Default);

        /// <summary>
        /// 銃後の世論（-1 厭戦..1 主戦）＝戦勝の高揚 victoryHype − 敗北の打撃 defeatMoraleImpact
        /// − 誇大報道の反動 hypeBacklash。沸けば主戦に振れ、敗報と嘘の発覚が厭戦に引く。
        /// </summary>
        public static float HomeFrontSentiment(float victoryHype, float defeatMoraleImpact,
                                               float hypeBacklash, WarJournalismParams p)
        {
            float hype = Mathf.Clamp01(victoryHype);
            float impact = Mathf.Clamp01(defeatMoraleImpact);
            float backlash = Mathf.Clamp01(hypeBacklash);
            return Mathf.Clamp(hype - impact - backlash, -1f, 1f);
        }

        public static float HomeFrontSentiment(float victoryHype, float defeatMoraleImpact, float hypeBacklash)
            => HomeFrontSentiment(victoryHype, defeatMoraleImpact, hypeBacklash, WarJournalismParams.Default);

        /// <summary>
        /// 戦争支持（-1..1）＝銃後の世論 homeFrontSentiment(-1..1) から犠牲 casualties(0..1)×係数を差し引く。
        /// 主戦の世論でも犠牲が嵩めば支持は削られる（厭戦へ）。
        /// </summary>
        public static float WarSupport(float homeFrontSentiment, float casualties, WarJournalismParams p)
        {
            float sentiment = Mathf.Clamp(homeFrontSentiment, -1f, 1f);
            float cost = Mathf.Clamp01(casualties) * p.casualtyWeight;
            return Mathf.Clamp(sentiment - cost, -1f, 1f);
        }

        public static float WarSupport(float homeFrontSentiment, float casualties)
            => WarSupport(homeFrontSentiment, casualties, WarJournalismParams.Default);

        /// <summary>戦争が支持されているか＝戦争支持 warSupport(-1..1) が閾値 threshold を超える。</summary>
        public static bool IsWarSupported(float warSupport, float threshold)
        {
            return Mathf.Clamp(warSupport, -1f, 1f) > Mathf.Clamp(threshold, -1f, 1f);
        }

        /// <summary>既定閾値0で委譲＝支持が正なら戦争は支持されている。</summary>
        public static bool IsWarSupported(float warSupport)
            => IsWarSupported(warSupport, 0f);
    }
}
