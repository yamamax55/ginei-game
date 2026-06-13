using UnityEngine;

namespace Ginei
{
    /// <summary>動員ドクトリン（戦争動員の様式）。命令型＝統制・指令で一気に／市場型＝価格・契約・自発で。</summary>
    public enum MobilizationDoctrine
    {
        命令型,   // 0 統制経済・徴用・指令で一気に動員（速いが持久力に欠ける／専制・全体主義が得意）
        市場型,   // 1 価格・契約・自発で資源を引き出す（遅いが持久力がある／民主・市場経済が得意）
        混合型    // 2 両者の折衷（速度・持久力ともに中庸）
    }

    /// <summary>動員ドクトリンの調整係数（MCN-6 #1395）。</summary>
    public readonly struct MobilizationDoctrineParams
    {
        /// <summary>命令型の立ち上がり速度ボーナス（命令型はこのぶん速く動員できる）。</summary>
        public readonly float commandSpeedBonus;
        /// <summary>市場型の立ち上がり速度ペナルティ（市場型は自発に任せるため初動が遅い）。</summary>
        public readonly float marketSpeedPenalty;
        /// <summary>市場型の持久力ボーナス（市場型は統制疲れなく長期戦に伸びる）。</summary>
        public readonly float marketSustainBonus;
        /// <summary>命令型の持久力ペナルティ（統制疲れで息切れする）。</summary>
        public readonly float commandSustainPenalty;
        /// <summary>命令型動員の強制コスト最大（強度1で民の不満・効率低下がこのぶん）。</summary>
        public readonly float coercionCostMax;
        /// <summary>市場型の価格インセンティブ効率の効き（価格シグナル1で市場型はこのぶん効率的に資源を引き出す）。</summary>
        public readonly float marketIncentiveScale;
        /// <summary>動員速度が効く重み（短期戦＝戦争の長さ0でのwarDuration補間の基点）。</summary>
        public readonly float speedWeight;

        public MobilizationDoctrineParams(float commandSpeedBonus, float marketSpeedPenalty,
            float marketSustainBonus, float commandSustainPenalty,
            float coercionCostMax, float marketIncentiveScale, float speedWeight)
        {
            this.commandSpeedBonus = Mathf.Clamp01(commandSpeedBonus);
            this.marketSpeedPenalty = Mathf.Clamp01(marketSpeedPenalty);
            this.marketSustainBonus = Mathf.Clamp01(marketSustainBonus);
            this.commandSustainPenalty = Mathf.Clamp01(commandSustainPenalty);
            this.coercionCostMax = Mathf.Clamp01(coercionCostMax);
            this.marketIncentiveScale = Mathf.Clamp01(marketIncentiveScale);
            this.speedWeight = Mathf.Clamp01(speedWeight);
        }

        /// <summary>
        /// 既定＝命令速度+0.3／市場速度−0.25／市場持久+0.3／命令持久−0.25／強制コスト最大0.5／
        /// 市場効率効き0.5／速度重み0.5。命令型は速く立ち上がるが持久に欠け、市場型は遅いが持久に伸びる
        /// ＝<b>速度と持久力のトレードオフ</b>を数値に固定。
        /// </summary>
        public static MobilizationDoctrineParams Default
            => new MobilizationDoctrineParams(0.3f, 0.25f, 0.3f, 0.25f, 0.5f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 動員ドクトリンの純ロジック（MCN-6 #1395）。<b>政体によって戦争動員の様式が異なる</b>＝命令型動員（統制経済・
    /// 徴用・指令で一気に動員。専制・全体主義が得意。速いが持久力に欠ける）と市場型動員（価格・契約・自発で資源を
    /// 引き出す。民主・市場経済が得意。立ち上がりは遅いが持久力がある）。<b>動員速度と持久力がトレードオフ</b>で、
    /// 短期決戦なら命令型・長期戦なら市場型が優り、戦争の長さに応じて優劣が変わる。
    /// <see cref="MobilizationRules"/>（動員水準＝平時/部分動員/総力戦の段階と過熱）／<see cref="ConscriptionRules"/>
    /// （徴募の人口コスト）／<see cref="CalculationProblemRules"/>（計画の効率損失＝価格なき計算の破綻）／
    /// <see cref="WarIndustryRules"/>（軍産複合体＝構造的な平和抵抗）とは分担し、ここは<b>政体別の動員様式（命令型vs
    /// 市場型）＝立ち上がり速度と持久力のトレードオフ</b>に専念する（動員の段階でも徴募の人口でも計画効率でも軍産でも
    /// なく、ドクトリンが決める速度・持久・強制コスト・市場効率が主役）。
    /// すべて plain な float で受け渡す。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MobilizationDoctrineRules
    {
        /// <summary>
        /// 動員の立ち上がり速度（0..1）＝開戦からどれだけ速く戦力を動員できるか。国家統制力を基点に、
        /// 命令型は+commandSpeedBonus／市場型は−marketSpeedPenalty／混合型は素のまま＝<b>命令型は指令で一気に
        /// 立ち上がるが市場型は自発の積み上げで初動が遅い</b>。stateCapacity が高いほどどの様式でも速い。
        /// </summary>
        public static float MobilizationSpeed(MobilizationDoctrine doctrine, float stateCapacity, MobilizationDoctrineParams p)
        {
            float cap = Mathf.Clamp01(stateCapacity);
            switch (doctrine)
            {
                case MobilizationDoctrine.命令型: return Mathf.Clamp01(cap + p.commandSpeedBonus);
                case MobilizationDoctrine.市場型: return Mathf.Clamp01(cap - p.marketSpeedPenalty);
                default:                          return cap;
            }
        }

        public static float MobilizationSpeed(MobilizationDoctrine doctrine, float stateCapacity)
            => MobilizationSpeed(doctrine, stateCapacity, MobilizationDoctrineParams.Default);

        /// <summary>
        /// 長期戦での持久力（0..1）＝動員を長く支える力。経済の厚みを基点に、市場型は+marketSustainBonus／命令型は
        /// −commandSustainPenalty（統制疲れで息切れ）／混合型は素のまま＝<b>市場型は自発が続き伸び、命令型は統制疲れ
        /// で息切れする</b>。economicDepth が深いほどどの様式でも持久する。
        /// </summary>
        public static float SustainedCapacity(MobilizationDoctrine doctrine, float economicDepth, MobilizationDoctrineParams p)
        {
            float depth = Mathf.Clamp01(economicDepth);
            switch (doctrine)
            {
                case MobilizationDoctrine.市場型: return Mathf.Clamp01(depth + p.marketSustainBonus);
                case MobilizationDoctrine.命令型: return Mathf.Clamp01(depth - p.commandSustainPenalty);
                default:                          return depth;
            }
        }

        public static float SustainedCapacity(MobilizationDoctrine doctrine, float economicDepth)
            => SustainedCapacity(doctrine, economicDepth, MobilizationDoctrineParams.Default);

        /// <summary>
        /// 速度と持久力のトレードオフ指標（−1..+1）＝そのドクトリンが短期決戦向きか長期戦向きか。
        /// 命令型は+commandSpeedBonus+commandSustainPenalty（速いが持久に欠ける＝正＝短期向き）／
        /// 市場型は−(marketSpeedPenalty+marketSustainBonus)（遅いが持久に伸びる＝負＝長期向き）／混合型は0。
        /// <b>正なら短期決戦向き・負なら長期戦向き</b>を一目で示す。
        /// </summary>
        public static float SpeedVsSustainability(MobilizationDoctrine doctrine, MobilizationDoctrineParams p)
        {
            switch (doctrine)
            {
                case MobilizationDoctrine.命令型: return Mathf.Clamp(p.commandSpeedBonus + p.commandSustainPenalty, -1f, 1f);
                case MobilizationDoctrine.市場型: return Mathf.Clamp(-(p.marketSpeedPenalty + p.marketSustainBonus), -1f, 1f);
                default:                          return 0f;
            }
        }

        public static float SpeedVsSustainability(MobilizationDoctrine doctrine)
            => SpeedVsSustainability(doctrine, MobilizationDoctrineParams.Default);

        /// <summary>
        /// 命令型動員の強制コスト（0..1＝民の不満・効率低下）＝統制・徴用が反発と非効率を生む。命令型のみ
        /// intensity×coercionCostMax＝<b>強制が強いほどコストが嵩む</b>。市場型は自発ゆえコストなし、混合型は半分。
        /// 呼び出し側が支持低下・産出倍率の削りに使う。
        /// </summary>
        public static float CoercionCost(MobilizationDoctrine doctrine, float intensity, MobilizationDoctrineParams p)
        {
            float inten = Mathf.Clamp01(intensity);
            switch (doctrine)
            {
                case MobilizationDoctrine.命令型: return Mathf.Clamp01(inten * p.coercionCostMax);
                case MobilizationDoctrine.混合型: return Mathf.Clamp01(inten * p.coercionCostMax * 0.5f);
                default:                          return 0f;
            }
        }

        public static float CoercionCost(MobilizationDoctrine doctrine, float intensity)
            => CoercionCost(doctrine, intensity, MobilizationDoctrineParams.Default);

        /// <summary>
        /// 市場インセンティブ効率（0..1）＝市場型動員が価格・利潤でどれだけ効率的に資源を引き出すか。市場型は
        /// 1.0を基準に priceSignals×marketIncentiveScale を上乗せ的に効かせ＝<b>価格シグナルが効くほど自発が効率を
        /// 生む</b>。命令型は価格を使わないので基礎効率(1−marketIncentiveScale)止まり、混合型は中間。
        /// </summary>
        public static float MarketIncentiveEfficiency(MobilizationDoctrine doctrine, float priceSignals, MobilizationDoctrineParams p)
        {
            float signals = Mathf.Clamp01(priceSignals);
            float floor = 1f - p.marketIncentiveScale;
            switch (doctrine)
            {
                case MobilizationDoctrine.市場型: return Mathf.Clamp01(floor + signals * p.marketIncentiveScale);
                case MobilizationDoctrine.混合型: return Mathf.Clamp01(floor + signals * p.marketIncentiveScale * 0.5f);
                default:                          return Mathf.Clamp01(floor);
            }
        }

        public static float MarketIncentiveEfficiency(MobilizationDoctrine doctrine, float priceSignals)
            => MarketIncentiveEfficiency(doctrine, priceSignals, MobilizationDoctrineParams.Default);

        /// <summary>
        /// 政体と動員様式の適合（0..1）＝<b>専制は命令型・民主は市場型が向く</b>。authoritarianism（専制度0..1）が
        /// 高いほど命令型が適合し、低いほど市場型が適合する。命令型は authoritarianism、市場型は (1−authoritarianism)、
        /// 混合型は 1−|authoritarianism−0.5|＝中庸の政体（専制度0.5）で最大1.0・両極で0.5（どちらにもそこそこ合う）。
        /// </summary>
        public static float DoctrineFitForPolity(MobilizationDoctrine doctrine, float authoritarianism)
        {
            float auth = Mathf.Clamp01(authoritarianism);
            switch (doctrine)
            {
                case MobilizationDoctrine.命令型: return auth;
                case MobilizationDoctrine.市場型: return 1f - auth;
                default:                          return Mathf.Clamp01(1f - Mathf.Abs(auth - 0.5f));
            }
        }

        /// <summary>
        /// 戦争の長さに応じた総動員出力（0..1）＝<b>短期は速度が・長期は持久力が効く</b>。warDuration（0=短期決戦…
        /// 1=長期持久戦）で speed と sustainedCapacity を補間＝Lerp(speed, sustainedCapacity, warDuration)。
        /// 戦争が短いほど立ち上がり速度が、長引くほど持久力が総出力を決める＝命令型と市場型の優劣が長さで逆転する。
        /// </summary>
        public static float MobilizationOutput(float speed, float sustainedCapacity, float warDuration)
        {
            float s = Mathf.Clamp01(speed);
            float sc = Mathf.Clamp01(sustainedCapacity);
            float dur = Mathf.Clamp01(warDuration);
            return Mathf.Clamp01(Mathf.Lerp(s, sc, dur));
        }

        /// <summary>
        /// 命令型動員体制か（true＝統制・指令主体の動員）。speedVsSustainability が threshold を上回ると成立＝
        /// <b>速度に振り持久を犠牲にした短期決戦型の動員</b>。閾値以下は市場型・混合型寄り（持久重視）。
        /// </summary>
        public static bool IsCommandMobilization(MobilizationDoctrine doctrine, float threshold)
        {
            float tradeoff = SpeedVsSustainability(doctrine);
            float th = Mathf.Clamp(threshold, -1f, 1f);
            return tradeoff > th;
        }
    }
}
