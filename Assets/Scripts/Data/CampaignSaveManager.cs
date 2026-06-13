using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦役セーブのファイル入出力＋SO解決（FND-2 #495・Unity層）。世界状態（<see cref="CampaignState"/>）を
    /// `persistentDataPath/campaign_save.json` にJSON保存/読込する（変換は <see cref="CampaignSerializer"/>）。
    /// 復元時に星系の所有 FactionData を `Resources/Factions` から<b>名前で解決</b>する（直列化は名前のみ＝SO参照を持たない）。
    /// 設定/戦績は `GameSettings`、会戦セットアップは `SaveManager`、戦役世界状態はこちら（FND-2＝CampaignState 一本化）。
    /// </summary>
    public static class CampaignSaveManager
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "campaign_save.json");

        /// <summary>戦役の世界状態をJSON保存する。</summary>
        public static void Save(CampaignState campaign)
        {
            if (campaign == null) return;
            string json = CampaignSerializer.ToJson(campaign, prettyPrint: true);
            File.WriteAllText(SavePath, json);
        }

        /// <summary>戦役の世界状態＋ネームド人物ロスターをJSON保存する（提督/文官を継続するための版）。</summary>
        public static void Save(CampaignState campaign, IEnumerable<Person> people)
        {
            if (campaign == null) return;
            File.WriteAllText(SavePath, CampaignSerializer.ToJson(campaign, people, prettyPrint: true));
        }

        /// <summary>セーブから人物ロスターのみ復元する（無ければ空）。世界状態は <see cref="Load"/>。</summary>
        public static List<Person> LoadPeople()
        {
            if (!File.Exists(SavePath)) return new List<Person>();
            CampaignSaveData save = CampaignSerializer.Parse(File.ReadAllText(SavePath));
            return CampaignSerializer.ReadPeople(save);
        }

        /// <summary>セーブが存在するか。</summary>
        public static bool HasSave() => File.Exists(SavePath);

        /// <summary>戦役の世界状態を読み込む（無ければ null）。所有 FactionData は名前で解決する。</summary>
        public static CampaignState Load()
        {
            if (!File.Exists(SavePath)) return null;
            string json = File.ReadAllText(SavePath);
            CampaignSaveData save = CampaignSerializer.Parse(json);
            if (save == null) return null;

            CampaignState campaign = CampaignSerializer.FromSaveData(save);
            ResolveFactionData(campaign, save);
            return campaign;
        }

        /// <summary>セーブを削除する。</summary>
        public static void Delete()
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }

        /// <summary>
        /// 復元後、平データの factionName を頼りに星系の <see cref="StarSystem.ownerData"/> を解決する（FND-1 #496＝索引は
        /// <see cref="ContentDatabase"/> に集約）。直列化は名前のみ＝多勢力対応。見つからなければ enum owner のまま＝後方互換。
        /// </summary>
        private static void ResolveFactionData(CampaignState campaign, CampaignSaveData save)
        {
            if (campaign == null || campaign.map == null || save == null) return;
            for (int i = 0; i < save.systems.Count; i++)
            {
                StarSystemSave ss = save.systems[i];
                if (ss == null || string.IsNullOrEmpty(ss.ownerFactionName)) continue;
                StarSystem sys = campaign.map.GetSystem(ss.id);
                if (sys == null) continue;
                FactionData fd = ContentDatabase.FactionByName(ss.ownerFactionName);
                if (fd != null) sys.ownerData = fd;
            }
        }
    }
}
