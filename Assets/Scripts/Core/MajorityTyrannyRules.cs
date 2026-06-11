using UnityEngine;

namespace Ginei
{
    /// <summary>少数意見の純データ＝多数者の専制が圧迫する対象（トクヴィル）。</summary>
    public struct MinorityOpinion
    {
        /// <summary>少数派のシェア（share 0..1）＝この意見を抱く者の人口比。小さいほど多数派の同調圧力に晒される。</summary>
        public float share;
        /// <summary>表明の自由（expressionFreedom 0..1）＝この少数意見を公然と表明できる度合い。圧力が下げる。</summary>
        public float expressionFreedom;
        /// <summary>受ける社会的圧力（socialPressure 0..1）＝多数派の同質化圧力がこの意見にかける精神的圧迫。</summary>
        public float socialPressure;

        public MinorityOpinion(float share, float expressionFreedom, float socialPressure)
        {
            this.share = Mathf.Clamp01(share);
            this.expressionFreedom = Mathf.Clamp01(expressionFreedom);
            this.socialPressure = Mathf.Clamp01(socialPressure);
        }
    }

    /// <summary>多数者の専制の調整係数。</summary>
    public readonly struct MajorityTyrannyParams
    {
        /// <summary>魂の幽閉が1tickで進む速さ（per dt・社会的圧力が少数派を精神的に萎縮させる速度）。</summary>
        public readonly float imprisonmentRate;
        /// <summary>多数派が道徳的全能（異論を不道徳視）になる強さ（多数派の権力→道徳独占の利得）。</summary>
        public readonly float moralEmpireGain;
        /// <summary>制度的保護に効く司法独立の重み（権利章典と司法独立をどう合成するか）。</summary>
        public readonly float judicialWeight;

        public MajorityTyrannyParams(float imprisonmentRate, float moralEmpireGain, float judicialWeight)
        {
            this.imprisonmentRate = Mathf.Max(0f, imprisonmentRate);
            this.moralEmpireGain = Mathf.Clamp01(moralEmpireGain);
            this.judicialWeight = Mathf.Clamp01(judicialWeight);
        }

        /// <summary>既定＝幽閉速度0.1・道徳的全能係数0.8・司法独立重み0.5。</summary>
        public static MajorityTyrannyParams Default => new MajorityTyrannyParams(0.1f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 多数者の専制の純ロジック（TOCQ-1 #1478・トクヴィル『アメリカのデモクラシー』参考。ミルが継承）。
    /// 多数者の専制（tyranny of the majority）＝民主社会では多数派が法的にも道徳的にも全能になり、制度的歯止めが
    /// 弱いと社会的同質化圧力が少数意見を封殺する＝<b>物理的暴力でなく世論による精神的圧迫が少数派の魂を閉じ込める</b>。
    /// 多数派のシェア×（1−制度的歯止め）が多数者の権力を生み、社会の同質性がそれを同調圧力に変え、同調圧力が
    /// 少数意見を社会的に封殺し、多数派は道徳的にも全能になって異論を不道徳とみなす。権利章典・司法の独立が
    /// この専制から少数派を守る制度的保護をなす。
    /// <see cref="PluralityRules"/>（複数性＝視点の多様性・公的領域）・<see cref="PreferenceFalsificationRules"/>
    /// （選好偽装＝表明と本音の乖離）とは別＝こちらは「多数派の社会的同調圧力（多数者の専制＝魂への精神的圧迫）」を
    /// 扱う（MinorityOpinion が中核データ）。沈黙の螺旋＝<c>PublicOpinionRules</c>（別 EPIC MILL・生成見込み）と
    /// <see cref="DissentChilling"/> で整合し、派閥で専制を防ぐ <see cref="FactionMultiplicityRules"/>（生成済み）とも
    /// 分担。乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MajorityTyrannyRules
    {
        /// <summary>
        /// 多数者の権力（0..1）＝多数派のシェア majorityShare(0..1) ×（1−制度的歯止め institutionalCheck(0..1)）。
        /// 歯止め（権力分立・少数派保護）なき多数派は法的に全能になる＝歯止めが効くほど権力は抑えられる。
        /// </summary>
        public static float MajorityPower(float majorityShare, float institutionalCheck)
        {
            float share = Mathf.Clamp01(majorityShare);
            float check = Mathf.Clamp01(institutionalCheck);
            return Mathf.Clamp01(share * (1f - check));
        }

        /// <summary>
        /// 社会的同調圧力（0..1）＝多数者の権力 majorityPower(0..1) × 社会の同質性 homogeneity(0..1)。
        /// 多数派が全能でも社会が多様なら圧力は弱い。均質な社会ほど「空気」が少数派を縛る（積＝両方が要る）。
        /// </summary>
        public static float SocialConformityPressure(float majorityPower, float homogeneity)
        {
            return Mathf.Clamp01(majorityPower) * Mathf.Clamp01(homogeneity);
        }

        /// <summary>
        /// 少数意見の封殺（0..1）＝同調圧力 socialPressure(0..1) が少数意見を社会的に封殺する度合い。
        /// 暴力でなく精神的圧迫＝シェア minorityShare が小さいほど（孤立して）封殺されやすい
        /// ＝socialPressure×(1−minorityShare)。少数派ほど多数派の空気に呑まれる。
        /// </summary>
        public static float MinoritySuppression(float socialPressure, float minorityShare)
        {
            float pressure = Mathf.Clamp01(socialPressure);
            float share = Mathf.Clamp01(minorityShare);
            return Mathf.Clamp01(pressure * (1f - share));
        }

        /// <summary>
        /// 道徳的全能（0..1）＝多数者の権力 majorityPower(0..1) が大きいほど多数派は道徳的にも全能になり、
        /// 異論を「不道徳」とみなして正義を独占する＝majorityPower×moralEmpireGain。
        /// 多数派が真理と道徳を僭称するとき、少数派は黙るしかなくなる。
        /// </summary>
        public static float MoralEmpire(float majorityPower, MajorityTyrannyParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(majorityPower) * p.moralEmpireGain);
        }

        public static float MoralEmpire(float majorityPower)
            => MoralEmpire(majorityPower, MajorityTyrannyParams.Default);

        /// <summary>
        /// 魂の幽閉の1tick後の値（0..1）。社会的圧力 socialPressure(0..1) が物理的でなく精神的に少数派を
        /// 萎縮させる＝imprisonmentRate×socialPressure×dt ずつ上昇。トクヴィルの「魂を閉じ込める」表現＝
        /// 身体は自由でも精神が縛られ、ついには異論を抱くことすらやめる。
        /// </summary>
        public static float SoulImprisonment(float soulConstraint, float socialPressure, float dt, MajorityTyrannyParams p)
        {
            float delta = p.imprisonmentRate * Mathf.Clamp01(socialPressure) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(soulConstraint) + delta);
        }

        public static float SoulImprisonment(float soulConstraint, float socialPressure, float dt)
            => SoulImprisonment(soulConstraint, socialPressure, dt, MajorityTyrannyParams.Default);

        /// <summary>
        /// 制度的保護（0..1）＝少数派を多数者の専制から守る制度の強さ＝権利章典 constitutionalRights(0..1) と
        /// 司法の独立 judicialIndependence(0..1) の合成（judicialWeight で重み付け）。
        /// 権利が文面にあっても司法が独立していなければ守れない＝両者が要る。
        /// </summary>
        public static float InstitutionalProtection(float constitutionalRights, float judicialIndependence, MajorityTyrannyParams p)
        {
            float rights = Mathf.Clamp01(constitutionalRights);
            float judicial = Mathf.Clamp01(judicialIndependence);
            return Mathf.Clamp01(rights * (1f - p.judicialWeight) + rights * judicial * p.judicialWeight);
        }

        public static float InstitutionalProtection(float constitutionalRights, float judicialIndependence)
            => InstitutionalProtection(constitutionalRights, judicialIndependence, MajorityTyrannyParams.Default);

        /// <summary>
        /// 異論の萎縮（0..1）＝社会的圧力 socialPressure(0..1) と排斥の恐怖 fearOfOstracism(0..1) が異論を萎縮させる
        /// ＝socialPressure×fearOfOstracism。村八分を恐れて口をつぐむ（沈黙の螺旋＝PublicOpinionRules と整合）。
        /// 圧力があっても排斥を恐れなければ萎縮しない（積）。
        /// </summary>
        public static float DissentChilling(float socialPressure, float fearOfOstracism)
        {
            return Mathf.Clamp01(socialPressure) * Mathf.Clamp01(fearOfOstracism);
        }

        /// <summary>
        /// 多数者の専制の判定。少数派への社会的圧力 socialPressure が threshold(0..1) 以上に達し、かつ
        /// 制度的保護 institutionalProtection が 1−threshold 未満（守りが弱い）とき true。
        /// 多数派の同調圧力が少数派を封殺し、制度がそれを止められない状態＝多数者の専制。
        /// </summary>
        public static bool IsMajorityTyranny(float socialPressure, float institutionalProtection, float threshold)
        {
            float t = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(socialPressure) >= t
                   && Mathf.Clamp01(institutionalProtection) < (1f - t);
        }
    }
}
