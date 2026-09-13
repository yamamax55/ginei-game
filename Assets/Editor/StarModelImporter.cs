using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 恒星/要塞の 3D モデル（<c>Assets/Resources/Models/Stars</c>・<c>.../Models/Fortress</c>）の
    /// インポート設定を自動で整える（#恒星モデル）。手作業の設定漏れで見た目が壊れるのを防ぐ。
    ///
    /// ・<b>Read/Write を有効</b>にする：実行時に頂点カラーを読んでコロナの色を決め、
    ///   要塞は主砲（発光サブメッシュ）の位置から向きを自動決定するため、メッシュが読める必要がある。
    /// ・カメラ/ライトは取り込まない：盤面の見え方を乱す余計なオブジェクトを持ち込ませない。
    /// ・アニメーションは取り込まない：静止モデルなので不要（インポート時間とサイズの節約）。
    ///
    /// FBX 自体には触れない（生成物は Blender 側の担当）。ここで変えるのは Unity のインポート設定だけ。
    /// </summary>
    public class StarModelImporter : AssetPostprocessor
    {
        private const string StarsFolder = "Assets/Resources/Models/Stars/";
        private const string FortressFolder = "Assets/Resources/Models/Fortress/";

        private void OnPreprocessModel()
        {
            if (assetImporter is not ModelImporter importer) return;
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(StarsFolder) && !path.StartsWith(FortressFolder)) return;

            bool changed = false;

            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (importer.importCameras) { importer.importCameras = false; changed = true; }
            if (importer.importLights) { importer.importLights = false; changed = true; }
            if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
            if (importer.importBlendShapes) { importer.importBlendShapes = false; changed = true; }

            // 頂点カラーは恒星の表面色そのもの。最適化で落とされないよう明示的に残す。
            if (importer.optimizeMeshVertices) { importer.optimizeMeshVertices = false; changed = true; }

            if (changed)
                Debug.Log($"[StarModelImporter] インポート設定を自動調整しました（Read/Write 有効・カメラ/ライト/アニメ無効）: {assetPath}");
        }
    }
}
