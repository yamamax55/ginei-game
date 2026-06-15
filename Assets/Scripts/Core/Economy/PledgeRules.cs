using UnityEngine;

namespace Ginei
{
    /// <summary>個人結盟の種別（#1105・桃園結義型の誓約）。</summary>
    public enum PledgeType
    {
        /// <summary>義兄弟（桃園の誓い＝天地に誓った最も重い私的契り）。</summary>
        義兄弟,
        /// <summary>主従の誓い（君臣の盟・忠義の誓約）。</summary>
        主従の誓い,
        /// <summary>婚姻同盟（縁戚で結ぶ盟・<see cref="MarriageRules"/> が婚姻自体を扱う）。</summary>
        婚姻同盟,
        /// <summary>共闘の誓約（一時的な共同戦線の契り＝最も軽い）。</summary>
        共闘の誓約,
    }

    /// <summary>個人結盟・盟誓の調整係数（#1105・桃園結義型の誓約）。</summary>
    public readonly struct PledgeParams
    {
        /// <summary>公的に立てた誓い（証人あり）の拘束力倍率（天地に誓った重み）。</summary>
        public readonly float witnessMultiplier;
        /// <summary>共有した修羅場が拘束力を強める最大幅（苦楽を共にした誓いは固い）。</summary>
        public readonly float adversityScale;
        /// <summary>誓い破棄の名声失墜の最大幅（裏切り者の烙印）。</summary>
        public readonly float betrayalPenaltyScale;
        /// <summary>誓いを守る名声ボーナスの最大幅（義に厚い者）。</summary>
        public readonly float honorBonusScale;
        /// <summary>盟主への忠誠連鎖の最大幅（義兄弟は離れない）。</summary>
        public readonly float cascadeScale;

        public PledgeParams(float witnessMultiplier, float adversityScale, float betrayalPenaltyScale,
                            float honorBonusScale, float cascadeScale)
        {
            this.witnessMultiplier = Mathf.Max(1f, witnessMultiplier);
            this.adversityScale = Mathf.Max(0f, adversityScale);
            this.betrayalPenaltyScale = Mathf.Max(0f, betrayalPenaltyScale);
            this.honorBonusScale = Mathf.Max(0f, honorBonusScale);
            this.cascadeScale = Mathf.Max(0f, cascadeScale);
        }

        /// <summary>既定＝証人×1.5/修羅場0.5/破棄罰0.8/義名声0.4/連鎖0.6。</summary>
        public static PledgeParams Default => new PledgeParams(1.5f, 0.5f, 0.8f, 0.4f, 0.6f);
    }

    /// <summary>
    /// 個人結盟・盟誓の純ロジック（#1105・三国志演義の桃園結義型）。人物間の個人的な誓い（義兄弟の契り）が
    /// 制度的な拘束力を持ち、公的に立てた誓い（天地に誓った桃園の誓い）ほど破りにくく、破れば天下の信を失う
    /// （名声失墜・裏切り者の烙印）。義を守れば義に厚い者として慕われ（関羽の義）、盟友の危機には駆けつける
    /// 義務が生じ、誓いで結ばれた集団は盟主と運命を共にする（義兄弟は離れない＝忠誠連鎖）。
    /// 「個人の契りが制度の拘束力を持つ・破れば天下の信を失う」を式に出す。
    /// <see cref="LoyaltyRules"/>（会戦の旗幟＝戦う前に決まる戦い・調略）／<see cref="FriendshipRules"/>
    /// （個人的紐帯＝役職に依らぬ私的な友情）／<see cref="MarriageRules"/>（政略結婚＝家同士の同盟）とは別系統
    /// ＝こちらは「私的に誓った契りが制度的拘束力を帯びる」誓約そのものを扱う。
    /// 乱数なし決定論・全入力クランプ・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PledgeRules
    {
        /// <summary>義兄弟と共闘の誓約で拘束力に差をつける係数（義兄弟が最も重い）。</summary>
        private static float TypeWeight(PledgeType type)
        {
            switch (type)
            {
                case PledgeType.義兄弟: return 1f;        // 桃園の誓い＝最重
                case PledgeType.主従の誓い: return 0.85f;
                case PledgeType.婚姻同盟: return 0.7f;
                case PledgeType.共闘の誓約: return 0.5f;   // 一時的な共同＝最も軽い
                default: return 0.5f;
            }
        }

        /// <summary>
        /// 誓いの拘束力（0..1）。誠意(sincerity 0..1)×種別の重み。公的に立てた誓い（witnessed=true）は
        /// witnessMultiplier 倍で重くなる＝天地に誓った桃園の誓いは私的な口約束より破りにくい。
        /// </summary>
        public static float PledgeStrength(PledgeType type, float sincerity, bool witnessed, PledgeParams p)
        {
            float s = Mathf.Clamp01(sincerity) * TypeWeight(type);
            if (witnessed) s *= p.witnessMultiplier;
            return Mathf.Clamp01(s);
        }

        public static float PledgeStrength(PledgeType type, float sincerity, bool witnessed)
            => PledgeStrength(type, sincerity, witnessed, PledgeParams.Default);

        /// <summary>
        /// 拘束力（0..1）。共に修羅場を潜るほど強まる＝誓いの基礎拘束力を、苦楽を共にした度合い
        /// (sharedAdversity 0..1)×adversityScale ぶん（誓いに比例して）上乗せする＝苦楽を共にした誓いは固い。
        /// </summary>
        public static float BindingForce(float pledgeStrength, float sharedAdversity, PledgeParams p)
        {
            float ps = Mathf.Clamp01(pledgeStrength);
            float adv = Mathf.Clamp01(sharedAdversity);
            return Mathf.Clamp01(ps * (1f + p.adversityScale * adv));
        }

        public static float BindingForce(float pledgeStrength, float sharedAdversity)
            => BindingForce(pledgeStrength, sharedAdversity, PledgeParams.Default);

        /// <summary>
        /// 誓いを破る代償（0..betrayalPenaltyScale）。重い誓いほど破ると天下の信を失う＝拘束力×種別の重み×
        /// betrayalPenaltyScale ＝名声失墜・他者からの信用喪失（裏切り者の烙印）。義兄弟の誓いを破る代償は
        /// 軽い共闘の誓約より格段に重い。
        /// </summary>
        public static float BetrayalPenalty(float pledgeStrength, PledgeType type, PledgeParams p)
        {
            float ps = Mathf.Clamp01(pledgeStrength);
            return ps * TypeWeight(type) * p.betrayalPenaltyScale;
        }

        public static float BetrayalPenalty(float pledgeStrength, PledgeType type)
            => BetrayalPenalty(pledgeStrength, type, PledgeParams.Default);

        /// <summary>
        /// 誓いを守る名声（0..honorBonusScale／破れば0）。義に厚い者として慕われる（関羽の義）＝守った
        /// (pledgeUpheld=true)ときのみ拘束力×honorBonusScale を得る。破った者には名声ボーナスは無い。
        /// </summary>
        public static float HonorBonus(float pledgeStrength, bool pledgeUpheld, PledgeParams p)
        {
            if (!pledgeUpheld) return 0f;
            return Mathf.Clamp01(pledgeStrength) * p.honorBonusScale;
        }

        public static float HonorBonus(float pledgeStrength, bool pledgeUpheld)
            => HonorBonus(pledgeStrength, pledgeUpheld, PledgeParams.Default);

        /// <summary>
        /// 誓いの義務（0..1）。盟友が危機(allyInPeril=true)なら駆けつける義務が生じる＝種別の重み分の義務が
        /// 課される（助けねば誓いが崩れる）。危機でなければ義務は無い。
        /// </summary>
        public static float PledgeObligation(PledgeType type, bool allyInPeril)
            => allyInPeril ? Mathf.Clamp01(TypeWeight(type)) : 0f;

        /// <summary>
        /// 盟主への忠誠連鎖（0..1）。誓いで結ばれた集団は盟主と運命を共にする＝拘束力×cascadeScale を基礎に、
        /// 盟主の行い(leaderAction 0..1・義を貫けば1/不義なら0)で増減する＝盟主が義を貫けば義兄弟は離れず、
        /// 不義に走れば連鎖は緩む。
        /// </summary>
        public static float CascadingLoyalty(float pledgeStrength, float leaderAction, PledgeParams p)
        {
            float ps = Mathf.Clamp01(pledgeStrength);
            float act = Mathf.Clamp01(leaderAction);
            return Mathf.Clamp01(ps * p.cascadeScale * act);
        }

        public static float CascadingLoyalty(float pledgeStrength, float leaderAction)
            => CascadingLoyalty(pledgeStrength, leaderAction, PledgeParams.Default);
    }
}
