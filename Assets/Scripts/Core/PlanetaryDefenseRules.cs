using UnityEngine;

namespace Ginei
{
    /// <summary>惑星防衛の層（外層→内層の順）。攻撃側は外層から順に抜かねば惑星に届かない。</summary>
    public enum DefenseLayer
    {
        /// <summary>最外層＝防衛艦隊（軌道外で迎撃）。</summary>
        防衛艦隊,
        /// <summary>中間層＝防衛衛星（軌道上の固定砲台・機雷網）。</summary>
        防衛衛星,
        /// <summary>最内層＝軌道部隊（地表直上の最終防衛）。</summary>
        軌道部隊,
    }

    /// <summary>惑星防衛3層の調整係数（#1070・層別迎撃の数値モデル）。</summary>
    public readonly struct PlanetaryDefenseParams
    {
        /// <summary>外層ほど迎撃が効きやすい係数（防衛艦隊＝最外層・広く構える）。</summary>
        public readonly float fleetInterceptScale;
        /// <summary>防衛衛星の迎撃係数（固定砲台＝堅いが射程が狭い）。</summary>
        public readonly float satelliteInterceptScale;
        /// <summary>軌道部隊の迎撃係数（最終防衛・最も近接）。</summary>
        public readonly float orbitalInterceptScale;
        /// <summary>1層あたりの迎撃が攻撃力を削れる上限割合（0..1）。1層で全滅はしない。</summary>
        public readonly float maxLayerAttrition;
        /// <summary>層の相乗強度（3層が揃うと単独の和を相乗ぶん超える＝重層防御の妙）。</summary>
        public readonly float synergyFactor;
        /// <summary>健在な層1つあたりの防御縦深ボーナス（時間を稼ぎ援軍を待てる）。</summary>
        public readonly float depthBonusPerLayer;

        public PlanetaryDefenseParams(float fleetInterceptScale, float satelliteInterceptScale,
                                      float orbitalInterceptScale, float maxLayerAttrition,
                                      float synergyFactor, float depthBonusPerLayer)
        {
            this.fleetInterceptScale = Mathf.Max(0f, fleetInterceptScale);
            this.satelliteInterceptScale = Mathf.Max(0f, satelliteInterceptScale);
            this.orbitalInterceptScale = Mathf.Max(0f, orbitalInterceptScale);
            this.maxLayerAttrition = Mathf.Clamp01(maxLayerAttrition);
            this.synergyFactor = Mathf.Max(0f, synergyFactor);
            this.depthBonusPerLayer = Mathf.Max(0f, depthBonusPerLayer);
        }

        /// <summary>
        /// 既定＝艦隊迎撃1.0・衛星迎撃0.8・軌道部隊迎撃0.6・1層減衰上限0.7・相乗0.25・縦深ボーナス0.2。
        /// 外層ほど迎撃が効き（広く構える）、内層は近接で堅いが射程が狭い＝係数を下げる。
        /// </summary>
        public static PlanetaryDefenseParams Default
            => new PlanetaryDefenseParams(1f, 0.8f, 0.6f, 0.7f, 0.25f, 0.2f);
    }

    /// <summary>
    /// 惑星防衛3層の純ロジック（#1070 Almagest・防衛艦隊／防衛衛星／軌道部隊の層別迎撃）。
    /// 惑星は3層で守る＝攻撃は各層を抜くたびに痩せ（外層から順に削られる）、層の相乗が単独の和を超える
    /// （艦隊が衛星を守り衛星が軌道部隊を支える＝重層防御の妙）。最弱の層が突破口＝攻撃側が狙う穴になる。
    /// こちらは攻城の<b>防御側＝層別の迎撃</b>を担い、占領そのものの解決（制空権抑制→侵略→占領）は
    /// <see cref="PlanetSiegeRules"/>（攻城）が、単一要塞の点防御（シールド・主砲・難攻不落）は
    /// <see cref="FortressRules"/>（要塞）が、複数陣地を束ねた線と縦深は <see cref="DefenseLineRules"/>（縦深防御）が担う（別系統）。
    /// 制空権争い（<see cref="OrbitalSupremacyContest"/>）は PlanetSiegeRules の orbitalDefense へ接続する。
    /// 乱数なし・決定論（判定は呼び出し側 roll）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PlanetaryDefenseRules
    {
        /// <summary>層ごとの迎撃係数を返す（外層ほど大きい）。</summary>
        public static float LayerScale(DefenseLayer layer, PlanetaryDefenseParams p)
        {
            switch (layer)
            {
                case DefenseLayer.防衛艦隊: return p.fleetInterceptScale;
                case DefenseLayer.防衛衛星: return p.satelliteInterceptScale;
                default:                  return p.orbitalInterceptScale; // 軌道部隊
            }
        }

        /// <summary>
        /// 各層の迎撃で減らす攻撃力（≧0）。層の戦力と攻撃力の比に層係数を掛け、攻撃力の maxLayerAttrition 倍を
        /// 上限とする＝1層では攻撃を全滅できず（外層を抜くたびに攻撃側が痩せる）、強い層ほど多く削る。
        /// </summary>
        public static float LayerInterception(DefenseLayer layer, float layerStrength, float attackerStrength,
                                              PlanetaryDefenseParams p)
        {
            float attack = Mathf.Max(0f, attackerStrength);
            if (attack <= 0f) return 0f;
            float strength = Mathf.Max(0f, layerStrength);
            float intercept = strength * LayerScale(layer, p);
            float cap = attack * p.maxLayerAttrition;
            return Mathf.Clamp(intercept, 0f, cap);
        }

        public static float LayerInterception(DefenseLayer layer, float layerStrength, float attackerStrength)
            => LayerInterception(layer, layerStrength, attackerStrength, PlanetaryDefenseParams.Default);

        /// <summary>
        /// 3層を抜けて惑星に届く残存攻撃力（≧0）。layerStrengths は外層→内層の順（[0]=防衛艦隊…）。
        /// 外層から順に <see cref="LayerInterception"/> で削り、痩せた攻撃力を次の層へ渡す＝
        /// 各層を抜くたびに攻撃側が痩せる。配列が短ければ在る層のみで処理（手書きループ・enum 添字対応）。
        /// </summary>
        public static float PenetratingForce(float attackerStrength, float[] layerStrengths, PlanetaryDefenseParams p)
        {
            float remaining = Mathf.Max(0f, attackerStrength);
            if (layerStrengths == null) return remaining;
            for (int i = 0; i < layerStrengths.Length; i++)
            {
                if (remaining <= 0f) return 0f;
                DefenseLayer layer = (i < 3) ? (DefenseLayer)i : DefenseLayer.軌道部隊;
                float lost = LayerInterception(layer, layerStrengths[i], remaining, p);
                remaining = Mathf.Max(0f, remaining - lost);
            }
            return remaining;
        }

        public static float PenetratingForce(float attackerStrength, float[] layerStrengths)
            => PenetratingForce(attackerStrength, layerStrengths, PlanetaryDefenseParams.Default);

        /// <summary>
        /// 層の相乗を含んだ防御総合強度。3層の単純な和に、揃った層どうしの相互支援ぶん（synergyFactor×
        /// 健在層数に応じた幾何平均）を上乗せ＝単独の和を超える（艦隊が衛星を守り衛星が軌道部隊を支える）。
        /// 1層だけなら相乗は無く和そのもの。配列 null/空は0。
        /// </summary>
        public static float LayerSynergy(float[] layerStrengths, PlanetaryDefenseParams p)
        {
            if (layerStrengths == null || layerStrengths.Length == 0) return 0f;
            float sum = 0f;
            float product = 1f;
            int intact = 0;
            for (int i = 0; i < layerStrengths.Length; i++)
            {
                float s = Mathf.Max(0f, layerStrengths[i]);
                sum += s;
                if (s > 0f) { product *= s; intact++; }
            }
            if (intact < 2) return sum; // 単独では相乗なし
            // 健在層の幾何平均を相乗の基礎に（揃うほど効く＝弱い層が足を引っ張る）。
            float geoMean = Mathf.Pow(product, 1f / intact);
            float bonus = p.synergyFactor * geoMean * (intact - 1);
            return sum + bonus;
        }

        public static float LayerSynergy(float[] layerStrengths)
            => LayerSynergy(layerStrengths, PlanetaryDefenseParams.Default);

        /// <summary>
        /// 最弱の層の添字（突破口＝攻撃側が狙う穴）。layerStrengths が外層→内層の順。
        /// 同値は外層を優先（小さい添字）。配列 null/空は -1。
        /// </summary>
        public static int WeakestLayer(float[] layerStrengths)
        {
            if (layerStrengths == null || layerStrengths.Length == 0) return -1;
            int weakest = 0;
            float min = Mathf.Max(0f, layerStrengths[0]);
            for (int i = 1; i < layerStrengths.Length; i++)
            {
                float s = Mathf.Max(0f, layerStrengths[i]);
                if (s < min) { min = s; weakest = i; }
            }
            return weakest;
        }

        /// <summary>
        /// 防御縦深ボーナス（≧1）。健在な層が多いほど時間を稼ぐ＝援軍を待てる（1層あたり depthBonusPerLayer）。
        /// 0層なら1.0（ボーナス無し＝裸の惑星）。intactLayers は負はクランプ。
        /// </summary>
        public static float DefenseDepthBonus(int intactLayers, PlanetaryDefenseParams p)
        {
            int layers = Mathf.Max(0, intactLayers);
            return 1f + p.depthBonusPerLayer * layers;
        }

        public static float DefenseDepthBonus(int intactLayers)
            => DefenseDepthBonus(intactLayers, PlanetaryDefenseParams.Default);

        /// <summary>
        /// 制空権争い（0..1＝守備側の優勢度）＝軌道を制した側が地上を支配する。
        /// 守備側の軌道戦力 ÷ (守備+攻撃) ＝戦力比そのまま（0.5で拮抗・1で守備が完全制圧）。
        /// <see cref="PlanetSiegeRules"/> の orbitalDefense（制空権抑制）へ接続する想定の指標。
        /// 双方0は係争なし＝守備優勢1.0（攻撃が来なければ軌道は守備のもの）。
        /// </summary>
        public static float OrbitalSupremacyContest(float defenderOrbital, float attackerOrbital)
        {
            float def = Mathf.Max(0f, defenderOrbital);
            float atk = Mathf.Max(0f, attackerOrbital);
            float total = def + atk;
            if (total <= 0f) return 1f;
            return Mathf.Clamp01(def / total);
        }
    }
}
