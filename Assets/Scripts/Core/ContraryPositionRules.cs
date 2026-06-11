using UnityEngine;

namespace Ginei
{
    /// <summary>逆張り迫害・勝利構造（マッカイ『狂気とバブル』型）の調整係数。</summary>
    public readonly struct ContraryPositionParams
    {
        /// <summary>迫害コストの基礎強度（熱狂×可視性に掛ける係数）。</summary>
        public readonly float persecutionScale;
        /// <summary>名声逆転利得の基礎強度（崩壊後に逆らっていた熱狂の高さに掛ける係数）。</summary>
        public readonly float vindicationScale;
        /// <summary>生存判定の基礎レジリエンス重み（迫害コストに抗う粘り強さの効き）。</summary>
        public readonly float resilienceWeight;
        /// <summary>タイミング鋭さ（ピーク近接が迫害・利得をどれだけ非線形に跳ねさせるか）。</summary>
        public readonly float timingSharpness;
        /// <summary>予言者として認知される名声しきい値の既定。</summary>
        public readonly float prophetThreshold;

        public ContraryPositionParams(float persecutionScale, float vindicationScale, float resilienceWeight,
                                      float timingSharpness, float prophetThreshold)
        {
            this.persecutionScale = Mathf.Max(0f, persecutionScale);
            this.vindicationScale = Mathf.Max(0f, vindicationScale);
            this.resilienceWeight = Mathf.Clamp01(resilienceWeight);
            this.timingSharpness = Mathf.Max(0f, timingSharpness);
            this.prophetThreshold = Mathf.Clamp01(prophetThreshold);
        }

        /// <summary>既定＝迫害強度1.0・逆転利得強度1.2・レジリエンス重み0.6・タイミング鋭さ1.5・予言者しきい値0.6。</summary>
        public static ContraryPositionParams Default => new ContraryPositionParams(1f, 1.2f, 0.6f, 1.5f, 0.6f);
    }

    /// <summary>
    /// 逆張り迫害・勝利構造の純ロジック（MNIA-3 #1624・マッカイ『狂気とバブル（異常な大衆妄想と群集の狂気）』型）。
    /// バブル（熱狂＝マニア）のピークで「これは異常だ」と群集に公然と逆らう者は、嘲笑・排斥という<b>迫害コスト</b>を払うが、
    /// 崩壊後には立場が逆転して<b>予言者＝勝者</b>になる＝逆張りの損益は時間で反転する。
    /// 核は「ピークで群集に逆らう者は最も叩かれ、生き延びれば最も報われる＝<b>早すぎる正しさは破滅と隣り合わせ</b>」。
    /// 高い熱狂に逆らっていたほど迫害は重く（PersecutionCost）、崩壊後の名声逆転も大きい（VindicationGain）が、
    /// 迫害を生き延びられなければ（SurvivalChance）逆転利得を回収できず迫害損のみが残る（NetPayoff）。
    /// <see cref="PreferenceFalsificationRules"/>（表明と本音の乖離＝沈黙の偽装）とは別＝こちらは<b>群集に公然と逆らう者</b>の
    /// 迫害コストと事後の立場逆転を扱う。<see cref="ReputationRules"/>（会戦の名声増減）は名声の汎用更新窓口で、
    /// 本ルールは逆張りという特殊文脈の名声逆転を担い、<see cref="ManiaRules"/>（信念感染・同 EPIC）が熱狂そのものの伝播を扱う。
    /// 乱数は roll 引数で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ContraryPositionRules
    {
        /// <summary>
        /// 迫害コスト（0..1）。熱狂 maniaIntensity(0..1) が高く、逆張りの目立ち方 contrarianVisibility(0..1) が
        /// 大きいほど叩かれる＝ピークで公然と逆らう者が最も迫害される。両者の積を persecutionScale で増幅しクランプ。
        /// 熱狂0なら（群集がいないので）迫害は無く、隠れて逆らえば（可視性0）コストも無い。
        /// </summary>
        public static float PersecutionCost(float maniaIntensity, float contrarianVisibility, ContraryPositionParams p)
        {
            float mania = Mathf.Clamp01(maniaIntensity);
            float vis = Mathf.Clamp01(contrarianVisibility);
            return Mathf.Clamp01(mania * vis * p.persecutionScale);
        }

        public static float PersecutionCost(float maniaIntensity, float contrarianVisibility)
            => PersecutionCost(maniaIntensity, contrarianVisibility, ContraryPositionParams.Default);

        /// <summary>
        /// 名声逆転利得（0..1）。逆張りした時点の熱狂 maniaIntensityAtStance(0..1) が高いほど、
        /// 崩壊後（postCrash=true）に「正しかった」評価が跳ねる＝高い熱狂に逆らっていたほど事後の名声が大きい。
        /// 崩壊前（postCrash=false）はまだ群集が正しいと信じているので利得は0（逆転は崩壊後にのみ生じる）。
        /// </summary>
        public static float VindicationGain(float maniaIntensityAtStance, bool postCrash, ContraryPositionParams p)
        {
            if (!postCrash) return 0f;
            float mania = Mathf.Clamp01(maniaIntensityAtStance);
            return Mathf.Clamp01(mania * p.vindicationScale);
        }

        public static float VindicationGain(float maniaIntensityAtStance, bool postCrash)
            => VindicationGain(maniaIntensityAtStance, postCrash, ContraryPositionParams.Default);

        /// <summary>
        /// 純損益（-1..1）。生き残れば（survived=true）逆転利得から迫害コストを差し引いた逆転の純益、
        /// 潰されれば（survived=false）逆転利得を回収できず迫害損のみ（−persecutionCost）が残る
        /// ＝早すぎる正しさは破滅と紙一重。
        /// </summary>
        public static float NetPayoff(float persecutionCost, float vindicationGain, bool survived)
        {
            float cost = Mathf.Clamp01(persecutionCost);
            float gain = Mathf.Clamp01(vindicationGain);
            if (survived) return Mathf.Clamp(gain - cost, -1f, 1f);
            return -cost;
        }

        /// <summary>
        /// 迫害を生き延びるか（決定論 roll）。粘り強さ resilience(0..1) が高いほど、迫害コスト persecutionCost が
        /// 低いほど生存しやすい。生存確率＝resilience×resilienceWeight ＋（迫害が軽いぶんの余裕）×(1−resilienceWeight)、
        /// から迫害コストぶんを差し引く。roll(0..1) がその確率を下回れば生存。
        /// </summary>
        public static bool SurvivalChance(float resilience, float persecutionCost, float roll, ContraryPositionParams p)
        {
            float res = Mathf.Clamp01(resilience);
            float cost = Mathf.Clamp01(persecutionCost);
            float chance = res * p.resilienceWeight + (1f - cost) * (1f - p.resilienceWeight);
            chance = Mathf.Clamp01(chance - cost * (1f - res));
            return Mathf.Clamp01(roll) < chance;
        }

        public static bool SurvivalChance(float resilience, float persecutionCost, float roll)
            => SurvivalChance(resilience, persecutionCost, roll, ContraryPositionParams.Default);

        /// <summary>
        /// タイミング品質（0..1）。逆張りした時点 stanceTiming(0..1) がピーク peakTiming(0..1) に近いほど高い
        /// ＝ピーク近くで逆らうほど事後の利得は大きいが迫害も大きい（諸刃）。近接度＝1−|stance−peak| を
        /// timingSharpness で非線形に強調（鋭いほどピーク直近だけが高評価）。
        /// </summary>
        public static float TimingQuality(float stanceTiming, float peakTiming, ContraryPositionParams p)
        {
            float stance = Mathf.Clamp01(stanceTiming);
            float peak = Mathf.Clamp01(peakTiming);
            float closeness = 1f - Mathf.Abs(stance - peak);
            return Mathf.Clamp01(Mathf.Pow(Mathf.Clamp01(closeness), p.timingSharpness));
        }

        public static float TimingQuality(float stanceTiming, float peakTiming)
            => TimingQuality(stanceTiming, peakTiming, ContraryPositionParams.Default);

        /// <summary>
        /// 予言者として認知されるか＝名声逆転利得 vindicationGain がしきい値 threshold 以上に達したか。
        /// 崩壊後に大きく報われた者だけが「群集が狂っていたと見抜いた予言者」として語られる。
        /// </summary>
        public static bool IsProphet(float vindicationGain, float threshold)
            => Mathf.Clamp01(vindicationGain) >= Mathf.Clamp01(threshold);

        /// <summary>既定しきい値（<see cref="ContraryPositionParams.prophetThreshold"/>）での予言者判定。</summary>
        public static bool IsProphet(float vindicationGain, ContraryPositionParams p)
            => IsProphet(vindicationGain, p.prophetThreshold);

        public static bool IsProphet(float vindicationGain)
            => IsProphet(vindicationGain, ContraryPositionParams.Default);
    }
}
