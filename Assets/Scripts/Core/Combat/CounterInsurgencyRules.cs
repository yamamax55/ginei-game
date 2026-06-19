using UnityEngine;

namespace Ginei
{
    /// <summary>対反乱（COIN作戦＝住民隔離・諜報・支持遮断）の調整係数。</summary>
    public readonly struct CounterInsurgencyParams
    {
        /// <summary>検問密度が住民管理に効く重み（0..1）。</summary>
        public readonly float checkpointWeight;
        /// <summary>住民登録/地形が住民管理に効く重み（0..1）。</summary>
        public readonly float registrationWeight;
        /// <summary>住民管理×密告網で得る諜報浸透の重み（0..1）。</summary>
        public readonly float intelWeight;
        /// <summary>諜報×急襲能力で細胞を摘発する重み（0..1）。</summary>
        public readonly float disruptionWeight;
        /// <summary>住民管理×諜報で支持基盤を遮断する重み（0..1）。</summary>
        public readonly float severanceWeight;
        /// <summary>過剰武力×住民被害が住民を疎外する重み（0..1・COINの逆効果の強さ）。</summary>
        public readonly float alienationWeight;
        /// <summary>治安×開発×支持遮断で正統性を回復する重み（0..1）。</summary>
        public readonly float legitimacyWeight;
        /// <summary>COIN総合効果で摘発が押し上げる重み（0..1）。</summary>
        public readonly float effectGainWeight;
        /// <summary>COIN総合効果で疎外が押し下げる重み（0..1）。</summary>
        public readonly float effectPenaltyWeight;
        /// <summary>反乱封じ込めの既定閾値（-1..1・これ以上で封じ込め成功）。</summary>
        public readonly float containThreshold;

        public CounterInsurgencyParams(float checkpointWeight, float registrationWeight, float intelWeight,
            float disruptionWeight, float severanceWeight, float alienationWeight,
            float legitimacyWeight, float effectGainWeight, float effectPenaltyWeight, float containThreshold)
        {
            this.checkpointWeight = Mathf.Clamp01(checkpointWeight);
            this.registrationWeight = Mathf.Clamp01(registrationWeight);
            this.intelWeight = Mathf.Clamp01(intelWeight);
            this.disruptionWeight = Mathf.Clamp01(disruptionWeight);
            this.severanceWeight = Mathf.Clamp01(severanceWeight);
            this.alienationWeight = Mathf.Clamp01(alienationWeight);
            this.legitimacyWeight = Mathf.Clamp01(legitimacyWeight);
            this.effectGainWeight = Mathf.Clamp01(effectGainWeight);
            this.effectPenaltyWeight = Mathf.Clamp01(effectPenaltyWeight);
            this.containThreshold = Mathf.Clamp(containThreshold, -1f, 1f);
        }

        /// <summary>
        /// 既定＝検問重み0.6・登録重み0.4・諜報重み0.9・摘発重み0.9・遮断重み0.8・
        /// 疎外重み0.7・正統性重み0.8・効果加点1.0・効果減点1.0・封じ込め閾値0.5。
        /// </summary>
        public static CounterInsurgencyParams Default =>
            new CounterInsurgencyParams(0.6f, 0.4f, 0.9f, 0.9f, 0.8f, 0.7f, 0.8f, 1.0f, 1.0f, 0.5f);
    }

    /// <summary>
    /// 対反乱（COIN作戦＝住民隔離・諜報・支持遮断）の純ロジック。反乱を鎮圧する側のドクトリンを扱う。
    /// 住民と反乱勢力の分離（清郷）、諜報による細胞摘発、反乱の支持基盤の遮断、過剰武力の逆効果、防護と開発による正統性回復。
    /// <para>
    /// 分担：<see cref="PacificationRules"/>（平定の総合進捗＝掃討×民心）が消費する諜報・分離の下位部品＝住民管理に特化。
    /// <see cref="InsurgencyRules"/> は湧く側を扱う。本クラスは「魚と水を断つ」住民隔離と細胞摘発の解像度を担う。
    /// 式の核：住民管理＝検問×重み＋登録/地形×重み／諜報浸透＝住民管理×密告網／細胞摘発＝諜報×急襲／
    /// 支持遮断＝住民管理×諜報／住民疎外＝武力×住民被害／正統性回復＝治安×開発×遮断／
    /// COIN効果＝摘発と正統性で上げ疎外で下げる（-1..1）。
    /// </para>
    /// 全入力クランプ・乱数なし決定論・実効値パターン。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CounterInsurgencyRules
    {
        /// <summary>
        /// 住民管理度（0..1）＝検問密度 checkpointDensity × 重み ＋ 住民登録/地形の積 × 重み。
        /// 登録 registration が進み、開けた地形（terrain が低い＝隠れ場が少ない）ほど住民を掌握できる。
        /// 地形が険しい（terrain 高）と登録の効きが目減りする＝住民を反乱から隔離しにくい。
        /// </summary>
        public static float PopulationControl(float checkpointDensity, float registration, float terrain, CounterInsurgencyParams p)
        {
            float check = Mathf.Clamp01(checkpointDensity) * p.checkpointWeight;
            float openness = 1f - Mathf.Clamp01(terrain);
            float reg = Mathf.Clamp01(registration) * openness * p.registrationWeight;
            return Mathf.Clamp01(check + reg);
        }

        public static float PopulationControl(float checkpointDensity, float registration, float terrain)
            => PopulationControl(checkpointDensity, registration, terrain, CounterInsurgencyParams.Default);

        /// <summary>
        /// 諜報浸透（0..1）＝住民管理 populationControl × 密告網 informantNetwork × 重み。
        /// 住民を掌握し密告網が密なほど、反乱細胞の内側に諜報が届く（どちらか0なら浸透しない）。
        /// </summary>
        public static float IntelPenetration(float populationControl, float informantNetwork, CounterInsurgencyParams p)
        {
            float t = Mathf.Clamp01(populationControl) * Mathf.Clamp01(informantNetwork);
            return Mathf.Clamp01(t * p.intelWeight);
        }

        public static float IntelPenetration(float populationControl, float informantNetwork)
            => IntelPenetration(populationControl, informantNetwork, CounterInsurgencyParams.Default);

        /// <summary>
        /// 細胞摘発（0..1）＝諜報浸透 intelPenetration × 急襲能力 raidCapacity × 重み。
        /// 浸透した諜報を急襲につなげて反乱細胞を潰す＝諜報だけでも実行部隊だけでも摘発は進まない。
        /// </summary>
        public static float CellDisruption(float intelPenetration, float raidCapacity, CounterInsurgencyParams p)
        {
            float t = Mathf.Clamp01(intelPenetration) * Mathf.Clamp01(raidCapacity);
            return Mathf.Clamp01(t * p.disruptionWeight);
        }

        public static float CellDisruption(float intelPenetration, float raidCapacity)
            => CellDisruption(intelPenetration, raidCapacity, CounterInsurgencyParams.Default);

        /// <summary>
        /// 支持基盤の遮断（0..1）＝住民管理 populationControl × 諜報浸透 intelPenetration × 重み。
        /// 住民を掌握し諜報で支援者を割り出すほど、反乱の補給・隠匿・徴募を断つ＝魚から水を奪う。
        /// </summary>
        public static float SupportSeverance(float populationControl, float intelPenetration, CounterInsurgencyParams p)
        {
            float t = Mathf.Clamp01(populationControl) * Mathf.Clamp01(intelPenetration);
            return Mathf.Clamp01(t * p.severanceWeight);
        }

        public static float SupportSeverance(float populationControl, float intelPenetration)
            => SupportSeverance(populationControl, intelPenetration, CounterInsurgencyParams.Default);

        /// <summary>
        /// 住民疎外（0..1）＝武力 forceLevel × 住民被害 civilianHarm × 重み（COINの逆効果）。
        /// 武力を振るうほど、巻き添えが多いほど住民が反乱側へ傾く＝過剰武力が支持を反乱に押しやる。
        /// </summary>
        public static float OverreachAlienation(float forceLevel, float civilianHarm, CounterInsurgencyParams p)
        {
            float t = Mathf.Clamp01(forceLevel) * Mathf.Clamp01(civilianHarm);
            return Mathf.Clamp01(t * p.alienationWeight);
        }

        public static float OverreachAlienation(float forceLevel, float civilianHarm)
            => OverreachAlienation(forceLevel, civilianHarm, CounterInsurgencyParams.Default);

        /// <summary>
        /// 正統性回復（0..1）＝治安 security × 開発 development × 支持遮断 severance の幾何平均 × 重み。
        /// 守って（治安）開発し、反乱の支持基盤を断つ三拍子が揃うほど統治の正統性が戻る
        /// ＝幾何平均ゆえどれか1つでも欠けると回復が崩れる（住民の信頼は総合点）。
        /// </summary>
        public static float LegitimacyRecovery(float security, float development, float severance, CounterInsurgencyParams p)
        {
            float s = Mathf.Clamp01(security);
            float d = Mathf.Clamp01(development);
            float v = Mathf.Clamp01(severance);
            float geo = Mathf.Pow(s * d * v, 1f / 3f);
            return Mathf.Clamp01(geo * p.legitimacyWeight);
        }

        public static float LegitimacyRecovery(float security, float development, float severance)
            => LegitimacyRecovery(security, development, severance, CounterInsurgencyParams.Default);

        /// <summary>
        /// COIN総合効果（-1..1）＝(細胞摘発 + 正統性回復) の平均 × 加点 − 住民疎外 × 減点。
        /// 摘発と正統性で押し上げ、過剰武力の疎外で押し下げる＝清郷・諜報の成果を逆効果が食い合うバランスの帰結。
        /// 正＝COINが優位（反乱が縮む）／負＝疎外が成果を上回り反乱が広がる（逆効果）。
        /// </summary>
        public static float CoinEffectiveness(float cellDisruption, float legitimacyRecovery, float overreachAlienation, CounterInsurgencyParams p)
        {
            float gain = (Mathf.Clamp01(cellDisruption) + Mathf.Clamp01(legitimacyRecovery)) * 0.5f * p.effectGainWeight;
            float penalty = Mathf.Clamp01(overreachAlienation) * p.effectPenaltyWeight;
            return Mathf.Clamp(gain - penalty, -1f, 1f);
        }

        public static float CoinEffectiveness(float cellDisruption, float legitimacyRecovery, float overreachAlienation)
            => CoinEffectiveness(cellDisruption, legitimacyRecovery, overreachAlienation, CounterInsurgencyParams.Default);

        /// <summary>反乱を封じ込めたか＝COIN総合効果 coinEffectiveness が threshold 以上。</summary>
        public static bool IsInsurgencyContained(float coinEffectiveness, float threshold)
            => Mathf.Clamp(coinEffectiveness, -1f, 1f) >= Mathf.Clamp(threshold, -1f, 1f);

        public static bool IsInsurgencyContained(float coinEffectiveness)
            => IsInsurgencyContained(coinEffectiveness, CounterInsurgencyParams.Default.containThreshold);
    }
}
