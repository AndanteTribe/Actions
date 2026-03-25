using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AndanteTribe.TextMeshPro.Editor
{
    public static class FontAssetUpdater
    {
        private const string ParamsFileName = "tmp-font-update-params.txt";
        private const string ResultFileName = "tmp-font-asset-result.txt";

        public static void InsertCharacters()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var paramsFile = Path.Combine(projectRoot, ParamsFileName);
            var resultFile = Path.Combine(projectRoot, ResultFileName);

            if (!File.ReadAllLines(paramsFile).TryParseParams(out var fontAssetPaths, out var characters, out var includeFontFeatures, out var parseError))
            {
                Debug.LogError($"Failed to parse params file '{paramsFile}': {parseError}");
                WriteResult(resultFile, false, string.Empty);
                EditorApplication.Exit(1);
                return;
            }

            var overallResult = true;
            var missingCharsBuilder = new StringBuilder();

            AssetDatabase.Refresh();

            foreach (var assetPath in fontAssetPaths)
            {
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (fontAsset == null)
                {
                    Debug.LogError($"Could not load TMP_FontAsset at: {assetPath}");
                    overallResult = false;
                    continue;
                }

                var copy = ScriptableObject.CreateInstance<TMP_FontAsset>();
                EditorUtility.CopySerialized(fontAsset, copy);

                var success = copy.TryAddCharacters(characters, out var missing, includeFontFeatures);
                if (!success)
                {
                    overallResult = false;
                    Debug.LogWarning($"TryAddCharacters did not fully succeed for '{assetPath}'. Missing characters: '{missing}'");
                }

                if (!string.IsNullOrEmpty(missing))
                {
                    missingCharsBuilder.Append(missing);
                }

                EditorUtility.CopySerialized(copy, fontAsset);
                EditorUtility.SetDirty(fontAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            WriteResult(resultFile, overallResult, missingCharsBuilder.ToString());
            EditorApplication.Exit(0);
        }

        private static void WriteResult(string resultFile, bool result, string missingCharacters)
        {
            File.WriteAllText(resultFile, $"result={result.ToString().ToLower()}\nmissingCharacters={missingCharacters}");
        }
    }

    internal static class ParamsParser
    {
        internal static bool TryParseParams(this string[] lines, out string[] fontAssetPaths, out string characters, out bool includeFontFeatures, out string error)
        {
            fontAssetPaths = Array.Empty<string>();
            characters = string.Empty;
            includeFontFeatures = false;
            error = string.Empty;

            string rawPaths = string.Empty;
            string charsB64 = string.Empty;

            foreach (var line in lines)
            {
                if (line.StartsWith("fontAssetPaths=", StringComparison.Ordinal))
                    rawPaths = line.Substring("fontAssetPaths=".Length);
                else if (line.StartsWith("characters=", StringComparison.Ordinal))
                    charsB64 = line.Substring("characters=".Length);
                else if (line.StartsWith("includeFontFeatures=", StringComparison.Ordinal))
                    bool.TryParse(line.Substring("includeFontFeatures=".Length), out includeFontFeatures);
            }

            if (string.IsNullOrEmpty(rawPaths))
            {
                error = "Missing required field: fontAssetPaths";
                return false;
            }

            if (string.IsNullOrEmpty(charsB64))
            {
                error = "Missing required field: characters";
                return false;
            }

            fontAssetPaths = rawPaths.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            characters = Encoding.UTF8.GetString(Convert.FromBase64String(charsB64));
            return true;
        }
    }
}
