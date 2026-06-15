using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 名誉（名に対する義理）の状態（KIKU-4 #1832→#1841・ルース・ベネディクト『菊と刀』）の純データ。
    /// 名誉は侮辱で毀損され、公的な雪辱で回復される。全フィールド 0..1（可変＝時間更新で書き換える）。
    /// </summary>
    [System.Serializable]
    public readonly struct HonorState
    {
        /// <summary>現在の名誉 0..1（高いほど面目が立っている）。</summary>
        public readonly float honor;
        /// <summary>累積した名誉の毀損 0..1（侮辱・面目を潰された度合い）。</summary>
        public readonly float damage;
        /// <summary>面目への敏感さ 0..1（体面をどれだけ重んじるか＝毀損で雪辱の義務が募りやすい）。</summary>
        public readonly float faceSensitivity;

        public HonorState(float honor, float damage, float faceSensitivity)
        {
            this.honor = Mathf.Clamp01(honor);
            this.damage = Mathf.Clamp01(damage);
            this.faceSensitivity = Mathf.Clamp01(faceSensitivity);
        }
    }

    /// <summary>名誉の毀損と公的回復（#1832）の調整係数。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct HonorParams
    {
        /// <summary>公然性が毀損に効く重み（同じ侮辱でも人前ほど重い。0なら公然性は無関係）。</summary>
        public readonly float publicnessWeight;
        /// <summary>雪辱行為1.0・証人1.0あたりの公的回復の最大幅（公然と晴らすほど大きく回復）。</summary>
        public readonly float restorationScale;
        /// <summary>名誉の応酬がエスカレートする最大幅（相手の名誉が高いほど引かず激化）。</summary>
        public readonly float escalationScale;
        /// <summary>仲介者が面目を保つ出口を作る最大幅（メンツを潰さず収める＝雪辱衝動を逃がす）。</summary>
        public readonly float faceSavingScale;
        /// <summary>名誉を傷つけられたと判定する毀損の既定閾値。</summary>
        public readonly float breachThreshold;

        public HonorParams(float publicnessWeight, float restorationScale, float escalationScale,
                           float faceSavingScale, float breachThreshold)
        {
            this.publicnessWeight = Mathf.Clamp01(publicnessWeight);
            this.restorationScale = Mathf.Clamp01(restorationScale);
            this.escalationScale = Mathf.Clamp01(escalationScale);
            this.faceSavingScale = Mathf.Clamp01(faceSavingScale);
            this.breachThreshold = Mathf.Clamp01(breachThreshold);
        }

        /// <summary>
        /// 既定＝公然性重み0.6/公的回復上限0.8/応酬上限0.7/面目出口上限0.6/名誉毀損判定閾値0.4。
        /// </summary>
        public static HonorParams Default =>
            new HonorParams(0.6f, 0.8f, 0.7f, 0.6f, 0.4f);
    }

    /// <summary>
    /// 名誉の毀損と公的回復の純ロジック（KIKU-4 #1832→#1841・ルース・ベネディクト『菊と刀』＝名に対する義理）。
    /// 恥の文化では名誉は侮辱・面目を潰されること（毀損）で傷つき、それを<b>公的な行為</b>（雪辱・果たし合い・
    /// 公的謝罪の要求）で<b>可視的に</b>晴らさねばならない。罪の文化の内面的な償い（良心の納得）とは異なり、
    /// 名誉の回復は人目の前で行われる＝晴らさねばならない衝動が果たし合いや抗争へ発展しうる。
    /// <see cref="ReputationRules"/>（個人の武名＝勝敗で増減・敵威圧・名声）とは別＝こちらは恥文化の名誉
    /// （毀損は可視・回復は公的儀礼）。<see cref="HonorsRules"/>（栄典＝授与の名誉インフレ）とは別＝
    /// 侮辱と雪辱の名誉。同 EPIC KIKU の <see cref="ShameRules"/>（人目による外的制御）/GiriRules（義理）と接続。
    /// <see cref="FaceSavingExit"/> は EscalationRules の出口と同型だが名誉に特化（メンツを保って収める）。
    /// 値は徹底して 0..1 に clamp・乱数なし決定論。盤面非依存の plain 引数。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HonorRules
    {
        /// <summary>
        /// 名誉の毀損 0..1：侮辱の重さ×公然性。人前での侮辱ほど重い（publicnessWeight ぶんを公然性で底上げ）。
        /// 公然性ゼロでも侮辱自体の毀損は残る（私的な侮辱でも面目は傷つくが、人目があるほど重くなる）。
        /// </summary>
        public static float HonorDamage(float insultSeverity, float publicness, HonorParams p)
        {
            float s = Mathf.Clamp01(insultSeverity);
            float pub = Mathf.Clamp01(publicness);
            // 侮辱を土台に、公然性で効きを底上げ（pub=0なら(1-w)倍・pub=1なら満額）。
            float factor = (1f - p.publicnessWeight) + p.publicnessWeight * pub;
            return Mathf.Clamp01(s * factor);
        }

        public static float HonorDamage(float insultSeverity, float publicness)
            => HonorDamage(insultSeverity, publicness, HonorParams.Default);

        /// <summary>
        /// 雪辱の義務 0..1：毀損が大きく面目に敏感なほど晴らす義務が募る。
        /// honorDamage×faceSensitivity＝面目に鈍感（faceSensitivity=0）なら毀損されても義務は生じない。
        /// </summary>
        public static float ObligationToRestore(float honorDamage, float faceSensitivity)
        {
            return Mathf.Clamp01(Mathf.Clamp01(honorDamage) * Mathf.Clamp01(faceSensitivity));
        }

        /// <summary>
        /// 公的な名誉回復 0..1：公的な雪辱行為×証人で名誉を回復する（内面でなく公然と晴らす）。
        /// restorationAct×witnessCount×restorationScale＝証人ゼロでは公的回復は成立しない（人目の前でこそ晴れる）。
        /// </summary>
        public static float PublicRestoration(float restorationAct, float witnessCount, HonorParams p)
        {
            float act = Mathf.Clamp01(restorationAct);
            float w = Mathf.Clamp01(witnessCount);
            return Mathf.Clamp01(act * w * p.restorationScale);
        }

        public static float PublicRestoration(float restorationAct, float witnessCount)
            => PublicRestoration(restorationAct, witnessCount, HonorParams.Default);

        /// <summary>
        /// 晴らさねばならない衝動 0..1：毀損が衝動を生み、和解の申し出で和らぐ。
        /// honorDamage×(1-reconciliationOffered)＝相手が和解を申し出るほど雪辱の衝動は鎮まる。
        /// </summary>
        public static float VengeanceImperative(float honorDamage, float reconciliationOffered)
        {
            float d = Mathf.Clamp01(honorDamage);
            float r = Mathf.Clamp01(reconciliationOffered);
            return Mathf.Clamp01(d * (1f - r));
        }

        /// <summary>
        /// エスカレーションのリスク 0..1：雪辱衝動×相手の名誉。名誉の応酬が果たし合い・抗争へ発展するリスク。
        /// 相手も名誉が高いほど引かずに応酬し激化する（双方が面目を賭ける）。
        /// </summary>
        public static float EscalationRisk(float vengeanceImperative, float opponentHonor, HonorParams p)
        {
            float v = Mathf.Clamp01(vengeanceImperative);
            float o = Mathf.Clamp01(opponentHonor);
            return Mathf.Clamp01(v * o * p.escalationScale);
        }

        public static float EscalationRisk(float vengeanceImperative, float opponentHonor)
            => EscalationRisk(vengeanceImperative, opponentHonor, HonorParams.Default);

        /// <summary>
        /// 回復後の名誉 0..1：公的回復行為で現在の名誉がどこまで戻るか。
        /// currentHonor + publicRestoration を 0..1 にクランプ（回復行為のぶんだけ面目が立ち直る）。
        /// </summary>
        public static float HonorRecovered(float currentHonor, float publicRestoration)
        {
            return Mathf.Clamp01(Mathf.Clamp01(currentHonor) + Mathf.Clamp01(publicRestoration));
        }

        /// <summary>
        /// 公的雪辱を要する度合い 0..1：内面の納得でなく公的な雪辱が要る度合い（恥文化＝公的回復）。
        /// publicVindication×(1-internalAcceptance)＝本人が内心で折り合っても、公的に晴れていなければ名誉は癒えない。
        /// 内面の納得が高いほど（罪文化的）公的雪辱の必要は下がる。
        /// </summary>
        public static float PrivateVsPublicHealing(float internalAcceptance, float publicVindication)
        {
            float a = Mathf.Clamp01(internalAcceptance);
            float pv = Mathf.Clamp01(publicVindication);
            return Mathf.Clamp01(pv * (1f - a));
        }

        /// <summary>
        /// 面目を保つ出口 0..1：仲介者がメンツを潰さず収める出口を作る（雪辱衝動を逃がす）。
        /// honorDamage×mediatorPresence×faceSavingScale＝仲介者が居て毀損が大きいほど、面目を保ったまま収める余地が生まれる。
        /// EscalationRules の FaceSavingExit と同型だが名誉に特化。
        /// </summary>
        public static float FaceSavingExit(float honorDamage, float mediatorPresence, HonorParams p)
        {
            float d = Mathf.Clamp01(honorDamage);
            float m = Mathf.Clamp01(mediatorPresence);
            return Mathf.Clamp01(d * m * p.faceSavingScale);
        }

        public static float FaceSavingExit(float honorDamage, float mediatorPresence)
            => FaceSavingExit(honorDamage, mediatorPresence, HonorParams.Default);

        /// <summary>
        /// 名誉毀損の判定：毀損が threshold 以上なら名誉を傷つけられた状態と見なす（雪辱の対象）。
        /// </summary>
        public static bool IsHonorBreached(float honorDamage, float threshold)
        {
            return Mathf.Clamp01(honorDamage) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="HonorParams.breachThreshold"/>）での名誉毀損判定。</summary>
        public static bool IsHonorBreached(float honorDamage)
            => IsHonorBreached(honorDamage, HonorParams.Default.breachThreshold);
    }
}
