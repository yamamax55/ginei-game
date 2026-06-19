using UnityEngine;

namespace Ginei
{
    /// <summary>保護国・従属国（プロテクトレート）の調整係数。</summary>
    public readonly struct ProtectorateParams
    {
        /// <summary>主権制限における外交統制の重み（0..1）。</summary>
        public readonly float diplomaticWeight;
        /// <summary>主権制限における軍事統制の重み（0..1）。</summary>
        public readonly float militaryWeight;
        /// <summary>外的脅威が庇護の恩恵を増幅する重み（脅威が高いほど後ろ盾の価値が増す）。</summary>
        public readonly float threatBenefitWeight;
        /// <summary>従属国の不安定が宗主の負担を増幅する重み（揺れる属国ほど手がかかる）。</summary>
        public readonly float instabilityBurdenWeight;
        /// <summary>独立志向における民族意識の重み（0..1）。</summary>
        public readonly float identityWeight;
        /// <summary>独立志向における自立能力の重み（0..1）。</summary>
        public readonly float capabilityWeight;
        /// <summary>独立への移行圧における宗主弱体化の重み（後ろ盾が衰えるほど離反が加速）。</summary>
        public readonly float weaknessWeight;

        public ProtectorateParams(float diplomaticWeight, float militaryWeight, float threatBenefitWeight,
            float instabilityBurdenWeight, float identityWeight, float capabilityWeight, float weaknessWeight)
        {
            this.diplomaticWeight = Mathf.Clamp01(diplomaticWeight);
            this.militaryWeight = Mathf.Clamp01(militaryWeight);
            this.threatBenefitWeight = Mathf.Max(0f, threatBenefitWeight);
            this.instabilityBurdenWeight = Mathf.Max(0f, instabilityBurdenWeight);
            this.identityWeight = Mathf.Clamp01(identityWeight);
            this.capabilityWeight = Mathf.Clamp01(capabilityWeight);
            this.weaknessWeight = Mathf.Max(0f, weaknessWeight);
        }

        /// <summary>既定＝外交重み0.4・軍事重み0.6・脅威恩恵重み0.5・不安定負担重み0.5・民族意識重み0.5・自立能力重み0.5・弱体化重み1。</summary>
        public static ProtectorateParams Default => new ProtectorateParams(0.4f, 0.6f, 0.5f, 0.5f, 0.5f, 0.5f, 1f);
    }

    /// <summary>
    /// 保護国・従属国（プロテクトレート）の純ロジック＝主権を制限された従属関係。宗主は外交・軍事の主権を
    /// 制限する代わりに庇護（外的脅威からの後ろ盾）を与え、従属国は内政自治を保ちつつ自立志向を募らせる。
    /// 「庇護と引き換えに主権を差し出す」関係の安定は、恩恵と合意が支える正統性が、自治・民族意識・自立能力で
    /// 高まる独立志向を上回る限り保たれる＝宗主が弱れば従属国は独立へ動く。フェザーン型・属国・衛星国を式に出す。
    /// 外交状態（<see cref="DiplomacyRules"/>＝属国 enum）の中身を量で扱う層。乱数なし・決定論・実効値パターン。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ProtectorateRules
    {
        /// <summary>
        /// 主権制限の度合い（0..1）＝外交統制×外交重み＋軍事統制×軍事重み。外交・軍事の双方を握られるほど
        /// 主権は深く制限される（属国化）。両統制0なら制限なし＝対等な独立国。
        /// </summary>
        public static float SovereigntyRestriction(float diplomaticControl, float militaryControl, ProtectorateParams p)
        {
            float dc = Mathf.Clamp01(diplomaticControl);
            float mc = Mathf.Clamp01(militaryControl);
            return Mathf.Clamp01(dc * p.diplomaticWeight + mc * p.militaryWeight);
        }

        public static float SovereigntyRestriction(float diplomaticControl, float militaryControl)
            => SovereigntyRestriction(diplomaticControl, militaryControl, ProtectorateParams.Default);

        /// <summary>
        /// 庇護の恩恵（0..1）＝宗主の力×（1＋外的脅威×脅威恩恵重み）。強い宗主ほど後ろ盾になり、外的脅威が
        /// 高いほどその後ろ盾の価値が増す（敵が多いほど庇護がありがたい）。脅威ゼロ＝平時は宗主の力そのまま。
        /// </summary>
        public static float ProtectionBenefit(float overlordStrength, float externalThreat, ProtectorateParams p)
        {
            float s = Mathf.Clamp01(overlordStrength);
            float threat = Mathf.Clamp01(externalThreat);
            return Mathf.Clamp01(s * (1f + threat * p.threatBenefitWeight));
        }

        public static float ProtectionBenefit(float overlordStrength, float externalThreat)
            => ProtectionBenefit(overlordStrength, externalThreat, ProtectorateParams.Default);

        /// <summary>
        /// 内政自治の余地（0..1）＝1−主権制限。外交・軍事を握られても内政は従属国に委ねられる
        /// （庇護の見返りは外交・軍事の主権であって、内政自治は残る）。制限が深いほど自治は狭まる。
        /// </summary>
        public static float InternalAutonomy(float sovereigntyRestriction, ProtectorateParams p)
        {
            float r = Mathf.Clamp01(sovereigntyRestriction);
            return Mathf.Clamp01(1f - r);
        }

        public static float InternalAutonomy(float sovereigntyRestriction)
            => InternalAutonomy(sovereigntyRestriction, ProtectorateParams.Default);

        /// <summary>
        /// 宗主の負担（0..1）＝庇護義務×（1＋従属国の不安定×不安定負担重み）。守ると約束した属国ほど、
        /// その属国が揺れている（内乱・離反の危機）ほど宗主は手がかかる＝抱え込んだ属国の面倒を見る重み。
        /// 義務ゼロ＝庇護していなければ負担なし。
        /// </summary>
        public static float OverlordBurden(float protectionCommitment, float vassalInstability, ProtectorateParams p)
        {
            float c = Mathf.Clamp01(protectionCommitment);
            float inst = Mathf.Clamp01(vassalInstability);
            return Mathf.Clamp01(c * (1f + inst * p.instabilityBurdenWeight));
        }

        public static float OverlordBurden(float protectionCommitment, float vassalInstability)
            => OverlordBurden(protectionCommitment, vassalInstability, ProtectorateParams.Default);

        /// <summary>
        /// 独立志向（0..1）＝自治×（民族意識×重み＋自立能力×重み）。内政自治があり、独立した民族意識があり、
        /// 自力で立てる能力があるほど従属国は独立を志向する。自治がゼロ（完全併合）なら独立を志す余地もない。
        /// 三者の積＝どれか一つでも欠ければ志向は萎む（自治はあっても能力なし、能力はあっても従属意識…）。
        /// </summary>
        public static float IndependenceAspiration(float internalAutonomy, float nationalIdentity, float vassalCapability,
            ProtectorateParams p)
        {
            float auto = Mathf.Clamp01(internalAutonomy);
            float identity = Mathf.Clamp01(nationalIdentity);
            float cap = Mathf.Clamp01(vassalCapability);
            float drive = Mathf.Clamp01(identity * p.identityWeight + cap * p.capabilityWeight);
            return Mathf.Clamp01(auto * drive);
        }

        public static float IndependenceAspiration(float internalAutonomy, float nationalIdentity, float vassalCapability)
            => IndependenceAspiration(internalAutonomy, nationalIdentity, vassalCapability, ProtectorateParams.Default);

        /// <summary>
        /// 保護関係の正統性（0..1）＝恩恵×（1−主権制限）×合意。庇護の見返りがあり、主権の制限が軽く、
        /// 双方の合意に基づく関係ほど正統性が高い＝守られて納得しているなら安定。逆に恩恵が薄く、主権を奪われ、
        /// 押しつけられた（合意なき）関係は正統性を欠く＝不平等条約。三者の積＝どれかが崩れれば正統性は崩れる。
        /// </summary>
        public static float RelationshipLegitimacy(float protectionBenefit, float sovereigntyRestriction, float consent,
            ProtectorateParams p)
        {
            float benefit = Mathf.Clamp01(protectionBenefit);
            float restriction = Mathf.Clamp01(sovereigntyRestriction);
            float c = Mathf.Clamp01(consent);
            return Mathf.Clamp01(benefit * (1f - restriction) * c);
        }

        public static float RelationshipLegitimacy(float protectionBenefit, float sovereigntyRestriction, float consent)
            => RelationshipLegitimacy(protectionBenefit, sovereigntyRestriction, consent, ProtectorateParams.Default);

        /// <summary>
        /// 独立への移行圧（0..1）＝独立志向×（1＋宗主弱体化×弱体化重み）。独立を志向する従属国は、宗主が
        /// 衰えるほど（後ろ盾が弱まるほど）一気に離反へ動く＝弱った宗主から属国が独立する。志向ゼロなら宗主が
        /// 弱っても動かない（独立を望まない属国はそのまま）。
        /// </summary>
        public static float TransitionToIndependence(float independenceAspiration, float overlordWeakness, ProtectorateParams p)
        {
            float aspiration = Mathf.Clamp01(independenceAspiration);
            float weakness = Mathf.Clamp01(overlordWeakness);
            return Mathf.Clamp01(aspiration * (1f + weakness * p.weaknessWeight));
        }

        public static float TransitionToIndependence(float independenceAspiration, float overlordWeakness)
            => TransitionToIndependence(independenceAspiration, overlordWeakness, ProtectorateParams.Default);

        /// <summary>
        /// 保護関係が安定か（bool）＝正統性が独立志向を閾値ぶん上回るか。正統性 − 独立志向 ≥ 閾値 なら安定
        /// （守られて納得している関係は続く）。志向が正統性に追いつけば従属関係は揺らぐ＝独立へ向かう。
        /// </summary>
        public static bool IsProtectorateStable(float relationshipLegitimacy, float independenceAspiration, float threshold)
        {
            float legit = Mathf.Clamp01(relationshipLegitimacy);
            float aspiration = Mathf.Clamp01(independenceAspiration);
            float th = Mathf.Clamp01(threshold);
            return legit - aspiration >= th;
        }

        public static bool IsProtectorateStable(float relationshipLegitimacy, float independenceAspiration)
            => IsProtectorateStable(relationshipLegitimacy, independenceAspiration, 0f);
    }
}
