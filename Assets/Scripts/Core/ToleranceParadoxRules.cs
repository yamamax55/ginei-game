using UnityEngine;

namespace Ginei
{
    /// <summary>寛容のパラドックスの調整係数。</summary>
    public readonly struct ToleranceParadoxParams
    {
        /// <summary>寛容な社会の隙を不寛容派が利用する強さ（寛容が高いほど悪用の余地が大きい）。</summary>
        public readonly float exploitationScale;
        /// <summary>乗っ取りが寛容を破壊する速さ（per dt・寛容の自殺）。</summary>
        public readonly float erosionRate;
        /// <summary>抑制が自らの寛容原則を傷つける強さ（線引きの苦悩）。</summary>
        public readonly float suppressionPenaltyScale;
        /// <summary>戦う民主主義の制度防衛の効き（制度的排除が脅威を抑える強さ）。</summary>
        public readonly float militantDefenseScale;

        public ToleranceParadoxParams(float exploitationScale, float erosionRate,
                                      float suppressionPenaltyScale, float militantDefenseScale)
        {
            this.exploitationScale = Mathf.Clamp01(exploitationScale);
            this.erosionRate = Mathf.Max(0f, erosionRate);
            this.suppressionPenaltyScale = Mathf.Clamp01(suppressionPenaltyScale);
            this.militantDefenseScale = Mathf.Clamp01(militantDefenseScale);
        }

        /// <summary>既定＝悪用係数0.9・侵食速度0.06・抑制ペナルティ係数0.7・制度防衛係数0.8。</summary>
        public static ToleranceParadoxParams Default => new ToleranceParadoxParams(0.9f, 0.06f, 0.7f, 0.8f);
    }

    /// <summary>
    /// 寛容のパラドックスの純ロジック（POPR-4 #1518・ポパー『開かれた社会とその敵』脚注参考）。
    /// 「無制限の寛容は寛容そのものの消滅を招く」＝不寛容な者まで寛容に扱うと、彼らが寛容な社会を
    /// 乗っ取って寛容を破壊する。それゆえ寛容な社会は不寛容に対しては寛容であってはならない。だが
    /// どこで線を引くか＝抑制しすぎれば自らが不寛容になる（抑制のジレンマ）。寛容な社会ほど不寛容派が
    /// その隙を突いて勢力を伸ばし（IntolerantExploitation）、多数化×無防備で乗っ取りリスクが生じ
    /// （TakeoverRisk）、乗っ取りが進めば社会全体の寛容が破壊される（ToleranceErosion）。不寛容を
    /// 抑えるべきだが抑制は自らの寛容原則を傷つけ（SuppressionDilemma）、早すぎても遅すぎてもいけない
    /// 最適介入（OptimalIntervention）と、寛容しすぎて手遅れになる臨界（SelfDefeatingTolerance）、
    /// 制度的に不寛容を排除する戦う民主主義（MilitantDemocracy）を式に出す。
    /// <see cref="PluralityRules"/>（複数性＝視点の多様性・公的領域）・<see cref="FreePressRules"/>
    /// （報道の自由）とは別＝こちらは「寛容が自らを滅ぼす逆説（不寛容への寛容の限界）」を扱う。同 EPIC
    /// POPR の <see cref="OpennessRules"/>（開かれた社会）・MilitantDemocracy 系（戦う民主主義の制度防衛）
    /// とも分担。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ToleranceParadoxRules
    {
        /// <summary>
        /// 不寛容派の悪用度（0..1）＝寛容な社会 societalTolerance(0..1) × 不寛容派の攻撃性
        /// intolerantAggression(0..1) × 悪用係数。寛容な社会ほど不寛容派がその寛容（＝隙）を利用して
        /// 勢力を伸ばす＝寛容が高いほど、攻撃的な者ほど突きやすい（積）。社会が不寛容なら隙そのものが無い。
        /// </summary>
        public static float IntolerantExploitation(float societalTolerance, float intolerantAggression, ToleranceParadoxParams p)
        {
            float t = Mathf.Clamp01(societalTolerance);
            float a = Mathf.Clamp01(intolerantAggression);
            return Mathf.Clamp01(t * a * p.exploitationScale);
        }

        public static float IntolerantExploitation(float societalTolerance, float intolerantAggression)
            => IntolerantExploitation(societalTolerance, intolerantAggression, ToleranceParadoxParams.Default);

        /// <summary>
        /// 乗っ取りリスク（0..1）＝不寛容派の勢力 intolerantShare(0..1) × 寛容な社会の無防備さ。
        /// 無防備さ＝寛容 societalTolerance が高く制度的防壁 institutionalSafeguards(0..1) が薄いほど大きい
        /// ＝societalTolerance×(1−institutionalSafeguards)。多数化（勢力大）×無防備（寛容で無防衛）で
        /// 不寛容派が寛容を悪用して社会を乗っ取る。制度防壁が固ければ勢力があっても乗っ取れない。
        /// </summary>
        public static float TakeoverRisk(float intolerantShare, float societalTolerance, float institutionalSafeguards)
        {
            float share = Mathf.Clamp01(intolerantShare);
            float t = Mathf.Clamp01(societalTolerance);
            float defenseless = t * (1f - Mathf.Clamp01(institutionalSafeguards));
            return Mathf.Clamp01(share * defenseless);
        }

        /// <summary>
        /// 寛容の侵食＝乗っ取り進行後の社会の寛容（0..1）。乗っ取りリスク takeoverRisk(0..1) に比例して
        /// 社会全体の寛容が破壊される（erosionRate×takeoverRisk×dt ずつ低下）＝寛容の自殺。
        /// 乗っ取りが進むほど寛容そのものが失われていく（無制限の寛容が寛容を滅ぼす）。
        /// </summary>
        public static float ToleranceErosion(float societalTolerance, float takeoverRisk, float dt, ToleranceParadoxParams p)
        {
            float delta = p.erosionRate * Mathf.Clamp01(takeoverRisk) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(societalTolerance) - delta);
        }

        public static float ToleranceErosion(float societalTolerance, float takeoverRisk, float dt)
            => ToleranceErosion(societalTolerance, takeoverRisk, dt, ToleranceParadoxParams.Default);

        /// <summary>
        /// 抑制のジレンマ＝抑制が自らの寛容原則に与える傷（0..1）。不寛容の脅威 intolerantThreat(0..1) を
        /// 抑えるほど、また自らの寛容原則を重んじる ownToleranceValue(0..1) ほど傷は深い
        /// ＝intolerantThreat×ownToleranceValue×抑制ペナルティ係数。不寛容を抑えるべきだが、抑制という
        /// 行為そのものが寛容な社会の原則を裏切る＝どこで線を引くかの苦悩（寛容を守るために不寛容になる逆説）。
        /// </summary>
        public static float SuppressionDilemma(float intolerantThreat, float ownToleranceValue, ToleranceParadoxParams p)
        {
            float threat = Mathf.Clamp01(intolerantThreat);
            float value = Mathf.Clamp01(ownToleranceValue);
            return Mathf.Clamp01(threat * value * p.suppressionPenaltyScale);
        }

        public static float SuppressionDilemma(float intolerantThreat, float ownToleranceValue)
            => SuppressionDilemma(intolerantThreat, ownToleranceValue, ToleranceParadoxParams.Default);

        /// <summary>
        /// 最適な介入水準（0..1）＝乗っ取りを防ぎつつ寛容を保つ線引き。脅威 intolerantThreat(0..1) が
        /// 大きいほど介入を強め、抑制コスト suppressionCost(0..1)（＝寛容原則の毀損）が大きいほど控える
        /// ＝intolerantThreat×(1−suppressionCost)。脅威が小さければ介入は無用（早すぎる介入は自らが不寛容）、
        /// 抑制コストが大きければ慎重に＝早すぎても（過剰抑圧）遅すぎても（手遅れ）いけない線引き。
        /// </summary>
        public static float OptimalIntervention(float intolerantThreat, float suppressionCost)
        {
            float threat = Mathf.Clamp01(intolerantThreat);
            float cost = Mathf.Clamp01(suppressionCost);
            return Mathf.Clamp01(threat * (1f - cost));
        }

        /// <summary>
        /// 自滅する寛容の判定。寛容 societalTolerance が高く（threshold 以上）、かつ不寛容の脅威
        /// intolerantThreat が高い（threshold 以上）とき true＝無制限の寛容のまま脅威を放置した臨界
        /// ＝寛容しすぎて手遅れ（不寛容派が育ちきってから抑えようとしても遅い）。
        /// </summary>
        public static bool SelfDefeatingTolerance(float societalTolerance, float intolerantThreat, float threshold)
        {
            float t = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(societalTolerance) >= t
                   && Mathf.Clamp01(intolerantThreat) >= t;
        }

        /// <summary>
        /// 戦う民主主義の防衛力（0..1）＝制度的防壁 institutionalSafeguards(0..1) × 不寛容の脅威
        /// intolerantThreat(0..1) × 制度防衛係数。制度的に不寛容を排除する（戦う民主主義）＝防壁が固く、
        /// 脅威が現に存在するほど防衛が働く。ただし脅威が小さいのに制度を振るえば濫用（過剰抑圧）になるため
        /// 脅威に比例させる（脅威0なら防衛も0＝平時に振るうべきでない）。
        /// </summary>
        public static float MilitantDemocracy(float institutionalSafeguards, float intolerantThreat, ToleranceParadoxParams p)
        {
            float safeguards = Mathf.Clamp01(institutionalSafeguards);
            float threat = Mathf.Clamp01(intolerantThreat);
            return Mathf.Clamp01(safeguards * threat * p.militantDefenseScale);
        }

        public static float MilitantDemocracy(float institutionalSafeguards, float intolerantThreat)
            => MilitantDemocracy(institutionalSafeguards, intolerantThreat, ToleranceParadoxParams.Default);

        /// <summary>
        /// 寛容崩壊の判定。寛容の侵食後の値 toleranceErosion（＝<see cref="ToleranceErosion"/>の結果 0..1）が
        /// threshold 以下まで落ちたとき true＝乗っ取りによって寛容が破壊され尽くした状態（寛容の自殺の完了）。
        /// </summary>
        public static bool IsToleranceCollapse(float toleranceErosion, float threshold)
        {
            return Mathf.Clamp01(toleranceErosion) <= Mathf.Clamp01(threshold);
        }
    }
}
