using UnityEngine;

namespace Ginei
{
    /// <summary>地上侵攻（ドメイン・ダウン後の地上戦）の調整係数（#131 惑星戦 PB・二者化）。</summary>
    public readonly struct GroundInvasionParams
    {
        /// <summary>守備隊の損耗速度＝攻撃側地上戦力（名）×これ×dt が1tickの守備隊損失。</summary>
        public readonly float garrisonAttritionRate;
        /// <summary>攻撃側（包囲艦）が守備隊の対空砲火で受ける損害速度＝守備隊（名）×これ＝秒間ダメージ。</summary>
        public readonly float attackerCasualtyRate;
        /// <summary>侵略速度の正規化基準（名）。攻撃側の純優勢 ÷ これ＝侵略速度係数。1個師団規模が目安。</summary>
        public readonly float referenceForce;
        /// <summary>侵略速度係数の上限（過大兵力でも青天井にしない）。</summary>
        public readonly float maxInvadeFactor;

        public GroundInvasionParams(float garrisonAttritionRate, float attackerCasualtyRate,
                                    float referenceForce, float maxInvadeFactor)
        {
            this.garrisonAttritionRate = Mathf.Max(0f, garrisonAttritionRate);
            this.attackerCasualtyRate = Mathf.Max(0f, attackerCasualtyRate);
            this.referenceForce = Mathf.Max(1f, referenceForce);
            this.maxInvadeFactor = Mathf.Max(0f, maxInvadeFactor);
        }

        /// <summary>
        /// 既定係数。基準＝1個師団（<see cref="GroundForceRules"/>）。守備隊損耗 0.014/名・秒、
        /// 対空被害 0.001/名・秒、侵略上限 3。タクティカルに数十秒で決着する目安（作者調整可）。
        /// </summary>
        public static GroundInvasionParams Default =>
            new GroundInvasionParams(0.014f, 0.001f,
                GroundForceRules.ProfileFor(GroundEchelonType.師団).NominalPersonnel, 3f);
    }

    /// <summary>
    /// 地上侵攻（ドメイン・ダウン後の地上戦）の純ロジック（#131 惑星戦・二者化・test-first）。
    /// 制空権を失った（ドメイン・ダウン）惑星へ攻撃側が陸戦隊を降下させ、惑星の<b>守備隊</b>と消耗戦になる。
    /// ・侵略は攻撃側の<b>純優勢</b>（攻撃側地上戦力−守備隊）に比例して進む＝守備隊が上回る間は<b>停滞</b>（占領できない）。
    /// ・守備隊は攻撃側の規模に応じて<b>削られる</b>＝兵力を注げば時間で攻略できる。
    /// ・守備隊は<b>対空砲火</b>で攻撃側（包囲艦）にも損害を与える＝強い守備は艦も傷つける（押し戻す）。
    /// 占領そのものの解決（侵略値の蓄積→閾値）は <see cref="PlanetSiegeRules"/> が担い、本ルールは
    /// 二者の地上消耗と侵略速度係数だけを供給する（重複実装しない）。地上梯団の規模は <see cref="GroundForceRules"/>。
    /// 決定論・乱数なし。純ロジック（非 MonoBehaviour）。
    /// </summary>
    public static class GroundInvasionRules
    {
        /// <summary>
        /// 実効守備隊（名・≧0）＝守備隊×士気（0..1）。士気が崩れた守備隊は実効的に弱る（実効値パターン・基準非破壊）。
        /// 包囲（四面楚歌）で士気を削れば、頭数を物理的に削らずとも守備が崩れる。
        /// </summary>
        public static float EffectiveGarrison(float garrison, float morale)
            => Mathf.Max(0f, garrison) * Mathf.Clamp01(morale);

        /// <summary>攻撃側の純優勢（名・≧0）。攻撃側地上戦力が守備隊を上回るぶん。劣勢は0。</summary>
        public static float NetAdvantage(float attackerGround, float garrison)
            => Mathf.Max(0f, Mathf.Max(0f, attackerGround) - Mathf.Max(0f, garrison));

        /// <summary>守備隊が侵攻を食い止めているか（攻撃側に純優勢がない＝侵略が進まない）。</summary>
        public static bool DefendersHolding(float attackerGround, float garrison)
            => NetAdvantage(attackerGround, garrison) <= 0f;

        /// <summary>
        /// 侵略速度係数（≧0）。攻撃側の純優勢 ÷ referenceForce を maxInvadeFactor で頭打ち。
        /// 守備隊が上回る間は0（停滞）。<see cref="PlanetSiegeRules.Tick"/> の invadeRate へ渡す想定。
        /// </summary>
        public static float InvasionRateFactor(float attackerGround, float garrison, GroundInvasionParams p)
            => Mathf.Clamp(NetAdvantage(attackerGround, garrison) / p.referenceForce, 0f, p.maxInvadeFactor);

        public static float InvasionRateFactor(float attackerGround, float garrison)
            => InvasionRateFactor(attackerGround, garrison, GroundInvasionParams.Default);

        /// <summary>
        /// このtickで守備隊が失う兵力（名・≧0）。攻撃側地上戦力×garrisonAttritionRate×dt。
        /// 守備隊の残量を超えない（0で下げ止まり）。
        /// </summary>
        public static float GarrisonLosses(float attackerGround, float garrison, GroundInvasionParams p, float dt)
        {
            if (dt <= 0f) return 0f;
            float loss = Mathf.Max(0f, attackerGround) * p.garrisonAttritionRate * dt;
            return Mathf.Min(loss, Mathf.Max(0f, garrison));
        }

        public static float GarrisonLosses(float attackerGround, float garrison, float dt)
            => GarrisonLosses(attackerGround, garrison, GroundInvasionParams.Default, dt);

        /// <summary>
        /// 守備隊の対空砲火が攻撃側（包囲艦）に与える秒間ダメージ（≧0）＝残存守備隊×attackerCasualtyRate。
        /// 守備隊が削られるほど砲火も弱まる。呼び出し側が dt を掛けて包囲艦へ分配する。
        /// </summary>
        public static float AttackerCasualtyRate(float garrison, GroundInvasionParams p)
            => Mathf.Max(0f, garrison) * p.attackerCasualtyRate;

        public static float AttackerCasualtyRate(float garrison)
            => AttackerCasualtyRate(garrison, GroundInvasionParams.Default);
    }
}
