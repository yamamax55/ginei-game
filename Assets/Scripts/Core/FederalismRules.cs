using UnityEngine;

namespace Ginei
{
    /// <summary>連邦制の調整係数（ctor で全項クランプ・既定は <see cref="Default"/>）。</summary>
    public readonly struct FederalismParams
    {
        /// <summary>完全分権(devolution=1)時の統一行動速度の下限(0..1)。分権しても国家は完全停止はしない。</summary>
        public readonly float minUnifiedSpeed;
        /// <summary>実験場効果が飽和する州数（これ以上増やしても A/B テストの価値は伸びない）。最低1。</summary>
        public readonly int experimentRegionCap;
        /// <summary>分離独立の滑り坂の傾き（0以上）。大きいほど自治が独立志向へ転化しやすい。</summary>
        public readonly float secessionSlope;
        /// <summary>戦時の集権圧の重み（0以上）。大きいほど小さな脅威でも集権へ振れる。</summary>
        public readonly float warCentralizationWeight;

        public FederalismParams(float minUnifiedSpeed, int experimentRegionCap,
            float secessionSlope, float warCentralizationWeight)
        {
            this.minUnifiedSpeed = Mathf.Clamp01(minUnifiedSpeed);
            this.experimentRegionCap = Mathf.Max(1, experimentRegionCap);
            this.secessionSlope = Mathf.Max(0f, secessionSlope);
            this.warCentralizationWeight = Mathf.Max(0f, warCentralizationWeight);
        }

        /// <summary>既定＝完全分権でも速度0.4・実験飽和10州・滑り坂傾き1.0・集権圧重み1.0。</summary>
        public static FederalismParams Default => new FederalismParams(0.4f, 10, 1f, 1f);
    }

    /// <summary>
    /// 連邦制の純ロジック＝中央と地方の権限配分（垂直の権力分立）。
    /// <see cref="SeparationOfPowersRules"/> が同一レベル内の三府の抑制均衡（水平の分立）を扱うのに対し、
    /// こちらは中央↔地方という階層間の配分を扱う＝両者は直交する別軸。
    /// 核となる振り子の力学：分権(devolution)は多様な地域への政策適合（<see cref="LocalFitness"/>）と
    /// 州の実験場効果（<see cref="PolicyExperimentValue"/>）を生むが、引き換えに国家的決断が鈍り
    /// （<see cref="UnifiedActionSpeed"/>）、自治が育てた地域意識は独立志向の土壌になる
    /// （<see cref="SecessionGradient"/>）。戦争の脅威は集権圧（<see cref="CentralizationPressure"/>）を生み
    /// 「戦争は国家を作る」＝平時は分権が賢く、戦時は集権が速い。その綱引きの均衡点が
    /// <see cref="OptimalDevolution"/>。全入力クランプ・乱数なし決定論・基準値非破壊。test-first。
    /// </summary>
    public static class FederalismRules
    {
        /// <summary>
        /// 政策の地域適合度(0..1)。多様な国(regionalDiversity 高)ほど中央の画一政策(devolution 低)は
        /// 現地の実情から外れる＝1 − 多様性×(1−分権度)。均質な国(多様性0)は画一政策でも常に1.0、
        /// 完全分権(1)なら多様でも常に1.0（各地方が自分に合う政策を選べる）。
        /// </summary>
        /// <param name="devolution">分権度(0..1)。0=完全中央集権、1=完全分権。</param>
        /// <param name="regionalDiversity">地域の多様性(0..1)。0=均質、1=極めて多様。</param>
        public static float LocalFitness(float devolution, float regionalDiversity)
        {
            float d = Mathf.Clamp01(devolution);
            float div = Mathf.Clamp01(regionalDiversity);
            return Mathf.Clamp01(1f - div * (1f - d));
        }

        /// <summary>
        /// 国家的決断の速度(minUnifiedSpeed..1)。分権ほど合意調達のレイヤーが増えて統一行動が鈍る＝
        /// 戦時の弱点。集権(0)で1.0、完全分権(1)で <see cref="FederalismParams.minUnifiedSpeed"/>（線形補間）。
        /// </summary>
        public static float UnifiedActionSpeed(float devolution, FederalismParams p)
            => Mathf.Lerp(1f, p.minUnifiedSpeed, Mathf.Clamp01(devolution));

        /// <summary>既定パラメータ版。</summary>
        public static float UnifiedActionSpeed(float devolution)
            => UnifiedActionSpeed(devolution, FederalismParams.Default);

        /// <summary>
        /// 州の実験場効果(0..1)＝政策の A/B テスト価値。分権度×州数の収穫逓減 (1 − 1/n)。
        /// 1州以下では比較対象が無く0、州数は <see cref="FederalismParams.experimentRegionCap"/> で飽和。
        /// 集権(devolution=0)では州が幾つあっても実験できず0。
        /// </summary>
        public static float PolicyExperimentValue(float devolution, int regionCount, FederalismParams p)
        {
            int n = Mathf.Clamp(regionCount, 0, p.experimentRegionCap);
            if (n <= 1) return 0f; // 単一州＝比較不能
            return Mathf.Clamp01(Mathf.Clamp01(devolution) * (1f - 1f / n));
        }

        /// <summary>既定パラメータ版。</summary>
        public static float PolicyExperimentValue(float devolution, int regionCount)
            => PolicyExperimentValue(devolution, regionCount, FederalismParams.Default);

        /// <summary>
        /// 分権の滑り坂(0..1)＝自治が育てた地域意識(regionalIdentity)が独立志向へ転化する土壌の強さ。
        /// 分権度×地域意識×傾き。自治が無ければ(0)独立の足場も無く、意識が無ければ(0)自治は安全。
        /// <see cref="CultureRules.SeparatismRisk"/>（少数民族の分離独立リスク）と接続する想定＝
        /// この勾配を土壌係数としてリスクへ掛け合わせる（基準値は非破壊）。
        /// </summary>
        public static float SecessionGradient(float devolution, float regionalIdentity, FederalismParams p)
            => Mathf.Clamp01(p.secessionSlope * Mathf.Clamp01(devolution) * Mathf.Clamp01(regionalIdentity));

        /// <summary>既定パラメータ版。</summary>
        public static float SecessionGradient(float devolution, float regionalIdentity)
            => SecessionGradient(devolution, regionalIdentity, FederalismParams.Default);

        /// <summary>
        /// 戦時の集権圧(0..1)＝「戦争は国家を作る」。外的脅威(warThreat)に比例して中央へ権限を
        /// 集める圧力が生じる（重みは <see cref="FederalismParams.warCentralizationWeight"/>）。
        /// </summary>
        public static float CentralizationPressure(float warThreat, FederalismParams p)
            => Mathf.Clamp01(Mathf.Clamp01(warThreat) * p.warCentralizationWeight);

        /// <summary>既定パラメータ版。</summary>
        public static float CentralizationPressure(float warThreat)
            => CentralizationPressure(warThreat, FederalismParams.Default);

        /// <summary>
        /// 多様性と脅威の綱引きの均衡点(0..1)＝振り子の力学。多様性は分権を求め（適合のため）、
        /// 戦時の集権圧はそれを割り引く：均衡分権度＝多様性×(1−集権圧)。
        /// 平時(脅威0)は多様性ぶん分権するのが賢く、総力戦(集権圧1)では多様でも集権(0)が速い。
        /// 均質な国(多様性0)はそもそも分権の利得が無く常に0。
        /// </summary>
        public static float OptimalDevolution(float regionalDiversity, float warThreat, FederalismParams p)
            => Mathf.Clamp01(Mathf.Clamp01(regionalDiversity) * (1f - CentralizationPressure(warThreat, p)));

        /// <summary>既定パラメータ版。</summary>
        public static float OptimalDevolution(float regionalDiversity, float warThreat)
            => OptimalDevolution(regionalDiversity, warThreat, FederalismParams.Default);
    }
}
