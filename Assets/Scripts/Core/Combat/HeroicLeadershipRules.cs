using UnityEngine;

namespace Ginei
{
    /// <summary>英雄的指揮（陣頭指揮）の調整係数。猛将が前線に立つことの輝きと危険を解く。</summary>
    public readonly struct HeroicLeadershipParams
    {
        /// <summary>陣頭高揚の係数（0..1・カリスマ×前線近さ×敬慕が満点のときの上限）。</summary>
        public readonly float inspirationWeight;
        /// <summary>孤立した部隊でも届く高揚の床（0..1・連結0でも残る最低伝播）。</summary>
        public readonly float contagionFloor;
        /// <summary>伝播の係数（0..1・高揚×連結が満点のときの上限）。</summary>
        public readonly float contagionWeight;
        /// <summary>被弾リスクの係数（0..1・前線近さ×敵の狙いが満点のときの上限）。</summary>
        public readonly float exposureWeight;
        /// <summary>英雄的猛攻が最大となる無謀さの最適点（0..1・これを境に過小/過大で威力が落ちる）。</summary>
        public readonly float idealRecklessness;
        /// <summary>最適点からの外れに対する威力の落ち幅（大きいほど鋭く狭い・1以上）。</summary>
        public readonly float recklessnessSharpness;
        /// <summary>英雄的猛攻の係数（0..1・伝播×無謀適合が満点のときの上限）。</summary>
        public readonly float surgeWeight;
        /// <summary>将の戦死確率の上限（0..1・陣頭でも超えない頭打ち）。</summary>
        public readonly float casualtyMax;
        /// <summary>士気崩壊の係数（0..1・敬慕×高揚が満点のときの上限）。</summary>
        public readonly float shockWeight;
        /// <summary>陣頭指揮が割に合う既定閾値（高揚−戦死確率がこれ以上で価値あり）。</summary>
        public readonly float worthwhileThreshold;

        public HeroicLeadershipParams(float inspirationWeight, float contagionFloor, float contagionWeight,
            float exposureWeight, float idealRecklessness, float recklessnessSharpness,
            float surgeWeight, float casualtyMax, float shockWeight, float worthwhileThreshold)
        {
            this.inspirationWeight = Mathf.Clamp01(inspirationWeight);
            this.contagionFloor = Mathf.Clamp01(contagionFloor);
            this.contagionWeight = Mathf.Clamp01(contagionWeight);
            this.exposureWeight = Mathf.Clamp01(exposureWeight);
            this.idealRecklessness = Mathf.Clamp01(idealRecklessness);
            this.recklessnessSharpness = Mathf.Max(1f, recklessnessSharpness);
            this.surgeWeight = Mathf.Clamp01(surgeWeight);
            this.casualtyMax = Mathf.Clamp01(casualtyMax);
            this.shockWeight = Mathf.Clamp01(shockWeight);
            this.worthwhileThreshold = Mathf.Clamp01(worthwhileThreshold);
        }

        /// <summary>
        /// 既定＝高揚重み1.0・伝播の床0.5・伝播重み1.0・被弾重み1.0・最適無謀0.6・無謀鋭さ2.0・
        /// 猛攻重み1.0・戦死上限0.5・士気崩壊重み1.0・価値閾値0.4。
        /// </summary>
        public static HeroicLeadershipParams Default =>
            new HeroicLeadershipParams(1.0f, 0.5f, 1.0f, 1.0f, 0.6f, 2.0f, 1.0f, 0.5f, 1.0f, 0.4f);
    }

    /// <summary>
    /// 英雄的指揮（陣頭指揮）の純ロジック。将が自ら前線に立つ猛将型の指揮を解く。
    /// 陣頭に立つカリスマが士気を爆発的に高め（<see cref="FrontlineInspiration"/>）部隊に伝播するが
    /// （<see cref="CharismaContagion"/>）、将自身が敵の弾雨に身を晒し（<see cref="ExposureRisk"/>）、
    /// 勇猛と思慮の綱引き（<see cref="RecklessnessFactor"/>）で英雄的猛攻の威力（<see cref="HeroicSurge"/>）が決まる。
    /// しかし慕われた英雄ほど、その戦死（<see cref="CommanderCasualtyChance"/>）が部隊に与える士気崩壊
    /// （<see cref="MoraleShockIfFallen"/>）は大きい＝諸刃の剣。陣頭指揮が割に合うかを総合判定する
    /// （<see cref="IsHeroicLeadershipWorthwhile"/>）。
    /// <para>
    /// 分担：<see cref="MoraleContagionRules"/> は士気の隣接伝播の一般則、<see cref="MoraleShockRules"/> は士気衝撃、
    /// <see cref="ExposureRisk"/> 起点の指揮官被弾は人事/捕虜（<see cref="CaptivityRules"/>）へ橋渡しする。本クラスは
    /// 「将が前に出ること」の高揚と危険のトレードオフに特化し、係数を返すだけで既存窓口へ委ねる。
    /// </para>
    /// 全入力クランプ・乱数なし決定論・実効値パターン。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HeroicLeadershipRules
    {
        /// <summary>
        /// 陣頭高揚（0..1）＝カリスマ charisma × 前線への近さ proximityToFront × 部下の敬慕 troopDevotion × 高揚重み。
        /// 将が前に出るほど、慕われるほど士気が爆発的に高まる（どれか0なら高揚は出ない＝積で効く）。
        /// </summary>
        public static float FrontlineInspiration(float charisma, float proximityToFront, float troopDevotion, HeroicLeadershipParams p)
        {
            float t = Mathf.Clamp01(charisma) * Mathf.Clamp01(proximityToFront) * Mathf.Clamp01(troopDevotion);
            return Mathf.Clamp01(t * p.inspirationWeight);
        }

        public static float FrontlineInspiration(float charisma, float proximityToFront, float troopDevotion)
            => FrontlineInspiration(charisma, proximityToFront, troopDevotion, HeroicLeadershipParams.Default);

        /// <summary>
        /// カリスマの伝播（0..1）＝陣頭高揚 frontlineInspiration ×（床 + (1-床)×部隊連結 unitConnectivity）× 伝播重み。
        /// 連結が高いほど高揚が部隊全体へ広く波及する。連結0でも床ぶんは将の傍へ届く（孤立しても陣頭は鼓舞する）。
        /// </summary>
        public static float CharismaContagion(float frontlineInspiration, float unitConnectivity, HeroicLeadershipParams p)
        {
            float reach = Mathf.Lerp(p.contagionFloor, 1f, Mathf.Clamp01(unitConnectivity));
            return Mathf.Clamp01(Mathf.Clamp01(frontlineInspiration) * reach * p.contagionWeight);
        }

        public static float CharismaContagion(float frontlineInspiration, float unitConnectivity)
            => CharismaContagion(frontlineInspiration, unitConnectivity, HeroicLeadershipParams.Default);

        /// <summary>
        /// 将の被弾リスク（0..1）＝前線への近さ proximityToFront × 敵の狙い enemyFocus × 被弾重み。
        /// 前に出るほど、敵が将を狙い撃つほど身が危うい（陣頭に立つことの代償）。
        /// </summary>
        public static float ExposureRisk(float proximityToFront, float enemyFocus, HeroicLeadershipParams p)
        {
            float t = Mathf.Clamp01(proximityToFront) * Mathf.Clamp01(enemyFocus);
            return Mathf.Clamp01(t * p.exposureWeight);
        }

        public static float ExposureRisk(float proximityToFront, float enemyFocus)
            => ExposureRisk(proximityToFront, enemyFocus, HeroicLeadershipParams.Default);

        /// <summary>
        /// 無謀さ（0..1）＝勇猛 boldness /（勇猛 + 思慮 prudence）。
        /// 勇猛が思慮を上回るほど無謀になり、思慮が無謀さを抑える。両者0なら中庸0.5（判断材料なし）。
        /// </summary>
        public static float RecklessnessFactor(float boldness, float prudence, HeroicLeadershipParams p)
        {
            float b = Mathf.Clamp01(boldness);
            float pr = Mathf.Clamp01(prudence);
            float denom = b + pr;
            if (denom <= 0f) return 0.5f;
            return Mathf.Clamp01(b / denom);
        }

        public static float RecklessnessFactor(float boldness, float prudence)
            => RecklessnessFactor(boldness, prudence, HeroicLeadershipParams.Default);

        /// <summary>
        /// 英雄的猛攻の威力（0..1）＝伝播 charismaContagion × 無謀適合 × 猛攻重み。
        /// 無謀適合＝最適点 idealRecklessness からの外れを鋭さ recklessnessSharpness で罰する三角窓
        /// ＝clamp01(1 - sharpness×|reckless - ideal|)。臆病すぎても（伝播しても突っ込めない）、無謀すぎても
        /// （統制を失う）威力は出ず、ある程度の無謀さで頂点に達する（猪突の妙味）。
        /// </summary>
        public static float HeroicSurge(float charismaContagion, float recklessnessFactor, HeroicLeadershipParams p)
        {
            float dev = Mathf.Abs(Mathf.Clamp01(recklessnessFactor) - p.idealRecklessness);
            float fitness = Mathf.Clamp01(1f - p.recklessnessSharpness * dev);
            return Mathf.Clamp01(Mathf.Clamp01(charismaContagion) * fitness * p.surgeWeight);
        }

        public static float HeroicSurge(float charismaContagion, float recklessnessFactor)
            => HeroicSurge(charismaContagion, recklessnessFactor, HeroicLeadershipParams.Default);

        /// <summary>
        /// 将の戦死確率（0..casualtyMax）＝被弾 exposureRisk × 無謀さ recklessnessFactor /（1 + 護衛 guardStrength）。
        /// 弾雨に晒され、無謀に踏み込むほど死にやすく、手厚い護衛がそれを割り引く。上限で頭打ち（必死ではない）。
        /// </summary>
        public static float CommanderCasualtyChance(float exposureRisk, float recklessnessFactor, float guardStrength, HeroicLeadershipParams p)
        {
            float numer = Mathf.Clamp01(exposureRisk) * Mathf.Clamp01(recklessnessFactor);
            float denom = 1f + Mathf.Max(0f, guardStrength);
            return Mathf.Clamp(numer / denom, 0f, p.casualtyMax);
        }

        public static float CommanderCasualtyChance(float exposureRisk, float recklessnessFactor, float guardStrength)
            => CommanderCasualtyChance(exposureRisk, recklessnessFactor, guardStrength, HeroicLeadershipParams.Default);

        /// <summary>将が戦死するか（roll∈[0,1) を注入・決定論）。</summary>
        public static bool IsCommanderFallen(float exposureRisk, float recklessnessFactor, float guardStrength, float roll, HeroicLeadershipParams p)
            => roll < CommanderCasualtyChance(exposureRisk, recklessnessFactor, guardStrength, p);

        public static bool IsCommanderFallen(float exposureRisk, float recklessnessFactor, float guardStrength, float roll)
            => IsCommanderFallen(exposureRisk, recklessnessFactor, guardStrength, roll, HeroicLeadershipParams.Default);

        /// <summary>
        /// 将が倒れた時の士気崩壊（0..1）＝部下の敬慕 troopDevotion × 英雄的猛攻 heroicSurge × 崩壊重み。
        /// 慕われた英雄が、士気を高揚させた只中で倒れるほど、その喪失は部隊を深く打ちのめす（諸刃の剣）。
        /// </summary>
        public static float MoraleShockIfFallen(float troopDevotion, float heroicSurge, HeroicLeadershipParams p)
        {
            float t = Mathf.Clamp01(troopDevotion) * Mathf.Clamp01(heroicSurge);
            return Mathf.Clamp01(t * p.shockWeight);
        }

        public static float MoraleShockIfFallen(float troopDevotion, float heroicSurge)
            => MoraleShockIfFallen(troopDevotion, heroicSurge, HeroicLeadershipParams.Default);

        /// <summary>
        /// 陣頭指揮が割に合うか＝英雄的猛攻 heroicSurge − 将の戦死確率 commanderCasualtyChance が threshold 以上。
        /// 得られる猛攻が、将を失う危険を閾値ぶん上回ってはじめて前に出る価値がある（無謀の戒め）。
        /// </summary>
        public static bool IsHeroicLeadershipWorthwhile(float heroicSurge, float commanderCasualtyChance, float threshold)
            => (Mathf.Clamp01(heroicSurge) - Mathf.Clamp01(commanderCasualtyChance)) >= Mathf.Clamp01(threshold);

        public static bool IsHeroicLeadershipWorthwhile(float heroicSurge, float commanderCasualtyChance)
            => IsHeroicLeadershipWorthwhile(heroicSurge, commanderCasualtyChance, HeroicLeadershipParams.Default.worthwhileThreshold);
    }
}
