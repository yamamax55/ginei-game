using UnityEditor;

namespace Ginei
{
    /// <summary>
    /// `Assets/Art/Portraits/` 配下のテクスチャを自動で「透過 Sprite」として取り込む（③アート一貫性）。
    /// 生成した提督ポートレート（背景透過PNG）を置くだけで Sprite になり、AdmiralData.portrait に割当可能。
    /// 手動のインポート設定や .meta の作り込みを不要にする安全網。
    /// </summary>
    public class PortraitTextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("Assets/Art/Portraits/")) return;

            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;   // 透過を正しく扱う
            imp.mipmapEnabled = false;        // UI 用途＝ミップ不要
            imp.maxTextureSize = 1024;
        }
    }
}
