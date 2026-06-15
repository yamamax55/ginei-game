using UnityEngine;

namespace Ginei
{
    /// <summary>間合いドクトリンの調整係数（#1384）。</summary>
    public readonly struct EngagementDistanceParams
    {
        /// <summary>射程が最適交戦距離へ寄与する重み（遠間＝射程の長い側が距離を遠く取る）。</summary>
        public readonly float rangeWeight;
        /// <summary>機動が最適交戦距離へ寄与する重み（機動が高いほど間合いを選べる＝やや遠めに振れる）。</summary>
        public readonly float mobilityWeight;
        /// <summary>間合いを制した効果の最大ボーナス（最適距離で戦う戦闘力上昇の上限）。</summary>
        public readonly float maxControlBonus;
        /// <summary>不得意な間合いで戦う不利の最大値（遠間の艦が近接戦を強いられた損失の上限）。</summary>
        public readonly float maxMismatchPenalty;
        /// <summary>間合い支配と判定する優位の既定閾値。</summary>
        public readonly float dominanceThreshold;

        public EngagementDistanceParams(float rangeWeight, float mobilityWeight, float maxControlBonus, float maxMismatchPenalty, float dominanceThreshold)
        {
            this.rangeWeight = Mathf.Clamp01(rangeWeight);
            this.mobilityWeight = Mathf.Clamp01(mobilityWeight);
            this.maxControlBonus = Mathf.Clamp01(maxControlBonus);
            this.maxMismatchPenalty = Mathf.Clamp01(maxMismatchPenalty);
            this.dominanceThreshold = Mathf.Clamp01(dominanceThreshold);
        }

        /// <summary>既定＝射程重み0.7・機動重み0.3・制した効果の上限0.3・不適合罰の上限0.4・支配閾値0.3。</summary>
        public static EngagementDistanceParams Default => new EngagementDistanceParams(0.7f, 0.3f, 0.3f, 0.4f, 0.3f);
    }

    /// <summary>
    /// 間合いドクトリン＝最適交戦距離の純ロジック（#1384・宮本武蔵『五輪書』の「間合い」）。武器・状況に応じた
    /// 最適な間合いがあり、それを制する者が勝つ＝遠間（射程の長い側が有利）・近間（接近戦が得意な側が有利）。
    /// 自分の得意な間合いに持ち込み、敵の間合いを外す。交戦距離は 0=近接〜1=遠距離 の正規化値で扱い、
    /// 各係数は基準値に掛ける実効倍率として返す（基準非破壊・実効値パターン・乱数なし決定論・全入力クランプ）。
    /// 分担：<c>WeaponArc</c> 系（射界＝射程・扇角の幾何／Game層）が物理的な命中可否を、
    /// <see cref="OperationalAptitudeRules"/> が提督×戦場の戦闘適性を、<c>BattleRhythmRules</c>（拍子・同EPIC GRN）が
    /// 攻め時の呼吸を、<c>FleetDoctrineRules</c> が艦隊運用方針（決戦/漸減/通商破壊…）を担う。
    /// ここは五輪書の「間合い」＝交戦距離そのものの最適化だけを扱う（射界の幾何でも適性でも拍子でもドクトリンでもない）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EngagementDistanceRules
    {
        /// <summary>
        /// 自軍の最適交戦距離(0=近接〜1=遠距離)＝武器の射程と機動から導く。射程が長ければ遠間で撃ち、
        /// 機動が高ければ間合いを選べる（やや遠めに寄せられる）。射程・機動が低い近接型は near=0 に寄る。
        /// 重みは <see cref="EngagementDistanceParams"/> で配分（合計が1超でも結果はクランプ）。
        /// </summary>
        public static float OptimalDistance(float weaponRange, float mobility, EngagementDistanceParams p)
        {
            float r = Mathf.Clamp01(weaponRange);
            float m = Mathf.Clamp01(mobility);
            return Mathf.Clamp01(r * p.rangeWeight + m * p.mobilityWeight);
        }

        public static float OptimalDistance(float weaponRange, float mobility)
            => OptimalDistance(weaponRange, mobility, EngagementDistanceParams.Default);

        /// <summary>
        /// 距離の優位(0..1)＝現在の交戦距離が自軍の最適に近く、敵の最適から遠いほど高い（自分の間合いで戦う）。
        /// 「自分への近さ」(1-|current-own|)と「敵からの遠さ」(|current-enemy|)の平均。1=完全に自分の間合い、
        /// 0=完全に敵の間合い。武器・状況に応じた最適間合いを制した度合いを一本の値に出す。
        /// </summary>
        public static float DistanceAdvantage(float currentDistance, float ownOptimal, float enemyOptimal)
        {
            float c = Mathf.Clamp01(currentDistance);
            float own = Mathf.Clamp01(ownOptimal);
            float enemy = Mathf.Clamp01(enemyOptimal);

            float ownCloseness = 1f - Mathf.Abs(c - own); // 自分の最適への近さ（1=ぴったり）。
            float enemyFarness = Mathf.Abs(c - enemy);     // 敵の最適からの遠さ（1=最大に外す）。
            return Mathf.Clamp01((ownCloseness + enemyFarness) * 0.5f);
        }

        /// <summary>
        /// 間合いを制した効果(>=1)＝最適な距離で戦うと戦闘力が上がる（実効倍率・基準非破壊）。
        /// 距離の優位が高いほど 1〜(1+maxControlBonus) へ線形に増す。優位0.5（中庸）でほぼ1.0近辺、
        /// 自分の間合い(優位1)で最大ボーナス、敵の間合い(優位0)では 1未満には下げず1.0据え置き＝
        /// 不利は <see cref="MismatchPenalty"/> 側で表現する（効果は上振れ専用）。
        /// </summary>
        public static float RangeControlEffect(float distanceAdvantage, EngagementDistanceParams p)
        {
            float adv = Mathf.Clamp01(distanceAdvantage);
            // 優位0で1.0、優位1で 1+maxControlBonus。
            return 1f + adv * p.maxControlBonus;
        }

        public static float RangeControlEffect(float distanceAdvantage)
            => RangeControlEffect(distanceAdvantage, EngagementDistanceParams.Default);

        /// <summary>
        /// 間合いを詰めるか開くか＝自分の最適へ向かう符号付き移動量(-1..1)。正＝間合いを詰める（近づく・近間へ）、
        /// 負＝間合いを開く（離れる・遠間へ）、0＝既に最適。大きさは「最適までの距離」×自軍機動（速い側ほど素早く
        /// 自分の間合いへ持ち込める）。近間が得意なら詰め、遠間が得意なら離れる。
        /// </summary>
        public static float ClosingOrOpening(float currentDistance, float ownOptimal, float ownMobility)
        {
            float c = Mathf.Clamp01(currentDistance);
            float own = Mathf.Clamp01(ownOptimal);
            float mob = Mathf.Clamp01(ownMobility);

            // current > own なら遠すぎる＝詰める(正)。current < own なら近すぎる＝開く(負)。
            float gap = c - own; // 正=遠すぎ、負=近すぎ。
            return Mathf.Clamp(gap * mob, -1f, 1f);
        }

        /// <summary>
        /// 不得意な間合いのペナルティ(0..maxMismatchPenalty)＝自分の最適から離れた距離で戦う不利
        /// （遠間の艦が近接戦を強いられる）。現在距離と自軍最適の隔たり|current-own|に比例し、ぴったりなら0、
        /// 最大に外されると上限。敵が間合いを外した結果の損失を減点係数として返す（基準非破壊）。
        /// </summary>
        public static float MismatchPenalty(float currentDistance, float ownOptimal, EngagementDistanceParams p)
        {
            float c = Mathf.Clamp01(currentDistance);
            float own = Mathf.Clamp01(ownOptimal);
            float gap = Mathf.Abs(c - own); // 0..1。
            return Mathf.Clamp01(gap) * p.maxMismatchPenalty;
        }

        public static float MismatchPenalty(float currentDistance, float ownOptimal)
            => MismatchPenalty(currentDistance, ownOptimal, EngagementDistanceParams.Default);

        /// <summary>
        /// 交戦距離の強要力(-1..1)＝機動で優る側が間合いの主導権を握る（速い側が距離を決める）。
        /// 正＝自軍が交戦距離を強要できる、負＝敵に強要される、0＝互角で主導権が定まらない。
        /// 自軍機動と敵機動の差そのもの（速い者が逃げる相手を捕まえ、不利な間合いを押し付ける）。
        /// </summary>
        public static float ForcingEngagement(float ownMobility, float enemyMobility)
        {
            float own = Mathf.Clamp01(ownMobility);
            float enemy = Mathf.Clamp01(enemyMobility);
            return Mathf.Clamp(own - enemy, -1f, 1f);
        }

        /// <summary>
        /// 接近戦を挑むか遠距離で撃つか(-1..1)＝得意間合いの押し付け合いの選択。正＝接近戦を挑むべき
        /// （自軍が近間型＝own&lt;enemy で敵を近接へ引きずり込む）、負＝遠距離で撃つべき（自軍が遠間型＝own&gt;enemy）、
        /// 0＝同じ間合いの真っ向勝負。自軍最適と敵最適の差で、互いに得意間合いを押し付け合う構図を出す。
        /// </summary>
        public static float InterceptVsStandoff(float ownOptimal, float enemyOptimal)
        {
            float own = Mathf.Clamp01(ownOptimal);
            float enemy = Mathf.Clamp01(enemyOptimal);
            // 自軍が近間型(own小)＝接近で敵を引き込む(正)。自軍が遠間型(own大)＝遠距離で撃つ(負)。
            return Mathf.Clamp(enemy - own, -1f, 1f);
        }

        /// <summary>
        /// 間合いを支配して有利に戦える判定。距離の優位が閾値を超えれば true＝自分の間合いに持ち込み敵の間合いを
        /// 外せている。閾値は呼び出し側で渡す（既定は <see cref="EngagementDistanceParams.dominanceThreshold"/>）。
        /// </summary>
        public static bool IsRangeDominant(float distanceAdvantage, float threshold)
        {
            float adv = Mathf.Clamp01(distanceAdvantage);
            float th = Mathf.Clamp01(threshold);
            return adv > th;
        }

        public static bool IsRangeDominant(float distanceAdvantage)
            => IsRangeDominant(distanceAdvantage, EngagementDistanceParams.Default.dominanceThreshold);
    }
}
