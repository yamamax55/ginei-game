using UnityEngine;

namespace Ginei
{
    /// <summary>対外援助の調整係数。</summary>
    public readonly struct ForeignAidParams
    {
        /// <summary>援助1単位あたりの基礎 opinion 上昇（困窮ゼロの平時援助）。</summary>
        public readonly float opinionPerAid;
        /// <summary>困窮（recipientNeed=1）が基礎効果に上乗せする倍率幅（雪中の炭＝1+これ倍）。</summary>
        public readonly float needBonusMax;
        /// <summary>敵対国への災害救援の倍率（敵の善意は記憶に残る・1以上）。</summary>
        public readonly float hostileReliefMultiplier;
        /// <summary>紐付き援助の実利（基地・投票・市場開放）の係数。</summary>
        public readonly float leverageScale;
        /// <summary>紐付き援助への反発（内政干渉への怒り）の係数。条件度の二乗で効く。</summary>
        public readonly float backlashScale;
        /// <summary>援助フローが依存を形成する速度（per aidFlow per dt）。</summary>
        public readonly float dependencyGrowthRate;
        /// <summary>援助が止まったとき依存が自立へ戻る速度（per dt・形成より遅い）。</summary>
        public readonly float dependencyDecayRate;
        /// <summary>援助カットのショック幅（依存最大で被る打撃の上限・0..1）。</summary>
        public readonly float withdrawalShockScale;
        /// <summary>累積援助1単位あたりの援助疲れの蓄積速度。</summary>
        public readonly float fatigueRate;

        public ForeignAidParams(float opinionPerAid, float needBonusMax, float hostileReliefMultiplier,
                                float leverageScale, float backlashScale,
                                float dependencyGrowthRate, float dependencyDecayRate,
                                float withdrawalShockScale, float fatigueRate)
        {
            this.opinionPerAid = Mathf.Max(0f, opinionPerAid);
            this.needBonusMax = Mathf.Max(0f, needBonusMax);
            this.hostileReliefMultiplier = Mathf.Max(1f, hostileReliefMultiplier);
            this.leverageScale = Mathf.Max(0f, leverageScale);
            this.backlashScale = Mathf.Max(0f, backlashScale);
            this.dependencyGrowthRate = Mathf.Max(0f, dependencyGrowthRate);
            this.dependencyDecayRate = Mathf.Max(0f, dependencyDecayRate);
            this.withdrawalShockScale = Mathf.Clamp01(withdrawalShockScale);
            this.fatigueRate = Mathf.Max(0f, fatigueRate);
        }

        /// <summary>既定＝基礎opinion 0.1/困窮上乗せ2（雪中の炭3倍）/敵対救援2倍/実利0.5/反発0.8/依存形成0.05/自立回復0.01/カットショック0.5/援助疲れ0.01。</summary>
        public static ForeignAidParams Default =>
            new ForeignAidParams(0.1f, 2f, 2f, 0.5f, 0.8f, 0.05f, 0.01f, 0.5f, 0.01f);
    }

    /// <summary>
    /// 対外援助の純ロジック＝善意と買収の二重底。援助は最も安い武器＝感謝も依存も買える：
    /// 困窮した相手への援助は同額でも何倍も効き（雪中の炭）、敵対国への災害救援は平時援助より強く
    /// 記憶に残る。紐付き援助は実利を引き出すが条件が重いほど反発が二乗で募り、援助の継続は
    /// 受け手の自立を蝕んで依存を形成し、依存させてから切れば武器になる。一方で成果の見えない
    /// 援助は国内の支持を失う（援助疲れ）。国内の災害救援・被害量は <see cref="DisasterRules"/> が担い、
    /// ここは<b>越境する援助</b>の外交効果（opinion・依存・国内支持）だけを出す。
    /// opinion への加算は <see cref="DiplomacyRules"/> 側で行う想定。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ForeignAidRules
    {
        /// <summary>
        /// 援助による opinion 上昇＝援助量×基礎係数×（1＋困窮上乗せ×困窮度0..1）。
        /// 困窮時の援助は同額でも何倍も効く（雪中の炭・既定で最大3倍）。
        /// </summary>
        public static float OpinionGain(float aidAmount, float recipientNeed, ForeignAidParams p)
        {
            float needFactor = 1f + p.needBonusMax * Mathf.Clamp01(recipientNeed);
            return Mathf.Max(0f, aidAmount) * p.opinionPerAid * needFactor;
        }

        public static float OpinionGain(float aidAmount, float recipientNeed)
            => OpinionGain(aidAmount, recipientNeed, ForeignAidParams.Default);

        /// <summary>
        /// 災害救援の外交ボーナス（opinion 上昇）＝援助量×基礎係数×（敵対なら倍率）。
        /// 敵対国への災害救援は平時援助より強く効く＝敵の善意は記憶に残る。
        /// </summary>
        public static float DisasterDiplomacyBonus(float aidAmount, bool isHostile, ForeignAidParams p)
        {
            float mult = isHostile ? p.hostileReliefMultiplier : 1f;
            return Mathf.Max(0f, aidAmount) * p.opinionPerAid * mult;
        }

        public static float DisasterDiplomacyBonus(float aidAmount, bool isHostile)
            => DisasterDiplomacyBonus(aidAmount, isHostile, ForeignAidParams.Default);

        /// <summary>
        /// 紐付き援助の純益＝援助量×（実利係数×条件度 − 反発係数×条件度²）。
        /// 条件度（conditionality 0..1）が上がるほど実利（基地・投票・市場開放）は線形に増えるが、
        /// 反発は二乗で募る＝紐を付けすぎると純益は負（買収が露骨だと敵をつくる）。
        /// </summary>
        public static float StringsAttached(float aidAmount, float conditionality, ForeignAidParams p)
        {
            float c = Mathf.Clamp01(conditionality);
            return Mathf.Max(0f, aidAmount) * (p.leverageScale * c - p.backlashScale * c * c);
        }

        public static float StringsAttached(float aidAmount, float conditionality)
            => StringsAttached(aidAmount, conditionality, ForeignAidParams.Default);

        /// <summary>
        /// 援助依存の1tick後の値（0..1）＝依存＋（形成速度×援助フロー×伸び代(1−依存) − 回復速度×依存）×dt。
        /// 援助が流れ続けるほど自立が蝕まれて依存が深まり、止めればゆっくり自立へ戻る（形成は速く回復は遅い）。
        /// </summary>
        public static float DependencyTick(float dependency, float aidFlow, float dt, ForeignAidParams p)
        {
            float d = Mathf.Clamp01(dependency);
            float flow = Mathf.Max(0f, aidFlow);
            float delta = (p.dependencyGrowthRate * flow * (1f - d) - p.dependencyDecayRate * d) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(d + delta);
        }

        public static float DependencyTick(float dependency, float aidFlow, float dt)
            => DependencyTick(dependency, aidFlow, dt, ForeignAidParams.Default);

        /// <summary>
        /// 援助の突然カットが与えるショック（0..1）＝依存度×ショック幅。
        /// 依存させてから切る＝援助の武器化。深く依存させた相手ほど一撃が重い
        /// （安定度・産出の打撃係数として使う想定）。
        /// </summary>
        public static float SuddenWithdrawalShock(float dependency, ForeignAidParams p)
        {
            return Mathf.Clamp01(dependency) * p.withdrawalShockScale;
        }

        public static float SuddenWithdrawalShock(float dependency)
            => SuddenWithdrawalShock(dependency, ForeignAidParams.Default);

        /// <summary>
        /// 国内の援助疲れ（0..1）＝累積援助×蓄積速度×（1−見える成果0..1）。
        /// 成果が見えない援助は支持を失い、成果が完全に見えていれば疲れは積もらない。
        /// 国内支持の減算係数として使う想定。
        /// </summary>
        public static float DonorFatigue(float cumulativeAid, float visibleResults, ForeignAidParams p)
        {
            return Mathf.Clamp01(Mathf.Max(0f, cumulativeAid) * p.fatigueRate * (1f - Mathf.Clamp01(visibleResults)));
        }

        public static float DonorFatigue(float cumulativeAid, float visibleResults)
            => DonorFatigue(cumulativeAid, visibleResults, ForeignAidParams.Default);
    }
}
