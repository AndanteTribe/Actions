using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace AndanteTribe.TextMeshPro.Editor
{
    public static class FontAssetUpdater
    {
        public static void InsertCharacters()
        {
            var args = Environment.GetCommandLineArgs();

            var charactersFile = GetArg(args, "-charactersFile");
            var fontAssetPathsFile = GetArg(args, "-fontAssetPathsFile");
            var includeFontFeaturesStr = GetArg(args, "-includeFontFeatures") ?? "false";
            var outputPath = GetArg(args, "-outputPath");

            if (string.IsNullOrEmpty(charactersFile) || !File.Exists(charactersFile))
            {
                Debug.LogError("A valid -charactersFile argument is required.");
                EditorApplication.Exit(1);
                return;
            }

            if (string.IsNullOrEmpty(fontAssetPathsFile) || !File.Exists(fontAssetPathsFile))
            {
                Debug.LogError("A valid -fontAssetPathsFile argument is required.");
                EditorApplication.Exit(1);
                return;
            }

            var characters = File.ReadAllText(charactersFile);
            if (string.IsNullOrEmpty(characters))
            {
                Debug.LogError("The characters file is empty.");
                EditorApplication.Exit(1);
                return;
            }

            var fontAssetPaths = File.ReadAllLines(fontAssetPathsFile)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToArray();

            if (fontAssetPaths.Length == 0)
            {
                Debug.LogError("The font asset paths file contains no valid entries.");
                EditorApplication.Exit(1);
                return;
            }

            if (!bool.TryParse(includeFontFeaturesStr, out var includeFontFeatures))
            {
                includeFontFeatures = false;
            }

            AssetDatabase.Refresh();

            var overallResult = true;
            var missingCharacterSet = new HashSet<char>();

            foreach (var path in fontAssetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (asset == null)
                {
                    Debug.LogError($"Could not load TMP_FontAsset at path: {path}");
                    overallResult = false;
                    continue;
                }

                var result = asset.TryAddCharacters(characters, out var missingCharacters, includeFontFeatures);
                if (!result)
                {
                    overallResult = false;
                }

                if (!string.IsNullOrEmpty(missingCharacters))
                {
                    foreach (var c in missingCharacters)
                    {
                        missingCharacterSet.Add(c);
                    }
                }

                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(outputPath))
            {
                var allMissingCharacters = new string(missingCharacterSet.ToArray());
                using var writer = new StreamWriter(outputPath, append: false);
                writer.WriteLine($"result={overallResult.ToString().ToLower()}");
                writer.WriteLine($"missing-characters={allMissingCharacters}");
            }

            EditorApplication.Exit(0);
        }

        private static string GetArg(string[] args, string name)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == name && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
