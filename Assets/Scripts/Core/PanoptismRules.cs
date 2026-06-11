using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 監視の純データ（パノプティコン・#1507）。実際の摘発でなく「見られているかもしれない」という意識が自己規律を生む。
    /// 全フィールド 0..1（可変＝時間更新で書き換える）。
    /// </summary>
    [System.Serializable]
    public struct SurveillanceState
    {
        /// <summary>監視インフラ密度 0..1（監視塔・カメラ・記録網の整備度）。</summary>
        public float infrastructureDensity;
        /// <summary>見られている感覚 0..1（可視性の非対称＝実際の監視率でなく可能性で効く）。</summary>
        public float perceivedVisibility;
        /// <summary>内面化された規律 0..1（監視がなくても従う＝規律の主体化＝パノプティコンの完成）。</summary>
        public float internalizedDiscipline;

        public SurveillanceState(float infrastructureDensity, float perceivedVisibility, float internalizedDiscipline)
        {
            this.infrastructureDensity = Mathf.Clamp01(infrastructureDensity);
            this.perceivedVisibility = Mathf.Clamp01(perceivedVisibility);
            this.internalizedDiscipline = Mathf.Clamp01(internalizedDiscipline);
        }
    }

    /// <summary>パノプティコン係数（#1507）の調整係数。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct PanoptismParams
    {
        /// <summary>不確実性（いつ見られるか分からない度合い）が「見られている感覚」に効く重み。</summary>
        public readonly float uncertaintyWeight;
        /// <summary>見られている感覚1.0あたりの事前抑止効果の最大幅。</summary>
        public readonly float deterrenceScale;
        /// <summary>見られている感覚1.0・継続1.0あたりの規律内面化の上昇速度（per 時間単位）。</summary>
        public readonly float internalizeRate;
        /// <summary>監視が途切れた時の規律の自然減衰速度（内面化は緩やかに薄れる）。</summary>
        public readonly float disciplineDecay;
        /// <summary>監視インフラ密度1.0あたりの維持コストの最大幅（全員を見るのは高い）。</summary>
        public readonly float costScale;
        /// <summary>密度ゼロでも「見られているかもしれない」演出を維持する固定コスト（可能性の演出は安い）。</summary>
        public readonly float costFloor;
        /// <summary>過度な監視への反発が始まる「見られている感覚」の閾値（これ以下なら気にしない）。</summary>
        public readonly float resistanceThreshold;
        /// <summary>パノプティコン的支配と判定する内面化規律の既定閾値。</summary>
        public readonly float panopticThreshold;

        public PanoptismParams(float uncertaintyWeight, float deterrenceScale, float internalizeRate,
                               float disciplineDecay, float costScale, float costFloor,
                               float resistanceThreshold, float panopticThreshold)
        {
            this.uncertaintyWeight = Mathf.Clamp01(uncertaintyWeight);
            this.deterrenceScale = Mathf.Clamp01(deterrenceScale);
            this.internalizeRate = Mathf.Max(0f, internalizeRate);
            this.disciplineDecay = Mathf.Max(0f, disciplineDecay);
            this.costScale = Mathf.Max(0f, costScale);
            this.costFloor = Mathf.Clamp01(costFloor);
            // 1.0 ちょうどだと反発が定義できない（超過幅0除算）ため 0.99 を上限にクランプ
            this.resistanceThreshold = Mathf.Clamp(resistanceThreshold, 0f, 0.99f);
            this.panopticThreshold = Mathf.Clamp01(panopticThreshold);
        }

        /// <summary>
        /// 既定＝不確実性重み0.5/事前抑止上限0.9/内面化速度0.2/規律減衰0.05/維持コスト上限0.8/演出最低コスト0.1/反発閾値0.6/支配閾値0.7。
        /// </summary>
        public static PanoptismParams Default =>
            new PanoptismParams(0.5f, 0.9f, 0.2f, 0.05f, 0.8f, 0.1f, 0.6f, 0.7f);
    }

    /// <summary>
    /// パノプティコン係数の純ロジック（#1507・ミシェル・フーコー『監獄の誕生』参考＝規律権力 disciplinary power の唯一の窓口）。
    /// 一望監視施設では囚人は「見られているかもしれない」と意識するだけで自ら規律に従う＝可視性の非対称が自己規律を生む。
    /// よって監視インフラの密度は<b>実際の摘発でなく事前抑止（自己規律）</b>を生み、続くと規律が内面化されて<b>監視者が不要になる</b>。
    /// <see cref="SecurityRules"/>（秘密警察が反体制を<b>実際に抑圧・摘発</b>する）とは別＝こちらは見られている意識による事前抑止。
    /// <see cref="CensusRules"/>（国勢調査の可視性＝国家が国民を<b>見る</b>側の精度）とも別＝こちらは国民が<b>見られていると意識する</b>側。
    /// <see cref="NormalizationRules"/>（同 EPIC PANO の規律訓練＝行動の標準化）と分担し、
    /// <see cref="PreferenceFalsificationRules"/>（萎縮の<b>表明</b>＝本音と建前の乖離）へ <see cref="ChillingEffect"/> が波及する想定。
    /// 値は徹底して 0..1 に clamp・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PanoptismRules
    {
        /// <summary>
        /// 見られている感覚 0..1：監視インフラの密度×（いつ見られるか分からない不確実性）。
        /// 実際の監視率でなく「可能性」が効く＝密度が高くても確実に見られると分かる（不確実性0）なら隙を突かれ、
        /// 不確実性が高いほど常時意識せざるを得ない（パノプティコンの本質＝見られているかもしれない）。
        /// </summary>
        public static float PerceivedVisibility(float infrastructureDensity, float uncertainty, PanoptismParams p)
        {
            float density = Mathf.Clamp01(infrastructureDensity);
            float u = Mathf.Clamp01(uncertainty);
            // 密度を土台に、不確実性で効きを底上げ（uncertaintyWeight ぶんを不確実性で稼ぐ）。
            // u=1 なら density 満額、u=0 なら density×(1-uncertaintyWeight)＝確実に見られると分かれば隙を突かれ感覚は薄れる。
            float factor = (1f - p.uncertaintyWeight) + p.uncertaintyWeight * u;
            return Mathf.Clamp01(density * factor);
        }

        public static float PerceivedVisibility(float infrastructureDensity, float uncertainty)
            => PerceivedVisibility(infrastructureDensity, uncertainty, PanoptismParams.Default);

        /// <summary>
        /// 事前抑止効果 0..1：見られている感覚が違反を摘発より<b>前に</b>自制させる（自己規律）。
        /// 感覚が強いほど deterrenceScale を上限に線形で抑止が効く。
        /// </summary>
        public static float DeterrenceEffect(float perceivedVisibility, PanoptismParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(perceivedVisibility) * p.deterrenceScale);
        }

        public static float DeterrenceEffect(float perceivedVisibility)
            => DeterrenceEffect(perceivedVisibility, PanoptismParams.Default);

        /// <summary>
        /// 規律の内面化（時間更新）0..1：監視が続くと規律が内面化され、監視がなくても従うようになる（規律の主体化）。
        /// 見られている感覚 perceivedVisibility が高く・継続 duration が長いほど内面化が進み、感覚が薄れると緩やかに減衰する。
        /// dt は負を許さない。結果は 0..1 にクランプ。
        /// </summary>
        public static float DisciplineInternalization(float internalizedDiscipline, float perceivedVisibility, float duration, float dt, PanoptismParams p)
        {
            float d = Mathf.Clamp01(internalizedDiscipline);
            float v = Mathf.Clamp01(perceivedVisibility);
            float dur = Mathf.Clamp01(duration);
            float t = Mathf.Max(0f, dt);
            float gain = p.internalizeRate * v * dur;          // 監視の継続が規律を主体に焼き付ける
            float loss = p.disciplineDecay * (1f - v);         // 監視意識が薄れると緩やかに薄れる
            return Mathf.Clamp01(d + (gain - loss) * t);
        }

        public static float DisciplineInternalization(float internalizedDiscipline, float perceivedVisibility, float duration, float dt)
            => DisciplineInternalization(internalizedDiscipline, perceivedVisibility, duration, dt, PanoptismParams.Default);

        /// <summary>
        /// 自己規制 0..1：内面化された規律による自己規制（パノプティコンの完成＝監視者不要）。
        /// internalizedDiscipline がそのまま「監視がなくても自律的に従う度合い」になる。
        /// </summary>
        public static float SelfRegulation(float internalizedDiscipline)
        {
            return Mathf.Clamp01(internalizedDiscipline);
        }

        /// <summary>
        /// 監視インフラの維持コスト 0..costFloor+costScale：実際に全員を見るのは高い（密度比例）が、
        /// 「見られているかもしれない」という可能性の演出は安い（costFloor の固定コストのみ）。
        /// </summary>
        public static float SurveillanceCost(float infrastructureDensity, PanoptismParams p)
        {
            return Mathf.Clamp01(p.costFloor + p.costScale * Mathf.Clamp01(infrastructureDensity));
        }

        public static float SurveillanceCost(float infrastructureDensity)
            => SurveillanceCost(infrastructureDensity, PanoptismParams.Default);

        /// <summary>
        /// 過度な監視への反発 0..1：見られている感覚が resistanceThreshold を超えた割合に、プライバシーを重んじる
        /// 者ほど強く反発する privacyValue(0..1) を掛ける。閾値以下なら 0＝ほどほどの監視は気にされない。
        /// </summary>
        public static float ResistanceAwareness(float perceivedVisibility, float privacyValue, PanoptismParams p)
        {
            float v = Mathf.Clamp01(perceivedVisibility);
            if (v <= p.resistanceThreshold) return 0f;
            float excess = (v - p.resistanceThreshold) / (1f - p.resistanceThreshold);
            return Mathf.Clamp01(excess * Mathf.Clamp01(privacyValue));
        }

        public static float ResistanceAwareness(float perceivedVisibility, float privacyValue)
            => ResistanceAwareness(perceivedVisibility, privacyValue, PanoptismParams.Default);

        /// <summary>
        /// 萎縮効果 0..1：監視が異論・自由な発想を萎縮させる（chilling effect）。見られている感覚が強いほど、
        /// 元々の異論 dissent を抑え込む（口をつぐむ＝<see cref="PreferenceFalsificationRules"/> の表明乖離へ波及）。
        /// </summary>
        public static float ChillingEffect(float perceivedVisibility, float dissent)
        {
            return Mathf.Clamp01(Mathf.Clamp01(perceivedVisibility) * Mathf.Clamp01(dissent));
        }

        /// <summary>
        /// パノプティコン的支配の判定：規律が内面化され自律的に従う（監視者不要）状態か。
        /// internalizedDiscipline が threshold 以上で true。
        /// </summary>
        public static bool IsPanopticControl(float internalizedDiscipline, float threshold)
        {
            return Mathf.Clamp01(internalizedDiscipline) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="PanoptismParams.panopticThreshold"/>）でのパノプティコン的支配判定。</summary>
        public static bool IsPanopticControl(float internalizedDiscipline)
            => IsPanopticControl(internalizedDiscipline, PanoptismParams.Default.panopticThreshold);
    }
}
