using UnityEngine;

namespace Ginei
{
    /// <summary>聖職キャリアの役職ラダー（#1096・序列）。修道士→助祭→司祭→修道院長→司教→大司教。</summary>
    public enum ClericalRank { 修道士, 助祭, 司祭, 修道院長, 司教, 大司教 }

    /// <summary>聖職キャリアの数値解決の調整値（純構造体・既定 .Default）。マジックナンバーを1か所へ集約する。</summary>
    public readonly struct ClericalCareerParams
    {
        /// <summary>昇進力で敬虔さに掛ける重み（理想駆動の登り）。</summary>
        public readonly float pietyWeight;
        /// <summary>昇進力で野心に掛ける重み（出世主義の登り）。</summary>
        public readonly float ambitionWeight;
        /// <summary>昇進力で後ろ盾に掛ける重み（教会政治のコネ）。</summary>
        public readonly float patronageWeight;
        /// <summary>世俗権力の高位寄り係数（rankが上がるほど領地・政治力を持つ）。</summary>
        public readonly float temporalRankWeight;
        /// <summary>世俗権力で野心に掛ける重み（野心家ほど世俗権力を握る）。</summary>
        public readonly float temporalAmbitionWeight;
        /// <summary>理想vs野心ドリフトの基準振れ幅（出世×野心の侵食の最大）。</summary>
        public readonly float driftAmplitude;
        /// <summary>腐敗リスクの最大（世俗権力1×敬虔0でこの値）。</summary>
        public readonly float corruptionMax;

        public ClericalCareerParams(float pietyWeight, float ambitionWeight, float patronageWeight,
            float temporalRankWeight, float temporalAmbitionWeight, float driftAmplitude, float corruptionMax)
        {
            this.pietyWeight = pietyWeight;
            this.ambitionWeight = ambitionWeight;
            this.patronageWeight = patronageWeight;
            this.temporalRankWeight = temporalRankWeight;
            this.temporalAmbitionWeight = temporalAmbitionWeight;
            this.driftAmplitude = driftAmplitude;
            this.corruptionMax = corruptionMax;
        }

        /// <summary>既定＝敬虔0.4/野心0.35/後ろ盾0.25（昇進力の三要素は和1.0）・世俗rank0.6×野心0.4・ドリフト0.3・腐敗最大0.8。</summary>
        public static ClericalCareerParams Default => new ClericalCareerParams(
            pietyWeight: 0.4f,
            ambitionWeight: 0.35f,
            patronageWeight: 0.25f,
            temporalRankWeight: 0.6f,
            temporalAmbitionWeight: 0.4f,
            driftAmplitude: 0.3f,
            corruptionMax: 0.8f);
    }

    /// <summary>
    /// 聖職キャリアの純ロジック（#1096・Pillars of the Earth＝宗教組織の役職ラダー・test-first）。
    /// <see cref="CareerPipelineRules"/>（武＝士官学校／官＝科挙・有力者／技＝テクノクラート）に次ぐ<b>第4系統＝聖</b>。
    /// 修道士→院長→司教級のラダーを、<b>理想駆動</b>（共同体の希望↑）と<b>出世主義</b>（権力闘争）が同じ階段を別の動機で登る
    /// ＝聖職は理想と権力の交差点。役職一般の資格/任命は <see cref="OfficeRules"/>、宗教の社会効果・改宗は <see cref="ReligionRules"/>、
    /// 希望は <see cref="HopeRules"/> に委譲し、ここは<b>聖職者の昇進・権威・堕落</b>の係数のみを出す。
    /// Game層（GameSettings/FleetRegistry 等）非依存＝Core 純ロジック・乱数なし決定論。調整値は <see cref="ClericalCareerParams"/> に集約。
    /// </summary>
    public static class ClericalCareerRules
    {
        /// <summary>聖職ラダーの段数（修道士=0 … 大司教=最上位）。</summary>
        public const int RankCount = 6;

        /// <summary>
        /// 階位を0..1へ正規化（修道士=0、大司教=1）。世俗権力/腐敗の高位寄り係数に使う純関数。
        /// </summary>
        public static float RankFraction(ClericalRank rank)
            => RankCount <= 1 ? 0f : Mathf.Clamp01((int)rank / (float)(RankCount - 1));

        /// <summary>
        /// 昇進力(0..1)：敬虔さ・野心・後ろ盾の加重和。<b>理想家も野心家も後ろ盾持ちも同じ階段を登る</b>＝聖職も人事政治。
        /// 既定の重みは和=1.0なので3要素すべて1なら昇進力1。理想駆動でも出世主義でも上には行ける（動機が違うだけ）。
        /// </summary>
        public static float PromotionScore(float piety, float ambition, float patronage, ClericalCareerParams p)
        {
            float pi = Mathf.Clamp01(piety);
            float am = Mathf.Clamp01(ambition);
            float pa = Mathf.Clamp01(patronage);
            return Mathf.Clamp01(pi * p.pietyWeight + am * p.ambitionWeight + pa * p.patronageWeight);
        }

        /// <summary>
        /// 霊的権威(0..1)：高位×敬虔＝信徒を導く力。<see cref="ReligionRules.ConversionPressure"/>（改宗圧力）の rulerFaith 側へ接続できる。
        /// 高位でも敬虔が低ければ権威は痩せる（地位だけでは魂は導けない）。
        /// </summary>
        public static float SpiritualAuthority(ClericalRank rank, float piety)
            => Mathf.Clamp01(RankFraction(rank) * Mathf.Clamp01(piety));

        /// <summary>
        /// 世俗権力(0..1)：高位の聖職は領地と政治力を持つ＝<b>司教は諸侯でもある</b>。階位の高さ（temporalRankWeight）と
        /// 野心の強さ（temporalAmbitionWeight）の加重和を、高位ほど効くよう階位で底上げする。修道士は世俗権力ほぼ0。
        /// </summary>
        public static float TemporalPower(ClericalRank rank, float ambition, ClericalCareerParams p)
        {
            float rf = RankFraction(rank);
            float am = Mathf.Clamp01(ambition);
            float w = p.temporalRankWeight + p.temporalAmbitionWeight;
            float blend = w <= 0f ? 0f : (rf * p.temporalRankWeight + am * p.temporalAmbitionWeight) / w;
            return Mathf.Clamp01(rf * blend);   // 高位ほど世俗権力が乗る（階位で底上げ）
        }

        /// <summary>
        /// 理想と野心のドリフト：<b>出世するほど初心の理想が世俗権力に侵食されうる＝堕落の誘惑</b>。
        /// 野心×到達階位が侵食を生み、敬虔さが高ければ抗う。正＝理想が勝ち希望が増す方向、負＝野心に侵食され堕落する方向。
        /// piety=ambition のときは敬虔さの綱が拮抗を制し非負側に寄る（理想家は登っても堕ちにくい）。
        /// </summary>
        public static float IdealVsAmbitionDrift(float piety, float ambition, float rankAchieved, ClericalCareerParams p)
        {
            float pi = Mathf.Clamp01(piety);
            float am = Mathf.Clamp01(ambition);
            float ra = Mathf.Clamp01(rankAchieved);
            float corruptingPull = am * ra;     // 野心×出世＝侵食
            float idealPull = pi;               // 敬虔さ＝初心を守る綱
            return Mathf.Clamp((idealPull - corruptingPull) * p.driftAmplitude, -p.driftAmplitude, p.driftAmplitude);
        }

        /// <summary>
        /// 共同体の希望への寄与(0..1)：理想駆動の聖職者（霊的権威の高い者）は <see cref="HopeRules"/> の希望を生む。
        /// 霊的権威にそのまま比例＝高位×敬虔の聖職が信徒に意味と慰めを与える。
        /// </summary>
        public static float CommunityHopeContribution(float spiritualAuthority)
            => Mathf.Clamp01(spiritualAuthority);

        /// <summary>
        /// 聖職の腐敗リスク(0..1)：世俗権力×低敬虔＝<b>堕落した高位聖職</b>（銀英伝の地球教の影）。
        /// 世俗権力が強いほど、かつ敬虔さが低いほど危うい。敬虔1なら世俗権力がどれだけ強くても腐敗リスク0（清貧の高位は堕ちない）。
        /// </summary>
        public static float CorruptionRisk(float temporalPower, float piety, ClericalCareerParams p)
        {
            float tp = Mathf.Clamp01(temporalPower);
            float pi = Mathf.Clamp01(piety);
            return Mathf.Clamp01(tp * (1f - pi) * p.corruptionMax);
        }
    }
}
