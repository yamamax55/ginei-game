using System;

namespace Ginei
{
    /// <summary>
    /// 会戦セットアップの保存用データ構造。
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int playerFaction;
        public string scenarioName;
        public string selectedAdmiral;
        // 今後の拡張用にオプション等を追加可能
    }
}
