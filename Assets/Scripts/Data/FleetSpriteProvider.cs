using System.Collections.Generic;
using UnityEngine;

namespace Ginei.Data
{
    /// <summary>
    /// 勢力から艦隊表示用スプライトを解決する単一窓口。
    /// Resources 参照をここへ集約し、戦略マップと会戦で同じキャッシュを共有する。
    /// </summary>
    public static class FleetSpriteProvider
    {
        private static readonly Dictionary<Faction, Sprite> byFaction = new Dictionary<Faction, Sprite>();
        private static readonly Dictionary<string, Sprite> byName = new Dictionary<string, Sprite>();
        private static bool loaded;

        /// <summary>既定画像を一度だけ読み込む。呼び出し側は未登録時の null を処理する。</summary>
        public static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            LoadDefault(Faction.帝国, "Ships/ImperialFlagship");
            LoadDefault(Faction.同盟, "Ships/AllianceFlagship");
        }

        public static Sprite SpriteForFaction(Faction faction)
        {
            EnsureLoaded();
            return byFaction.TryGetValue(faction, out Sprite sprite) ? sprite : null;
        }

        public static Sprite SpriteForFactionName(string factionName)
        {
            if (string.IsNullOrEmpty(factionName)) return null;
            EnsureLoaded();
            return byName.TryGetValue(factionName, out Sprite sprite) ? sprite : null;
        }

        /// <summary>
        /// テストまたは動的コンテンツ用の手動登録。手動登録後は Resources を走査しない。
        /// </summary>
        public static void Register(Faction faction, Sprite sprite)
        {
            loaded = true;
            Set(faction, faction.ToString(), sprite);
        }

        /// <summary>enum を持たない追加勢力を名前で登録する。</summary>
        public static void Register(string factionName, Sprite sprite)
        {
            if (string.IsNullOrEmpty(factionName)) return;
            loaded = true;
            if (sprite == null) byName.Remove(factionName);
            else byName[factionName] = sprite;
        }

        /// <summary>キャッシュを破棄する。次の解決時に既定画像を再読込する。</summary>
        public static void Clear()
        {
            byFaction.Clear();
            byName.Clear();
            loaded = false;
        }

        private static void LoadDefault(Faction faction, string resourcePath)
        {
            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            Set(faction, faction.ToString(), sprite);
        }

        private static void Set(Faction faction, string factionName, Sprite sprite)
        {
            if (sprite == null)
            {
                byFaction.Remove(faction);
                byName.Remove(factionName);
                return;
            }

            byFaction[faction] = sprite;
            byName[factionName] = sprite;
        }
    }
}
