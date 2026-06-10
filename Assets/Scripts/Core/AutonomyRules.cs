using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 指揮ドクトリン（#544-550）。集団依存＝統制された一斉行動（低分散・士気底上げ・反応遅）、
    /// 自律分散＝各艦隊のスタンドプレーから創発するシナジー（高反応・高天井・高分散）。
    /// </summary>
    public enum CommandDoctrine
    {
        集団依存,
        自律分散,
    }

    /// <summary>
    /// 指揮ドクトリンの基礎特性（#544-550）。実効値パターンで読む側に渡す純データ。
    /// reactivity＝反応速度、ceiling＝出力天井、variance＝結果のばらつき、moraleFloor＝士気の底上げ。
    /// </summary>
    public readonly struct DoctrineProfile
    {
        /// <summary>反応速度（高いほど臨機応変・自律分散が高い）。</summary>
        public readonly float Reactivity;
        /// <summary>出力天井（高いほど傑物の上振れを許容・自律分散が高い）。</summary>
        public readonly float Ceiling;
        /// <summary>結果のばらつき（高いほど博打・自律分散が高い）。</summary>
        public readonly float Variance;
        /// <summary>士気の底上げ（高いほど凡庸でも崩れにくい・集団依存が高い）。</summary>
        public readonly float MoraleFloor;

        public DoctrineProfile(float reactivity, float ceiling, float variance, float moraleFloor)
        {
            Reactivity = reactivity;
            Ceiling = ceiling;
            Variance = variance;
            MoraleFloor = moraleFloor;
        }
    }

    /// <summary>
    /// 自律分散チームワーク＝スタンドプレーから生じるシナジー（#544-550・傑物前提）。
    /// 全員が好成績のときだけ創発ボーナスが立ち、一人でも凡庸だと崩れる（非依存＝相互補完しない）。
    /// 傑物前提＝高能力×高自律でのみ機能し、能力不足の自律は逆機能（負の補正）になる。
    /// 仏教/空の実践（#564）の系譜だが純数式に留める。純ロジック・test-first。
    /// </summary>
    public static class AutonomyRules
    {
        /// <summary>
        /// 指揮ドクトリンの基礎特性を返す。集団依存＝低分散・士気底上げ・反応遅・天井低、
        /// 自律分散＝高反応・高天井・高分散・士気底上げ無し。
        /// </summary>
        public static DoctrineProfile DoctrineFactor(CommandDoctrine doctrine, AutonomyParams p)
        {
            switch (doctrine)
            {
                case CommandDoctrine.自律分散:
                    return new DoctrineProfile(
                        p.AutonomyReactivity,
                        p.AutonomyCeiling,
                        p.AutonomyVariance,
                        p.AutonomyMoraleFloor);
                case CommandDoctrine.集団依存:
                default:
                    return new DoctrineProfile(
                        p.DependentReactivity,
                        p.DependentCeiling,
                        p.DependentVariance,
                        p.DependentMoraleFloor);
            }
        }

        /// <summary>
        /// 創発シナジー＝各艦隊の好成績(0..1)が全員 synergyThreshold を超えたときだけ立つ非依存ボーナス。
        /// 一人でも閾値未満なら 0（相互補完しないので穴を埋められない）。自律度(0..1)に比例し、
        /// 揃った好成績の平均で強度が決まる。基準値は変えずローカルに加算分を返す（実効値パターン）。
        /// </summary>
        public static float EmergentSynergy(IList<float> performances, float autonomy, AutonomyParams p)
        {
            if (performances == null || performances.Count == 0) return 0f;

            float sum = 0f;
            for (int i = 0; i < performances.Count; i++)
            {
                float perf = Mathf.Clamp01(performances[i]);
                if (perf < p.SynergyThreshold) return 0f; // 一人でも凡庸なら創発しない
                sum += perf;
            }

            float average = sum / performances.Count;
            return Mathf.Clamp01(autonomy) * p.SynergyGain * average;
        }

        /// <summary>
        /// 機能補正＝傑物前提。capability×autonomy が functionalThreshold 以上でのみ正の補正（最大 functionalBonus）。
        /// 閾値未満は逆機能＝負の補正（最大 -dysfunctionPenalty）＝能力不足の自律は統制崩壊を招く。
        /// </summary>
        public static float IsFunctional(float capability, float autonomy, AutonomyParams p)
        {
            float drive = Mathf.Clamp01(capability) * Mathf.Clamp01(autonomy);
            if (drive >= p.FunctionalThreshold)
            {
                // 閾値〜1.0 を 0..1 に正規化して正のボーナスへ
                float t = (drive - p.FunctionalThreshold) / Mathf.Max(1e-4f, 1f - p.FunctionalThreshold);
                return p.FunctionalBonus * Mathf.Clamp01(t);
            }
            // 0〜閾値を 1..0 に正規化して逆機能ペナルティへ（閾値ちょうどで 0）
            float u = (p.FunctionalThreshold - drive) / Mathf.Max(1e-4f, p.FunctionalThreshold);
            return -p.DysfunctionPenalty * Mathf.Clamp01(u);
        }

        /// <summary>
        /// 信頼(自律型)と結束(依存型)の二系統を返す。自律分散は信頼が厚く（cohesionBase を信頼へ寄せる）、
        /// 集団依存は結束が厚い。trust＝相互信頼で動く度合い、cohesion＝統制でまとまる度合い。
        /// </summary>
        public static void TrustVsCohesion(CommandDoctrine doctrine, AutonomyParams p, out float trust, out float cohesion)
        {
            switch (doctrine)
            {
                case CommandDoctrine.自律分散:
                    trust = p.AutonomyTrust;
                    cohesion = p.AutonomyCohesion;
                    break;
                case CommandDoctrine.集団依存:
                default:
                    trust = p.DependentTrust;
                    cohesion = p.DependentCohesion;
                    break;
            }
        }
    }

    /// <summary>
    /// AutonomyRules の調整値（マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// </summary>
    public readonly struct AutonomyParams
    {
        /// <summary>創発シナジーが立つ好成績の閾値（全員がこれを超える必要がある）。</summary>
        public readonly float SynergyThreshold;
        /// <summary>創発シナジーの最大係数（揃った好成績の平均×自律度に乗じる）。</summary>
        public readonly float SynergyGain;
        /// <summary>機能/逆機能の境目（capability×autonomy がこれ未満で逆機能）。</summary>
        public readonly float FunctionalThreshold;
        /// <summary>機能時の最大ボーナス。</summary>
        public readonly float FunctionalBonus;
        /// <summary>逆機能時の最大ペナルティ（絶対値）。</summary>
        public readonly float DysfunctionPenalty;

        // 自律分散ドクトリンの特性
        public readonly float AutonomyReactivity;
        public readonly float AutonomyCeiling;
        public readonly float AutonomyVariance;
        public readonly float AutonomyMoraleFloor;
        public readonly float AutonomyTrust;
        public readonly float AutonomyCohesion;

        // 集団依存ドクトリンの特性
        public readonly float DependentReactivity;
        public readonly float DependentCeiling;
        public readonly float DependentVariance;
        public readonly float DependentMoraleFloor;
        public readonly float DependentTrust;
        public readonly float DependentCohesion;

        public AutonomyParams(
            float synergyThreshold, float synergyGain,
            float functionalThreshold, float functionalBonus, float dysfunctionPenalty,
            float autonomyReactivity, float autonomyCeiling, float autonomyVariance, float autonomyMoraleFloor,
            float autonomyTrust, float autonomyCohesion,
            float dependentReactivity, float dependentCeiling, float dependentVariance, float dependentMoraleFloor,
            float dependentTrust, float dependentCohesion)
        {
            SynergyThreshold = synergyThreshold;
            SynergyGain = synergyGain;
            FunctionalThreshold = functionalThreshold;
            FunctionalBonus = functionalBonus;
            DysfunctionPenalty = dysfunctionPenalty;
            AutonomyReactivity = autonomyReactivity;
            AutonomyCeiling = autonomyCeiling;
            AutonomyVariance = autonomyVariance;
            AutonomyMoraleFloor = autonomyMoraleFloor;
            AutonomyTrust = autonomyTrust;
            AutonomyCohesion = autonomyCohesion;
            DependentReactivity = dependentReactivity;
            DependentCeiling = dependentCeiling;
            DependentVariance = dependentVariance;
            DependentMoraleFloor = dependentMoraleFloor;
            DependentTrust = dependentTrust;
            DependentCohesion = dependentCohesion;
        }

        /// <summary>
        /// 既定（synergyThreshold=0.7／functionalThreshold=0.5）。自律分散＝高反応/高天井/高分散・低結束、
        /// 集団依存＝低分散/士気底上げ/反応遅・高結束。
        /// </summary>
        public static AutonomyParams Default => new AutonomyParams(
            synergyThreshold: 0.7f, synergyGain: 0.5f,
            functionalThreshold: 0.5f, functionalBonus: 0.4f, dysfunctionPenalty: 0.3f,
            autonomyReactivity: 1.3f, autonomyCeiling: 1.5f, autonomyVariance: 0.6f, autonomyMoraleFloor: 0.2f,
            autonomyTrust: 0.85f, autonomyCohesion: 0.4f,
            dependentReactivity: 0.8f, dependentCeiling: 1.1f, dependentVariance: 0.2f, dependentMoraleFloor: 0.6f,
            dependentTrust: 0.4f, dependentCohesion: 0.85f);
    }
}
