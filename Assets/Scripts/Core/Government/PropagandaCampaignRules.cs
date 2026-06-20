using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 組織的プロパガンダ作戦（世論操作キャンペーン）の調整係数。
    /// メッセージの到達・刷り込み・信憑性・反宣伝・飽和の各力学の重みを束ねる。
    /// </summary>
    public readonly struct PropagandaCampaignParams
    {
        /// <summary>繰り返しの逓減度（大きいほど反復のうまみが早く頭打ちになる）。</summary>
        public readonly float repetitionDiminish;
        /// <summary>誇張が信憑性を割り引く強さ（1+誇張×gain で割る）。</summary>
        public readonly float exaggerationPenalty;
        /// <summary>刷り込みの利得（到達×信憑×素地に掛ける増幅）。</summary>
        public readonly float indoctrinationGain;
        /// <summary>反プロパガンダ打ち消し圧の利得。</summary>
        public readonly float counterGain;
        /// <summary>過剰宣伝による白け（飽和疲労）の利得。</summary>
        public readonly float saturationGain;

        public PropagandaCampaignParams(float repetitionDiminish, float exaggerationPenalty, float indoctrinationGain, float counterGain, float saturationGain)
        {
            this.repetitionDiminish = Mathf.Clamp01(repetitionDiminish);
            this.exaggerationPenalty = Mathf.Max(0f, exaggerationPenalty);
            this.indoctrinationGain = Mathf.Clamp01(indoctrinationGain);
            this.counterGain = Mathf.Clamp01(counterGain);
            this.saturationGain = Mathf.Clamp01(saturationGain);
        }

        /// <summary>既定＝反復逓減0.5・誇張割引0.5・刷り込み利得0.8・反宣伝利得1.0・飽和利得0.6。</summary>
        public static PropagandaCampaignParams Default => new PropagandaCampaignParams(0.5f, 0.5f, 0.8f, 1.0f, 0.6f);
    }

    /// <summary>
    /// プロパガンダ作戦（能動的な世論操作キャンペーン）の純ロジック。
    /// メッセージの浸透（媒体×反復の逓減）、信憑性と誇張のトレードオフ、受け手の素地への刷り込み、
    /// 対立陣営の反プロパガンダによる打ち消し、世論の転換、過剰宣伝による白け（飽和疲労）を扱う。
    /// <see cref="PublicOpinionRules"/>（世論<b>場</b>のダイナミクス）とは別＝こちらは能動的な宣伝<b>工作</b>。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PropagandaCampaignRules
    {
        /// <summary>
        /// メッセージ到達度（0..1）＝媒体浸透 mediaPenetration(0..1) × 繰り返しの効き。
        /// 反復 repetition(0..) は逓減（repetition/(repetition+repetitionDiminish)）で頭打ち＝刷り込みは増えるが飽和もする。
        /// 反復ゼロでも媒体浸透ぶんの最低到達は出る（媒体に出した時点で一定は届く）。
        /// </summary>
        public static float MessageReach(float mediaPenetration, float repetition, PropagandaCampaignParams p)
        {
            float media = Mathf.Clamp01(mediaPenetration);
            float rep = Mathf.Max(0f, repetition);
            // 反復の効き 0.5..1：rep=0 で 0.5、増えるほど 1 へ逓減収束
            float repFactor = 0.5f + 0.5f * (rep / (rep + p.repetitionDiminish + 1e-6f));
            return Mathf.Clamp01(media * repFactor);
        }

        public static float MessageReach(float mediaPenetration, float repetition)
            => MessageReach(mediaPenetration, repetition, PropagandaCampaignParams.Default);

        /// <summary>
        /// 信憑性（0..1）＝到達 messageReach(0..1) × 事実性 factualBasis(0..1) / (1 + 誇張×penalty)。
        /// 事実に基づくほど信じられ、誇張 exaggeration(0..) が増えるほど割り引かれる（信憑性と誇張のトレードオフ）。
        /// </summary>
        public static float Credibility(float messageReach, float factualBasis, float exaggeration, PropagandaCampaignParams p)
        {
            float reach = Mathf.Clamp01(messageReach);
            float fact = Mathf.Clamp01(factualBasis);
            float exag = Mathf.Max(0f, exaggeration);
            float cred = reach * fact / (1f + exag * p.exaggerationPenalty);
            return Mathf.Clamp01(cred);
        }

        public static float Credibility(float messageReach, float factualBasis, float exaggeration)
            => Credibility(messageReach, factualBasis, exaggeration, PropagandaCampaignParams.Default);

        /// <summary>
        /// 刷り込み度（0..1）＝到達 messageReach × 信憑 credibility × 受け手の素地 audienceReceptivity(0..1)。
        /// 届いて信じられ、かつ受け手に下地があるほど刷り込まれる＝三者の積に indoctrinationGain を掛ける。
        /// どれか一つでも欠ければ刷り込みは進まない。
        /// </summary>
        public static float Indoctrination(float messageReach, float credibility, float audienceReceptivity, PropagandaCampaignParams p)
        {
            float reach = Mathf.Clamp01(messageReach);
            float cred = Mathf.Clamp01(credibility);
            float recept = Mathf.Clamp01(audienceReceptivity);
            return Mathf.Clamp01(reach * cred * recept * (1f + p.indoctrinationGain));
        }

        public static float Indoctrination(float messageReach, float credibility, float audienceReceptivity)
            => Indoctrination(messageReach, credibility, audienceReceptivity, PropagandaCampaignParams.Default);

        /// <summary>
        /// 反プロパガンダの打ち消し圧（0..1）＝敵の反宣伝努力 enemyEffort(0..1) / (敵努力 + 自陣の信憑 ownCredibility(0..1))。
        /// 敵が強く、こちらの信憑性が低いほど打ち消される＝信用が反論への盾になる。
        /// 両者ゼロなら打ち消し圧ゼロ。counterGain で全体の効きを調整。
        /// </summary>
        public static float CounterPropaganda(float enemyEffort, float ownCredibility, PropagandaCampaignParams p)
        {
            float enemy = Mathf.Clamp01(enemyEffort);
            float own = Mathf.Clamp01(ownCredibility);
            float denom = enemy + own;
            if (denom <= 1e-6f) return 0f;
            return Mathf.Clamp01(enemy / denom * p.counterGain);
        }

        public static float CounterPropaganda(float enemyEffort, float ownCredibility)
            => CounterPropaganda(enemyEffort, ownCredibility, PropagandaCampaignParams.Default);

        /// <summary>
        /// 世論の転換（-1..1）＝刷り込み indoctrination(0..1) − 打ち消し圧 counterPropaganda(0..1)。
        /// 刷り込みが勝てば正（世論が動く）、反宣伝が勝てば負（逆に押し返される）。
        /// </summary>
        public static float OpinionShift(float indoctrination, float counterPropaganda, PropagandaCampaignParams p)
        {
            float indoc = Mathf.Clamp01(indoctrination);
            float counter = Mathf.Clamp01(counterPropaganda);
            return Mathf.Clamp(indoc - counter, -1f, 1f);
        }

        public static float OpinionShift(float indoctrination, float counterPropaganda)
            => OpinionShift(indoctrination, counterPropaganda, PropagandaCampaignParams.Default);

        /// <summary>
        /// 飽和疲労（白け・0..1）＝過剰な繰り返し repetition(0..) × 誇張 exaggeration(0..)。
        /// 同じ煽りを繰り返し誇張するほど受け手が白ける＝反復は逓減で効かせ、誇張で増幅し saturationGain を掛ける。
        /// </summary>
        public static float SaturationFatigue(float repetition, float exaggeration, PropagandaCampaignParams p)
        {
            float rep = Mathf.Max(0f, repetition);
            float exag = Mathf.Max(0f, exaggeration);
            float repSat = rep / (rep + p.repetitionDiminish + 1e-6f); // 0..1 逓減
            float exagSat = exag / (exag + 1f);                        // 0..1 逓減
            return Mathf.Clamp01(repSat * (0.5f + 0.5f * exagSat) * p.saturationGain);
        }

        public static float SaturationFatigue(float repetition, float exaggeration)
            => SaturationFatigue(repetition, exaggeration, PropagandaCampaignParams.Default);

        /// <summary>
        /// 作戦の正味効果（-1..1）＝世論転換 opinionShift(-1..1) − 飽和疲労 saturationFatigue(0..1)。
        /// 世論が動いても白けが上回れば逆効果。宣伝の押し引きと過剰宣伝のしっぺ返しの綱引き。
        /// </summary>
        public static float CampaignEffectiveness(float opinionShift, float saturationFatigue, PropagandaCampaignParams p)
        {
            float shift = Mathf.Clamp(opinionShift, -1f, 1f);
            float fatigue = Mathf.Clamp01(saturationFatigue);
            return Mathf.Clamp(shift - fatigue, -1f, 1f);
        }

        public static float CampaignEffectiveness(float opinionShift, float saturationFatigue)
            => CampaignEffectiveness(opinionShift, saturationFatigue, PropagandaCampaignParams.Default);

        /// <summary>
        /// 宣伝が効いたか＝正味効果 campaignEffectiveness が閾値 threshold を上回ったか。
        /// </summary>
        public static bool IsPropagandaEffective(float campaignEffectiveness, float threshold)
            => Mathf.Clamp(campaignEffectiveness, -1f, 1f) > threshold;

        /// <summary>既定閾値 0.1 で宣伝が効いたか。</summary>
        public static bool IsPropagandaEffective(float campaignEffectiveness)
            => IsPropagandaEffective(campaignEffectiveness, 0.1f);
    }
}
