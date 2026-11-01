using UnityEditor;

namespace MagicTree.EditorTools
{
    /// <summary>
    /// Assets/Resources/Art 의 승인 이미지를 항상 같은 설정으로 임포트한다.
    /// - Sprite 타입: 2의 거듭제곱이 아닌 크기(1254, 1536×2736 등)를 억지로 늘리지 않아 비율이 유지된다.
    /// - 최대 4096: 배경(2736px 높이)이 2048로 줄지 않게 한다.
    /// - 밉맵 끔(UI 전용), 알파는 투명으로 처리.
    /// 이미 임포트된 뒤에 이 파일이 추가됐다면 Art 폴더를 우클릭 → Reimport 하면 적용된다.
    /// </summary>
    public class ArtImportSettings : AssetPostprocessor
    {
        const string ArtFolder = "Assets/Resources/Art/";

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(ArtFolder)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }
}
