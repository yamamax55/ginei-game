using UnityEngine;

namespace Ginei
{
    /// <summary>戦略的受動撤退ドクトリン（空間を時間で買う）の調整係数。</summary>
    public readonly struct TradeSpaceForTimeParams
    {
        /// <summary>譲った土地が時間に換わる効率（0..1・縦深を掛ける基礎係数）。広大な国土ほど空間を時間に換えられる。</summary>
        public readonly float spaceToTimeEfficiency;
        /// <summary>決戦回避の規律が崩れる敵圧の重み（0..1・敵に捕捉されると計画的撤退が潰走に転じる）。</summary>
        public readonly float pressureDisruptionWeight;
        /// <summary>敵の過伸張を誘発する重み（0..1・稼いだ時間×敵の進撃欲がどれだけ補給線を伸ばさせるか）。</summary>
        public readonly float overextensionWeight;
        /// <summary>稼いだ時間が動員・反攻準備に効く重み（0..1・時間がどれだけ味方になるか）。</summary>
        public readonly float timeValueWeight;
        /// <summary>決戦せず消耗させる基礎速度（dt あたり・敵過伸張×遊撃で削る上限割合）。</summary>
        public readonly float attritionRate;
        /// <summary>退却の政治的代償の重み（0..1・土地を譲ると国民・宮廷がどれだけ弱腰と非難するか）。</summary>
        public readonly float politicalCostWeight;
        /// <summary>反攻の好機の重み（0..1・敵の攻勢終末点×自軍残存がどれだけ反攻を開かせるか）。</summary>
        public readonly float counterWindowWeight;

        public TradeSpaceForTimeParams(float spaceToTimeEfficiency, float pressureDisruptionWeight,
            float overextensionWeight, float timeValueWeight, float attritionRate,
            float politicalCostWeight, float counterWindowWeight)
        {
            this.spaceToTimeEfficiency = Mathf.Clamp01(spaceToTimeEfficiency);
            this.pressureDisruptionWeight = Mathf.Clamp01(pressureDisruptionWeight);
            this.overextensionWeight = Mathf.Clamp01(overextensionWeight);
            this.timeValueWeight = Mathf.Clamp01(timeValueWeight);
            this.attritionRate = Mathf.Max(0f, attritionRate);
            this.politicalCostWeight = Mathf.Clamp01(politicalCostWeight);
            this.counterWindowWeight = Mathf.Clamp01(counterWindowWeight);
        }

        /// <summary>既定＝空間時間効率0.8・敵圧崩し0.6・過伸張誘発0.7・時間価値0.6・消耗速度0.2・政治代償0.5・反攻好機0.7。</summary>
        public static TradeSpaceForTimeParams Default =>
            new TradeSpaceForTimeParams(0.8f, 0.6f, 0.7f, 0.6f, 0.2f, 0.5f, 0.7f);
    }

    /// <summary>
    /// 戦略的受動撤退ドクトリン＝ロシア戦役型「空間を時間で買う（trade space for time）」の純ロジック（#1421・
    /// 革命戦争／ロシア戦役）。正規軍が決戦をあえて拒否し、組織的に後退して敵を自国の奥深く＝攻勢終末点まで
    /// 誘い込む。クトゥーゾフのナポレオン迎撃のように、土地を譲って時間を稼ぎ、敵の補給線を伸ばして冬・補給難で
    /// 自滅させる＝空間を時間に換え、時間を味方につける。
    /// <see cref="CulminatingPointRules"/>（攻勢終末点＝進撃側の作戦距離による戦力減衰そのもの）とは別系統＝
    /// こちらは決戦拒否で敵をその終末点まで誘い込む退却側の戦略（終末点の値を入力に取り、好機を見出す）。
    /// <see cref="PursuitRules"/>（追撃＝振り切り・殿軍の会戦後損害）とも別＝こちらは捕捉される前に組織的に退く戦略判断。
    /// <see cref="HomelandResistanceRules"/>（縦深抵抗・同 EPIC WAP＝後背地での遊撃・パルチザン）とも別＝
    /// こちらは正規軍の主力を温存して退く本体の運動。<see cref="ScorchedEarthStateRules"/>（焦土・同 EPIC WAP＝
    /// 退却路の資源を焼いて敵の現地調達を断つ）とも別＝こちらは土地そのものを譲って時間を稼ぐ運動側。
    /// 倍率・割合は基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TradeSpaceForTimeRules
    {
        /// <summary>
        /// 空間を時間で買う効率（0..1）＝譲る土地（0..1）×戦略縦深（0..1）×空間時間効率。広大な国土ほど
        /// 同じ土地を譲っても稼げる時間が大きい（敵を奥へ引き込める）。譲る土地も縦深も無ければ0。
        /// </summary>
        public static float SpaceForTimeRate(float territoryCeded, float strategicDepth, TradeSpaceForTimeParams p)
        {
            float ceded = Mathf.Clamp01(territoryCeded);
            float depth = Mathf.Clamp01(strategicDepth);
            return Mathf.Clamp01(ceded * depth * p.spaceToTimeEfficiency);
        }

        public static float SpaceForTimeRate(float territoryCeded, float strategicDepth)
            => SpaceForTimeRate(territoryCeded, strategicDepth, TradeSpaceForTimeParams.Default);

        /// <summary>
        /// 決戦回避の規律（0..1）＝撤退規律（0..1）が、敵圧（0..1）×崩し重みのぶん損なわれる。敵に捕捉されず
        /// 組織的に退ける度合い＝崩れた潰走ではなく計画的撤退。撤退規律が高く敵圧が低いほど1へ近づく。
        /// </summary>
        public static float DecisiveBattleAvoidance(float retreatDiscipline, float enemyPressure, TradeSpaceForTimeParams p)
        {
            float disc = Mathf.Clamp01(retreatDiscipline);
            float pressure = Mathf.Clamp01(enemyPressure);
            return Mathf.Clamp01(disc * (1f - p.pressureDisruptionWeight * pressure));
        }

        public static float DecisiveBattleAvoidance(float retreatDiscipline, float enemyPressure)
            => DecisiveBattleAvoidance(retreatDiscipline, enemyPressure, TradeSpaceForTimeParams.Default);

        /// <summary>
        /// 誘発される敵の過伸張（0..1）＝空間時間レート×敵の進撃欲（0..1）×過伸張重み。奥地へ誘い込み補給線を
        /// 伸ばさせる＝攻勢終末点へ向かわせる度合い。<see cref="CulminatingPointRules"/>/<see cref="HomelandResistanceRules"/>
        /// への橋渡し。敵が深追いするほど（進撃欲が高いほど）伸びきる。
        /// </summary>
        public static float EnemyOverextensionInduced(float spaceForTimeRate, float enemyAdvanceAppetite, TradeSpaceForTimeParams p)
        {
            float rate = Mathf.Clamp01(spaceForTimeRate);
            float appetite = Mathf.Clamp01(enemyAdvanceAppetite);
            return Mathf.Clamp01(rate * appetite * p.overextensionWeight);
        }

        public static float EnemyOverextensionInduced(float spaceForTimeRate, float enemyAdvanceAppetite)
            => EnemyOverextensionInduced(spaceForTimeRate, enemyAdvanceAppetite, TradeSpaceForTimeParams.Default);

        /// <summary>
        /// 稼いだ時間の価値（0..1）＝空間時間レート×（動員進捗 を時間価値重みで底上げ）。稼いだ時間で動員・冬の到来・
        /// 反攻準備がどれだけ整うか＝時間が味方になる度合い。動員進捗（0..1）が進むほど時間の価値が増す。
        /// </summary>
        public static float TimeBoughtValue(float spaceForTimeRate, float mobilizationProgress, TradeSpaceForTimeParams p)
        {
            float rate = Mathf.Clamp01(spaceForTimeRate);
            float mob = Mathf.Clamp01(mobilizationProgress);
            // 時間そのもの＋動員が進むほど時間が活きる（時間価値重みで底上げ）
            float valueMultiplier = 1f - p.timeValueWeight * (1f - mob);
            return Mathf.Clamp01(rate * valueMultiplier);
        }

        public static float TimeBoughtValue(float spaceForTimeRate, float mobilizationProgress)
            => TimeBoughtValue(spaceForTimeRate, mobilizationProgress, TradeSpaceForTimeParams.Default);

        /// <summary>
        /// 決戦せず消耗させる損耗（0..1）＝敵過伸張（0..1）×遊撃（0..1）×消耗速度×dt。会戦に持ち込まず、
        /// 補給難・遊撃（コサックの襲撃）で敵を削る＝戦わずして敵を弱らせる。敵が伸びきっているほど・遊撃が
        /// 盛んなほど削れる。dt 比例（フレーム/ターン非依存）。
        /// </summary>
        public static float AttritionWithoutBattle(float enemyOverextension, float harassment, float dt, TradeSpaceForTimeParams p)
        {
            float over = Mathf.Clamp01(enemyOverextension);
            float harass = Mathf.Clamp01(harassment);
            float t = Mathf.Max(0f, dt);
            return Mathf.Clamp01(over * harass * p.attritionRate * t);
        }

        public static float AttritionWithoutBattle(float enemyOverextension, float harassment, float dt)
            => AttritionWithoutBattle(enemyOverextension, harassment, dt, TradeSpaceForTimeParams.Default);

        /// <summary>
        /// 退却の政治的代償（0..1）＝譲る土地（0..1）×（国民士気が低いほど嵩む）×政治代償重み。土地を譲ると
        /// 国民・宮廷が退却を弱腰と非難する＝クトゥーゾフへの批判。譲る土地が多く国民士気が低いほど代償が重い。
        /// </summary>
        public static float PoliticalCostOfRetreat(float territoryCeded, float populaceMorale, TradeSpaceForTimeParams p)
        {
            float ceded = Mathf.Clamp01(territoryCeded);
            float morale = Mathf.Clamp01(populaceMorale);
            // 士気が低いほど非難が強まる（1−morale）
            return Mathf.Clamp01(ceded * (1f - morale) * p.politicalCostWeight);
        }

        public static float PoliticalCostOfRetreat(float territoryCeded, float populaceMorale)
            => PoliticalCostOfRetreat(territoryCeded, populaceMorale, TradeSpaceForTimeParams.Default);

        /// <summary>
        /// 反攻の好機（0..1）＝敵の攻勢終末点到達度（0..1）×自軍残存（0..1）×反攻好機重み。敵が伸びきって弱った
        /// 瞬間に温存した戦力で反攻する好機＝誘い込んだ敵を叩く（冬将軍の後の追撃）。敵が終末点に達し自軍が
        /// 残っているほど好機が開く。
        /// </summary>
        public static float CounterOffensiveWindow(float enemyCulmination, float ownStrength, TradeSpaceForTimeParams p)
        {
            float culm = Mathf.Clamp01(enemyCulmination);
            float own = Mathf.Clamp01(ownStrength);
            return Mathf.Clamp01(culm * own * p.counterWindowWeight);
        }

        public static float CounterOffensiveWindow(float enemyCulmination, float ownStrength)
            => CounterOffensiveWindow(enemyCulmination, ownStrength, TradeSpaceForTimeParams.Default);

        /// <summary>
        /// 弾力的縦深防御が機能しているか＝決戦回避の規律と空間時間レートが、ともに閾値以上なら true。
        /// 計画的に退きつつ（決戦拒否が崩れず）、退却が時間を生んでいる（空間を時間で買えている）＝
        /// 弾力的縦深防御が成立。どちらかが閾値を割れば、潰走か、無為な土地の喪失に陥る。
        /// </summary>
        public static bool IsElasticDefense(float decisiveBattleAvoidance, float spaceForTimeRate, float threshold)
        {
            float th = Mathf.Clamp01(threshold);
            return Mathf.Clamp01(decisiveBattleAvoidance) >= th && Mathf.Clamp01(spaceForTimeRate) >= th;
        }
    }
}
