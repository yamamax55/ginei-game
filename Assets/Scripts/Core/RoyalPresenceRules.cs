using UnityEngine;

namespace Ginei
{
    /// <summary>君主の臨御（親征）の調整係数（#899）。</summary>
    public readonly struct RoyalPresenceParams
    {
        /// <summary>臨御の基礎士気ボーナス（カリスマ0でも王が前線に立てば将兵は奮い立つ）。</summary>
        public readonly float baseMoraleBonus;
        /// <summary>カリスマの士気重み（charisma=1 で基礎に加えてこの幅まで上乗せ＝名君ほど格別）。</summary>
        public readonly float charismaMoraleScale;
        /// <summary>士気→戦力への波及係数（士気ボーナスにこれを掛けて戦力ボーナスとする）。</summary>
        public readonly float combatSpillover;
        /// <summary>君主戦死/捕虜の基礎リスク（前線に立つ以上わずかでも常にある）。</summary>
        public readonly float baseCasualtyRisk;
        /// <summary>激戦のリスク重み（battleIntensity=1 で基礎に加えてこの幅まで危険が増す）。</summary>
        public readonly float intensityRiskScale;
        /// <summary>護衛の防護効果（guardStrength=1 でリスクが (1−この値) 倍まで下がる）。</summary>
        public readonly float guardProtection;
        /// <summary>君主リスクの上限（どれほど激戦でも護衛があれば確実な死にはしない）。</summary>
        public readonly float maxCasualtyRisk;
        /// <summary>親征勝利の威信ボーナス（陣頭に立って勝てば英雄＝威信が跳ねる）。</summary>
        public readonly float victoryPrestige;
        /// <summary>親征敗北の威信ペナルティ（自ら出て負ければ権威が傷つく）。</summary>
        public readonly float defeatPrestige;
        /// <summary>後方に留まる威信の目減り（戦わぬ王は侮られる＝臨御しないだけで減る）。</summary>
        public readonly float absencePrestigePenalty;
        /// <summary>君主戦死の基礎継承危機（後継が明確でも要人の急逝は揺らす）。</summary>
        public readonly float baseSuccessionCrisis;

        public RoyalPresenceParams(float baseMoraleBonus, float charismaMoraleScale, float combatSpillover,
                                   float baseCasualtyRisk, float intensityRiskScale, float guardProtection,
                                   float maxCasualtyRisk, float victoryPrestige, float defeatPrestige,
                                   float absencePrestigePenalty, float baseSuccessionCrisis)
        {
            this.baseMoraleBonus = Mathf.Max(0f, baseMoraleBonus);
            this.charismaMoraleScale = Mathf.Max(0f, charismaMoraleScale);
            this.combatSpillover = Mathf.Max(0f, combatSpillover);
            this.baseCasualtyRisk = Mathf.Clamp01(baseCasualtyRisk);
            this.intensityRiskScale = Mathf.Max(0f, intensityRiskScale);
            this.guardProtection = Mathf.Clamp01(guardProtection);
            this.maxCasualtyRisk = Mathf.Clamp01(maxCasualtyRisk);
            this.victoryPrestige = Mathf.Max(0f, victoryPrestige);
            this.defeatPrestige = Mathf.Max(0f, defeatPrestige);
            this.absencePrestigePenalty = Mathf.Max(0f, absencePrestigePenalty);
            this.baseSuccessionCrisis = Mathf.Clamp01(baseSuccessionCrisis);
        }

        /// <summary>
        /// 既定＝基礎士気0.1・カリスマ士気0.3・士気→戦力0.5・基礎リスク0.02・激戦リスク0.3・
        /// 護衛防護0.8・リスク上限0.5・勝利威信0.3・敗北威信0.25・後方威信減0.1・基礎継承危機0.2。
        /// </summary>
        public static RoyalPresenceParams Default => new RoyalPresenceParams(
            0.1f, 0.3f, 0.5f, 0.02f, 0.3f, 0.8f, 0.5f, 0.3f, 0.25f, 0.1f, 0.2f);
    }

    /// <summary>
    /// 君主の臨御＝親征（#899）の純ロジック。前線に立つ王は将兵の士気を格別に高め戦力を押し上げる
    /// （<see cref="MoraleBonus"/>＝臨御の士気・<see cref="CombatBonus"/>＝戦力への波及）が、
    /// 戦死/捕虜のリスクを負う（<see cref="MonarchCasualtyRisk"/>＝激戦ほど・護衛が薄いほど命懸け）。
    /// 逆に安全な後方に留まる王は威信が下がる（<see cref="PrestigeFromPresence"/>＝戦わぬ王は侮られる）。
    /// 王が親征で斃れれば国が揺らぐ（<see cref="SuccessionCrisisOnDeath"/>）。臨御すべきかは
    /// 「士気の利得 vs 戦死リスク」で測る（<see cref="PresenceDecision"/>）＝親征は両刃＝勝てば英雄・
    /// 死ねば国が傾く・退けば侮られる。
    /// 分担：<see cref="ReputationRules"/>＝名将個人の名声（勝敗の評判）／<see cref="IllnessRules"/>＝
    /// 君主の健康（病臥・突発イベント）／<see cref="SuccessionWarRules"/>＝君主戦死が招く継承危機の
    /// 帰結（本クラスの危機度を入力に取る）／本クラス＝親征という選択の損得（士気・戦力・リスク・威信）。
    /// 士気ボーナスは Game 層 FleetMorale へ掛ける係数を返すのみ（基準値非破壊・実効値パターン）。
    /// 乱数は roll を引数で受ける決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RoyalPresenceRules
    {
        /// <summary>
        /// 臨御の士気ボーナス（0..baseMoraleBonus+charismaMoraleScale）。王が前線にいる（isPresent）間だけ
        /// 基礎＋カリスマ（monarchCharisma 0..1）×重みで将兵が奮い立つ＝名君ほど格別。後方なら0
        /// （臨御していない王は士気を上げない）。FleetMorale（Game層）へ加算する係数として使う。
        /// </summary>
        public static float MoraleBonus(float monarchCharisma, bool isPresent, RoyalPresenceParams p)
        {
            if (!isPresent) return 0f;
            float c = Mathf.Clamp01(monarchCharisma);
            return p.baseMoraleBonus + p.charismaMoraleScale * c;
        }

        public static float MoraleBonus(float monarchCharisma, bool isPresent)
            => MoraleBonus(monarchCharisma, isPresent, RoyalPresenceParams.Default);

        /// <summary>
        /// 戦力への波及（0..）。臨御の士気ボーナスに combatSpillover を掛けた戦力ボーナス＝
        /// 士気が上がれば戦いぶりも上がる（士気→戦力）。負値は0に丸める。
        /// </summary>
        public static float CombatBonus(float presenceMoraleBonus, RoyalPresenceParams p)
        {
            return Mathf.Max(0f, presenceMoraleBonus) * p.combatSpillover;
        }

        public static float CombatBonus(float presenceMoraleBonus)
            => CombatBonus(presenceMoraleBonus, RoyalPresenceParams.Default);

        /// <summary>
        /// 君主の戦死/捕虜リスク（0..maxCasualtyRisk）。臨御していなければ0（後方の王は前線で死なない）。
        /// 前線に立てば 基礎＋激戦（battleIntensity 0..1）×重み を土台に、護衛（guardStrength 0..1）が
        /// (1−guardProtection×guardStrength) 倍まで減じる＝激戦ほど・護衛が薄いほど危険。上限で頭打ち
        /// （手厚い護衛でも親征は命懸け＝ゼロにはならない）。
        /// </summary>
        public static float MonarchCasualtyRisk(float battleIntensity, float guardStrength, bool isPresent,
                                                RoyalPresenceParams p)
        {
            if (!isPresent) return 0f;
            float intensity = Mathf.Clamp01(battleIntensity);
            float guard = Mathf.Clamp01(guardStrength);
            float risk = p.baseCasualtyRisk + p.intensityRiskScale * intensity;
            risk *= 1f - p.guardProtection * guard;
            return Mathf.Clamp(risk, 0f, p.maxCasualtyRisk);
        }

        public static float MonarchCasualtyRisk(float battleIntensity, float guardStrength, bool isPresent)
            => MonarchCasualtyRisk(battleIntensity, guardStrength, isPresent, RoyalPresenceParams.Default);

        /// <summary>君主が斃れる（戦死/捕虜）かの判定（決定論）。roll（0..1）がリスク未満なら斃れる。roll=1 は無傷。</summary>
        public static bool MonarchFalls(float risk, float roll)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(risk);
        }

        /// <summary>
        /// 臨御による威信の増減。親征（isPresent）して勝てば +victoryPrestige、負ければ −defeatPrestige＝
        /// 自ら出るほど勝敗の威信が大きく振れる。後方に留まれば（!isPresent）勝敗に関わらず
        /// −absencePrestigePenalty＝戦わぬ王は侮られる（出ないこと自体が威信を削る）。
        /// 正統性（<see cref="SuccessionWarRules"/>等）への外生入力として使う想定。
        /// </summary>
        public static float PrestigeFromPresence(bool isPresent, bool victory, RoyalPresenceParams p)
        {
            if (!isPresent) return -p.absencePrestigePenalty;
            return victory ? p.victoryPrestige : -p.defeatPrestige;
        }

        public static float PrestigeFromPresence(bool isPresent, bool victory)
            => PrestigeFromPresence(isPresent, victory, RoyalPresenceParams.Default);

        /// <summary>
        /// 君主戦死による継承危機（0..1）。基礎＋要人度（monarchImportance 0..1＝建国の英雄ほど重い）に応じ、
        /// 後継の明確さ（heirClarity 0..1＝1で立太子済み）が (1−heirClarity) 倍に和らげる＝後継が曖昧なほど
        /// 親征での王の死は国を揺らす。<see cref="SuccessionWarRules"/> の入力（継承戦争の火種度）として渡す。
        /// </summary>
        public static float SuccessionCrisisOnDeath(float monarchImportance, float heirClarity, RoyalPresenceParams p)
        {
            float importance = Mathf.Clamp01(monarchImportance);
            float clarity = Mathf.Clamp01(heirClarity);
            float crisis = (p.baseSuccessionCrisis + (1f - p.baseSuccessionCrisis) * importance) * (1f - clarity);
            return Mathf.Clamp01(crisis);
        }

        public static float SuccessionCrisisOnDeath(float monarchImportance, float heirClarity)
            => SuccessionCrisisOnDeath(monarchImportance, heirClarity, RoyalPresenceParams.Default);

        /// <summary>
        /// 親征の損得（純益）。期待される士気の利得（expectedMoraleGain≥0）から、戦死リスク（casualtyRisk 0..1）が
        /// 威信の賭け金（prestigeStake≥0＝王の死で失う国家価値）に及ぼす期待損失を差し引く＝
        /// 「臨御で得る士気 − 君主を喪う期待コスト」。正なら臨御すべき、負なら退くべき＝両刃の天秤。
        /// </summary>
        public static float PresenceDecision(float expectedMoraleGain, float casualtyRisk, float prestigeStake)
        {
            float gain = Mathf.Max(0f, expectedMoraleGain);
            float risk = Mathf.Clamp01(casualtyRisk);
            float stake = Mathf.Max(0f, prestigeStake);
            return gain - risk * stake;
        }

        /// <summary>親征すべきか（損得が正＝利得がリスクを上回る）。<see cref="PresenceDecision"/> の符号判定。</summary>
        public static bool ShouldTakeField(float expectedMoraleGain, float casualtyRisk, float prestigeStake)
        {
            return PresenceDecision(expectedMoraleGain, casualtyRisk, prestigeStake) > 0f;
        }
    }
}
