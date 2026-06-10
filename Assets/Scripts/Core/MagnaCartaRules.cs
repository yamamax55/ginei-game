using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// マグナカルタの数値解決（#624 王権制約・純ロジック test-first）。Charter を扱う唯一の窓口。
    /// 「権力は契約で制約される」をモデル化する：実効王権は条項×慣習法化度で削られ、
    /// 課税は同意を要し、王が契約を破れば抵抗権が発動しうる。慣習法化度は再確認/破棄で揺れる。
    /// 乱数は持たない（決定論）。調整値は <see cref="MagnaCartaParams"/> に集約（基準非破壊・実効値パターン）。
    /// </summary>
    public static class MagnaCartaRules
    {
        /// <summary>
        /// 実効王権＝基準王権から契約による制約分を引いた値（0..baseAuthority）。
        /// 各条項が立つほど王権は削られ、削りの強さは慣習法化度 <see cref="Charter.strength"/> に比例する
        /// （紙の上だけ＝strength=0 ならほぼ削れない、定着＝1 で最大に削れる）。
        /// </summary>
        public static float EffectiveRoyalAuthority(float baseAuthority, Charter charter, MagnaCartaParams p)
        {
            if (charter == null) return Mathf.Max(0f, baseAuthority);

            float s = Mathf.Clamp01(charter.strength);
            float constraint = 0f;
            if (charter.taxConsent) constraint += p.TaxConsentConstraint;
            if (charter.dueProcess) constraint += p.DueProcessConstraint;
            if (charter.resistanceRight) constraint += p.ResistanceConstraint;

            // 慣習法化度で実効化＝定着していない条項は王権を縛れない
            float reduction = baseAuthority * Mathf.Clamp01(constraint) * s;
            return Mathf.Clamp(baseAuthority - reduction, 0f, Mathf.Max(0f, baseAuthority));
        }

        /// <summary>
        /// 課税に同意が要るか＝課税同意条項が立ち、かつ慣習法化が機能閾値以上で定着しているとき true。
        /// 条項があっても紙の上だけ（strength が低い）なら王は同意なしに課税を強行できる。
        /// </summary>
        public static bool TaxRequiresConsent(Charter charter, MagnaCartaParams p)
        {
            if (charter == null) return false;
            return charter.taxConsent && Mathf.Clamp01(charter.strength) >= p.EffectiveThreshold;
        }

        /// <summary>同意要否（既定パラメータ）。</summary>
        public static bool TaxRequiresConsent(Charter charter) => TaxRequiresConsent(charter, MagnaCartaParams.Default);

        /// <summary>
        /// 抵抗権が発動しうるか＝抵抗権条項が立ち、慣習法化が機能閾値以上で、王が契約を侵害したとき true。
        /// 条項が無い／定着していない／侵害が無い、のいずれかなら発動しない。
        /// </summary>
        public static bool ResistanceTriggered(Charter charter, bool kingViolation, MagnaCartaParams p)
        {
            if (charter == null || !kingViolation) return false;
            return charter.resistanceRight && Mathf.Clamp01(charter.strength) >= p.EffectiveThreshold;
        }

        /// <summary>抵抗権発動可否（既定パラメータ）。</summary>
        public static bool ResistanceTriggered(Charter charter, bool kingViolation)
            => ResistanceTriggered(charter, kingViolation, MagnaCartaParams.Default);

        /// <summary>
        /// 慣習法化度のドリフト（0..1 へクランプ）。再確認(upheld=true)で定着が進み、
        /// 破棄/侵害(upheld=false)で定着が剥がれる＝契約は守られ続けてこそ常識になる。dt 比例（時間追従）。
        /// 基準値（呼び出し側の Charter.strength）は変えず、新しい strength を返す（実効値パターン）。
        /// </summary>
        public static float HabituationDrift(float strength, bool upheld, float deltaTime, MagnaCartaParams p)
        {
            float s = Mathf.Clamp01(strength);
            if (deltaTime <= 0f) return s;
            float rate = upheld ? p.HabituationGain : -p.HabituationDecay;
            return Mathf.Clamp01(s + rate * deltaTime);
        }

        /// <summary>慣習法化ドリフト（既定パラメータ）。</summary>
        public static float HabituationDrift(float strength, bool upheld, float deltaTime)
            => HabituationDrift(strength, upheld, deltaTime, MagnaCartaParams.Default);
    }

    /// <summary>
    /// MagnaCartaRules の調整値（マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// 制約値の合計が 1 を超えると王権がゼロまで削れうる（条項が全部立ち定着するほど立憲化が進む）。
    /// </summary>
    public readonly struct MagnaCartaParams
    {
        /// <summary>課税同意条項が王権を削る割合（0..1・慣習法化度で乗じられる）。</summary>
        public readonly float TaxConsentConstraint;
        /// <summary>適正手続き条項が王権を削る割合（0..1）。</summary>
        public readonly float DueProcessConstraint;
        /// <summary>抵抗権条項が王権を削る割合（0..1）。</summary>
        public readonly float ResistanceConstraint;
        /// <summary>条項が実効を持つ慣習法化度の閾値（これ未満は紙の上だけ＝機能しない）。</summary>
        public readonly float EffectiveThreshold;
        /// <summary>再確認時の慣習法化の伸び（/時間）。</summary>
        public readonly float HabituationGain;
        /// <summary>破棄/侵害時の慣習法化の剥がれ（/時間）。</summary>
        public readonly float HabituationDecay;

        public MagnaCartaParams(
            float taxConsentConstraint, float dueProcessConstraint, float resistanceConstraint,
            float effectiveThreshold, float habituationGain, float habituationDecay)
        {
            TaxConsentConstraint = taxConsentConstraint;
            DueProcessConstraint = dueProcessConstraint;
            ResistanceConstraint = resistanceConstraint;
            EffectiveThreshold = effectiveThreshold;
            HabituationGain = habituationGain;
            HabituationDecay = habituationDecay;
        }

        /// <summary>
        /// 既定（課税同意0.3／適正手続き0.2／抵抗権0.25・機能閾値0.5・伸び0.05/剥がれ0.08）。
        /// 破棄の方が定着より速い＝契約は崩れやすく育ちにくい。
        /// </summary>
        public static MagnaCartaParams Default => new MagnaCartaParams(
            taxConsentConstraint: 0.3f, dueProcessConstraint: 0.2f, resistanceConstraint: 0.25f,
            effectiveThreshold: 0.5f, habituationGain: 0.05f, habituationDecay: 0.08f);
    }
}
