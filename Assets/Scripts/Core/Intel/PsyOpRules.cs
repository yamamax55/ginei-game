using UnityEngine;

namespace Ginei
{
    /// <summary>積極的情報戦・世論戦の調整係数（#1386）。</summary>
    public readonly struct PsyOpParams
    {
        /// <summary>1tick の合意侵食の最大幅（per dt）。</summary>
        public readonly float maxErosion;
        /// <summary>1tick の信頼腐食の最大幅（per dt）。</summary>
        public readonly float maxCorrosion;
        /// <summary>防諜・情報リテラシーが心理作戦を防ぐ強さ（耐性係数）。</summary>
        public readonly float counterShield;
        /// <summary>露見が発信元の信用を焼く強さ（逆効果の係数）。</summary>
        public readonly float blowbackScale;
        /// <summary>合意崩壊とみなす侵食の閾値。</summary>
        public readonly float collapseThreshold;

        public PsyOpParams(float maxErosion, float maxCorrosion, float counterShield, float blowbackScale, float collapseThreshold)
        {
            this.maxErosion = Mathf.Max(0f, maxErosion);
            this.maxCorrosion = Mathf.Max(0f, maxCorrosion);
            this.counterShield = Mathf.Clamp01(counterShield);
            this.blowbackScale = Mathf.Max(0f, blowbackScale);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
        }

        /// <summary>既定＝最大侵食0.15・最大腐食0.12・防諜耐性0.7・露見逆効果1.5倍・崩壊閾値0.6。</summary>
        public static PsyOpParams Default => new PsyOpParams(0.15f, 0.12f, 0.7f, 1.5f, 0.6f);
    }

    /// <summary>
    /// 積極的情報戦・世論戦の純ロジック（ULW-3 #1386・限定戦争）。敵国の内部の合意・結束を蝕む心理作戦・偽情報＝
    /// 敵国民の戦意を削ぎ、政府への信頼を崩し、社会の分断を煽る「攻めの情報戦」。心理作戦の浸透は敵国民への
    /// 到達×受け入れやすさ（既存の不満があるほど刺さる）で決まり、浸透が敵内部の合意・結束を時間で蝕み、
    /// 厭戦を煽って戦意を削ぎ、指導部と国民の間に楔を打って政府への信頼を崩す。だが敵の防諜・情報リテラシーが
    /// 耐性になり、偽情報がバレて発信元が露見すると逆効果（信用失墜）になる。
    /// 自国世論を固める発信（<see cref="PropagandaRules"/>）・軍事的欺瞞で敵 AI の認識を歪める
    /// （<see cref="DeceptionRules"/>）・抑圧下で本音を隠す選好偽装（<see cref="PreferenceFalsificationRules"/>）とは別系統＝
    /// こちらは敵内部の合意・戦意・信頼を狙って崩す攻めの情報戦。同 EPIC ULW のハイブリッド戦
    /// （<see cref="HybridCampaignRules"/>）の心理戦コンポーネントに相当。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PsyOpRules
    {
        /// <summary>
        /// 心理作戦の浸透（0..1）＝敵国民への到達 reach(0..1)×受け入れやすさ targetReceptivity(0..1)。
        /// 既存の不満が大きい（受容性が高い）敵国民ほど偽情報・心理作戦が刺さる＝届いて受け入れられた分だけ浸透する。
        /// </summary>
        public static float MessagePenetration(float reach, float targetReceptivity)
        {
            return Mathf.Clamp01(Mathf.Clamp01(reach) * Mathf.Clamp01(targetReceptivity));
        }

        /// <summary>
        /// 敵国内部の合意・結束の1tick侵食量（0..maxErosion）＝浸透 penetration(0..1)×（1＋既存の分断 existingDivisions(0..1)）
        /// ×最大侵食×dt。既に社会に分断があるほど心理作戦が分断を煽って効く（楔は割れ目に打つほど深く入る）。
        /// </summary>
        public static float ConsensusErosion(float penetration, float existingDivisions, float dt, PsyOpParams p)
        {
            float pen = Mathf.Clamp01(penetration);
            float div = Mathf.Clamp01(existingDivisions);
            float d = Mathf.Max(0f, dt);
            return Mathf.Max(0f, pen * (1f + div) * p.maxErosion * d);
        }

        public static float ConsensusErosion(float penetration, float existingDivisions, float dt)
            => ConsensusErosion(penetration, existingDivisions, dt, PsyOpParams.Default);

        /// <summary>
        /// 偽情報の効果（0..1）＝もっともらしさ plausibility(0..1)×（1−事実検証 factCheckStrength(0..1)）×偽の物語の強さ falseNarrative(0..1)。
        /// もっともらしく、事実検証（ファクトチェック）が弱いほど効く＝検証が完璧なら偽情報は効かない。
        /// </summary>
        public static float DisinformationEffect(float falseNarrative, float plausibility, float factCheckStrength)
        {
            float e = Mathf.Clamp01(falseNarrative) * Mathf.Clamp01(plausibility) * (1f - Mathf.Clamp01(factCheckStrength));
            return Mathf.Clamp01(e);
        }

        /// <summary>
        /// 敵国民の戦意を削ぐ効果（0..1）＝合意侵食 consensusErosion(0..1)×敵の戦争支持 enemyWarSupport(0..1)。
        /// 厭戦を煽って内部から崩す＝合意が崩れるほど、そして元の戦意が高いほど削げる余地があり大きく削れる。
        /// </summary>
        public static float MoraleSubversion(float consensusErosion, float enemyWarSupport)
        {
            return Mathf.Clamp01(Mathf.Clamp01(consensusErosion) * Mathf.Clamp01(enemyWarSupport));
        }

        /// <summary>
        /// 敵政府への信頼の1tick腐食量（0..maxCorrosion）＝浸透 penetration(0..1)×（1−政府正統性 governmentLegitimacy(0..1)）
        /// ×最大腐食×dt。指導部と国民の間に楔を打つ＝正統性が低い政府ほど信頼を崩しやすい（正統な政府は楔が入らない）。
        /// </summary>
        public static float TrustCorrosion(float penetration, float governmentLegitimacy, float dt, PsyOpParams p)
        {
            float pen = Mathf.Clamp01(penetration);
            float vuln = 1f - Mathf.Clamp01(governmentLegitimacy);
            float d = Mathf.Max(0f, dt);
            return Mathf.Max(0f, pen * vuln * p.maxCorrosion * d);
        }

        public static float TrustCorrosion(float penetration, float governmentLegitimacy, float dt)
            => TrustCorrosion(penetration, governmentLegitimacy, dt, PsyOpParams.Default);

        /// <summary>
        /// 敵の防諜・情報リテラシーによる心理作戦への耐性（0..1）＝（事実検証 factCheckStrength(0..1)＋
        /// メディアリテラシー mediaLiteracy(0..1)）の合成を counterShield でスケール。検証力と見抜く力が高いほど
        /// 心理作戦・偽情報を防ぐ＝浸透・効果に対する将来ペナルティの大きさとして使う想定。
        /// </summary>
        public static float Counterintelligence(float factCheckStrength, float mediaLiteracy, PsyOpParams p)
        {
            float defense = 1f - (1f - Mathf.Clamp01(factCheckStrength)) * (1f - Mathf.Clamp01(mediaLiteracy));
            return Mathf.Clamp01(defense * p.counterShield);
        }

        public static float Counterintelligence(float factCheckStrength, float mediaLiteracy)
            => Counterintelligence(factCheckStrength, mediaLiteracy, PsyOpParams.Default);

        /// <summary>
        /// 偽情報露見の逆効果（信用失墜＝0以上）＝偽情報の露見度 disinformationExposed(0..1)×発信元の特定 attribution(0..1)
        /// ×逆効果係数。偽情報がバレて発信元が露見すると逆効果＝発信元の信用を焼く（<see cref="DeceptionRules.BacklashOnExposure"/> と同型）。
        /// 特定されなければ（attribution=0）逆効果は生じない＝匿名性が保たれる限り焼かれない。
        /// </summary>
        public static float BlowbackRisk(float disinformationExposed, float attribution, PsyOpParams p)
        {
            return Mathf.Max(0f, Mathf.Clamp01(disinformationExposed) * Mathf.Clamp01(attribution) * p.blowbackScale);
        }

        public static float BlowbackRisk(float disinformationExposed, float attribution)
            => BlowbackRisk(disinformationExposed, attribution, PsyOpParams.Default);

        /// <summary>
        /// 敵国内部の合意が崩壊しつつあるか＝累積した合意侵食 consensusErosion が崩壊閾値 threshold 以上。
        /// 心理作戦と偽情報が積み重なって内部の合意・結束が臨界を超えた状態（分断が修復不能に近づく）。
        /// </summary>
        public static bool IsConsensusCollapsing(float consensusErosion, float threshold)
        {
            return Mathf.Clamp01(consensusErosion) >= Mathf.Clamp01(threshold);
        }

        public static bool IsConsensusCollapsing(float consensusErosion, PsyOpParams p)
            => IsConsensusCollapsing(consensusErosion, p.collapseThreshold);

        public static bool IsConsensusCollapsing(float consensusErosion)
            => IsConsensusCollapsing(consensusErosion, PsyOpParams.Default.collapseThreshold);
    }
}
