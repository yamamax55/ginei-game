using UnityEngine;

namespace Ginei
{
    /// <summary>星間交易の調整係数（フェザーン型の対外交易）。</summary>
    public readonly struct TradeParams
    {
        /// <summary>交易量1あたりの総利得（双方で分け合う原資）。</summary>
        public readonly float gainPerVolume;
        /// <summary>補完性が利得を増幅する最大倍率（互いに無い物を持つ相手ほど儲かる）。</summary>
        public readonly float complementarityBonus;
        /// <summary>中立仲介者が抜く口銭の割合（フェザーンの取り分）。</summary>
        public readonly float brokerCut;
        /// <summary>依存が危険とみなされる閾値（自国経済に占める対一国交易の比率）。</summary>
        public readonly float dependenceThreshold;

        public TradeParams(float gainPerVolume, float complementarityBonus, float brokerCut, float dependenceThreshold)
        {
            this.gainPerVolume = Mathf.Max(0f, gainPerVolume);
            this.complementarityBonus = Mathf.Max(0f, complementarityBonus);
            this.brokerCut = Mathf.Clamp01(brokerCut);
            this.dependenceThreshold = Mathf.Clamp01(dependenceThreshold);
        }

        /// <summary>既定＝利得1.0/量・補完ボーナス+100%・口銭10%・依存閾値30%。</summary>
        public static TradeParams Default => new TradeParams(1f, 1f, 0.1f, 0.3f);
    }

    /// <summary>
    /// 星間交易の純ロジック（対外交易＝フェザーン型）。交易は双方に利得を生み（補完性が高いほど大きい）、
    /// 取り分は交渉力で割れる。交戦で断絶し（戦争の機会費用）、中立の仲介者は両陣営から口銭を抜いて肥える＝
    /// 戦争が続くほど仲介者だけが儲かる構造。一国への依存が深いと制裁・断絶が刺さる。域内市場
    /// （<see cref="MarketRules"/>＝需給価格）とは別系統＝国家間のフロー。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TradeRules
    {
        /// <summary>交易の総利得＝量×単価×（1＋補完ボーナス×補完性(0..1)）。</summary>
        public static float TotalGain(float volume, float complementarity, TradeParams p)
        {
            return Mathf.Max(0f, volume) * p.gainPerVolume * (1f + p.complementarityBonus * Mathf.Clamp01(complementarity));
        }

        public static float TotalGain(float volume, float complementarity)
            => TotalGain(volume, complementarity, TradeParams.Default);

        /// <summary>
        /// 自国側の取り分（0..総利得）。交渉力 bargainingPower(0..1、0.5=対等) で総利得を割る。
        /// 大国・代替先のある側が多く取る。
        /// </summary>
        public static float ShareOfGain(float totalGain, float bargainingPower)
        {
            return Mathf.Max(0f, totalGain) * Mathf.Clamp01(bargainingPower);
        }

        /// <summary>
        /// 仲介交易の口銭＝総利得×brokerCut。当事者同士が断絶している（直接交易できない）ときだけ
        /// 仲介者が成立し、残りを当事者が分ける＝戦争が仲介者を太らせる。
        /// </summary>
        public static float BrokerProfit(float totalGain, bool directTradeBlocked, TradeParams p)
        {
            if (!directTradeBlocked) return 0f; // 直接取引できるなら中抜きは要らない
            return Mathf.Max(0f, totalGain) * p.brokerCut;
        }

        public static float BrokerProfit(float totalGain, bool directTradeBlocked)
            => BrokerProfit(totalGain, directTradeBlocked, TradeParams.Default);

        /// <summary>交戦による交易断絶の損失＝失われる自国側取り分（戦争の機会費用の見える化）。</summary>
        public static float WarDisruptionLoss(float volume, float complementarity, float bargainingPower, TradeParams p)
        {
            return ShareOfGain(TotalGain(volume, complementarity, p), bargainingPower);
        }

        public static float WarDisruptionLoss(float volume, float complementarity, float bargainingPower)
            => WarDisruptionLoss(volume, complementarity, bargainingPower, TradeParams.Default);

        /// <summary>対一国の依存度（0..1）＝当該国との交易額÷自国の総交易額。総額0は0。</summary>
        public static float Dependence(float bilateralVolume, float totalTradeVolume)
        {
            float total = Mathf.Max(0f, totalTradeVolume);
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Max(0f, bilateralVolume) / total);
        }

        /// <summary>依存が危険水準か＝依存度が閾値以上（断絶・制裁が急所になる）。</summary>
        public static bool IsDangerouslyDependent(float bilateralVolume, float totalTradeVolume, TradeParams p)
        {
            return Dependence(bilateralVolume, totalTradeVolume) >= p.dependenceThreshold;
        }

        public static bool IsDangerouslyDependent(float bilateralVolume, float totalTradeVolume)
            => IsDangerouslyDependent(bilateralVolume, totalTradeVolume, TradeParams.Default);
    }
}
