using UnityEngine;

namespace Ginei
{
    /// <summary>戦犯裁判の判決区分（軽い順）。</summary>
    public enum TrialOutcome { 無罪, 減刑, 有罪, 極刑 }

    /// <summary>
    /// 指揮系統における一個人の責任連鎖の純データ（#1536）。
    /// 上下関係（指揮階層）と裁量の有無で「命令に従っただけ」が免責になるかが決まる。
    /// </summary>
    [System.Serializable]
    public struct AccountabilityChain
    {
        /// <summary>指揮階層（0..1・1で最高司令、0で末端兵）。上官ほど重い。</summary>
        public float commandRank;
        /// <summary>裁量（0..1・1で自由意思があった、0で完全に強制された）。</summary>
        public float discretion;
        /// <summary>関与度（0..1・1で自ら手を下した、0で無関与）。</summary>
        public float participation;

        public AccountabilityChain(float commandRank, float discretion, float participation)
        {
            this.commandRank = Mathf.Clamp01(commandRank);
            this.discretion = Mathf.Clamp01(discretion);
            this.participation = Mathf.Clamp01(participation);
        }
    }

    /// <summary>戦争犯罪・責任連鎖の調整係数。</summary>
    public readonly struct WarCrimesParams
    {
        /// <summary>個人有責性で指揮階層が占める重み（上官の地位そのものの責任）。</summary>
        public readonly float rankWeight;
        /// <summary>個人有責性で裁量が占める重み（選べたのに選んだ責任）。</summary>
        public readonly float discretionWeight;
        /// <summary>個人有責性で関与度が占める重み（実際に手を下した責任）。</summary>
        public readonly float participationWeight;
        /// <summary>「命令に従っただけ」抗弁が成立しうる裁量の上限閾値（これ以下で一部成立）。</summary>
        public readonly float obedienceThreshold;
        /// <summary>明白に違法な命令と見なす違法度の閾値（これ以上は従う義務なし＝抗弁不成立）。</summary>
        public readonly float manifestThreshold;
        /// <summary>強要・脅迫による減刑の最大幅（0..1・これだけ有責性を割り引ける）。</summary>
        public readonly float maxDuressMitigation;
        /// <summary>減刑後も残る最終的な良心の下限（有責性をゼロにはしない床）。</summary>
        public readonly float culpabilityFloor;
        /// <summary>有罪（極刑未満）と判定する有責性×証拠スコアの閾値。</summary>
        public readonly float convictThreshold;
        /// <summary>極刑と判定するスコアの閾値（convictThreshold より高い）。</summary>
        public readonly float capitalThreshold;

        public WarCrimesParams(float rankWeight, float discretionWeight, float participationWeight,
                               float obedienceThreshold, float manifestThreshold, float maxDuressMitigation,
                               float culpabilityFloor, float convictThreshold, float capitalThreshold)
        {
            this.rankWeight = Mathf.Max(0f, rankWeight);
            this.discretionWeight = Mathf.Max(0f, discretionWeight);
            this.participationWeight = Mathf.Max(0f, participationWeight);
            this.obedienceThreshold = Mathf.Clamp01(obedienceThreshold);
            this.manifestThreshold = Mathf.Clamp01(manifestThreshold);
            this.maxDuressMitigation = Mathf.Clamp01(maxDuressMitigation);
            this.culpabilityFloor = Mathf.Clamp01(culpabilityFloor);
            this.convictThreshold = Mathf.Clamp01(convictThreshold);
            this.capitalThreshold = Mathf.Clamp01(capitalThreshold);
        }

        /// <summary>
        /// 既定＝階層重0.4・裁量重0.3・関与重0.3（合計1）・服従閾値0.3・明白違法閾値0.7・
        /// 強要減刑上限0.5・良心の床0.1・有罪閾値0.4・極刑閾値0.75。
        /// </summary>
        public static WarCrimesParams Default => new WarCrimesParams(
            0.4f, 0.3f, 0.3f, 0.3f, 0.7f, 0.5f, 0.1f, 0.4f, 0.75f);
    }

    /// <summary>
    /// 戦争犯罪の責任連鎖と個人有責性の純ロジック（#1536・アーレント／ニュルンベルク裁判の問い）。
    /// 組織犯罪の責任を「命令を下した上官・実行した部下・黙認した傍観者」へ階層の上下と裁量の有無で
    /// 配分する。核は **「命令に従っただけ（superior orders）の抗弁は通用するか」**＝裁量がなく強制
    /// された場合のみ一部成立し、裁量ある関与や誰が見ても明白に違法な命令には成立しない＝免責されない。
    /// 上官は部下の犯罪を知り得たのに止めなければ上官責任（command responsibility）を負い、違法命令を
    /// 下した命令者が最も重い。<see cref="TribunalRules"/>（法廷の正統性・区切り＝裁きの政治）とは別で、
    /// こちらは裁く前の「誰がどれだけ有責か」の配分。<see cref="AtrocityRules"/>（虐殺の実行そのもの）の
    /// 後段＝その罪を指揮系統の誰に帰すか。個別の処断・処遇は <see cref="CaptivityRules"/> へ委譲。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WarCrimesRules
    {
        /// <summary>
        /// 個人の有責性（0..1）＝指揮階層×裁量×関与の重み付き和。上官ほど・裁量があったほど・
        /// 関与したほど重い。重みは正規化して合計1の尺度に揃える（合計0なら0を返す）。
        /// </summary>
        public static float IndividualCulpability(float commandRank, float discretion, float participation, WarCrimesParams p)
        {
            float r = Mathf.Clamp01(commandRank);
            float d = Mathf.Clamp01(discretion);
            float pa = Mathf.Clamp01(participation);
            float total = p.rankWeight + p.discretionWeight + p.participationWeight;
            if (total <= 0f) return 0f;
            float sum = r * p.rankWeight + d * p.discretionWeight + pa * p.participationWeight;
            return Mathf.Clamp01(sum / total);
        }

        public static float IndividualCulpability(float commandRank, float discretion, float participation)
            => IndividualCulpability(commandRank, discretion, participation, WarCrimesParams.Default);

        /// <summary>責任連鎖から個人有責性を引く糖衣。</summary>
        public static float IndividualCulpability(AccountabilityChain chain, WarCrimesParams p)
            => IndividualCulpability(chain.commandRank, chain.discretion, chain.participation, p);

        public static float IndividualCulpability(AccountabilityChain chain)
            => IndividualCulpability(chain, WarCrimesParams.Default);

        /// <summary>
        /// 「命令に従っただけ」抗弁の一部成立判定。裁量 discretion が threshold 以下＝選択の余地が
        /// なく強制された場合のみ true（裁量があれば免責されない＝アーレントの問いの核）。
        /// </summary>
        public static bool CanClaimObedience(float discretion, float threshold)
        {
            return Mathf.Clamp01(discretion) <= Mathf.Clamp01(threshold);
        }

        public static bool CanClaimObedience(float discretion)
            => CanClaimObedience(discretion, WarCrimesParams.Default.obedienceThreshold);

        /// <summary>
        /// 上官責任（command responsibility・0..1）＝指揮階層×（部下の犯罪を知り得た度 knewOrShould）。
        /// 知り得たのに止めなかった指揮官は、自ら手を下さずとも責任を負う＝地位は免罪符ではない。
        /// </summary>
        public static float SuperiorResponsibility(float commandRank, float knewOrShould)
        {
            return Mathf.Clamp01(commandRank) * Mathf.Clamp01(knewOrShould);
        }

        /// <summary>
        /// 違法命令を下した者の責任（0..1）＝指揮階層×命令の違法度 orderIllegality。
        /// 命令者は連鎖の頂点＝最も重い（実行者は命令されたが、命令者は自ら違法を選んだ）。
        /// </summary>
        public static float OrderGiverCulpability(float commandRank, float orderIllegality)
        {
            return Mathf.Clamp01(commandRank) * Mathf.Clamp01(orderIllegality);
        }

        /// <summary>
        /// 傍観者の共犯性（0..1）＝関与度の低さを止める力で埋める＝（1−関与）成分は無視し、
        /// 「認識 awareness × 止める力 power × 黙認分」で測る。止める力がありながら知って黙認した者ほど重い
        /// （関与が高ければそれ自体が実行者寄りの有責性＝共犯性は黙認分 (1−participation) に掛かる）。
        /// </summary>
        public static float BystanderComplicity(float participation, float awareness, float power)
        {
            float condoned = 1f - Mathf.Clamp01(participation);
            return condoned * Mathf.Clamp01(awareness) * Mathf.Clamp01(power);
        }

        /// <summary>
        /// 明白に違法な命令か（誰が見ても従う義務がない＝抗弁不成立）。違法度 orderIllegality が
        /// threshold 以上で true＝この命令に「従っただけ」は通らない。
        /// </summary>
        public static bool IsManifestlyIllegal(float orderIllegality, float threshold)
        {
            return Mathf.Clamp01(orderIllegality) >= Mathf.Clamp01(threshold);
        }

        public static bool IsManifestlyIllegal(float orderIllegality)
            => IsManifestlyIllegal(orderIllegality, WarCrimesParams.Default.manifestThreshold);

        /// <summary>
        /// 判決＝有責性 individualCulpability×証拠の強さ evidenceStrength のスコアで <see cref="TrialOutcome"/> を返す。
        /// 証拠が弱ければ有責でも無罪（疑わしきは罰せず）、スコアが極刑閾値を超えれば極刑。
        /// </summary>
        public static TrialOutcome TrialVerdict(float individualCulpability, float evidenceStrength, WarCrimesParams p)
        {
            float score = Mathf.Clamp01(individualCulpability) * Mathf.Clamp01(evidenceStrength);
            if (score >= p.capitalThreshold) return TrialOutcome.極刑;
            if (score >= p.convictThreshold) return TrialOutcome.有罪;
            // 有罪閾値未満でも証拠・有責性がそれなりにあれば減刑、ごく薄ければ無罪。
            if (score >= p.convictThreshold * 0.5f) return TrialOutcome.減刑;
            return TrialOutcome.無罪;
        }

        public static TrialOutcome TrialVerdict(float individualCulpability, float evidenceStrength)
            => TrialVerdict(individualCulpability, evidenceStrength, WarCrimesParams.Default);

        /// <summary>
        /// 強要・脅迫下での減刑後の有責性。duress(0..1) に応じ最大 maxDuressMitigation まで割り引くが、
        /// culpabilityFloor を下回らない＝最終的な良心は残る（脅されても人を殺さない選択はあった）。
        /// </summary>
        public static float MitigationFromDuress(float culpability, float duress, WarCrimesParams p)
        {
            float c = Mathf.Clamp01(culpability);
            float relief = Mathf.Clamp01(duress) * p.maxDuressMitigation;
            float floor = Mathf.Min(c, p.culpabilityFloor);
            return Mathf.Max(floor, c * (1f - relief));
        }

        public static float MitigationFromDuress(float culpability, float duress)
            => MitigationFromDuress(culpability, duress, WarCrimesParams.Default);
    }
}
