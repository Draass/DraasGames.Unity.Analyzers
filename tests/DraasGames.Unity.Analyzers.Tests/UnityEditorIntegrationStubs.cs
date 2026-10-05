namespace UnityEditor
{
    public class AssetPostprocessor
    {
    }
}

namespace UnityEditor.PackageManager
{
    public sealed class PackageInfo
    {
        public string? resolvedPath { get; set; }

        public static string? TestResolvedPath { get; set; }

        public static PackageInfo? FindForAssetPath(string assetPath)
        {
            return TestResolvedPath == null
                ? null
                : new PackageInfo { resolvedPath = TestResolvedPath };
        }
    }
}
