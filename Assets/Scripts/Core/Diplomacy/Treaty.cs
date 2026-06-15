using System;

namespace Ginei
{
    /// <summary>
    /// 外交条約の純データ（外交EPIC #189・DIP-2 #191）。
    /// 2勢力間に結ばれる1本の条約＝種別＋当事者＋存続ターン数。
    /// 既存の <see cref="DiplomacyState"/> は編集せず、こちらは独立した値オブジェクトとして完結する。
    /// 数値ロジック（修正子・レバレッジ・違約判定）は <see cref="TreatyRules"/> が唯一の窓口。
    /// </summary>
    [Serializable]
    public class Treaty
    {
        /// <summary>条約種別。</summary>
        public TreatyType type;
        /// <summary>当事者A（勢力名）。</summary>
        public string aName;
        /// <summary>当事者B（勢力名）。</summary>
        public string bName;
        /// <summary>存続ターン数（0以下＝無期限）。</summary>
        public int durationTurns;

        public Treaty() { }

        public Treaty(TreatyType type, string aName, string bName, int durationTurns = 0)
        {
            this.type = type;
            this.aName = aName;
            this.bName = bName;
            this.durationTurns = durationTurns;
        }

        /// <summary>無期限の条約か（存続ターンが指定されていない）。</summary>
        public bool IsPerpetual => durationTurns <= 0;

        /// <summary>指定勢力が当事者か（無向）。</summary>
        public bool Involves(string faction)
            => faction == aName || faction == bName;
    }
}
