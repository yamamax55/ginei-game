using UnityEngine;
using System.IO;

namespace Ginei
{
    /// <summary>
    /// JSON形式でのセーブ／ロードを管理するクラス。
    /// </summary>
    public static class SaveManager
    {
        private static string SavePath => Path.Combine(Application.persistentDataPath, "setup_save.json");

        /// <summary>
        /// 指定されたデータをファイルに保存します。
        /// </summary>
        public static void Save(SaveData data)
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
            Debug.Log("SaveManager: Setup saved to " + SavePath);
        }

        /// <summary>
        /// ファイルからデータを読み込みます。
        /// </summary>
        public static SaveData Load()
        {
            if (!HasSave()) return null;

            try
            {
                string json = File.ReadAllText(SavePath);
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError("SaveManager: Failed to load save data. " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// セーブファイルが存在するか確認します。
        /// </summary>
        public static bool HasSave()
        {
            return File.Exists(SavePath);
        }
    }
}
