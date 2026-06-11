using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 義賊組織（梁山泊型）の純データ struct（水滸伝・SHZ-1 #1357 の中核状態）。
    /// 官の腐敗・圧政で行き場を失った者が拠点（梁山泊）へ集まって武装組織を成す＝
    /// 国家でない武装勢力の規模・結束・義賊としての正統性を保持する。
    /// </summary>
    public struct RebelOrg
    {
        /// <summary>規模（0..1・組織の人数/勢力の大きさ。行き場を失った者の駆け込みで増える）。</summary>
        public float membership;
        /// <summary>結束（0..1・義兄弟の契り・大義・頭領のカリスマで保たれ、内部対立で緩む）。</summary>
        public float cohesion;
        /// <summary>義賊としての正統性（0..1・「替天行道」＝官の腐敗ゆえ義賊が正義とされる度合い）。</summary>
        public float legitimacy;

        public RebelOrg(float membership, float cohesion, float legitimacy)
        {
            this.membership = Mathf.Clamp01(membership);
            this.cohesion = Mathf.Clamp01(cohesion);
            this.legitimacy = Mathf.Clamp01(legitimacy);
        }
    }

    /// <summary>義賊組織の調整係数。</summary>
    public readonly struct OutlawOrganizationParams
    {
        /// <summary>組織形成の閾値（形成圧がこれ以上で梁山泊が立つ）。</summary>
        public readonly float formationThreshold;
        /// <summary>行き場のなさ（statelessDesperation）が形成圧に効く重み（0..1）。</summary>
        public readonly float desperationWeight;
        /// <summary>逃げ込める拠点（refugeAvailable）が形成圧に効く重み（0..1）。</summary>
        public readonly float refugeWeight;
        /// <summary>義賊への加入速度（per dt・不満×拠点の威名1のとき）。</summary>
        public readonly float recruitmentRate;
        /// <summary>結束の源の合成における義兄弟の契り（brotherhood）の重み。</summary>
        public readonly float brotherhoodWeight;
        /// <summary>結束の源の合成における大義（sharedCause）の重み。</summary>
        public readonly float causeWeight;
        /// <summary>結束の源の合成における頭領のカリスマ（leaderCharisma）の重み。</summary>
        public readonly float charismaWeight;
        /// <summary>内部対立が結束を緩める速度（per dt・内部対立1のとき）。</summary>
        public readonly float rivalryDecayRate;
        /// <summary>外圧（官の弾圧）が結束を逆に固める速度（per dt・外圧1のとき＝敵が結束を強いる）。</summary>
        public readonly float externalPressureBondRate;
        /// <summary>官の弾圧が解体リスクに効く重み（0..1）。</summary>
        public readonly float suppressionWeight;
        /// <summary>内部分裂が解体リスクに効く重み（0..1）。</summary>
        public readonly float splitWeight;
        /// <summary>存続性における住民支持（民が匿う）の重み（0..1）。</summary>
        public readonly float localSupportWeight;
        /// <summary>存続性における略奪経済（食い扶持）の重み（0..1）。</summary>
        public readonly float plunderWeight;
        /// <summary>非国家権力における支配領域の重み（0..1・事実上の地域権力化）。</summary>
        public readonly float territoryWeight;

        public OutlawOrganizationParams(float formationThreshold, float desperationWeight, float refugeWeight,
            float recruitmentRate, float brotherhoodWeight, float causeWeight, float charismaWeight,
            float rivalryDecayRate, float externalPressureBondRate, float suppressionWeight, float splitWeight,
            float localSupportWeight, float plunderWeight, float territoryWeight)
        {
            this.formationThreshold = Mathf.Clamp01(formationThreshold);
            this.desperationWeight = Mathf.Clamp01(desperationWeight);
            this.refugeWeight = Mathf.Clamp01(refugeWeight);
            this.recruitmentRate = Mathf.Max(0f, recruitmentRate);
            this.brotherhoodWeight = Mathf.Max(0f, brotherhoodWeight);
            this.causeWeight = Mathf.Max(0f, causeWeight);
            this.charismaWeight = Mathf.Max(0f, charismaWeight);
            this.rivalryDecayRate = Mathf.Max(0f, rivalryDecayRate);
            this.externalPressureBondRate = Mathf.Max(0f, externalPressureBondRate);
            this.suppressionWeight = Mathf.Clamp01(suppressionWeight);
            this.splitWeight = Mathf.Clamp01(splitWeight);
            this.localSupportWeight = Mathf.Clamp01(localSupportWeight);
            this.plunderWeight = Mathf.Clamp01(plunderWeight);
            this.territoryWeight = Mathf.Clamp01(territoryWeight);
        }

        /// <summary>
        /// 既定＝形成閾値0.5・行き場のなさ重み0.5/拠点重み0.5・加入速度0.2・
        /// 結束源 義兄弟0.4/大義0.35/カリスマ0.25・内部対立減衰0.15・外圧固結0.1・
        /// 解体 弾圧重み0.5/内部分裂重み0.5・存続 住民支持0.6/略奪0.4・領域0.6。
        /// </summary>
        public static OutlawOrganizationParams Default =>
            new OutlawOrganizationParams(0.5f, 0.5f, 0.5f, 0.2f, 0.4f, 0.35f, 0.25f,
                0.15f, 0.1f, 0.5f, 0.5f, 0.6f, 0.4f, 0.6f);
    }

    /// <summary>
    /// 義賊組織化の純ロジック（梁山泊型・SHZ-1 #1357・水滸伝）。
    /// 国家でない武装組織（義賊・反乱軍・梁山泊）の組織化＝官の腐敗・圧政で行き場を失った者が集まり、
    /// 形成圧が閾値を超えると組織化する。結束の源（義兄弟の契り・大義・頭領のカリスマ）が組織を保ち、
    /// 内部対立・官の弾圧・招安などで解体リスクを負う。
    /// 式の核：形成圧＝不満×行き場のなさ×拠点（どれか欠ければ立たない）／結束は内部対立で緩み外圧で固まる
    /// （敵が結束を強いる）／解体リスクは結束が低いほど・官の弾圧と内部分裂が高いほど上がる。
    /// <para>
    /// 分担：<see cref="MutinyRules"/> は軍隊内の集団的命令拒否＝艦隊が割れる反乱（組織の内側の事態）。
    /// <see cref="MercenaryRules"/> は金で雇われる傭兵＝理念でなく給与で動く戦力。
    /// <see cref="InsurgencyRules"/> は占領地の外部支援された反乱＝隣国の扇動・武器で組織化（生成済み）。
    /// <see cref="CounterLegitimacyRules"/> は対抗的正統性＝既存秩序に対抗する正統性の主張（同EPIC SHZ）。
    /// 本クラスは「行き場を失った者が拠点に集う梁山泊型の義賊組織」＝<see cref="RebelOrg"/> を中核データに、
    /// 形成閾値・結束源・解体リスクに責任を持つ（軍内反乱・傭兵・占領地反乱とは別系統）。
    /// </para>
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OutlawOrganizationRules
    {
        /// <summary>
        /// 組織形成の閾値判定＝官の圧政への不満 grievance×行き場のなさ statelessDesperation×逃げ込める拠点
        /// refugeAvailable（梁山泊）で形成圧を出し、<see cref="OutlawOrganizationParams.formationThreshold"/> を超えれば true。
        /// 三因の積＝どれか0なら形成圧0＝義賊は立たない（不満だけでも、拠点だけでも組織化しない）。
        /// </summary>
        public static bool FormationThreshold(float grievance, float statelessDesperation, float refugeAvailable, OutlawOrganizationParams p)
        {
            return FormationPressure(grievance, statelessDesperation, refugeAvailable, p) >= p.formationThreshold;
        }

        public static bool FormationThreshold(float grievance, float statelessDesperation, float refugeAvailable)
            => FormationThreshold(grievance, statelessDesperation, refugeAvailable, OutlawOrganizationParams.Default);

        /// <summary>
        /// 形成圧（0..1）＝不満 grievance×（行き場のなさ・拠点の重み付き合成）。重みで両者の効きを按分しつつ、
        /// 不満との積で「圧政（不満）＋行き場のなさ＋逃げ込める拠点」が揃って初めて圧が立つ。
        /// </summary>
        public static float FormationPressure(float grievance, float statelessDesperation, float refugeAvailable, OutlawOrganizationParams p)
        {
            float g = Mathf.Clamp01(grievance);
            float enabler = Mathf.Clamp01(statelessDesperation) * p.desperationWeight
                            + Mathf.Clamp01(refugeAvailable) * p.refugeWeight;
            return Mathf.Clamp01(g * Mathf.Clamp01(enabler));
        }

        public static float FormationPressure(float grievance, float statelessDesperation, float refugeAvailable)
            => FormationPressure(grievance, statelessDesperation, refugeAvailable, OutlawOrganizationParams.Default);

        /// <summary>
        /// 規模の1tick後（0..1）＝行き場を失った者が義賊に加わる。加入はロジスティック型＝不満 grievance×
        /// 拠点の威名 refugePrestige を駆動力に、既存規模と未加入の残余の積で広がる（官に追われた者の駆け込み）。
        /// 威名ある拠点ほど人を呼ぶが、まだ誰も居ない（規模0）なら核が無く広がらない。
        /// </summary>
        public static float RecruitmentTick(float membership, float grievance, float refugePrestige, float dt, OutlawOrganizationParams p)
        {
            float m = Mathf.Clamp01(membership);
            float drive = Mathf.Clamp01(grievance) * Mathf.Clamp01(refugePrestige);
            float growth = p.recruitmentRate * drive * m * (1f - m) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(m + growth);
        }

        public static float RecruitmentTick(float membership, float grievance, float refugePrestige, float dt)
            => RecruitmentTick(membership, grievance, refugePrestige, dt, OutlawOrganizationParams.Default);

        /// <summary>
        /// 結束の源（0..1）＝義兄弟の契り brotherhood・大義 sharedCause・頭領のカリスマ leaderCharisma の加重平均。
        /// 義（恩義・契り）と大義とカリスマが組織を一つに保つ＝梁山泊を結びつける三本の糸。
        /// </summary>
        public static float CohesionSource(float brotherhood, float sharedCause, float leaderCharisma, OutlawOrganizationParams p)
        {
            float wsum = p.brotherhoodWeight + p.causeWeight + p.charismaWeight;
            if (wsum <= 0f) return 0f;
            float weighted = Mathf.Clamp01(brotherhood) * p.brotherhoodWeight
                             + Mathf.Clamp01(sharedCause) * p.causeWeight
                             + Mathf.Clamp01(leaderCharisma) * p.charismaWeight;
            return Mathf.Clamp01(weighted / wsum);
        }

        public static float CohesionSource(float brotherhood, float sharedCause, float leaderCharisma)
            => CohesionSource(brotherhood, sharedCause, leaderCharisma, OutlawOrganizationParams.Default);

        /// <summary>
        /// 結束の1tick後（0..1）＝内部対立 internalRivalry で緩み、外圧 externalPressure（官の弾圧）で逆に固まる。
        /// 敵が結束を強いる＝外から攻められるほど梁山泊は団結し、内輪揉めだけが組織をほどく。
        /// </summary>
        public static float CohesionTick(float cohesion, float internalRivalry, float externalPressure, float dt, OutlawOrganizationParams p)
        {
            float c = Mathf.Clamp01(cohesion);
            float d = Mathf.Max(0f, dt);
            float decay = p.rivalryDecayRate * Mathf.Clamp01(internalRivalry) * d;
            float bond = p.externalPressureBondRate * Mathf.Clamp01(externalPressure) * d;
            return Mathf.Clamp01(c - decay + bond);
        }

        public static float CohesionTick(float cohesion, float internalRivalry, float externalPressure, float dt)
            => CohesionTick(cohesion, internalRivalry, externalPressure, dt, OutlawOrganizationParams.Default);

        /// <summary>
        /// 解体リスク（0..1）＝結束が低いほど・官の弾圧 governmentSuppression と内部分裂 internalSplit が高いほど上がる。
        /// 弾圧と分裂の重み付き圧を、結束 cohesion が蓋として（1−結束）で割り引く＝
        /// 固く結束した組織は弾圧にも分裂にも耐え、緩んだ組織は同じ圧で容易に瓦解する（招安・帰順も分裂の一形態）。
        /// </summary>
        public static float DissolutionRisk(float cohesion, float governmentSuppression, float internalSplit, OutlawOrganizationParams p)
        {
            float pressure = Mathf.Clamp01(Mathf.Clamp01(governmentSuppression) * p.suppressionWeight
                                           + Mathf.Clamp01(internalSplit) * p.splitWeight);
            float lid = 1f - Mathf.Clamp01(cohesion);
            return Mathf.Clamp01(pressure * lid);
        }

        public static float DissolutionRisk(float cohesion, float governmentSuppression, float internalSplit)
            => DissolutionRisk(cohesion, governmentSuppression, internalSplit, OutlawOrganizationParams.Default);

        /// <summary>
        /// 存続性（0..1）＝義賊組織が住民支持 localSupport（民が匿う）・略奪経済 plunderEconomy（食い扶持）で
        /// 規模 membership を養えるか。供給力（支持＋略奪の重み付き合成）を規模で割り引く＝
        /// 大所帯ほど養うのが難しく、民が匿い略奪が回るほど存続する（民の海に泳ぐ魚）。
        /// </summary>
        public static float OutlawSustainability(float membership, float localSupport, float plunderEconomy, OutlawOrganizationParams p)
        {
            float supply = Mathf.Clamp01(Mathf.Clamp01(localSupport) * p.localSupportWeight
                                         + Mathf.Clamp01(plunderEconomy) * p.plunderWeight);
            float demand = Mathf.Clamp01(membership);
            // 規模が大きいほど要求が増す＝供給を（1+規模）で割って薄める。
            return Mathf.Clamp01(supply / (1f + demand));
        }

        public static float OutlawSustainability(float membership, float localSupport, float plunderEconomy)
            => OutlawSustainability(membership, localSupport, plunderEconomy, OutlawOrganizationParams.Default);

        /// <summary>
        /// 非国家権力の度合い（0..1）＝義賊としての正統性 legitimacy×支配領域 territoryControlled。
        /// 正統性（替天行道の大義）と実効支配の両方を持つほど、義賊は事実上の地域権力（独立王国的）になる＝
        /// どちらか欠ければ単なる野盗（権力に届かない）。
        /// </summary>
        public static float NonStateAuthority(float legitimacy, float territoryControlled, OutlawOrganizationParams p)
        {
            float t = Mathf.Clamp01(territoryControlled) * p.territoryWeight;
            return Mathf.Clamp01(Mathf.Clamp01(legitimacy) * t);
        }

        public static float NonStateAuthority(float legitimacy, float territoryControlled)
            => NonStateAuthority(legitimacy, territoryControlled, OutlawOrganizationParams.Default);

        /// <summary>
        /// 確立した義賊組織（梁山泊）の判定＝規模 membership×結束 cohesion が threshold 以上。
        /// 規模と結束の両方が要る＝人数だけ（結束低）でも、少数精鋭（規模低）でも確立した組織には届かない。
        /// </summary>
        public static bool IsEstablishedOutlawBand(float membership, float cohesion, float threshold)
        {
            float strength = Mathf.Clamp01(membership) * Mathf.Clamp01(cohesion);
            return strength >= Mathf.Clamp01(threshold);
        }
    }
}
