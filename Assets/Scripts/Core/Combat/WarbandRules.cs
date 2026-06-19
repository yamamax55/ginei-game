using UnityEngine;

namespace Ginei
{
    /// <summary>武装集団（ウォーバンド）の調整係数。</summary>
    public readonly struct WarbandParams
    {
        /// <summary>個人の武勇がカリスマに効く重み。</summary>
        public readonly float prowessWeight;
        /// <summary>気前の良さ（戦利品分配）がカリスマに効く重み。</summary>
        public readonly float generosityWeight;
        /// <summary>直近の勝利が結束に効く強さ。</summary>
        public readonly float victoryCohesion;
        /// <summary>勝利数の効きを飽和させる基準（これ以上は逓減）。</summary>
        public readonly float victorySaturation;
        /// <summary>分け前が引き留めに効く重み。</summary>
        public readonly float shareWeight;
        /// <summary>カリスマが引き留めに効く重み。</summary>
        public readonly float charismaWeight;
        /// <summary>結束が高いほど離散を抑える強さ。</summary>
        public readonly float cohesionGuard;
        /// <summary>獰猛さが略奪力に効く重み。</summary>
        public readonly float ferocityWeight;
        /// <summary>無統制の暴力が膨らむ強さ（味方への害）。</summary>
        public readonly float violenceScale;

        public WarbandParams(
            float prowessWeight, float generosityWeight, float victoryCohesion, float victorySaturation,
            float shareWeight, float charismaWeight, float cohesionGuard, float ferocityWeight, float violenceScale)
        {
            this.prowessWeight = Mathf.Clamp01(prowessWeight);
            this.generosityWeight = Mathf.Clamp01(generosityWeight);
            this.victoryCohesion = Mathf.Clamp01(victoryCohesion);
            this.victorySaturation = Mathf.Max(1f, victorySaturation);
            this.shareWeight = Mathf.Clamp01(shareWeight);
            this.charismaWeight = Mathf.Clamp01(charismaWeight);
            this.cohesionGuard = Mathf.Clamp01(cohesionGuard);
            this.ferocityWeight = Mathf.Clamp01(ferocityWeight);
            this.violenceScale = Mathf.Clamp01(violenceScale);
        }

        /// <summary>既定＝武勇0.6/気前0.4・勝利結束0.5/飽和5・分け前0.6/カリスマ0.4・離散抑制0.7・獰猛0.5・暴力0.5。</summary>
        public static WarbandParams Default => new WarbandParams(0.6f, 0.4f, 0.5f, 5f, 0.6f, 0.4f, 0.7f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 武装集団（ウォーバンド）の純ロジック。カリスマ的首領のもと戦利品で結束する戦士団＝勝利と分け前が
    /// 結束を生み、敗北は離散を招き、無統制の暴力は味方をも蝕む。すべて 0..1 に正規化した実効値で扱い
    /// （基準値非破壊）、乱数は用いず決定論で解決する。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WarbandRules
    {
        /// <summary>
        /// 首領のカリスマ（0..1）＝個人の武勇 personalProwess(0..1) と気前の良さ generosity(0..1) の重み付き和。
        /// 戦士は強く気前のよい首領に従う。
        /// </summary>
        public static float ChieftainCharisma(float personalProwess, float generosity, WarbandParams p)
        {
            float prowess = Mathf.Clamp01(personalProwess);
            float gen = Mathf.Clamp01(generosity);
            float wSum = Mathf.Max(1e-4f, p.prowessWeight + p.generosityWeight);
            return Mathf.Clamp01((prowess * p.prowessWeight + gen * p.generosityWeight) / wSum);
        }

        public static float ChieftainCharisma(float personalProwess, float generosity)
            => ChieftainCharisma(personalProwess, generosity, WarbandParams.Default);

        /// <summary>
        /// 武装集団の結束（0..1）＝カリスマを土台に、直近の勝利 recentVictories が結束を高める（飽和あり）。
        /// 連勝は結束を固めるが効果は逓減する。
        /// </summary>
        public static float WarbandCohesion(float chieftainCharisma, int recentVictories, WarbandParams p)
        {
            float charisma = Mathf.Clamp01(chieftainCharisma);
            float vict = Mathf.Max(0, recentVictories);
            float victoryFactor = Mathf.Clamp01(vict / p.victorySaturation); // 0..1（飽和）
            // 結束＝カリスマを土台に、勝利で上振れする（カリスマと勝利上限の間を victoryCohesion で補間）
            float cohesion = Mathf.Lerp(charisma, Mathf.Max(charisma, victoryFactor), p.victoryCohesion);
            return Mathf.Clamp01(cohesion);
        }

        public static float WarbandCohesion(float chieftainCharisma, int recentVictories)
            => WarbandCohesion(chieftainCharisma, recentVictories, WarbandParams.Default);

        /// <summary>
        /// 1人あたりの分け前（0..1）＝獲得した戦利品 spoilsCaptured を集団規模 warbandSize で割る。
        /// 大集団ほど薄まる。規模0は分け前0。
        /// </summary>
        public static float PlunderShare(float spoilsCaptured, float warbandSize, WarbandParams p)
        {
            float spoils = Mathf.Max(0f, spoilsCaptured);
            float size = Mathf.Max(0f, warbandSize);
            if (size <= 0f) return 0f;
            return Mathf.Clamp01(spoils / size);
        }

        public static float PlunderShare(float spoilsCaptured, float warbandSize)
            => PlunderShare(spoilsCaptured, warbandSize, WarbandParams.Default);

        /// <summary>
        /// 戦士の引き留め（0..1）＝分け前 plunderShare と首領のカリスマの重み付き和。十分な戦利品と
        /// カリスマがあれば戦士は留まる。
        /// </summary>
        public static float FollowerRetention(float plunderShare, float chieftainCharisma, WarbandParams p)
        {
            float share = Mathf.Clamp01(plunderShare);
            float charisma = Mathf.Clamp01(chieftainCharisma);
            float wSum = Mathf.Max(1e-4f, p.shareWeight + p.charismaWeight);
            return Mathf.Clamp01((share * p.shareWeight + charisma * p.charismaWeight) / wSum);
        }

        public static float FollowerRetention(float plunderShare, float chieftainCharisma)
            => FollowerRetention(plunderShare, chieftainCharisma, WarbandParams.Default);

        /// <summary>
        /// 敗北時の離散（0..1）＝敗北の深刻さ defeatSeverity に比例し、結束が高いほど（cohesionGuard で）抑えられる。
        /// 結束の固い集団は敗れても散りにくい。
        /// </summary>
        public static float DefeatDispersal(float defeatSeverity, float warbandCohesion, WarbandParams p)
        {
            float severity = Mathf.Clamp01(defeatSeverity);
            float cohesion = Mathf.Clamp01(warbandCohesion);
            float guard = 1f - cohesion * p.cohesionGuard; // 結束で離散を抑える
            return Mathf.Clamp01(severity * guard);
        }

        public static float DefeatDispersal(float defeatSeverity, float warbandCohesion)
            => DefeatDispersal(defeatSeverity, warbandCohesion, WarbandParams.Default);

        /// <summary>
        /// 略奪・襲撃の戦力（0..1）＝結束 warbandCohesion と獰猛さ ferocity の積寄り。獰猛さは ferocityWeight で
        /// 結束に上乗せされる（結束が土台、獰猛さが牙）。
        /// </summary>
        public static float RaidingPower(float warbandCohesion, float ferocity, WarbandParams p)
        {
            float cohesion = Mathf.Clamp01(warbandCohesion);
            float fero = Mathf.Clamp01(ferocity);
            float feroFactor = Mathf.Lerp(1f - p.ferocityWeight, 1f, fero); // 獰猛さで増幅
            return Mathf.Clamp01(cohesion * feroFactor);
        }

        public static float RaidingPower(float warbandCohesion, float ferocity)
            => RaidingPower(warbandCohesion, ferocity, WarbandParams.Default);

        /// <summary>
        /// 無統制の暴力（0..1・味方にも害）＝集団規模 warbandSize が大きく首領の統制 chieftainControl が低いほど膨らむ。
        /// 規模は violenceScale で飽和させ、統制の欠如 (1−control) を掛ける。
        /// </summary>
        public static float UndisciplinedViolence(float warbandSize, float chieftainControl, WarbandParams p)
        {
            float size = Mathf.Max(0f, warbandSize);
            float control = Mathf.Clamp01(chieftainControl);
            float sizeFactor = Mathf.Clamp01(size * p.violenceScale); // 規模で飽和
            return Mathf.Clamp01(sizeFactor * (1f - control));
        }

        public static float UndisciplinedViolence(float warbandSize, float chieftainControl)
            => UndisciplinedViolence(warbandSize, chieftainControl, WarbandParams.Default);

        /// <summary>
        /// 武装集団の実効戦力（0..1）＝略奪力 raidingPower を引き留め followerRetention で支え、無統制の暴力
        /// undisciplinedViolence ぶん差し引く。留まる戦士のいない、あるいは内部崩壊した集団は戦力にならない。
        /// </summary>
        public static float WarbandStrength(float raidingPower, float followerRetention, float undisciplinedViolence, WarbandParams p)
        {
            float raid = Mathf.Clamp01(raidingPower);
            float retention = Mathf.Clamp01(followerRetention);
            float violence = Mathf.Clamp01(undisciplinedViolence);
            float strength = raid * retention - violence;
            return Mathf.Clamp01(strength);
        }

        public static float WarbandStrength(float raidingPower, float followerRetention, float undisciplinedViolence)
            => WarbandStrength(raidingPower, followerRetention, undisciplinedViolence, WarbandParams.Default);
    }
}
