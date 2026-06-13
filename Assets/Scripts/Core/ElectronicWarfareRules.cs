using UnityEngine;

namespace Ginei
{
    /// <summary>電子戦（ECM/ECCM）の調整係数。</summary>
    public readonly struct EwParams
    {
        /// <summary>妨害が敵命中を削る最大割合。</summary>
        public readonly float maxAccuracyDegrade;
        /// <summary>妨害が敵探知を削る最大割合。</summary>
        public readonly float maxDetectionDegrade;
        /// <summary>発信源逆探知のしやすさ（妨害出力に比例して自分の位置がさらされる）。</summary>
        public readonly float emissionExposure;

        public EwParams(float maxAccuracyDegrade, float maxDetectionDegrade, float emissionExposure)
        {
            this.maxAccuracyDegrade = Mathf.Clamp01(maxAccuracyDegrade);
            this.maxDetectionDegrade = Mathf.Clamp01(maxDetectionDegrade);
            this.emissionExposure = Mathf.Clamp01(emissionExposure);
        }

        /// <summary>既定＝命中低下上限50%・探知低下上限70%・逆探知係数0.8。</summary>
        public static EwParams Default => new EwParams(0.5f, 0.7f, 0.8f);
    }

    /// <summary>
    /// 電子戦（ECM/ECCM）の純ロジック（能動妨害）。妨害出力と対抗手段（ECCM）の競り＝実効妨害が
    /// 敵の命中・探知を削る。だが妨害電波の発信は自分の位置を晒す＝強く焚くほど対輻射攻撃の的になる
    /// （沈黙か優位かのトレードオフ）。受動的な環境（<see cref="TerrainRules"/>）・自前センサー
    /// （<see cref="ReconRules"/>）とは別系統＝こちらは敵への能動干渉。乱数なし・決定論。
    /// 倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ElectronicWarfareRules
    {
        /// <summary>
        /// 実効妨害強度（0..1）＝妨害出力 jammerPower(0..1) から相手の対抗 eccm(0..1) を相殺した残り。
        /// ECCM が妨害を上回れば 0（妨害は無効化される）。
        /// </summary>
        public static float EffectiveJamming(float jammerPower, float eccm)
        {
            return Mathf.Clamp01(Mathf.Clamp01(jammerPower) - Mathf.Clamp01(eccm));
        }

        /// <summary>被妨害側の命中倍率（1−実効妨害×上限）。ダメージ・命中計算に掛ける。</summary>
        public static float AccuracyFactor(float jammerPower, float eccm, EwParams p)
        {
            return 1f - EffectiveJamming(jammerPower, eccm) * p.maxAccuracyDegrade;
        }

        public static float AccuracyFactor(float jammerPower, float eccm)
            => AccuracyFactor(jammerPower, eccm, EwParams.Default);

        /// <summary>被妨害側の探知倍率（1−実効妨害×上限）。索敵・探知率に掛ける。</summary>
        public static float DetectionFactor(float jammerPower, float eccm, EwParams p)
        {
            return 1f - EffectiveJamming(jammerPower, eccm) * p.maxDetectionDegrade;
        }

        public static float DetectionFactor(float jammerPower, float eccm)
            => DetectionFactor(jammerPower, eccm, EwParams.Default);

        /// <summary>
        /// 妨害源の被逆探知度（0..1）＝妨害出力×逆探知係数。ECCM では消えない（電波を出している事実は隠せない）。
        /// 対輻射攻撃の命中ボーナス等の入力に使う。
        /// </summary>
        public static float EmitterExposure(float jammerPower, EwParams p)
        {
            return Mathf.Clamp01(jammerPower) * p.emissionExposure;
        }

        public static float EmitterExposure(float jammerPower)
            => EmitterExposure(jammerPower, EwParams.Default);

        /// <summary>
        /// 妨害を焚く価値があるか＝得るもの（実効妨害）が代償（被逆探知度×脅威 threat(0..1)）を上回る。
        /// 敵に対輻射打撃力が無ければ（threat=0）焚き得、強ければ沈黙が正解になる。
        /// </summary>
        public static bool WorthJamming(float jammerPower, float eccm, float threat, EwParams p)
        {
            float gain = EffectiveJamming(jammerPower, eccm);
            float cost = EmitterExposure(jammerPower, p) * Mathf.Clamp01(threat);
            return gain > cost;
        }

        public static bool WorthJamming(float jammerPower, float eccm, float threat)
            => WorthJamming(jammerPower, eccm, threat, EwParams.Default);
    }
}
