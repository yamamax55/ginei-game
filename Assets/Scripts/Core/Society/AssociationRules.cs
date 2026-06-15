using UnityEngine;

namespace Ginei
{
    /// <summary>市民結社の純データ＝中間団体の密度・国家からの自律・市民参加（トクヴィルの自発的結社）。</summary>
    public struct CivicAssociation
    {
        /// <summary>結社の密度（density 0..1）＝教会・組合・クラブ・市民団体など自発的結社が社会にどれだけ分厚く存在するか。</summary>
        public float density;
        /// <summary>国家からの自律（autonomy 0..1）＝中間団体が国家に従属せず独立して活動できる度合い。後見国家はこれを奪う。</summary>
        public float autonomy;
        /// <summary>市民参加（participation 0..1）＝市民が結社の運営・公共の事柄に主体的に関わる度合い。民主主義の学校。</summary>
        public float participation;

        public CivicAssociation(float density, float autonomy, float participation)
        {
            this.density = Mathf.Clamp01(density);
            this.autonomy = Mathf.Clamp01(autonomy);
            this.participation = Mathf.Clamp01(participation);
        }
    }

    /// <summary>市民結社の調整係数。</summary>
    public readonly struct AssociationParams
    {
        /// <summary>結社が専制への防壁になる強さ（中間団体が分厚いほど個人は孤立せず専制に抵抗できる）。</summary>
        public readonly float bufferScale;
        /// <summary>市民参加が自治能力（公共心）を育てる速さ（per dt・民主主義の学校）。</summary>
        public readonly float schoolingRate;
        /// <summary>国家が中間団体の機能を奪うとき結社が痩せる速さ（per dt・後見国家の萎縮）。</summary>
        public readonly float atrophyRate;
        /// <summary>市民社会が活発で専制に強いと判定する密度の閾値（0..1）。</summary>
        public readonly float vitalityThreshold;

        public AssociationParams(float bufferScale, float schoolingRate,
                                 float atrophyRate, float vitalityThreshold)
        {
            this.bufferScale = Mathf.Clamp01(bufferScale);
            this.schoolingRate = Mathf.Max(0f, schoolingRate);
            this.atrophyRate = Mathf.Max(0f, atrophyRate);
            this.vitalityThreshold = Mathf.Clamp01(vitalityThreshold);
        }

        /// <summary>既定＝防壁係数0.9・学校速度0.05・萎縮速度0.06・活力閾値0.5。</summary>
        public static AssociationParams Default => new AssociationParams(0.9f, 0.05f, 0.06f, 0.5f);
    }

    /// <summary>
    /// 中間団体・市民結社の純ロジック（TOCQ-2 #1482・トクヴィル『アメリカのデモクラシー』参考）。
    /// 自発的結社（voluntary associations）＝教会・組合・クラブ・市民団体など、国家と個人の間にある中間団体が、
    /// 民主社会で個人が孤立して専制（穏やかな専制）に屈するのを防ぐ防壁になる＝結社の技術が自由を守る。
    /// 自発的団体の数×市民参加が結社の密度を生み（<see cref="AssociationalDensity"/>＝豊かな市民社会）、
    /// 中間団体が分厚いほど専制への防壁になり（<see cref="BufferAgainstDespotism"/>＝孤立した個人は屈するが結社した市民は抵抗できる）、
    /// 結社が民主社会の孤立（個人主義の病）を防ぎ（<see cref="IsolationProtection"/>）、
    /// 結社の参加が市民の自治能力を育て（<see cref="SchoolOfDemocracy"/>＝民主主義の学校）、
    /// 共通の関心で共に行動する力を生み（<see cref="CollectiveActionCapacity"/>）、
    /// 国家が中間団体の機能を奪うと結社が衰え（<see cref="StateAtrophyTick"/>＝後見国家が市民社会を痩せさせる）、
    /// 中間団体が国家権力と個人の間で多元的均衡を保つ（<see cref="PluralisticBalance"/>）。
    /// 分担：<see cref="PluralityRules"/>（複数性＝視点の多様性・公的領域・共に行動する力＝アーレント）／
    /// <see cref="LobbyRules"/>（圧力団体＝中間団体が政策を歪める負の側面）／
    /// <see cref="SoftDespotismRules"/>（穏やかな専制＝結社が防ぐ専制そのもの・本クラスはその逆＝同 EPIC TOCQ）／
    /// <see cref="ConsentRules"/>（被支配者の合意・非協力）とは別＝**こちらは国家と個人の間の自発的緩衝体としての中間団体**。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。調整値は <see cref="AssociationParams"/>（既定 <see cref="AssociationParams.Default"/>）。
    /// </summary>
    public static class AssociationRules
    {
        /// <summary>
        /// 結社の密度（0..1）＝自発的団体の数 voluntaryGroups(0..1) × 市民参加 civicParticipation(0..1)。
        /// 団体が多くても参加が無ければ空虚な器、参加があっても団体が無ければ受け皿が無い（積＝両方が要る）。
        /// 豊かな市民社会は数と参加の両輪で立ち上がる。
        /// </summary>
        public static float AssociationalDensity(float voluntaryGroups, float civicParticipation)
        {
            return Mathf.Clamp01(voluntaryGroups) * Mathf.Clamp01(civicParticipation);
        }

        /// <summary>専制への防壁（既定 Params）。</summary>
        public static float BufferAgainstDespotism(float associationalDensity)
            => BufferAgainstDespotism(associationalDensity, AssociationParams.Default);

        /// <summary>
        /// 専制への防壁の強さ（0..1）＝結社の密度 × bufferScale。
        /// 中間団体が分厚いほど個人は孤立せず、専制（穏やかな専制）に対する防壁が厚くなる
        /// ＝孤立した個人は権力に屈するが、結社した市民は共に抵抗できる（<see cref="SoftDespotismRules"/> の逆）。
        /// 呼び出し側は穏やかな専制の進行・正統性の侵食からこの分を差し引く想定（基準非破壊）。
        /// </summary>
        public static float BufferAgainstDespotism(float associationalDensity, AssociationParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(associationalDensity) * p.bufferScale);
        }

        /// <summary>
        /// 孤立の防止（0..1）＝結社が民主社会の孤立（個人主義の病）をどれだけ防ぐか
        /// ＝結社の密度 associationalDensity × 個人主義 individualism(0..1)。
        /// 民主社会では人々が私生活に引きこもり孤立する（個人主義の病）が、結社はその引力を打ち消す
        /// ＝個人主義が強い社会ほど結社が孤立を防ぐ働きが大きい（病が重いほど薬が効く）。
        /// </summary>
        public static float IsolationProtection(float associationalDensity, float individualism)
        {
            return Mathf.Clamp01(associationalDensity) * Mathf.Clamp01(individualism);
        }

        /// <summary>民主主義の学校（既定 Params）。</summary>
        public static float SchoolOfDemocracy(float participation, float dt)
            => SchoolOfDemocracy(participation, dt, AssociationParams.Default);

        /// <summary>
        /// 民主主義の学校による自治能力の伸び（0..1・1tick ぶんの増分）＝結社の参加 participation が市民の公共心・
        /// 自治能力を育てる ＝schoolingRate×participation×dt。結社の運営に関わるほど市民は共同の事柄を取り仕切る術を学ぶ
        /// ＝自発的結社は民主主義の学校（参加が公共心を育てる）。呼び出し側が既存の自治能力へ足し込む想定（増分を返す）。
        /// </summary>
        public static float SchoolOfDemocracy(float participation, float dt, AssociationParams p)
        {
            return Mathf.Clamp01(p.schoolingRate * Mathf.Clamp01(participation) * Mathf.Max(0f, dt));
        }

        /// <summary>
        /// 集合行動の力（0..1）＝結社の密度 associationalDensity × 共通の関心 sharedInterest(0..1)。
        /// 結社が分厚く、共通の関心があるとき、人々は共に行動できる（<see cref="PluralityRules.ActionCapacity"/> と整合
        /// ＝結社が共に行動する力の受け皿になる）。密度か関心が欠ければ共同行動は立ち上がらない（積）。
        /// </summary>
        public static float CollectiveActionCapacity(float associationalDensity, float sharedInterest)
        {
            return Mathf.Clamp01(associationalDensity) * Mathf.Clamp01(sharedInterest);
        }

        /// <summary>国家による萎縮（既定 Params）。</summary>
        public static CivicAssociation StateAtrophyTick(CivicAssociation association, float stateEncroachment, float dt)
            => StateAtrophyTick(association, stateEncroachment, dt, AssociationParams.Default);

        /// <summary>
        /// 国家による萎縮の1tick後の結社（密度・自律が低下）。国家の介入 stateEncroachment(0..1) が中間団体の機能を
        /// 肩代わりして奪うと、結社は存在意義を失って痩せる ＝atrophyRate×stateEncroachment×dt ずつ密度と自律が低下。
        /// 後見国家（穏やかな専制の手口）が市民社会を痩せさせ、市民を国家に依存した孤立した個人へ還元する。
        /// 参加は密度の低下に引きずられて減衰（受け皿が痩せれば参加の場も減る）。
        /// </summary>
        public static CivicAssociation StateAtrophyTick(CivicAssociation association, float stateEncroachment, float dt, AssociationParams p)
        {
            float enc = Mathf.Clamp01(stateEncroachment);
            float delta = p.atrophyRate * enc * Mathf.Max(0f, dt);
            float newDensity = Mathf.Clamp01(association.density - delta);
            float newAutonomy = Mathf.Clamp01(association.autonomy - delta);
            // 参加は密度の落ち込みに比例して目減りする（受け皿の縮小ぶんだけ参加の場が失われる）
            float newParticipation = Mathf.Clamp01(Mathf.Min(association.participation, newDensity >= association.density
                ? association.participation
                : association.participation - delta));
            return new CivicAssociation(newDensity, newAutonomy, newParticipation);
        }

        /// <summary>多元的均衡（既定 Params）。</summary>
        public static float PluralisticBalance(float associationalDensity, float statePower)
            => PluralisticBalance(associationalDensity, statePower, AssociationParams.Default);

        /// <summary>
        /// 多元的均衡の度合い（0..1）＝中間団体が国家権力と個人の間でどれだけ均衡を保てているか。
        /// 結社の密度（防壁＝<see cref="BufferAgainstDespotism"/>）が国家権力 statePower(0..1) を相殺する：
        /// 防壁が国家権力に拮抗するほど多元的均衡は高く（1付近）、国家が圧倒して中間団体が薄いと均衡は崩れる（0付近）。
        /// ＝1−|防壁−国家権力| を密度で重み付け（密度が低ければ均衡そのものが成立しない）。
        /// </summary>
        public static float PluralisticBalance(float associationalDensity, float statePower, AssociationParams p)
        {
            float density = Mathf.Clamp01(associationalDensity);
            float buffer = BufferAgainstDespotism(density, p);
            float state = Mathf.Clamp01(statePower);
            float balance = 1f - Mathf.Abs(buffer - state);   // 拮抗（buffer≈state）ほど1に近い
            return Mathf.Clamp01(balance * density);          // 中間団体が薄ければ均衡は成立しない
        }

        /// <summary>
        /// 市民的活力の判定＝市民社会が活発で専制に強いか。結社の密度が vitalityThreshold 以上で true。
        /// 中間団体が一定以上に分厚い社会は、孤立した個人を抱えず、穏やかな専制への抵抗力を持つ。
        /// </summary>
        public static bool IsCivicVitality(float associationalDensity, float threshold)
        {
            return Mathf.Clamp01(associationalDensity) >= Mathf.Clamp01(threshold);
        }
    }
}
