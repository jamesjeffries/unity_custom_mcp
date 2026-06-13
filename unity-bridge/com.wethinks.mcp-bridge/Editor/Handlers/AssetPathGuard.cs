using System.IO;

namespace WeThinks.Mcp.Editor
{
    /// <summary>
    /// Guards asset/file paths so the bridge only writes inside the project's
    /// Assets/ folder, preventing path traversal outside the project.
    /// </summary>
    internal static class AssetPathGuard
    {
        public static void RequireUnderAssets(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new System.ArgumentException("path is required");
            }

            string normalized = path.Replace('\\', '/');

            if (normalized.Contains(".."))
            {
                throw new System.UnauthorizedAccessException(
                    "Path traversal ('..') is not allowed.");
            }

            if (!normalized.StartsWith("Assets/") && normalized != "Assets")
            {
                throw new System.UnauthorizedAccessException(
                    $"Path must be inside the Assets/ folder: '{path}'");
            }

            // Confirm the resolved absolute path stays within the project's Assets.
            string projectAssets = Path.GetFullPath(UnityEngine.Application.dataPath)
                .Replace('\\', '/');
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName
                .Replace('\\', '/');
            string resolved = Path.GetFullPath(Path.Combine(projectRoot, normalized))
                .Replace('\\', '/');

            if (!resolved.StartsWith(projectAssets))
            {
                throw new System.UnauthorizedAccessException(
                    $"Resolved path escapes the Assets/ folder: '{path}'");
            }
        }
    }
}
