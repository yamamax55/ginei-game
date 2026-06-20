using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 世論場の純データ（ミル『自由論』の世論ダイナミクス）。diversity は意見多様度(0..1)、
    /// dominantShare は支配的意見のシェア(0..1)、conformityPressure は同調圧力(0..1)。
    /// 解決は <see cref="PublicOpinionRules"/> が窓口。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public struct OpinionField
    {
        public float diversity;            // 意見多様度 0..1（高いほど多様な意見が並立）
        public float dominantShare;        // 支配的意見のシェア 0..1（高いほど一意見に偏る）
        public float conformityPressure;   // 同調圧力 0..1（空気が個人を縛る強さ）

        public OpinionField(float diversity, float dominantShare, float conformityPressure)
        {
            this.diversity = Mathf.Clamp01(diversity);
            this.dominantShare = Mathf.Clamp01(dominantShare);
            this.conformityPressure = Mathf.Clamp01(conformityPressure);
        }
    }

    /// <summary>世論ダイナミクスの調整係数（ミル）。</summary>
    public readonly struct PublicOpinionParams
    {
        /// <summary>熟議が情報品質へ寄与する重み（多様度との配合・残りは多様度の比重）。</summary>
        public readonly float deliberationWeight;
        /// <summary>多数派圧力が少数意見を沈黙させる強さ。</summary>
        public readonly float silencingGain;
        /// <summary>集団思考の非線形度（低多様×高同調が判断を誤らせる勾配）。</summary>
        public readonly float groupthinkGain;
        /// <summary>多様性の配当（多様度が社会の判断・適応力をどれだけ底上げするか）。</summary>
        public readonly float dividendGain;
        /// <summary>同調圧力が多様度を画一化させる速さ（per dt）。</summary>
        public readonly float convergenceRate;

        public PublicOpinionParams(float deliberationWeight, float silencingGain, float groupthinkGain, float dividendGain, float convergenceRate)
        {
            this.deliberationWeight = Mathf.Clamp01(deliberationWeight);
            this.silencingGain = Mathf.Clamp01(silencingGain);
            this.groupthinkGain = Mathf.Clamp01(groupthinkGain);
            this.dividendGain = Mathf.Clamp01(dividendGain);
            this.convergenceRate = Mathf.Max(0f, convergenceRate);
        }

        /// <summary>既定＝熟議重み0.4・沈黙化0.6・集団思考0.7・多様性配当0.3・画一化率0.2。</summary>
        public static PublicOpinionParams Default => new PublicOpinionParams(0.4f, 0.6f, 0.7f, 0.3f, 0.2f);
    }

    /// <summary>
    /// 世論ダイナミクスと多数派専制の純ロジック（MILL-2 #1477・ミル『自由論』の「世論の専制」）。
    /// 多数派の意見は法律でなく<b>社会的圧力（空気）</b>として個人を縛り、意見の多様性が保たれるほど
    /// 多様な意見の衝突が情報の品質を高め真理に近づけるが、同調圧力による画一化が進むと反対意見が消え
    /// <b>集団思考</b>に陥って判断を誤る＝意見多様度が情報品質係数を決める。乱数なし・決定論。
    /// <see cref="PropagandaRules"/>（世論操作の発信＝到達×信用×主張）・
    /// <see cref="DemagogueRules"/>（扇動家の個人訴求）とは別＝こちらは世論<b>場</b>の多数派専制と情報品質。
    /// 多数者の専制の政治制度面（別EPIC TOCQ）は <see cref="MajorityTyrannyRules"/>、
    /// 表明と本音の乖離（沈黙の蓄積）は <see cref="PreferenceFalsificationRules"/> が扱う＝分担。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PublicOpinionRules
    {
        /// <summary>
        /// 情報の品質（0..1）＝意見多様度 diversity(0..1) と熟議 deliberation(0..1) の配合。
        /// 多様な意見が衝突し熟議で吟味されるほど真理に近づく＝diversity×(1−w)＋(diversity×deliberation)×w。
        /// 多様度がゼロなら熟議があっても品質は出ない（吟味する材料が無い）。
        /// </summary>
        public static float InformationQuality(float diversity, float deliberation, PublicOpinionParams p)
        {
            float div = Mathf.Clamp01(diversity);
            float del = Mathf.Clamp01(deliberation);
            float w = p.deliberationWeight;
            float q = div * (1f - w) + (div * del) * w;
            return Mathf.Clamp01(q);
        }

        public static float InformationQuality(float diversity, float deliberation)
            => InformationQuality(diversity, deliberation, PublicOpinionParams.Default);

        /// <summary>
        /// 多数派の社会的専制（0..1）＝支配的意見のシェア dominantShare(0..1) × 同調圧力 conformityPressure(0..1)。
        /// 法でなく空気が縛る＝多数派が大きく、かつ空気が強いほど個人への圧力が高い。どちらかが弱ければ圧力は薄い。
        /// </summary>
        public static float MajorityPressure(float dominantShare, float conformityPressure)
        {
            float share = Mathf.Clamp01(dominantShare);
            float conf = Mathf.Clamp01(conformityPressure);
            return Mathf.Clamp01(share * conf);
        }

        /// <summary>
        /// 少数意見の沈黙度（0..1）＝多数派圧力 majorityPressure(0..1) が少数派を黙らせる。
        /// 異論を述べる勇気 dissenterCourage(0..1) が圧力に抗う＝圧力×(1−勇気)×silencingGain。
        /// 沈黙した不満は <see cref="PreferenceFalsificationRules"/>（表明と本音の乖離）へ流れる。
        /// </summary>
        public static float MinoritySilencing(float majorityPressure, float dissenterCourage, PublicOpinionParams p)
        {
            float mp = Mathf.Clamp01(majorityPressure);
            float courage = Mathf.Clamp01(dissenterCourage);
            return Mathf.Clamp01(mp * (1f - courage) * p.silencingGain);
        }

        public static float MinoritySilencing(float majorityPressure, float dissenterCourage)
            => MinoritySilencing(majorityPressure, dissenterCourage, PublicOpinionParams.Default);

        /// <summary>
        /// 集団思考度（0..1）＝多様度が低く（1−diversity）同調圧力 conformityPressure が高いほど陥る。
        /// 反対意見が消えて判断を誤る＝両者の積に非線形度 groupthinkGain を掛けて勾配を効かせる。
        /// 多様度が高ければ同調圧力が強くても集団思考にはなりにくい（異論が残る）。
        /// </summary>
        public static float Groupthink(float diversity, float conformityPressure, PublicOpinionParams p)
        {
            float monotony = 1f - Mathf.Clamp01(diversity);
            float conf = Mathf.Clamp01(conformityPressure);
            float raw = monotony * conf;
            return Mathf.Clamp01(raw * (1f + p.groupthinkGain) - p.groupthinkGain * raw * raw);
        }

        public static float Groupthink(float diversity, float conformityPressure)
            => Groupthink(diversity, conformityPressure, PublicOpinionParams.Default);

        /// <summary>
        /// 意見の収束（1tick後の多様度・0..1）。社会的圧力 socialPressure(0..1) が意見を画一化させ多様度を下げる
        /// ＝diversity を convergenceRate×socialPressure×dt の分だけ減らす（同調が多様性を食い潰す）。
        /// 圧力ゼロなら多様度は不変。
        /// </summary>
        public static float OpinionConvergenceTick(float diversity, float socialPressure, float dt, PublicOpinionParams p)
        {
            float div = Mathf.Clamp01(diversity);
            float pressure = Mathf.Clamp01(socialPressure);
            float d = Mathf.Max(0f, dt);
            float decay = p.convergenceRate * pressure * d;
            return Mathf.Clamp01(div - decay);
        }

        public static float OpinionConvergenceTick(float diversity, float socialPressure, float dt)
            => OpinionConvergenceTick(diversity, socialPressure, dt, PublicOpinionParams.Default);

        /// <summary>
        /// 多様性の配当（倍率・1.0基準）＝多様な意見が保たれるほど社会の判断の質・適応力が上がる。
        /// diversity(0..1) に応じて 1.0〜(1.0+dividendGain) を返す＝画一化は配当を失う。
        /// <see cref="LibertyCultureRules"/>（自由の文化）と整合。
        /// </summary>
        public static float DiversityDividend(float diversity, PublicOpinionParams p)
        {
            float div = Mathf.Clamp01(diversity);
            return 1f + div * p.dividendGain;
        }

        public static float DiversityDividend(float diversity)
            => DiversityDividend(diversity, PublicOpinionParams.Default);

        /// <summary>
        /// 沈黙の螺旋（0..1・ノエル＝ノイマン）＝自分が少数派だという認識 minorityPerception(0..1) と
        /// 孤立への恐れ fearOfIsolation(0..1) が高いほど、少数派が沈黙して多数派がさらに大きく見える。
        /// 沈黙→多数派が肥大→さらに沈黙の自己強化を、両者の積で出す（どちらか欠ければ螺旋は回らない）。
        /// </summary>
        public static float SpiralOfSilence(float minorityPerception, float fearOfIsolation)
        {
            float perc = Mathf.Clamp01(minorityPerception);
            float fear = Mathf.Clamp01(fearOfIsolation);
            return Mathf.Clamp01(perc * fear);
        }

        /// <summary>
        /// 画一化したモノカルチャー（多数派専制に陥った状態）か＝意見多様度 diversity が閾値 threshold(0..1) を
        /// 下回ったか。意見が画一化し異論が消えた＝多数派の社会的専制の成立判定。
        /// </summary>
        public static bool IsConformistMonoculture(float diversity, float threshold)
            => Mathf.Clamp01(diversity) < Mathf.Clamp01(threshold);
    }
}
