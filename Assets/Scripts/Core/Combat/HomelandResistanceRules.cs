using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 侵攻深度の純データ（ロシア戦役型＝縦深抵抗の中核状態）。侵攻軍が敵国の奥地へ
    /// どこまで入り込んでいるか・補給線がどれだけ伸びたか・占領地がどれだけ敵意を抱くか。
    /// </summary>
    public struct InvasionDepthState
    {
        /// <summary>侵攻深度（0..1・敵国の奥地へどこまで進んだか。1=最深部）。</summary>
        public float penetrationDepth;
        /// <summary>補給線の長さ（0..1・本国から前線までどれだけ伸びたか。1=極限まで伸びきり）。</summary>
        public float supplyLineLength;
        /// <summary>占領地の敵意（0..1・住民の反抗心。時間とともにパルチザン・反乱へ組織化する）。</summary>
        public float occupiedHostility;

        public InvasionDepthState(float penetrationDepth, float supplyLineLength, float occupiedHostility)
        {
            this.penetrationDepth = Mathf.Clamp01(penetrationDepth);
            this.supplyLineLength = Mathf.Clamp01(supplyLineLength);
            this.occupiedHostility = Mathf.Clamp01(occupiedHostility);
        }
    }

    /// <summary>縦深抵抗の調整係数。</summary>
    public readonly struct HomelandResistanceParams
    {
        /// <summary>補給線負担の非線形度（侵攻深度の冪指数・1以上）。奥へ進むほど加速して補給が細る。</summary>
        public readonly float supplyStrainExponent;
        /// <summary>地形の険しさが補給負担を増幅する重み（0..1・荒野・極寒ほど補給を阻む）。</summary>
        public readonly float terrainStrainWeight;
        /// <summary>抵抗増幅の非線形度（侵攻深度の冪指数・1以上）。深く入るほど住民の抵抗が加速して増える。</summary>
        public readonly float resistanceExponent;
        /// <summary>祖国防衛の意志が抵抗を増幅する重み（0..1・民族の抗戦心ほどパルチザンが燃える）。</summary>
        public readonly float defianceWeight;
        /// <summary>パルチザンの組織化速度（per dt・占領地の敵意が時間で武装抵抗へ育つ率）。</summary>
        public readonly float partisanGrowthRate;
        /// <summary>外部支援がパルチザンを後押しする重み（0..1・武器援助ほど抵抗が持続する）。</summary>
        public readonly float externalSupportWeight;
        /// <summary>侵攻継続コストの非線形度（深度の冪指数・1以上）。深追いほど代償が加速して重くなる。</summary>
        public readonly float advanceCostExponent;
        /// <summary>過伸張ペナルティの非線形度（持続可能深度の超過分の冪指数・1以上）。越えるほど一気に不利。</summary>
        public readonly float overreachExponent;
        /// <summary>過伸張ペナルティの最大値（0..1・補給破綻＝攻勢終末点の上限）。</summary>
        public readonly float maxOverreachPenalty;

        public HomelandResistanceParams(float supplyStrainExponent, float terrainStrainWeight,
            float resistanceExponent, float defianceWeight, float partisanGrowthRate,
            float externalSupportWeight, float advanceCostExponent,
            float overreachExponent, float maxOverreachPenalty)
        {
            this.supplyStrainExponent = Mathf.Max(1f, supplyStrainExponent);
            this.terrainStrainWeight = Mathf.Clamp01(terrainStrainWeight);
            this.resistanceExponent = Mathf.Max(1f, resistanceExponent);
            this.defianceWeight = Mathf.Clamp01(defianceWeight);
            this.partisanGrowthRate = Mathf.Max(0f, partisanGrowthRate);
            this.externalSupportWeight = Mathf.Clamp01(externalSupportWeight);
            this.advanceCostExponent = Mathf.Max(1f, advanceCostExponent);
            this.overreachExponent = Mathf.Max(1f, overreachExponent);
            this.maxOverreachPenalty = Mathf.Clamp01(maxOverreachPenalty);
        }

        /// <summary>既定＝補給冪1.5・地形重み0.5・抵抗冪2・抗戦重み0.6・パルチザン率0.1・外部支援重み0.4・侵攻コスト冪2・過伸張冪2・過伸張上限0.9。</summary>
        public static HomelandResistanceParams Default =>
            new HomelandResistanceParams(1.5f, 0.5f, 2f, 0.6f, 0.1f, 0.4f, 2f, 2f, 0.9f);
    }

    /// <summary>
    /// 縦深抵抗の純ロジック（ロシア戦役型・#1413・革命戦争／ナポレオン・ヒトラーのロシア侵攻）。
    /// 侵攻軍が敵国の奥深くへ進むほど、補給線が伸びてコストが非線形に増し、占領地の住民の抵抗
    /// （パルチザン・焦土・反乱）が自動的に増幅する＝縦深のある国土は敵を呑み込んで疲弊させる。
    /// 「深く入るほど不利になる」を式に出す。
    /// <see cref="OverextensionRules"/>（版図と国力の比＝国家規模の恒常的な過伸張負担）とは別系統＝
    /// こちらは1侵攻の深度による補給難と住民抵抗の増幅（ロシア戦役型の縦深）。
    /// InsurgencyRules（占領地反乱＝別EPIC SPW・反乱勢力の蜂起そのもの）とも別＝こちらは
    /// 侵攻深度が抵抗を増幅する関係を扱い、PartisanPressure で反乱側へ接続する。
    /// <see cref="CulminatingPointRules"/>（攻勢終末点＝1作戦の作戦距離による戦力減衰）とは
    /// InvasionCulmination で接続するが、こちらは深度・補給難・抵抗の三者で頓挫を判定する。
    /// <see cref="SupplyRules"/>（補給線が回廊で繋がるか＝面の到達）とも別＝こちらは深度による負担の逓増。
    /// 倍率は各係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HomelandResistanceRules
    {
        /// <summary>
        /// 補給線の負担（0..1）。侵攻が深いほど補給線が伸びて負担が増す＝奥地ほど補給が届きにくい。
        /// 侵攻深度を冪で非線形に効かせ、地形の険しさ（荒野・極寒）が（1＋険しさ×地形重み）で増幅する。
        /// 深度0なら負担0（本国直上）、最深部・険しい地形ほど1へ近づく。
        /// </summary>
        public static float SupplyLineStrain(float penetrationDepth, float terrainHostility,
            HomelandResistanceParams p)
        {
            float depth = Mathf.Clamp01(penetrationDepth);
            float terrain = Mathf.Clamp01(terrainHostility);
            float baseStrain = Mathf.Pow(depth, p.supplyStrainExponent);
            float amplified = baseStrain * (1f + terrain * p.terrainStrainWeight);
            return Mathf.Clamp01(amplified);
        }

        public static float SupplyLineStrain(float penetrationDepth, float terrainHostility)
            => SupplyLineStrain(penetrationDepth, terrainHostility, HomelandResistanceParams.Default);

        /// <summary>
        /// 抵抗の増幅（0..1）。深く入るほど住民の抵抗（パルチザン・焦土・反乱）が増幅する＝祖国防衛の意志。
        /// 侵攻深度を冪で非線形に効かせ、民族の抗戦心（0..1）が（1＋抗戦心×抗戦重み）で増幅する。
        /// 深度0なら抵抗なし、最深部・高い抗戦心ほど1へ近づく＝侵攻軍を呑み込む縦深抵抗。
        /// </summary>
        public static float ResistanceAmplification(float penetrationDepth, float nationalDefianceSpirit,
            HomelandResistanceParams p)
        {
            float depth = Mathf.Clamp01(penetrationDepth);
            float defiance = Mathf.Clamp01(nationalDefianceSpirit);
            float baseResistance = Mathf.Pow(depth, p.resistanceExponent);
            float amplified = baseResistance * (1f + defiance * p.defianceWeight);
            return Mathf.Clamp01(amplified);
        }

        public static float ResistanceAmplification(float penetrationDepth, float nationalDefianceSpirit)
            => ResistanceAmplification(penetrationDepth, nationalDefianceSpirit, HomelandResistanceParams.Default);

        /// <summary>
        /// パルチザン圧力（1tick後の占領地敵意 0..1）。占領地のパルチザンが時間で組織化し補給線を脅かす。
        /// 既存の敵意×組織化速度×(1＋外部支援×外部支援重み)×dt ぶん増える＝放置するほど武装抵抗が育つ。
        /// 敵意が高いほど自己増殖が速く、外部支援（武器援助）が持続を後押しする。InsurgencyRules へ接続。
        /// </summary>
        public static float PartisanPressure(float occupiedHostility, float externalSupport, float dt,
            HomelandResistanceParams p)
        {
            float hostility = Mathf.Clamp01(occupiedHostility);
            float support = Mathf.Clamp01(externalSupport);
            float t = Mathf.Max(0f, dt);
            float growth = p.partisanGrowthRate * hostility * (1f + support * p.externalSupportWeight) * t;
            return Mathf.Clamp01(hostility + growth);
        }

        public static float PartisanPressure(float occupiedHostility, float externalSupport, float dt)
            => PartisanPressure(occupiedHostility, externalSupport, dt, HomelandResistanceParams.Default);

        /// <summary>
        /// 侵攻継続コスト（0..1・深追いの代償）。侵攻を続けるコストが深度とともに非線形に増す。
        /// 侵攻深度を冪で効かせ、補給線の負担（0..1）が（1＋負担）で乗算して増幅する＝補給が細るほど
        /// 同じ前進が高くつく。深度0なら0、最深部＋補給難ほど1へ近づく。
        /// </summary>
        public static float DepthAdvanceCost(float penetrationDepth, float supplyLineStrain,
            HomelandResistanceParams p)
        {
            float depth = Mathf.Clamp01(penetrationDepth);
            float strain = Mathf.Clamp01(supplyLineStrain);
            float baseCost = Mathf.Pow(depth, p.advanceCostExponent);
            float amplified = baseCost * (1f + strain);
            return Mathf.Clamp01(amplified);
        }

        public static float DepthAdvanceCost(float penetrationDepth, float supplyLineStrain)
            => DepthAdvanceCost(penetrationDepth, supplyLineStrain, HomelandResistanceParams.Default);

        /// <summary>
        /// 過伸張ペナルティ（0..maxOverreachPenalty）。持続可能な深度を超えると一気に不利になる＝
        /// 補給が破綻＝攻勢終末点。持続可能深度（0..1・自軍が無理なく保てる深さ）までは0、超えた分を
        /// 冪で非線形に効かせる。係数に（1−これ）を掛けて使う。
        /// </summary>
        public static float OverreachPenalty(float penetrationDepth, float sustainableDepth,
            HomelandResistanceParams p)
        {
            float depth = Mathf.Clamp01(penetrationDepth);
            float sustainable = Mathf.Clamp01(sustainableDepth);
            if (depth <= sustainable) return 0f;
            float excess = depth - sustainable; // 持続可能な深度を超えた分だけが効く
            // 残りの伸びしろ（1−sustainable）で正規化＝どれだけ無理をしたかの相対量
            float headroom = Mathf.Max(0.0001f, 1f - sustainable);
            float norm = excess / headroom;
            float raw = Mathf.Pow(norm, p.overreachExponent);
            return Mathf.Min(p.maxOverreachPenalty, raw);
        }

        public static float OverreachPenalty(float penetrationDepth, float sustainableDepth)
            => OverreachPenalty(penetrationDepth, sustainableDepth, HomelandResistanceParams.Default);

        /// <summary>
        /// 防御側の縦深の利（0..1）。防御側が広大な国土を持ち空間を譲る覚悟があるほど侵攻軍を呑み込める＝
        /// 縦深防御。国土の広さ（0..1）×空間を譲る覚悟（0..1）＝広い国土でも焦土・後退の覚悟がなければ
        /// 縦深は活きない（両方が要る積）。ロシア戦役で侵攻軍を疲弊させた縦深の本体。
        /// </summary>
        public static float DefenderDepthAdvantage(float defenderTerritory, float willingToTradeSpace)
        {
            float territory = Mathf.Clamp01(defenderTerritory);
            float willing = Mathf.Clamp01(willingToTradeSpace);
            return Mathf.Clamp01(territory * willing);
        }

        /// <summary>
        /// 侵攻の頓挫度（0..1）。侵攻が深度・補給難・抵抗で限界に達し攻勢が頓挫する＝深く入るほど不利。
        /// 侵攻深度・補給線の負担・抵抗の増幅の三者を掛け合わせ、どれか一つでも低ければ頓挫しない
        /// （深く入っても補給が保ち抵抗が弱ければ呑まれない＝積）。三拍子揃うと攻勢が泥沼で停止する。
        /// CulminatingPointRules（攻勢終末点）へ接続する縦深版の限界判定。
        /// </summary>
        public static float InvasionCulmination(float penetrationDepth, float supplyLineStrain,
            float resistanceAmplification)
        {
            float depth = Mathf.Clamp01(penetrationDepth);
            float strain = Mathf.Clamp01(supplyLineStrain);
            float resistance = Mathf.Clamp01(resistanceAmplification);
            return Mathf.Clamp01(depth * strain * resistance);
        }

        /// <summary>
        /// 敵国の奥地で泥沼にはまったか＝侵攻深度×抵抗の増幅が閾値を超えたら true。深く入り込み、
        /// なお住民の抵抗が増幅した状態＝侵攻軍が縦深に呑み込まれて身動きが取れない（ロシア戦役の泥沼）。
        /// </summary>
        public static bool IsBoggedDownInHomeland(float penetrationDepth, float resistanceAmplification,
            float threshold = 0.4f)
        {
            float depth = Mathf.Clamp01(penetrationDepth);
            float resistance = Mathf.Clamp01(resistanceAmplification);
            float thr = Mathf.Clamp01(threshold);
            return depth * resistance >= thr;
        }
    }
}
