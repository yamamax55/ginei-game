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
        // 多勢力対応：選択した FactionData の名前（Resources/Factions から復元する）。
        // 空なら playerFaction(enum) にフォールバック（旧セーブとの後方互換）。
        public string playerFactionName;
        public string scenarioName;
        public string selectedAdmiral;
        // 今後の拡張用にオプション等を追加可能
    }
}
