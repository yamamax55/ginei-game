using UnityEngine;

namespace Ginei
{
    /// <summary>離間の計の調整係数。</summary>
    public readonly struct AlienationStratagemParams
    {
        /// <summary>既存の不和が離間効果を増幅する強さ（不和の種に付け込む係数＝0で増幅なし）。</summary>
        public readonly float tensionLeverage;
        /// <summary>同盟の信頼が離間を打ち消す強さ（固い信頼ほど崩れにくい防壁）。</summary>
        public readonly float trustShield;
        /// <summary>発覚時の逆効果係数（嘘がばれると標的が結束する＝逆向きの opinion 回復）。</summary>
        public readonly float backfireScale;
        /// <summary>同盟崩壊が起きる累積不信の閾値（これを超えると割れうる）。</summary>
        public readonly float collapseThreshold;

        public AlienationStratagemParams(float tensionLeverage, float trustShield, float backfireScale, float collapseThreshold)
        {
            this.tensionLeverage = Mathf.Max(0f, tensionLeverage);
            this.trustShield = Mathf.Clamp01(trustShield);
            this.backfireScale = Mathf.Max(0f, backfireScale);
            this.collapseThreshold = Mathf.Clamp01(collapseThreshold);
        }

        /// <summary>既定＝不和てこ0.5・信頼防壁0.6・逆効果1.5倍・崩壊閾値0.6。</summary>
        public static AlienationStratagemParams Default => new AlienationStratagemParams(0.5f, 0.6f, 1.5f, 0.6f);
    }

    /// <summary>
    /// 離間の計の純ロジック（三国志演義・曹操が韓遂と馬超を仲違いさせた型・#1106）。敵対する2勢力の
    /// 同盟に偽証で不信の種を蒔き、内部から崩して戦わずして敵を分断する。**既に不和の種があるほど離間は
    /// 効き、信頼の固い同盟は崩しにくい**＝付け込む隙が要る。**ばれれば逆に標的同士が結束する**＝下手な
    /// 離間は逆効果。離間は一度でなく繰り返しで疑心暗鬼を育てる（<see cref="SuspicionTick"/>）。
    /// <see cref="EspionageRules"/>（諜報一般＝潜入/破壊工作）の同盟分断への拡張、与える打撃は
    /// <see cref="DiplomacyRules"/> の opinion を引き下げて波及させる想定（平文言及＝本クラスは係数を返すのみ）、
    /// 認識を歪める <see cref="DeceptionRules"/>（戦略 AI への欺瞞）とは別系統＝こちらは敵同士の関係を割る。
    /// 乱数は roll(0..1) で決定論。基準値は非破壊（実効値パターン）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AlienationStratagemRules
    {
        /// <summary>
        /// 不信を植え付ける効果（0..1）＝偽証の精巧さ forgedEvidence(0..1)×（1＋既存の不和×不和てこ）
        /// ×（1−同盟の信頼×信頼防壁）。精巧な偽証ほど効き、既に不和があるほど増幅され、信頼が固いほど
        /// 割り引かれる＝韓遂と馬超のように元から疑いがあれば一通の手紙で割れる。
        /// </summary>
        public static float SowDiscordEffect(float forgedEvidence, float existingTension, float targetTrust, AlienationStratagemParams p)
        {
            float fe = Mathf.Clamp01(forgedEvidence);
            float tension = Mathf.Clamp01(existingTension);
            float trust = Mathf.Clamp01(targetTrust);
            float leverage = 1f + tension * p.tensionLeverage;   // 既存の不和に付け込む
            float shield = 1f - trust * p.trustShield;           // 固い信頼が防壁
            return Mathf.Clamp01(fe * leverage * shield);
        }

        /// <summary>既定パラメータでの不信植え付け効果。</summary>
        public static float SowDiscordEffect(float forgedEvidence, float existingTension, float targetTrust)
            => SowDiscordEffect(forgedEvidence, existingTension, targetTrust, AlienationStratagemParams.Default);

        /// <summary>
        /// 同盟関係への opinion 打撃（0..1）＝不信効果 discordEffect×関係の深さ relationshipDepth(0..1)。
        /// 深い同盟ほど壊れたときの落差が大きい（崩す価値も高い）。<see cref="DiplomacyRules"/> の opinion を
        /// この割合ぶん引き下げる係数として消費する想定。
        /// </summary>
        public static float OpinionDamage(float discordEffect, float relationshipDepth)
            => Mathf.Clamp01(Mathf.Clamp01(discordEffect) * Mathf.Clamp01(relationshipDepth));

        /// <summary>
        /// 離間工作の発覚リスク（0..1）＝偽証の精巧さが低いほど・敵防諜 enemyCounterIntel(0..1) が強いほど
        /// 高い＝（1−偽証の精巧さ）×敵防諜。雑な偽証ほど足がつく。露見すると <see cref="Backfire"/> で逆効果。
        /// </summary>
        public static float ExposureRisk(float forgedEvidence, float enemyCounterIntel)
            => Mathf.Clamp01((1f - Mathf.Clamp01(forgedEvidence)) * Mathf.Clamp01(enemyCounterIntel));

        /// <summary>発覚判定（決定論）。roll∈[0,1) が発覚リスク未満なら見破られた＝true。</summary>
        public static bool IsExposed(float forgedEvidence, float enemyCounterIntel, float roll)
            => roll < ExposureRisk(forgedEvidence, enemyCounterIntel);

        /// <summary>
        /// 発覚時の逆効果（opinion の符号付き変化）。未発覚なら蒔いた不信ぶん opinion を下げる（負）、
        /// 発覚すると嘘がばれて標的同士が結束し、逆向きに信頼を回復させる（正＝逆効果係数で増幅）。
        /// 下手な離間は敵を団結させる＝戦わずして敵を強くする愚を式にする。
        /// </summary>
        public static float Backfire(bool exposed, float discordEffect, AlienationStratagemParams p)
        {
            float effect = Mathf.Clamp01(discordEffect);
            // 未発覚＝不信が残り opinion を下げる（負）／発覚＝結束して opinion が上がる（正・逆効果倍）。
            return exposed ? effect * p.backfireScale : -effect;
        }

        /// <summary>既定パラメータでの逆効果。</summary>
        public static float Backfire(bool exposed, float discordEffect)
            => Backfire(exposed, discordEffect, AlienationStratagemParams.Default);

        /// <summary>
        /// 同盟崩壊の確率（0..1）＝累積不信 cumulativeDiscord(0..1) が閾値を超えた超過分を、同盟の靭性
        /// allianceResilience(0..1) で割り引いて確率化。閾値未満では割れない＝疑心が積もって初めて割れる。
        /// 靭性の高い同盟ほど同じ不信でも持ちこたえる。
        /// </summary>
        public static float AllianceCollapseChance(float cumulativeDiscord, float allianceResilience, AlienationStratagemParams p)
        {
            float discord = Mathf.Clamp01(cumulativeDiscord);
            if (discord <= p.collapseThreshold) return 0f;
            float over = (discord - p.collapseThreshold) / Mathf.Max(1e-4f, 1f - p.collapseThreshold);
            float resist = 1f - Mathf.Clamp01(allianceResilience);
            return Mathf.Clamp01(over * resist);
        }

        /// <summary>既定パラメータでの同盟崩壊確率。</summary>
        public static float AllianceCollapseChance(float cumulativeDiscord, float allianceResilience)
            => AllianceCollapseChance(cumulativeDiscord, allianceResilience, AlienationStratagemParams.Default);

        /// <summary>このとき同盟が崩壊するか（roll が崩壊確率を下回れば崩壊）。</summary>
        public static bool AllianceCollapses(float cumulativeDiscord, float allianceResilience, AlienationStratagemParams p, float roll)
            => roll < AllianceCollapseChance(cumulativeDiscord, allianceResilience, p);

        /// <summary>
        /// 疑心の蓄積（0..1）＝現在の疑心 suspicion に、不信効果×dt を加算して育てる。離間は一度でなく
        /// 繰り返しで効く＝疑心暗鬼は積み重ねで深まる（累積不信＝<see cref="AllianceCollapseChance"/> の入力）。
        /// </summary>
        public static float SuspicionTick(float suspicion, float discordEffect, float dt)
        {
            float add = Mathf.Clamp01(discordEffect) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(suspicion) + add);
        }
    }
}
