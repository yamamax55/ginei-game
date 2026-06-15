using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// コヴェナント（信約）の調整係数（LEVI-3 #1463・ホッブズ『リヴァイアサン』）。既定は <see cref="Default"/>。
    /// </summary>
    public readonly struct CovenantParams
    {
        /// <summary>保護の供給で安全（securityDelivered）が占める重み（残りは主権者の戦力）。</summary>
        public readonly float securityWeight;
        /// <summary>保護がこれ未満なら服従義務が消滅する既定閾値（守れない主権者には従う理由がない）。</summary>
        public readonly float dissolutionThreshold;
        /// <summary>保護の失敗で脅威の実現が占める重み（残りは主権者の応答の欠如）。</summary>
        public readonly float threatWeight;

        public CovenantParams(float securityWeight, float dissolutionThreshold, float threatWeight)
        {
            this.securityWeight = securityWeight;
            this.dissolutionThreshold = dissolutionThreshold;
            this.threatWeight = threatWeight;
        }

        /// <summary>既定（安全の重み0.6／服従消滅閾値0.4／脅威の重み0.6）。</summary>
        public static CovenantParams Default => new CovenantParams(0.6f, 0.4f, 0.6f);
    }

    /// <summary>
    /// 保護と服従の契約＝ホッブズの「コヴェナント（信約）」の純ロジック（LEVI-3 #1463・参考『リヴァイアサン』）。
    /// 人々は安全のために自然権を主権者へ譲渡し服従するが、この契約の目的は<b>保護</b>なので、
    /// 主権者が臣民を保護できなくなった瞬間に服従義務は消滅する＝<b>保護と服従は相互的</b>
    /// （the mutual relation between protection and obedience）。防衛失敗で服従義務が消え、
    /// 合意撤回（<see cref="ConsentRules.Withdraw"/>）へ転送される。乱数なし・決定論。
    /// <para>
    /// 分担：<see cref="ConsentRules"/>(被治者の協力・合意撤回＝服従消滅の<b>転送先</b>)／
    /// <see cref="AnarchyCostRules"/>(同EPIC LEVI＝主権が無い場合のコスト＝主権の価値)／
    /// <see cref="Polity"/>(借り物の権力＝合意で成り立つ統治の被治者)／
    /// <see cref="MagnaCartaRules"/>(抵抗権＝契約による王権制約)。本クラスは
    /// 「守れない主権者への服従は消える＝<see cref="ConsentRules.Withdraw"/> へ転送」を式に出す。
    /// </para>
    /// </summary>
    public static class CovenantRules
    {
        /// <summary>
        /// 主権者が臣民に提供する保護（安全の供給・0..1）＝主権者の戦力と実際に届けられた安全の加重和。
        /// 戦力があっても安全が届かなければ保護は低い（保護＝結果としての安全）。
        /// </summary>
        public static float ProtectionProvided(float sovereignStrength, float securityDelivered, CovenantParams p)
        {
            float strength = Mathf.Clamp01(sovereignStrength);
            float security = Mathf.Clamp01(securityDelivered);
            float w = Mathf.Clamp01(p.securityWeight);
            return Mathf.Clamp01((1f - w) * strength + w * security);
        }

        /// <summary>提供される保護（既定パラメータ）。</summary>
        public static float ProtectionProvided(float sovereignStrength, float securityDelivered)
            => ProtectionProvided(sovereignStrength, securityDelivered, CovenantParams.Default);

        /// <summary>
        /// 保護に応じて生じる服従義務（0..1）＝保護されるから従う（相互的）。
        /// 保護が高いほど服従義務は強く、保護が無ければ服従義務も生じない。
        /// </summary>
        public static float ObedienceObligation(float protectionProvided)
            => Mathf.Clamp01(protectionProvided);

        /// <summary>
        /// 契約の健全性（0..1）＝保護と服従が釣り合っているほど健全。
        /// どちらかが欠けると契約は壊れる（保護はあるが服従しない／服従するが保護されない＝乖離が大きいほど低い）。
        /// </summary>
        public static float CovenantIntegrity(float protectionProvided, float obedience)
        {
            float prot = Mathf.Clamp01(protectionProvided);
            float obey = Mathf.Clamp01(obedience);
            // 釣り合い＝両者の最小値（低い方が契約を縛る）から乖離ペナルティを引く
            float balance = Mathf.Min(prot, obey);
            float gap = Mathf.Abs(prot - obey);
            return Mathf.Clamp01(balance * (1f - gap));
        }

        /// <summary>
        /// 主権者が脅威から守れなかった度合い（防衛の失敗・0..1）＝脅威の実現と主権者の応答の欠如の加重和。
        /// 脅威が実現し、かつ主権者が応えられないほど失敗は大きい（応答が完全なら失敗は小さい）。
        /// </summary>
        public static float ProtectionFailure(float threatRealized, float sovereignResponse, CovenantParams p)
        {
            float threat = Mathf.Clamp01(threatRealized);
            float response = Mathf.Clamp01(sovereignResponse);
            float w = Mathf.Clamp01(p.threatWeight);
            return Mathf.Clamp01(w * threat + (1f - w) * (1f - response));
        }

        /// <summary>保護の失敗（既定パラメータ）。</summary>
        public static float ProtectionFailure(float threatRealized, float sovereignResponse)
            => ProtectionFailure(threatRealized, sovereignResponse, CovenantParams.Default);

        /// <summary>
        /// 服従義務の消滅度（0..1）＝保護の失敗が閾値を割ると（＝失敗が閾値を超えると）服従義務が消える。
        /// 守れない主権者には従う理由がない。閾値手前では0、閾値超で失敗に比例して消滅が進む。
        /// </summary>
        public static float ObedienceDissolution(float protectionFailure, float threshold)
        {
            float fail = Mathf.Clamp01(protectionFailure);
            float th = Mathf.Clamp01(threshold);
            if (fail <= th) return 0f;
            float span = Mathf.Max(0.0001f, 1f - th);
            return Mathf.Clamp01((fail - th) / span);
        }

        /// <summary>服従義務の消滅度（既定閾値）。</summary>
        public static float ObedienceDissolution(float protectionFailure)
            => ObedienceDissolution(protectionFailure, CovenantParams.Default.dissolutionThreshold);

        /// <summary>
        /// 服従義務の消滅が合意撤回（<see cref="ConsentRules.Withdraw"/>）へ転送される量（0..1）＝離反・新主権者探し。
        /// 服従義務が消えた分だけ被治者は協力を引き上げる（守れない主権者から手を引く）。
        /// </summary>
        public static float WithdrawalTrigger(float obedienceDissolution)
            => Mathf.Clamp01(obedienceDissolution);

        /// <summary>
        /// 征服による主権（0..1）＝征服者でも保護を提供すれば服従が移る（ホッブズ＝保護できる者が主権者）。
        /// 勝者が保護を供給するほど臣民の服従は正当に移る（保護＝服従の根拠なので勝者への服従も正当）。
        /// </summary>
        public static float SovereignByAcquisition(float conquerorProtection)
            => Mathf.Clamp01(conquerorProtection);

        /// <summary>
        /// 自己保存の権利（0..1）＝譲渡不能の権利の強さ＝臣民に迫る死の脅威に比例する。
        /// 主権者でも臣民に死を強制できない＝服従の限界（死の脅威が大きいほど自己保存権が服従に優先する）。
        /// </summary>
        public static float SelfPreservationRight(float mortalThreat)
            => Mathf.Clamp01(mortalThreat);

        /// <summary>
        /// 契約が破れ服従が消えた判定＝保護の失敗が閾値を超えたとき true（守れない＝契約破綻）。
        /// </summary>
        public static bool IsCovenantBroken(float protectionFailure, float threshold)
            => Mathf.Clamp01(protectionFailure) > Mathf.Clamp01(threshold);

        /// <summary>契約破綻判定（既定閾値）。</summary>
        public static bool IsCovenantBroken(float protectionFailure)
            => IsCovenantBroken(protectionFailure, CovenantParams.Default.dissolutionThreshold);
    }
}
