using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 指導者の器量（度量）の純データ＝己より優れた才人を使いこなせる容量。
    /// 度量(magnanimity)・自己の安心感(security＝嫉妬の少なさ)・委任の度合い(delegation) を保持する。
    /// 値は 0..1。可変フィールド（時間発展や状況で書き換える）。
    /// </summary>
    public struct CapacityTolerance
    {
        /// <summary>度量（0..1・他者の卓越を受け入れる広さ）。</summary>
        public float magnanimity;
        /// <summary>自己の安心感（0..1・高いほど嫉妬が少なく才人を恐れない）。</summary>
        public float security;
        /// <summary>委任の度合い（0..1・高いほど思い切って任せる）。</summary>
        public float delegation;

        public CapacityTolerance(float magnanimity, float security, float delegation)
        {
            this.magnanimity = Mathf.Clamp01(magnanimity);
            this.security = Mathf.Clamp01(security);
            this.delegation = Mathf.Clamp01(delegation);
        }
    }

    /// <summary>器量（劉邦型）の調整係数。</summary>
    public readonly struct CapacityParams
    {
        /// <summary>器量に占める度量の重み。</summary>
        public readonly float magnanimityWeight;
        /// <summary>器量に占める自己の安心感の重み。</summary>
        public readonly float securityWeight;
        /// <summary>嫉妬ペナルティの最大幅（器量0で傑出した才能をこの割合まで抑え込む）。</summary>
        public readonly float jealousyScale;
        /// <summary>傑出の脅威感の最大幅（部下の才が器量を超えるほど警戒が募る）。</summary>
        public readonly float threatScale;
        /// <summary>大器判定の既定しきい値（器量がこれ以上なら己より優れた者を使える大器）。</summary>
        public readonly float greatVesselThreshold;

        public CapacityParams(float magnanimityWeight, float securityWeight, float jealousyScale,
                              float threatScale, float greatVesselThreshold)
        {
            this.magnanimityWeight = Mathf.Max(0f, magnanimityWeight);
            this.securityWeight = Mathf.Max(0f, securityWeight);
            this.jealousyScale = Mathf.Max(0f, jealousyScale);
            this.threatScale = Mathf.Max(0f, threatScale);
            this.greatVesselThreshold = Mathf.Clamp01(greatVesselThreshold);
        }

        /// <summary>既定＝度量重み0.6/安心感重み0.4/嫉妬幅0.7/脅威幅0.8/大器閾0.6。</summary>
        public static CapacityParams Default => new CapacityParams(0.6f, 0.4f, 0.7f, 0.8f, 0.6f);
    }

    /// <summary>
    /// 器量＝度量の純ロジック＝『項羽と劉邦』型（#1409・KORY-2）。
    /// 「劉邦は『謀は張良に、兵站は蕭何に、用兵は韓信に及ばないが、この三傑を使えたから天下を取った』＝
    /// 己より優れた者を恐れず使う度量。器量の小さい項羽は范増一人すら使いこなせず才人を遠ざけた」。
    /// 指導者の器量は自分より優れた才人を使いこなせる容量で、器量が小さいと傑出した部下に嫉妬し才を殺すが、
    /// 大器は己より優れた者を恐れず使う＝器量が組織全体の能力上限になる（小さい器には大きい才が入らない）。
    /// <see cref="CommandStaffRules"/>（指揮班の能力補完＝副提督・参謀が提督の実効能力を底上げ）とは別＝
    /// こちらは「主君が己より優れた者を使えるか」の器量。<see cref="AdvisorCandorRules"/>（直言と佞臣＝諫言の情報品質・生成済み）とも別。
    /// 才人の声望・処遇は <see cref="PrestigeRules"/>（声望・同EPIC KORY）／功臣の処遇は <see cref="MeritRetentionRules"/>（功臣処遇・同EPIC）が担当＝
    /// 本クラスは「器量が才を活かす／嫉妬が才を殺す」容量の式だけを扱う。
    /// 乱数なし決定論・全入力クランプ・C# 9.0。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CapacityRules
    {
        // ===== 指導者の器量 =====

        /// <summary>
        /// 指導者の器量（0..1）＝度量(magnanimity 0..1)×magnanimityWeight ＋ 自己の安心感(selfSecurity 0..1)×securityWeight
        /// を重み合計で正規化。度量が広く嫉妬が少ないほど才人を恐れず使える容量が大きい（劉邦＝高／項羽＝低）。
        /// </summary>
        public static float LeaderCapacity(float magnanimity, float selfSecurity, CapacityParams p)
        {
            float m = Mathf.Clamp01(magnanimity);
            float s = Mathf.Clamp01(selfSecurity);
            float weightSum = Mathf.Max(0.0001f, p.magnanimityWeight + p.securityWeight);
            return Mathf.Clamp01((m * p.magnanimityWeight + s * p.securityWeight) / weightSum);
        }

        public static float LeaderCapacity(float magnanimity, float selfSecurity)
            => LeaderCapacity(magnanimity, selfSecurity, CapacityParams.Default);

        /// <summary><see cref="CapacityTolerance"/> から器量を求める（度量×安心感）。</summary>
        public static float LeaderCapacity(CapacityTolerance tol, CapacityParams p)
            => LeaderCapacity(tol.magnanimity, tol.security, p);

        public static float LeaderCapacity(CapacityTolerance tol)
            => LeaderCapacity(tol, CapacityParams.Default);

        // ===== 才人の活用（器量がボトルネック） =====

        /// <summary>
        /// 才人の活用度（0..1）＝部下の才能(subordinateTalent 0..1)のうち器量(leaderCapacity 0..1)で受け止められる分。
        /// 器量が部下の才能以上なら才を満額活かせる（活用＝才能そのまま）。器量が才能未満だと器量が上限になり
        /// あふれた才は活かせない（器量＜才能＝器量がボトルネック・小さい器には大きい才が入らない）。
        /// </summary>
        public static float TalentUtilization(float leaderCapacity, float subordinateTalent)
        {
            float cap = Mathf.Clamp01(leaderCapacity);
            float talent = Mathf.Clamp01(subordinateTalent);
            return Mathf.Min(talent, cap); // 器量が才能の天井
        }

        // ===== 傑出の脅威（韓信粛清の芽） =====

        /// <summary>
        /// 傑出の脅威感（0..threatScale）＝部下の才(subordinateTalent 0..1)が器量(leaderCapacity 0..1)を超えた分だけ
        /// 器量の小さい主君が脅威を感じる（嫉妬・警戒）。才が器量以下なら脅威0、超えるほど警戒が募る＝韓信粛清の芽。
        /// </summary>
        public static float OverShadowingThreat(float subordinateTalent, float leaderCapacity, CapacityParams p)
        {
            float talent = Mathf.Clamp01(subordinateTalent);
            float cap = Mathf.Clamp01(leaderCapacity);
            float overshoot = Mathf.Max(0f, talent - cap); // 器量を超えた傑出分
            return Mathf.Clamp01(overshoot) * p.threatScale;
        }

        public static float OverShadowingThreat(float subordinateTalent, float leaderCapacity)
            => OverShadowingThreat(subordinateTalent, leaderCapacity, CapacityParams.Default);

        // ===== 嫉妬のペナルティ（才を殺す） =====

        /// <summary>
        /// 嫉妬ペナルティ（0..1の能力抑制率）＝器量(leaderCapacity 0..1)が小さいほど優れた部下(subordinateBrilliance 0..1)に
        /// 嫉妬して能力を抑え込む（才を殺す）。器量1ならペナルティ0・器量0で傑出した才を jealousyScale まで抑える。
        /// </summary>
        public static float JealousyPenalty(float leaderCapacity, float subordinateBrilliance, CapacityParams p)
        {
            float cap = Mathf.Clamp01(leaderCapacity);
            float brilliance = Mathf.Clamp01(subordinateBrilliance);
            // 器量の小ささ × 部下の輝き＝小器ほど・才人ほど抑え込まれる。
            return Mathf.Clamp01((1f - cap) * brilliance * p.jealousyScale);
        }

        public static float JealousyPenalty(float leaderCapacity, float subordinateBrilliance)
            => JealousyPenalty(leaderCapacity, subordinateBrilliance, CapacityParams.Default);

        // ===== 委任の有効性（任せるか抱え込むか） =====

        /// <summary>
        /// 委任の有効性（0..1）＝器量(leaderCapacity 0..1)が大きいほど思い切って委任でき、課題が複雑(taskComplexity 0..1)な
        /// ほど委任が効く（一人で抱えきれない）。器量×（複雑さで増す委任の必要）＝劉邦は任せ・項羽は抱え込んだ。
        /// </summary>
        public static float DelegationEffectiveness(float leaderCapacity, float taskComplexity)
        {
            float cap = Mathf.Clamp01(leaderCapacity);
            float complexity = Mathf.Clamp01(taskComplexity);
            // 委任は器量で可能になり、課題が複雑なほど委任の利得が増す（単純な課題は任せても差が出ない）。
            return Mathf.Clamp01(cap * (0.5f + 0.5f * complexity));
        }

        // ===== 器量による定着（才人が留まる） =====

        /// <summary>
        /// 才人の定着度（0..1）＝器量(leaderCapacity 0..1)ある主君のもとで活かされる満足と、厚遇(treatment 0..1)で才人が留まる。
        /// 器量×処遇＝活かされかつ厚遇されてこそ留まる（活かされぬ才は去る＝声望は <see cref="PrestigeRules"/> と接続）。
        /// </summary>
        public static float TalentRetentionFromCapacity(float leaderCapacity, float treatment)
        {
            float cap = Mathf.Clamp01(leaderCapacity);
            float care = Mathf.Clamp01(treatment);
            // 器量を主・処遇を従に重みづけ（活かされぬ才は厚遇だけでは留まらない）。
            return Mathf.Clamp01(0.6f * cap + 0.4f * care);
        }

        // ===== 小器の上限（器が組織の天井） =====

        /// <summary>
        /// 小器の上限（0..1）＝器量(leaderCapacity 0..1)が組織全体の能力上限になる。器量が小さいほど天井が低く、
        /// どれほど大きな才もこの上限までしか入らない（小さい器には大きい才が入らない＝器量＝組織の天井）。
        /// </summary>
        public static float SmallVesselCeiling(float leaderCapacity)
            => Mathf.Clamp01(leaderCapacity);

        // ===== 大器判定（己より優れた者を使える） =====

        /// <summary>
        /// 大器の判定＝器量(leaderCapacity 0..1)がしきい値(threshold 0..1)以上なら true。
        /// 己より優れた者を恐れず使える容量を備えた大器（劉邦＝三傑を使う）か否か。
        /// </summary>
        public static bool IsGreatVessel(float leaderCapacity, float threshold)
            => Mathf.Clamp01(leaderCapacity) >= Mathf.Clamp01(threshold);

        /// <summary>大器判定（既定しきい値 greatVesselThreshold を使用）。</summary>
        public static bool IsGreatVessel(float leaderCapacity, CapacityParams p)
            => IsGreatVessel(leaderCapacity, p.greatVesselThreshold);

        public static bool IsGreatVessel(float leaderCapacity)
            => IsGreatVessel(leaderCapacity, CapacityParams.Default);
    }
}
