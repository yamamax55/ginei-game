using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 情報の非対称と風説の相場（#1074・狼と香辛料・純ロジック test-first・唯一の窓口）。
    /// 市場参加者の間で情報に差があると、情報を持つ者が持たざる者から利益を得る（知は力）。
    /// 噂（真偽不明の情報）も真偽を問わず相場を動かす（風説の流布）。
    /// 分担：均衡価格そのものは <see cref="MarketRules"/>(需給均衡)、改鋳・通貨投機は CoinageSpeculationRules(同Wave並行)、
    /// 情報の精度・点推定は <see cref="ReconRules"/>(偵察)。ここは「情報格差が生む利得・噂が動かす相場」だけを式にする。
    /// 全入力クランプ・乱数なし決定論。調整値は <see cref="InformationAsymmetryParams"/> に集約（既定 <see cref="InformationAsymmetryParams.Default"/>）。
    /// </summary>
    public static class InformationAsymmetryRules
    {
        /// <summary>
        /// 情報優位＝自分の情報量と市場平均の差（>0で優位＝知っている分だけ儲けられる）。
        /// </summary>
        public static float InformationEdge(float ownInfo, float marketInfo)
        {
            float own = Mathf.Clamp01(ownInfo);
            float market = Mathf.Clamp01(marketInfo);
            return own - market; // -1..1（負＝相手の方が知っている＝逆に取られる）
        }

        /// <summary>
        /// 情報裁定の利得（#1074・知は力）。情報優位を持つ者が無知な相手から取り、優位が無ければ取れない。
        /// 利得＝情報優位×取引規模×価格乖離×係数。優位が負（情報劣位）なら損（負の利得＝取られる）。
        /// </summary>
        public static float ArbitrageProfit(float informationEdge, float tradeSize, float priceGap, InformationAsymmetryParams p)
        {
            float edge = Mathf.Clamp(informationEdge, -1f, 1f);
            float size = Mathf.Max(0f, tradeSize);
            float gap = Mathf.Max(0f, priceGap);
            return edge * size * gap * p.arbitrageGain;
        }

        /// <summary>
        /// 風説による相場変動（#1074・風説の流布）。噂は真偽に関わらず相場を動かす。
        /// 変動＝噂の強さ×信憑性×市場の騙されやすさ×係数（強く・信じられ・市場が軽信なほど大きく動く）。
        /// 真偽は問わない＝信憑性は「市場がどれだけ信じるか」であって真実度ではない。
        /// </summary>
        public static float RumorPriceMovement(float rumorStrength, float rumorCredibility, float marketGullibility, InformationAsymmetryParams p)
        {
            float strength = Mathf.Clamp01(rumorStrength);
            float cred = Mathf.Clamp01(rumorCredibility);
            float gull = Mathf.Clamp01(marketGullibility);
            return strength * cred * gull * p.rumorImpact;
        }

        /// <summary>
        /// 逆選択（#1074・レモン市場）。情報の少ない側が損を引くため買い手が用心し市場が縮む。
        /// 情報格差が大きいほど取引が成立しにくくなる係数（1=健全な市場・0=情報格差で市場崩壊）。
        /// </summary>
        public static float AdverseSelection(float infoGap, InformationAsymmetryParams p)
        {
            float gap = Mathf.Clamp01(infoGap);
            // 市場参加度＝1−格差×減退率。買い手が用心して市場が縮む。
            return Mathf.Clamp01(1f - gap * p.adverseSelectionDecay);
        }

        /// <summary>
        /// 情報優位の減衰（#1074・早い者勝ち）。情報が広まると優位が消える。
        /// 拡散速度が速いほど・dt が長いほど優位が0へ近づく（指数減衰＝広まりきれば誰も儲けられない）。
        /// </summary>
        public static float InformationDecay(float edge, float informationSpread, float dt, InformationAsymmetryParams p)
        {
            if (dt <= 0f) return edge;
            float e = Mathf.Clamp(edge, -1f, 1f);
            float spread = Mathf.Clamp01(informationSpread);
            float decay = spread * p.spreadDecayRate * dt; // 0..∞
            float remain = Mathf.Max(0f, 1f - decay);      // 残存率（拡散しきれば0）
            return e * remain;
        }

        /// <summary>
        /// シグナリングの費用（#1074・口先だけでは動かない）。情報を信じてもらうには裏付けの費用が要る。
        /// 必要な信憑性が高いほど費用が非線形に増す（高い信用を得るほど割高＝安い嘘では信じてもらえない）。
        /// </summary>
        public static float SignalingCost(float credibilityNeeded, InformationAsymmetryParams p)
        {
            float need = Mathf.Clamp01(credibilityNeeded);
            // 二乗で逓増＝高い信用ほど割高（裏付けの費用は安く済まない）。
            return need * need * p.signalingCostScale;
        }
    }

    /// <summary>
    /// 情報の非対称の調整値（#1074・裁定利得/風説/逆選択/拡散減衰/シグナリング費用）。
    /// 既定 <see cref="Default"/>。全フィールドは非負へクランプ。
    /// </summary>
    public readonly struct InformationAsymmetryParams
    {
        /// <summary>情報裁定の利得係数（情報優位×規模×乖離への倍率）。</summary>
        public readonly float arbitrageGain;
        /// <summary>風説の相場変動係数（噂×信憑性×軽信への倍率）。</summary>
        public readonly float rumorImpact;
        /// <summary>逆選択の市場減退率（情報格差が市場をどれだけ縮めるか）。</summary>
        public readonly float adverseSelectionDecay;
        /// <summary>情報拡散による優位の減衰速度（/戦略秒）。</summary>
        public readonly float spreadDecayRate;
        /// <summary>シグナリング費用の係数（必要信憑性²への倍率）。</summary>
        public readonly float signalingCostScale;

        public InformationAsymmetryParams(float arbitrageGain, float rumorImpact, float adverseSelectionDecay, float spreadDecayRate, float signalingCostScale)
        {
            this.arbitrageGain = Mathf.Max(0f, arbitrageGain);
            this.rumorImpact = Mathf.Max(0f, rumorImpact);
            this.adverseSelectionDecay = Mathf.Max(0f, adverseSelectionDecay);
            this.spreadDecayRate = Mathf.Max(0f, spreadDecayRate);
            this.signalingCostScale = Mathf.Max(0f, signalingCostScale);
        }

        /// <summary>
        /// 既定＝裁定利得1・風説変動0.5（噂は相場を半幅まで動かす）・逆選択減退0.8（格差最大で市場2割まで縮む）・
        /// 拡散減衰1（拡散1なら1秒で優位消失）・シグナリング費用1（最大信用に費用1）。
        /// </summary>
        public static InformationAsymmetryParams Default =>
            new InformationAsymmetryParams(1f, 0.5f, 0.8f, 1f, 1f);
    }
}
