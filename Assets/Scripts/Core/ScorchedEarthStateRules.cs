using UnityEngine;

namespace Ginei
{
    /// <summary>焦土の進行状態の純データ（退却しながら自領を焼く現在の進み具合）。</summary>
    public struct ScorchedEarthState
    {
        /// <summary>焦土化された範囲（0..1。焼け野原になった領土の割合）。</summary>
        public float scorchedFraction;
        /// <summary>補給デポの無効化（0..1。敵に取られる前に空にした・破壊したデポの割合）。</summary>
        public float depotDenial;
        /// <summary>インフラ破壊（0..1。橋・道路・鉄道を落とした割合＝敵の進撃を遅らせる）。</summary>
        public float infrastructureDestroyed;

        public ScorchedEarthState(float scorchedFraction, float depotDenial, float infrastructureDestroyed)
        {
            this.scorchedFraction = Mathf.Clamp01(scorchedFraction);
            this.depotDenial = Mathf.Clamp01(depotDenial);
            this.infrastructureDestroyed = Mathf.Clamp01(infrastructureDestroyed);
        }
    }

    /// <summary>焦土の進行（ScorchedEarthState）の調整係数。</summary>
    public readonly struct ScorchedEarthStateParams
    {
        /// <summary>焦土が広がる基準速度（退却全速・破壊全力のとき1秒で進む焦土化の割合）。</summary>
        public readonly float scorchRate;
        /// <summary>焦土化された範囲で敵の現地調達が無効化される強さ（1で焼けた範囲ぶん完全に断つ）。</summary>
        public readonly float foragingDenialScale;
        /// <summary>デポを破壊できる基準速度（破壊全力・敵の進撃が遅いとき1秒で空にできる割合）。</summary>
        public readonly float depotDenialRate;
        /// <summary>敵の進撃が破壊を妨げる強さ（進撃が速いと取られる前に壊しきれない。1で完全に妨げる）。</summary>
        public readonly float captureRacePenalty;
        /// <summary>インフラ破壊が敵の進撃を遅らせる最大幅（全インフラ破壊で侵攻速度に 1−これ を掛ける）。</summary>
        public readonly float advanceSlowdownScale;
        /// <summary>自領を焼く代償の最大量（全面焦土・全人口居住のときの自国民の窮乏）。</summary>
        public readonly float ownCostScale;
        /// <summary>戦後の復興の基準速度（平時全開のとき1秒で取り戻す焦土化の割合）。</summary>
        public readonly float reconstructionRate;

        public ScorchedEarthStateParams(float scorchRate, float foragingDenialScale, float depotDenialRate,
            float captureRacePenalty, float advanceSlowdownScale, float ownCostScale, float reconstructionRate)
        {
            this.scorchRate = Mathf.Max(0f, scorchRate);
            this.foragingDenialScale = Mathf.Clamp01(foragingDenialScale);
            this.depotDenialRate = Mathf.Max(0f, depotDenialRate);
            this.captureRacePenalty = Mathf.Clamp01(captureRacePenalty);
            this.advanceSlowdownScale = Mathf.Clamp01(advanceSlowdownScale);
            this.ownCostScale = Mathf.Max(0f, ownCostScale);
            this.reconstructionRate = Mathf.Max(0f, reconstructionRate);
        }

        /// <summary>既定＝焦土速度0.2・現地調達無効化1.0・デポ破壊速度0.3・進撃妨害0.8・進撃減速幅0.5・自領コスト0.6・復興速度0.1。</summary>
        public static ScorchedEarthStateParams Default
            => new ScorchedEarthStateParams(0.2f, 1f, 0.3f, 0.8f, 0.5f, 0.6f, 0.1f);
    }

    /// <summary>
    /// 焦土作戦の<b>進行状態</b>の純ロジック（#1410・革命戦争＝ロシア戦役/ナポレオンのモスクワ遠征型）。
    /// 退却する側が自領土を破壊（畑を焼き・橋を落とし・デポを空にする）して、追ってくる敵の現地調達・補給拠点を
    /// 段階的に無効化する＝<b>荒野で敵が干上がる</b>。「退却側が自領を焼いて敵の現地調達・補給デポを無効化し、
    /// 焦土が時間で広がり敵補給を段階的に締め上げる」を式に出す：①退却が速く破壊に注力するほど焦土の範囲が
    /// 時間で広がり（<see cref="ScorchTick"/>）、焼け野原では敵が食えず現地調達が無効化され（<see cref="ForagingDenial"/>）、
    /// ②敵に取られる前にデポを破壊・空にするが、敵の進撃が速いと間に合わず（<see cref="DepotDenialTick"/>）、
    /// ③現地調達もデポも断たれた敵は補給に窮して干上がる（<see cref="EnemySupplyStrangulation"/>/<see cref="IsEnemyStarving"/>）。
    /// ④自領を焼く代償は自国民の窮乏（<see cref="OwnTerritoryCost"/>＝<see cref="RefugeeRules"/> へ）、
    /// ⑤橋・道路の破壊は敵の進撃を遅らせて時間を稼ぎ（<see cref="AdvanceSlowdown"/>）、
    /// ⑥戦後の復興は焦土を取り戻すが荒廃は残る（<see cref="Reconstruction"/>＝<see cref="ReconstructionRules"/> へ）。
    /// 焼くか否かの一回ぶんの損益判断は <see cref="ScorchedEarthRules"/>（既存）が、敵が前線で奪う徴発量は
    /// <see cref="ForageRules"/>（現地調達）が、占領地徴発で敵軍が自活する側は <see cref="KontributionRules"/>（生成済み）が扱う。
    /// 同 EPIC（WAP）で空間を時間で買う遅滞戦は <see cref="TradeSpaceForTimeRules"/>＝こちらは焦土の進行状態と敵補給の無効化のみ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ScorchedEarthStateRules
    {
        /// <summary>
        /// 焦土の範囲を時間で広げる＝現在の焦土化＋退却ペース×破壊努力×焦土速度×dt。
        /// 退却が速く（retreatPace）破壊に注力する（demolitionEffort）ほど焦土が広がる。広がった範囲（0..1）を返す。
        /// </summary>
        public static float ScorchTick(float scorchedFraction, float retreatPace, float demolitionEffort,
            float dt, ScorchedEarthStateParams p)
        {
            float current = Mathf.Clamp01(scorchedFraction);
            if (dt <= 0f) return current;
            float pace = Mathf.Clamp01(retreatPace);
            float effort = Mathf.Clamp01(demolitionEffort);
            float spread = pace * effort * p.scorchRate * dt;
            return Mathf.Clamp01(current + spread);
        }

        public static float ScorchTick(float scorchedFraction, float retreatPace, float demolitionEffort, float dt)
            => ScorchTick(scorchedFraction, retreatPace, demolitionEffort, dt, ScorchedEarthStateParams.Default);

        /// <summary>
        /// 敵の現地調達の無効化（0..1）＝焦土化された範囲×現地調達無効化係数。
        /// 焼け野原では食えない＝焦土が広がるほど敵の <see cref="ForageRules"/> の取り立て先が消える。
        /// </summary>
        public static float ForagingDenial(float scorchedFraction, ScorchedEarthStateParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(scorchedFraction) * p.foragingDenialScale);
        }

        public static float ForagingDenial(float scorchedFraction)
            => ForagingDenial(scorchedFraction, ScorchedEarthStateParams.Default);

        /// <summary>
        /// 補給デポを敵に取られる前に破壊・空にする＝現在のデポ無効化＋破壊努力×（1−進撃×進撃妨害）×デポ破壊速度×dt。
        /// 破壊に注力するほど進むが、敵の進撃 captureSpeed が速いと取られる前に壊しきれない＝間に合わない。
        /// 無効化できたデポの割合（0..1）を返す。
        /// </summary>
        public static float DepotDenialTick(float depotDenial, float demolitionEffort, float captureSpeed,
            float dt, ScorchedEarthStateParams p)
        {
            float current = Mathf.Clamp01(depotDenial);
            if (dt <= 0f) return current;
            float effort = Mathf.Clamp01(demolitionEffort);
            float capture = Mathf.Clamp01(captureSpeed);
            float race = 1f - capture * p.captureRacePenalty; // 進撃が速いと破壊が間に合わない
            float progress = effort * race * p.depotDenialRate * dt;
            return Mathf.Clamp01(current + progress);
        }

        public static float DepotDenialTick(float depotDenial, float demolitionEffort, float captureSpeed, float dt)
            => DepotDenialTick(depotDenial, demolitionEffort, captureSpeed, dt, ScorchedEarthStateParams.Default);

        /// <summary>
        /// 敵補給の締め上げ（0..1）＝現地調達もデポも断たれた敵が補給に窮する度合い。
        /// foragingDenial と depotDenial の双方が敵の補給依存 enemySupplyDependence に応じて効く＝
        /// 1−（1−現地調達無効化）×（1−デポ無効化） で「どちらか断てば窮し、両方断てば干上がる」を表し、
        /// 補給依存が高い（前線が深く後方頼みの）敵ほど締め上げが効く。<b>現地調達もデポも断たれた敵は干上がる</b>。
        /// </summary>
        public static float EnemySupplyStrangulation(float foragingDenial, float depotDenial, float enemySupplyDependence)
        {
            float forage = Mathf.Clamp01(foragingDenial);
            float depot = Mathf.Clamp01(depotDenial);
            float dependence = Mathf.Clamp01(enemySupplyDependence);
            float denial = 1f - (1f - forage) * (1f - depot); // どちらか断てば窮し両方で干上がる
            return Mathf.Clamp01(denial * dependence);
        }

        /// <summary>
        /// 自領土を焼く代償（0..ownCostScale）＝焦土化された範囲×自国の居住度×自領コスト。
        /// 焼いた範囲に住民が多いほど深い窮乏・荒廃＝<see cref="RefugeeRules"/>（難民の流出）の入力になる。
        /// 自領を焼く焦土は敵を干上がらせる代わりに自国民を犠牲にする。
        /// </summary>
        public static float OwnTerritoryCost(float scorchedFraction, float ownPopulation, ScorchedEarthStateParams p)
        {
            return Mathf.Clamp01(scorchedFraction) * Mathf.Clamp01(ownPopulation) * p.ownCostScale;
        }

        public static float OwnTerritoryCost(float scorchedFraction, float ownPopulation)
            => OwnTerritoryCost(scorchedFraction, ownPopulation, ScorchedEarthStateParams.Default);

        /// <summary>
        /// 敵の進撃速度の倍率＝1−インフラ破壊×進撃減速幅。橋・道路・鉄道を落とすほど敵は遅れる＝<b>焦土が時間を稼ぐ</b>。
        /// インフラ破壊0で1.0倍（無傷の道を快進撃）、破壊1で 1−advanceSlowdownScale 倍。実効値パターン（基準速度は非破壊）。
        /// </summary>
        public static float AdvanceSlowdown(float infrastructureDestroyed, ScorchedEarthStateParams p)
        {
            float destroyed = Mathf.Clamp01(infrastructureDestroyed);
            return 1f - destroyed * p.advanceSlowdownScale;
        }

        public static float AdvanceSlowdown(float infrastructureDestroyed)
            => AdvanceSlowdown(infrastructureDestroyed, ScorchedEarthStateParams.Default);

        /// <summary>
        /// 戦後に焦土を復興する＝焦土化された範囲を平時 peacetime に応じて取り戻す。
        /// 現在の焦土化−平時×復興速度×dt の残り焦土化（0..1）を返す＝<see cref="ReconstructionRules"/> が荒廃を再建する。
        /// 平時が無い（戦時継続）なら復興は進まない。荒廃は時間をかけてしか戻らない。
        /// </summary>
        public static float Reconstruction(float scorchedFraction, float peacetime, float dt, ScorchedEarthStateParams p)
        {
            float current = Mathf.Clamp01(scorchedFraction);
            if (dt <= 0f) return current;
            float peace = Mathf.Clamp01(peacetime);
            float recovery = peace * p.reconstructionRate * dt;
            return Mathf.Max(0f, current - recovery);
        }

        public static float Reconstruction(float scorchedFraction, float peacetime, float dt)
            => Reconstruction(scorchedFraction, peacetime, dt, ScorchedEarthStateParams.Default);

        /// <summary>
        /// 焦土で敵が補給に窮し干上がったか＝締め上げ enemySupplyStrangulation が閾値 threshold 以上。
        /// 現地調達もデポも断たれ、補給に依存する敵が前線で立ち行かなくなった判定（荒野で敵が干上がる）。
        /// </summary>
        public static bool IsEnemyStarving(float enemySupplyStrangulation, float threshold)
        {
            return Mathf.Clamp01(enemySupplyStrangulation) >= Mathf.Clamp01(threshold);
        }
    }
}
