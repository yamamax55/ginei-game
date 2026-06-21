using UnityEditor;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 階級章スプライト（Resources/RankInsignia/&lt;勢力&gt;/&lt;階級&gt;.png・#階級章）の自動インポート設定。
    /// PIL生成の透過PNGを <b>Sprite</b> として取り込む（既定の Default テクスチャだと Resources.Load&lt;Sprite&gt; が失敗するため）。
    /// 透過・ミップなし・非圧縮で小サイズUIアイコン向けに整える。手書き .meta は不要＝置けば自動でスプライト化。
    /// </summary>
    public class RankInsigniaImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/RankInsignia/")) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
