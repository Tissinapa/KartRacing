using UnityEditor;
using UnityEngine;

namespace Ilumisoft.ArcardeRacingKit.Editor.Internal
{
    public static class MenuItems
    {
        [MenuItem("Window/Arcade Racing Kit/Documentation")]
        static void ShowDocumentation()
        {
            var config = EditorAssetInfo.Find();

            if (config != null)
            {
                if (!string.IsNullOrEmpty(config.DocumentationURL))
                {
                    Application.OpenURL(config.DocumentationURL);
                }
                else
                {
                    AssetDatabase.OpenAsset(config.Documentation);
                }
            }
        }
    }
}