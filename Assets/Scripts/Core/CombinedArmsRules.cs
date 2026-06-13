using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 諸兵科連合（戦闘艦種の組合せ相乗）の調整係数。マジックナンバー禁止＝ここに集約。
    /// </summary>
    public readonly struct CombinedArmsParams
    {
        /// <summary>連携ボーナスの最大幅（バランス完璧で +synergyScale）。</summary>
        public readonly float synergyScale;
        /// <summary>役割充足が連携ボーナスへ寄与する重み（バランスと役割充足の混合）。</summary>
        public readonly float coverageWeight;
        /// <summary>単一艦種偏重ペナルティが効き始める支配率の閾値（これ未満は無罰）。</summary>
        public readonly float monocultureThreshold;
        /// <summary>支配率が1.0（完全単一）のときのペナルティ最大幅。</summary>
        public readonly float monocultureScale;
        /// <summary>欠けた役割1つあたりの弱点露出量。</summary>
        public readonly float weaknessPerRole;
        /// <summary>諸兵科連合の実効戦闘力倍率の下限（連携が崩れても残る最低値）。</summary>
        public readonly float minEffectiveness;
        /// <summary>諸兵科連合の実効戦闘力倍率の上限（精緻な連携の天井）。</summary>
        public readonly float maxEffectiveness;
        /// <summary>バランス編成と判定する既定の閾値（IsBalancedComposition の既定）。</summary>
        public readonly float balancedThreshold;

        public CombinedArmsParams(
            float synergyScale,
            float coverageWeight,
            float monocultureThreshold,
            float monocultureScale,
            float weaknessPerRole,
            float minEffectiveness,
            float maxEffectiveness,
            float balancedThreshold)
        {
            this.synergyScale = Mathf.Clamp(synergyScale, 0f, 1f);
            this.coverageWeight = Mathf.Clamp01(coverageWeight);
            this.monocultureThreshold = Mathf.Clamp(monocultureThreshold, 0.34f, 1f);
            this.monocultureScale = Mathf.Clamp(monocultureScale, 0f, 1f);
            this.weaknessPerRole = Mathf.Clamp(weaknessPerRole, 0f, 1f);
            this.minEffectiveness = Mathf.Clamp(minEffectiveness, 0.1f, 1f);
            this.maxEffectiveness = Mathf.Clamp(maxEffectiveness, 1f, 3f);
            this.balancedThreshold = Mathf.Clamp01(balancedThreshold);
        }

        /// <summary>既定の調整係数。</summary>
        public static CombinedArmsParams Default => new CombinedArmsParams(
            synergyScale: 0.30f,
            coverageWeight: 0.5f,
            monocultureThreshold: 0.6f,
            monocultureScale: 0.25f,
            weaknessPerRole: 0.2f,
            minEffectiveness: 0.7f,
            maxEffectiveness: 1.5f,
            balancedThreshold: 0.6f);
    }

    /// <summary>
    /// 諸兵科連合＝戦闘艦種（戦艦/巡航艦/駆逐艦）の組合せ相乗の純ロジック（test-first・盤面非依存・唯一の窓口）。
    /// 艦種を組み合わせると単一艦種より強い＝戦艦の火力・防御、巡航艦の汎用、駆逐艦の機動・雷撃が
    /// 相互に弱点を消し合う。バランスの取れた編成が連携ボーナスを得て、偏った編成は弱点を晒す。
    /// <para>分担（混同しない）：
    /// <see cref="ShipRoleRules"/> は<b>戦闘/非戦闘の混成可否</b>（戦闘艦隊と非戦闘艦隊を混ぜない）を扱い、本窓口は
    /// <b>戦闘艦種の内部組合せ相乗</b>を扱う＝別レイヤー。
    /// <see cref="ForceQualityRules"/> は<b>軍の質</b>（下士官団・練度・即応）を扱い、本窓口は<b>編成バランスの相乗</b>を扱う＝別レイヤー。</para>
    /// 入力比率は合計1になるよう内部で正規化（合計0は均等扱い）。実効値パターン（基準ダメージは変えず倍率で効かせる）。
    /// </summary>
    public static class CombinedArmsRules
    {
        // 役割充足の判定で「その役割が居る」とみなす最小シェア（極微量は戦力にならない）。
        private const float RolePresenceShare = 0.1f;

        /// <summary>3比率を合計1へ正規化（合計0は均等 1/3 ずつ）。null/負はクランプ。</summary>
        private static void Normalize(float a, float b, float c, out float na, out float nb, out float nc)
        {
            a = Mathf.Max(0f, a);
            b = Mathf.Max(0f, b);
            c = Mathf.Max(0f, c);
            float sum = a + b + c;
            if (sum <= 0f)
            {
                na = 1f / 3f;
                nb = 1f / 3f;
                nc = 1f / 3f;
                return;
            }
            na = a / sum;
            nb = b / sum;
            nc = c / sum;
        }

        /// <summary>
        /// 編成バランス（0..1）。均等（1/3,1/3,1/3）で1.0、単一艦種に偏るほど0へ。
        /// 均等比からの絶対偏差合計を最大偏差で正規化して反転（Pow/Log 不使用）。
        /// </summary>
        public static float Balance(float battleshipRatio, float cruiserRatio, float destroyerRatio, CombinedArmsParams p)
        {
            Normalize(battleshipRatio, cruiserRatio, destroyerRatio, out float b, out float c, out float d);
            const float even = 1f / 3f;
            float dev = Mathf.Abs(b - even) + Mathf.Abs(c - even) + Mathf.Abs(d - even);
            // 完全単一（1,0,0）の偏差合計 = (2/3)+(1/3)+(1/3) = 4/3 が最大。
            const float maxDev = 4f / 3f;
            return Mathf.Clamp01(1f - dev / maxDev);
        }

        /// <summary>既定 Params 版。</summary>
        public static float Balance(float battleshipRatio, float cruiserRatio, float destroyerRatio)
            => Balance(battleshipRatio, cruiserRatio, destroyerRatio, CombinedArmsParams.Default);

        /// <summary>連携ボーナス（0..synergyScale）。バランスが取れているほど大きい（線形）。</summary>
        public static float SynergyBonus(float balance, CombinedArmsParams p)
            => p.synergyScale * Mathf.Clamp01(balance);

        /// <summary>既定 Params 版。</summary>
        public static float SynergyBonus(float balance)
            => SynergyBonus(balance, CombinedArmsParams.Default);

        /// <summary>
        /// 役割充足度（0..1）。火力（戦艦）・汎用（巡航艦）・機動（駆逐艦）の3役割のうち
        /// 居る役割の割合。3役割すべて居れば1.0、2つで2/3、1つで1/3。
        /// </summary>
        public static float RoleCoverage(float battleshipRatio, float cruiserRatio, float destroyerRatio)
        {
            Normalize(battleshipRatio, cruiserRatio, destroyerRatio, out float b, out float c, out float d);
            int present = 0;
            if (b >= RolePresenceShare) present++;
            if (c >= RolePresenceShare) present++;
            if (d >= RolePresenceShare) present++;
            return present / 3f;
        }

        /// <summary>
        /// 欠けた役割ぶんの弱点露出（0..1）。<paramref name="missingRole"/>＝欠けている役割数（0..3）。
        /// 駆逐欠如＝対機動に弱い、戦艦欠如＝打撃力不足、巡航艦欠如＝汎用穴 のように欠けるほど弱点が増す。
        /// </summary>
        public static float WeaknessExposure(int missingRole, CombinedArmsParams p)
        {
            int m = Mathf.Clamp(missingRole, 0, 3);
            return Mathf.Clamp01(m * p.weaknessPerRole);
        }

        /// <summary>既定 Params 版。</summary>
        public static float WeaknessExposure(int missingRole)
            => WeaknessExposure(missingRole, CombinedArmsParams.Default);

        /// <summary>
        /// 前衛と打撃の連携（0..1）。駆逐艦が前衛スクリーン、戦艦が打撃の組合せが揃うほど高い。
        /// 両者の幾何平均（どちらか欠けると崩れる＝相補）。正規化済み比率で評価。
        /// </summary>
        public static float ScreenAndStrike(float destroyerRatio, float battleshipRatio)
        {
            float d = Mathf.Max(0f, destroyerRatio);
            float b = Mathf.Max(0f, battleshipRatio);
            float sum = d + b;
            if (sum <= 0f) return 0f;
            // 二者間の比率に正規化してから幾何平均（どちらかが0なら0＝相補が崩れる）。
            float dn = d / sum;
            float bn = b / sum;
            return Mathf.Sqrt(Mathf.Clamp01(dn) * Mathf.Clamp01(bn)) * 2f; // 均等(0.5,0.5)で1.0、偏ると0へ。
        }

        /// <summary>
        /// 単一艦種偏重ペナルティ（0..monocultureScale）。<paramref name="dominantRatio"/>＝最多艦種の支配率（0..1）。
        /// 閾値未満は無罰、閾値を超えるほど線形にペナルティが増す（単一艦種は弱点を突かれる）。
        /// </summary>
        public static float MonoculturePenalty(float dominantRatio, CombinedArmsParams p)
        {
            float dom = Mathf.Clamp01(dominantRatio);
            if (dom <= p.monocultureThreshold) return 0f;
            float span = 1f - p.monocultureThreshold;
            if (span <= 0f) return 0f;
            float t = (dom - p.monocultureThreshold) / span; // 0..1
            return Mathf.Clamp01(t) * p.monocultureScale;
        }

        /// <summary>既定 Params 版。</summary>
        public static float MonoculturePenalty(float dominantRatio)
            => MonoculturePenalty(dominantRatio, CombinedArmsParams.Default);

        /// <summary>
        /// 諸兵科連合の実効戦闘力倍率（minEffectiveness..maxEffectiveness）。
        /// 基準1.0＋連携ボーナス×役割充足の混合で押し上げる（実効値パターン＝基準ダメージは非破壊）。
        /// </summary>
        public static float CombinedEffectiveness(float synergyBonus, float roleCoverage, CombinedArmsParams p)
        {
            float bonus = Mathf.Max(0f, synergyBonus);
            float cov = Mathf.Clamp01(roleCoverage);
            // 連携ボーナスは役割が揃っているほど効く（穴があると連携しきれない）。
            float blended = bonus * Mathf.Lerp(1f - p.coverageWeight, 1f, cov);
            return Mathf.Clamp(1f + blended, p.minEffectiveness, p.maxEffectiveness);
        }

        /// <summary>既定 Params 版。</summary>
        public static float CombinedEffectiveness(float synergyBonus, float roleCoverage)
            => CombinedEffectiveness(synergyBonus, roleCoverage, CombinedArmsParams.Default);

        /// <summary>バランス編成か（balance が threshold 以上）。</summary>
        public static bool IsBalancedComposition(float balance, float threshold)
            => Mathf.Clamp01(balance) >= Mathf.Clamp01(threshold);

        /// <summary>既定閾値（CombinedArmsParams.Default.balancedThreshold）版。</summary>
        public static bool IsBalancedComposition(float balance)
            => IsBalancedComposition(balance, CombinedArmsParams.Default.balancedThreshold);
    }
}
