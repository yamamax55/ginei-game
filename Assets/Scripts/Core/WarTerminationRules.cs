using UnityEngine;

namespace Ginei
{
    /// <summary>戦争終結（講和の機運・引き際）の調整値。マジックナンバー禁止＝集約。ctor で全 Clamp。</summary>
    public readonly struct WarTerminationParams
    {
        public readonly float casualtyWeight;     // 損害(0..1)が厭戦へ寄与する重み
        public readonly float durationWeight;     // 戦争の長さ(0..1)が厭戦へ寄与する重み
        public readonly float economicWeight;     // 経済負担(0..1)が厭戦へ寄与する重み
        public readonly float sunkCostWeight;     // つぎ込んだ犠牲がエスカレーションへ寄与する重み
        public readonly float culminationSlope;   // 攻勢限界の効き（進出深度×補給負担）

        public WarTerminationParams(float casualtyWeight, float durationWeight, float economicWeight,
            float sunkCostWeight, float culminationSlope)
        {
            this.casualtyWeight = Mathf.Clamp01(casualtyWeight);
            this.durationWeight = Mathf.Clamp01(durationWeight);
            this.economicWeight = Mathf.Clamp01(economicWeight);
            this.sunkCostWeight = Mathf.Clamp01(sunkCostWeight);
            this.culminationSlope = Mathf.Clamp(culminationSlope, 0f, 4f);
        }

        /// <summary>既定値（厭戦＝損害0.45/長期0.3/経済0.25・サンクコスト0.5・攻勢限界傾き1.5）。</summary>
        public static WarTerminationParams Default =>
            new WarTerminationParams(0.45f, 0.3f, 0.25f, 0.5f, 1.5f);
    }

    /// <summary>
    /// 戦争終結の純ロジック（外交EPIC #189・<b>終戦の機運と引き際の判断に特化</b>・唯一の窓口）。
    /// 戦争は勝敗が決すれば終わるとは限らない＝両者の厭戦・損害・戦況・面子で講和の機運が生まれる。
    /// 勝ちすぎても引き際を誤れば泥沼（攻勢限界 <see cref="CulminationOfVictory"/>）、負けても降伏のタイミングを
    /// 誤れば壊滅。<see cref="WarGoalRules"/>/<see cref="WarStateRules"/>（戦争目標・厭戦の管理）とは別＝
    /// こちらは「どこで戦争を終えるか」の機運と引き際の判断のみ。<see cref="DiplomacyRules"/>（外交状態遷移）とも別。
    /// 盤面非依存の plain 引数・乱数なし・入力クランプ・実効値パターン。各メソッドに Params 明示版＋Default 委譲版。test-first。
    /// </summary>
    public static class WarTerminationRules
    {
        // ── 厭戦気分 ──────────────────────────────────────────
        /// <summary>厭戦気分＝損害・長期化・経済負担の加重和（0..1）。Params 明示版。</summary>
        public static float WarWeariness(float casualties, float warDuration, float economicStrain,
            WarTerminationParams p)
        {
            float c = Mathf.Clamp01(casualties);
            float d = Mathf.Clamp01(warDuration);
            float e = Mathf.Clamp01(economicStrain);
            float w = c * p.casualtyWeight + d * p.durationWeight + e * p.economicWeight;
            return Mathf.Clamp01(w);
        }

        /// <summary>厭戦気分（Default 委譲版）。</summary>
        public static float WarWeariness(float casualties, float warDuration, float economicStrain) =>
            WarWeariness(casualties, warDuration, economicStrain, WarTerminationParams.Default);

        // ── 講和への動機 ──────────────────────────────────────
        /// <summary>講和への動機＝厭戦と膠着の双方が高いほど高まる（0..1）。膠着なしなら厭戦のみで頭打ち。</summary>
        public static float PeaceIncentive(float warWeariness, float battlefieldStalemate)
        {
            float ww = Mathf.Clamp01(warWeariness);
            float bs = Mathf.Clamp01(battlefieldStalemate);
            // 厭戦が土台、膠着が拍車。膠着0でも厭戦の7割は動機になる。
            return Mathf.Clamp01(ww * (0.7f + 0.3f * bs));
        }

        // ── 交渉の立場 ────────────────────────────────────────
        /// <summary>交渉の立場の強さ＝自軍の戦況優位＋敵の厭戦（0..1）。敵が疲れているほど強気に出られる。</summary>
        public static float NegotiationLeverage(float militaryPosition, float enemyWarWeariness)
        {
            float mp = Mathf.Clamp01(militaryPosition);
            float ew = Mathf.Clamp01(enemyWarWeariness);
            return Mathf.Clamp01(mp * 0.65f + ew * 0.35f);
        }

        // ── 受け入れ可能な講和条件 ─────────────────────────────
        /// <summary>受け入れ可能な講和条件の有利さ（0..1）。交渉力が高いほど好条件を呑ませられるが、面子の必要が下限を押し上げる。</summary>
        public static float AcceptableTerms(float negotiationLeverage, float faceSavingNeed)
        {
            float nl = Mathf.Clamp01(negotiationLeverage);
            float fs = Mathf.Clamp01(faceSavingNeed);
            // 立場の強さで取れる条件。面子が要るほど最低限の体面（floor）を確保せざるを得ず妥協の幅が狭まる。
            float floor = 0.25f * fs;
            return Mathf.Clamp01(Mathf.Max(floor, nl));
        }

        // ── 攻勢限界（勝ちすぎの引き際） ───────────────────────
        /// <summary>攻勢限界＝進出が深く補給が伸びきるほど高まる（0..1）。高いほど「ここが引き際」。Params 明示版。</summary>
        public static float CulminationOfVictory(float advanceDepth, float supplyStrain, WarTerminationParams p)
        {
            float ad = Mathf.Clamp01(advanceDepth);
            float ss = Mathf.Clamp01(supplyStrain);
            // 深く進むほど補給負担が二乗的に効く＝勝ちすぎが泥沼を呼ぶ。
            float v = ad * ss * p.culminationSlope;
            return Mathf.Clamp01(v);
        }

        /// <summary>攻勢限界（Default 委譲版）。</summary>
        public static float CulminationOfVictory(float advanceDepth, float supplyStrain) =>
            CulminationOfVictory(advanceDepth, supplyStrain, WarTerminationParams.Default);

        // ── サンクコストのエスカレーション ─────────────────────
        /// <summary>サンクコスト＝つぎ込んだ犠牲ゆえ止められない非合理（0..1）。投資が大きいほど撤退・講和を阻む。Params 明示版。</summary>
        public static float SunkCostEscalation(float investedCost, WarTerminationParams p)
        {
            float ic = Mathf.Clamp01(investedCost);
            return Mathf.Clamp01(ic * p.sunkCostWeight);
        }

        /// <summary>サンクコスト（Default 委譲版）。</summary>
        public static float SunkCostEscalation(float investedCost) =>
            SunkCostEscalation(investedCost, WarTerminationParams.Default);

        // ── 双方疲弊での講和成立 ───────────────────────────────
        /// <summary>双方疲弊での講和成立度＝両者が共に疲れていてはじめて成立（幾何平均・0..1）。片方が元気なら成立しない。</summary>
        public static float MutualExhaustionPeace(float ownWeariness, float enemyWeariness)
        {
            float a = Mathf.Clamp01(ownWeariness);
            float b = Mathf.Clamp01(enemyWeariness);
            // 幾何平均＝どちらか低いと崩れる（片方だけ疲弊では手打ちにならない）。
            return Mathf.Clamp01(Mathf.Sqrt(a * b));
        }

        // ── 講和を求めるべきか ─────────────────────────────────
        /// <summary>講和を求めるべきか＝立場の弱さで割り引いた講和動機が閾値を超えるか。交渉力が高いほど講和を急がない。</summary>
        public static bool ShouldSeekPeace(float peaceIncentive, float negotiationLeverage, float threshold)
        {
            float pi = Mathf.Clamp01(peaceIncentive);
            float nl = Mathf.Clamp01(negotiationLeverage);
            float th = Mathf.Clamp01(threshold);
            // 立場が強い（leverage 高い）ほど講和を急がない＝動機を割り引く。
            float effective = pi * (1f - 0.5f * nl);
            return effective >= th;
        }
    }
}
