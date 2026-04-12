#nullable enable
using System;
using System.IO;
using UnityEngine;

namespace DogGame.Lua
{
    public static class LuaScriptLoader
    {
        private static readonly string[] SearchRoots =
        {
            "A_Dogs_Tale/Scripts/Lua/LuaCodeExamples.lua",
            "A_Dogs_Tale/Scripts/Lua"
        };

        public static bool TryLoad(string fileNameLua, out string source, out string friendlyName, out string? error)
        {
            source = string.Empty;
            friendlyName = string.Empty;
            error = null;

            if (string.IsNullOrWhiteSpace(fileNameLua))
            {
                error = "Lua filename is empty.";
                return false;
            }

            string normalized = NormalizeFileName(fileNameLua);
            if (TryResolveExistingPath(normalized, out string resolvedPath))
            {
                source = File.ReadAllText(resolvedPath);
                friendlyName = Path.GetFileName(resolvedPath);
                return true;
            }

            error = $"Lua script '{fileNameLua}' was not found under Assets/A_Dogs_Tale/Scripts/Lua.";
            return false;
        }

        private static string NormalizeFileName(string fileNameLua)
        {
            string trimmed = fileNameLua.Trim();
            if (Path.HasExtension(trimmed))
                return trimmed;

            return trimmed + ".lua";
        }

        private static bool TryResolveExistingPath(string normalizedFileName, out string resolvedPath)
        {
            resolvedPath = string.Empty;

            if (Path.IsPathRooted(normalizedFileName) && File.Exists(normalizedFileName))
            {
                resolvedPath = normalizedFileName;
                return true;
            }

            string assetsRoot = Application.dataPath;
            for (int i = 0; i < SearchRoots.Length; i++)
            {
                string candidate = Path.Combine(assetsRoot, SearchRoots[i], normalizedFileName);
                if (!File.Exists(candidate))
                    continue;

                resolvedPath = candidate;
                return true;
            }

            if (normalizedFileName.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
                string candidate = Path.Combine(projectRoot, normalizedFileName);
                if (File.Exists(candidate))
                {
                    resolvedPath = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
