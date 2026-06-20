namespace Ginei
{
    /// <summary>
    /// 戦術ドクトリン（戦術姿勢・#2365 STSH-5）。陣形 <see cref="Formation"/> とは直交する次元＝
    /// 「どの陣形を組むか」とは別に「どう戦うか（火力を一点に集めるか／広く散らすか／速度で機動するか）」を表す。
    /// 集中＝火力集約（攻撃↑だが広域攻撃に脆い）／分散＝広く展開（生存性↑だが火力やや控えめ）／
    /// 機動＝速度重視（機動↑）。係数は <see cref="BattleDoctrineRules"/> が単一窓口（実効値パターン）。
    /// </summary>
    public enum BattleDoctrine
    {
        集中, // Concentration：火力を一点へ集約（攻撃↑・広域攻撃に脆い）
        分散, // Dispersal：広く展開（生存性↑・火力やや控えめ）
        機動  // Maneuver：速度重視（機動↑）
    }
}
