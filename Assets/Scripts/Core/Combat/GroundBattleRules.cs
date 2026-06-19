using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 地上戦の戦場地形（#131 惑星戦・地上戦の戦闘解決）。守備側に有利が乗る順＝平地＜市街＜山岳＜要塞。
    /// 宇宙会戦の地形（<see cref="TerrainType"/>＝小惑星帯/星雲）とは別系統＝地上戦専用の段（混同しない）。
    /// </summary>
    public enum BattlefieldTerrain { 平地, 要塞, 市街, 山岳 }

    /// <summary>
    /// 地上戦を1回解決した結果（攻撃側が降下→守備隊と一会戦した収支）。呼び出し側が
    /// これを <see cref="Planet.groundGarrison"/>／<see cref="Planet.invasionProgress"/> 等へ適用する（純ロジックは状態を持たない）。
    /// </summary>
    public readonly struct GroundBattleResult
    {
        /// <summary>攻撃側が押し切った（この会戦で守備が崩れた）か。</summary>
        public readonly bool attackerWon;
        /// <summary>攻撃側の損害（名・≧0）。実投入兵力を超えない。</summary>
        public readonly float attackerLosses;
        /// <summary>守備側の損害（名・≧0）。残守備隊を超えない。</summary>
        public readonly float defenderLosses;
        /// <summary>この会戦で進む占領進捗の増分（≧0）。攻撃側優勢ほど大きい。</summary>
        public readonly float captureProgressDelta;

        public GroundBattleResult(bool attackerWon, float attackerLosses, float defenderLosses, float captureProgressDelta)
        {
            this.attackerWon = attackerWon;
            this.attackerLosses = Mathf.Max(0f, attackerLosses);
            this.defenderLosses = Mathf.Max(0f, defenderLosses);
            this.captureProgressDelta = Mathf.Max(0f, captureProgressDelta);
        }
    }

    /// <summary>地上戦の戦闘解決（<see cref="GroundBattleRules"/>）の調整係数。マジックナンバー禁止＝ここへ集約。</summary>
    public readonly struct GroundBattleParams
    {
        /// <summary>会戦1回で失われる戦力の基準率（拮抗時の損害率の目安）。</summary>
        public readonly float baseCasualtyRate;
        /// <summary>占領進捗の正規化基準（名）。攻撃側の実効純優勢 ÷ これ＝占領進捗の増分。1個師団規模が目安。</summary>
        public readonly float captureReference;
        /// <summary>1会戦で進む占領進捗の上限（過大兵力でも青天井にしない）。</summary>
        public readonly float maxCaptureDelta;
        /// <summary>士気が実効戦力へ効く強さ（0=士気無関係／1=士気そのまま倍率）。</summary>
        public readonly float moraleWeight;

        public GroundBattleParams(float baseCasualtyRate, float captureReference, float maxCaptureDelta, float moraleWeight)
        {
            this.baseCasualtyRate = Mathf.Clamp01(baseCasualtyRate);
            this.captureReference = Mathf.Max(1f, captureReference);
            this.maxCaptureDelta = Mathf.Max(0f, maxCaptureDelta);
            this.moraleWeight = Mathf.Clamp01(moraleWeight);
        }

        /// <summary>
        /// 既定係数。基準＝1個師団（<see cref="GroundForceRules"/>）。基準損害率10%・占領上限3・士気の効き0.6。
        /// 攻防比が偏るほど損害が非対称になる（ランチェスター集中＝<see cref="LanchesterRules"/> 委譲）。
        /// </summary>
        public static GroundBattleParams Default =>
            new GroundBattleParams(0.10f, GroundForceRules.ProfileFor(GroundEchelonType.師団).NominalPersonnel, 3f, 0.6f);
    }

    /// <summary>
    /// 地上戦の戦闘解決（#131 惑星戦・地上戦の戦闘解決・test-first）。攻撃側の地上戦力 vs 守備隊を
    /// <b>1会戦</b>として解き、損害＋占領進捗の増分＋勝敗を返す純ロジック。継続消耗の per-tick 供給
    /// （<see cref="GroundInvasionRules"/>）とは別レイヤー＝こちらは「一度ぶつかった会戦の収支」を返す。
    /// <para>設計（実効値パターン・基準非破壊・委譲して二重実装しない）：
    /// ・<b>地形</b>＝守備側の実効戦力に <see cref="DefenderAdvantage"/>（要塞/山岳ほど守備有利）＋築城度を掛ける。
    /// ・<b>複合兵科</b>＝攻撃側の編成バランスを <see cref="CombinedArmsRules"/> へ委譲して攻撃側実効戦力に掛ける。
    /// ・<b>士気</b>＝双方の実効戦力に士気倍率（<see cref="MoraleFactor"/>）を掛ける（崩れた側は頭数どおり戦えない）。
    /// ・<b>集中</b>＝実効戦力比から <see cref="LanchesterRules.ConcentrationFactor"/>（二乗則）で損害を非対称化＝
    ///   局所優勢が損害を減らし劣勢が各個撃破される。</para>
    /// 占領進捗は攻撃側の純優勢に単調（攻め勝つほど速い・守り勝つ間は0）。決定論（roll を渡せば確率も再現可能）。
    /// 盤面非依存・純ロジック（非 MonoBehaviour）。占領の蓄積→閾値判定そのものは <see cref="PlanetSiegeRules"/> が担う。
    /// </summary>
    public static class GroundBattleRules
    {
        // ===== 地形ごとの守備有利倍率（守備側の実効戦力に掛ける。1.0＝有利なし）=====
        /// <summary>平地＝守備有利なし。</summary>
        public const float PlainsAdvantage = 1.0f;
        /// <summary>市街＝市街戦の遮蔽で守備やや有利。</summary>
        public const float UrbanAdvantage = 1.4f;
        /// <summary>山岳＝高地・隘路で守備有利（攻め手が削がれる）。</summary>
        public const float MountainAdvantage = 1.6f;
        /// <summary>要塞＝築城された防衛拠点で守備最強。</summary>
        public const float FortressAdvantage = 2.0f;

        /// <summary>築城度（fortification 0..1）が地形の守備有利を最大どれだけ上乗せするか（要塞化の天井）。</summary>
        public const float FortificationBonus = 0.5f;

        /// <summary>
        /// 地形＋築城度から守備側の実効戦力倍率（≧1）。地形ごとの既定値（平地1.0〜要塞2.0）に
        /// 築城度ぶん（最大 <see cref="FortificationBonus"/>）を上乗せ。守備隊の頭数は変えず倍率で効かせる（実効値パターン）。
        /// </summary>
        public static float DefenderAdvantage(BattlefieldTerrain terrain, float fortification)
        {
            float baseAdv;
            switch (terrain)
            {
                case BattlefieldTerrain.要塞:  baseAdv = FortressAdvantage; break;
                case BattlefieldTerrain.山岳:  baseAdv = MountainAdvantage; break;
                case BattlefieldTerrain.市街:  baseAdv = UrbanAdvantage; break;
                default:                       baseAdv = PlainsAdvantage; break;
            }
            float fort = Mathf.Clamp01(fortification);
            return baseAdv * (1f + fort * FortificationBonus);
        }

        /// <summary>地形のみ（築城度0）の守備有利倍率。</summary>
        public static float DefenderAdvantage(BattlefieldTerrain terrain)
            => DefenderAdvantage(terrain, 0f);

        /// <summary>
        /// 士気（0..1）→ 実効戦力倍率。moraleWeight=0 で常に1.0（士気無関係）、=1 で士気そのまま。
        /// = Lerp(1−moraleWeight, 1, morale)＝士気満で1.0、士気0で (1−moraleWeight)。崩れた側は頭数どおり戦えない。
        /// </summary>
        public static float MoraleFactor(float morale, GroundBattleParams p)
            => Mathf.Lerp(1f - p.moraleWeight, 1f, Mathf.Clamp01(morale));

        public static float MoraleFactor(float morale)
            => MoraleFactor(morale, GroundBattleParams.Default);

        /// <summary>
        /// 攻撃側の複合兵科（歩兵/機甲/砲兵の混成）相乗倍率（minEffectiveness..maxEffectiveness）。
        /// バランスの取れた編成ほど高い＝<see cref="CombinedArmsRules"/> へ委譲（艦種の相乗式をそのまま地上の兵科へ流用＝二重実装しない）。
        /// 3兵科の比率を渡す（合計は内部で正規化・全0は均等扱い）。
        /// </summary>
        public static float CombinedArmsFactor(float infantryRatio, float armorRatio, float artilleryRatio)
        {
            float balance = CombinedArmsRules.Balance(infantryRatio, armorRatio, artilleryRatio);
            float synergy = CombinedArmsRules.SynergyBonus(balance);
            float coverage = CombinedArmsRules.RoleCoverage(infantryRatio, armorRatio, artilleryRatio);
            return CombinedArmsRules.CombinedEffectiveness(synergy, coverage);
        }

        /// <summary>
        /// 地上戦を1会戦解決する（複合兵科＋地形＋士気＋ランチェスター集中）。返り値は呼び出し側が状態へ適用する。
        /// <para>引数：<br/>
        /// attackerStrength＝攻撃側の地上戦力（名）／defenderGarrison＝守備隊（名）／terrain＝戦場地形／
        /// fortification＝築城度(0..1)／attackerMorale・defenderMorale＝双方の士気(0..1)／
        /// attackerArmsFactor＝攻撃側の複合兵科倍率（<see cref="CombinedArmsFactor"/> の値・1.0で中立）／
        /// p＝調整係数／roll＝決定論乱数(0..1 既定 null＝0.5＝中央値で確率なし)。</para>
        /// </summary>
        public static GroundBattleResult Resolve(
            float attackerStrength, float defenderGarrison,
            BattlefieldTerrain terrain, float fortification,
            float attackerMorale, float defenderMorale,
            float attackerArmsFactor,
            GroundBattleParams p, Func<float> roll)
        {
            float atk = Mathf.Max(0f, attackerStrength);
            float def = Mathf.Max(0f, defenderGarrison);

            // 守備隊が居なければ無抵抗占領（攻撃側無傷・最大進捗）。
            if (def <= 0f)
                return new GroundBattleResult(true, 0f, 0f, atk > 0f ? p.maxCaptureDelta : 0f);
            // 攻撃側が居なければ会戦不成立（双方無傷・進捗なし）。
            if (atk <= 0f)
                return new GroundBattleResult(false, 0f, 0f, 0f);

            // 実効戦力＝頭数 × 士気 ×（攻撃側は複合兵科倍率／守備側は地形＋築城度の守備有利）。
            float arms = attackerArmsFactor <= 0f ? 1f : attackerArmsFactor;
            float effAtk = atk * MoraleFactor(attackerMorale, p) * arms;
            float effDef = def * MoraleFactor(defenderMorale, p) * DefenderAdvantage(terrain, fortification);

            // 集中倍率（ランチェスター二乗則・既存窓口へ委譲）。攻側>1で攻撃側が損害を減らし、守側が各個撃破される。
            float atkConc = LanchesterRules.ConcentrationFactor(effAtk, effDef); // 攻撃側の優勢度（>1で有利）
            float defConc = LanchesterRules.ConcentrationFactor(effDef, effAtk); // 守備側の優勢度

            // roll（0..1）で損害を少し揺らす（既定0.5＝中央値＝確率なしの決定論）。0.75..1.25倍。
            float r = roll != null ? Mathf.Clamp01(roll()) : 0.5f;
            float jitter = 0.75f + 0.5f * r;

            // 損害＝基準率 × 自軍の頭数 ÷ 相手から見た集中優勢（優勢側ほど損害が軽い＝二乗則の体感）。
            float atkLossRaw = atk * p.baseCasualtyRate * jitter / Mathf.Max(0.0001f, atkConc);
            float defLossRaw = def * p.baseCasualtyRate * jitter / Mathf.Max(0.0001f, defConc);
            float attackerLosses = Mathf.Min(atk, atkLossRaw);
            float defenderLosses = Mathf.Min(def, defLossRaw);

            // 占領進捗＝攻撃側の実効純優勢 ÷ 基準（守備が上回る間は0＝停滞）を上限でクランプ。
            float netAdvantage = Mathf.Max(0f, effAtk - effDef);
            float captureDelta = Mathf.Clamp(netAdvantage / p.captureReference, 0f, p.maxCaptureDelta);

            // 勝敗＝攻撃側の実効戦力が守備側を上回れば攻め勝ち（純優勢あり＝占領が進む側）。
            bool attackerWon = effAtk > effDef;

            return new GroundBattleResult(attackerWon, attackerLosses, defenderLosses, captureDelta);
        }

        /// <summary>既定係数・決定論（roll 中央値・複合兵科中立 1.0）で1会戦解決する簡易版。</summary>
        public static GroundBattleResult Resolve(
            float attackerStrength, float defenderGarrison,
            BattlefieldTerrain terrain, float attackerMorale, float defenderMorale)
            => Resolve(attackerStrength, defenderGarrison, terrain, 0f,
                       attackerMorale, defenderMorale, 1f, GroundBattleParams.Default, null);
    }
}
