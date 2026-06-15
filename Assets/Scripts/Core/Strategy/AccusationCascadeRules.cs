using UnityEngine;

namespace Ginei
{
    /// <summary>告発カスケードの調整係数（マッカイ『狂気とバブル』型）。</summary>
    public readonly struct AccusationCascadeParams
    {
        /// <summary>責任転嫁傾向1のときの告発圧力スケール（損失が誰かのせいにされる強さ）。</summary>
        public readonly float blameScale;
        /// <summary>自己増殖率（一件の処断が次の標的を呼ぶロジスティック増殖速度・per dt）。</summary>
        public readonly float selfReinforceRate;
        /// <summary>制度強度1での連鎖抑制の最大幅（法治がどれだけ増殖を殺せるか）。</summary>
        public readonly float institutionalBrakeScale;
        /// <summary>証拠の質ゼロでの冤罪率の上限（熱狂が高いほど無実が告発される）。</summary>
        public readonly float maxFalseAccusation;
        /// <summary>時間による自然沈静の速度（per dt＝熱狂は冷める）。</summary>
        public readonly float subsidenceRate;

        public AccusationCascadeParams(float blameScale, float selfReinforceRate, float institutionalBrakeScale,
                                       float maxFalseAccusation, float subsidenceRate)
        {
            this.blameScale = Mathf.Clamp01(blameScale);
            this.selfReinforceRate = Mathf.Max(0f, selfReinforceRate);
            this.institutionalBrakeScale = Mathf.Clamp01(institutionalBrakeScale);
            this.maxFalseAccusation = Mathf.Clamp01(maxFalseAccusation);
            this.subsidenceRate = Mathf.Max(0f, subsidenceRate);
        }

        /// <summary>既定＝責任転嫁0.8・自己増殖0.6・制度抑制0.9・冤罪上限0.7・沈静0.1。</summary>
        public static AccusationCascadeParams Default => new AccusationCascadeParams(0.8f, 0.6f, 0.9f, 0.7f, 0.1f);
    }

    /// <summary>
    /// 告発カスケードの純ロジック（MNIA-4 #1625・マッカイ『狂気とバブル』参考）。マニア（熱狂）が崩壊すると、
    /// 人々は損失の責任を誰かに転嫁する＝スケープゴート探し。そして一人を吊るすと損失は消えないので
    /// **次の標的が要る**＝告発が告発を呼ぶ自己増殖（ロジスティック）。これを止められるのは強い制度
    /// （法の支配・適正手続き）だけ＝弱い制度ほど魔女狩りへ走り、冤罪が膨らむ。
    /// 政策としての粛清（<see cref="PurgeRules"/>＝上からの排除）・恐怖の媒体増幅（<see cref="TerrorRules"/>）・
    /// 個人の醜聞（<see cref="ScandalRules"/>）とは別系統＝マニア崩壊後の社会的責任転嫁の連鎖そのもの。
    /// 乱数は roll 引数で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AccusationCascadeRules
    {
        /// <summary>
        /// スケープゴート圧力（0..1）＝損失の大きさ lossSeverity(0..1)×責任転嫁傾向 blameDirection(0..1)×係数。
        /// 大きな損失ほど、また誰かのせいにしたがる社会ほど、告発の初期圧力が高い。
        /// </summary>
        public static float ScapegoatPressure(float lossSeverity, float blameDirection, AccusationCascadeParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(lossSeverity) * Mathf.Clamp01(blameDirection) * p.blameScale);
        }

        public static float ScapegoatPressure(float lossSeverity, float blameDirection)
            => ScapegoatPressure(lossSeverity, blameDirection, AccusationCascadeParams.Default);

        /// <summary>
        /// 制度による抑制倍率（0..1）＝1−制度強度×抑制幅。強い法治（institutionalStrength→1）ほど
        /// 増殖を強く殺す（倍率→1−brakeScale）。「制度の強さだけが連鎖を止める」の核。
        /// </summary>
        public static float InstitutionalBrake(float institutionalStrength, AccusationCascadeParams p)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(institutionalStrength) * p.institutionalBrakeScale);
        }

        public static float InstitutionalBrake(float institutionalStrength)
            => InstitutionalBrake(institutionalStrength, AccusationCascadeParams.Default);

        /// <summary>
        /// 告発強度の1tick後（0..1）。ロジスティック自己増殖＝強度×(1−強度) に比例して一件が次を呼ぶ
        /// （余地があるほど勢いづく）が、その増殖ぶんを制度抑制倍率（<see cref="InstitutionalBrake"/>）で減衰
        /// ＝強い法治は連鎖を止める。さらに時間で自然沈静（<see cref="Subsidence"/>）を差し引く。
        /// </summary>
        public static float CascadeTick(float intensity, float institutionalStrength, float dt, AccusationCascadeParams p)
        {
            float i = Mathf.Clamp01(intensity);
            float d = Mathf.Max(0f, dt);
            float brake = InstitutionalBrake(institutionalStrength, p);          // 制度が増殖を殺す倍率
            float growth = p.selfReinforceRate * i * (1f - i) * brake * d;       // 一件が次を呼ぶ（ロジスティック）
            float decay = p.subsidenceRate * i * d;                              // 熱狂は冷める
            return Mathf.Clamp01(i + growth - decay);
        }

        public static float CascadeTick(float intensity, float institutionalStrength, float dt)
            => CascadeTick(intensity, institutionalStrength, dt, AccusationCascadeParams.Default);

        /// <summary>
        /// 次の標的が現れるか（決定論 roll∈[0,1)）＝告発強度が roll を上回れば新たなスケープゴートが立つ。
        /// 強度が高いほど（吊るしても損失が消えないほど）次の標的が要る。
        /// </summary>
        public static bool NextTargetEmergence(float intensity, float roll)
        {
            return Mathf.Clamp01(intensity) > Mathf.Clamp01(roll);
        }

        /// <summary>
        /// 冤罪比率（0..maxFalseAccusation）＝告発強度×(1−証拠の質 evidenceQuality(0..1))×上限。
        /// 熱狂が高く証拠を問わないほど無実が告発される＝魔女狩りの本質。
        /// </summary>
        public static float FalseAccusationRatio(float intensity, float evidenceQuality, AccusationCascadeParams p)
        {
            return Mathf.Clamp01(intensity) * (1f - Mathf.Clamp01(evidenceQuality)) * p.maxFalseAccusation;
        }

        public static float FalseAccusationRatio(float intensity, float evidenceQuality)
            => FalseAccusationRatio(intensity, evidenceQuality, AccusationCascadeParams.Default);

        /// <summary>魔女狩り化したか＝告発強度が閾値 threshold(0..1) を超えた状態（証拠より熱狂が支配する）。</summary>
        public static bool IsWitchHunt(float intensity, float threshold)
        {
            return Mathf.Clamp01(intensity) > Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// 時間による沈静後の告発強度（0..1）＝強度から沈静ぶんを差し引く（新たな増殖入力なしの自然減衰）。
        /// 熱狂は放っておけば冷める＝制度が連鎖を断てば、あとは時間が片付ける。
        /// </summary>
        public static float Subsidence(float intensity, float dt, AccusationCascadeParams p)
        {
            float i = Mathf.Clamp01(intensity);
            float d = Mathf.Max(0f, dt);
            return Mathf.Clamp01(i - p.subsidenceRate * i * d);
        }

        public static float Subsidence(float intensity, float dt)
            => Subsidence(intensity, dt, AccusationCascadeParams.Default);
    }
}
