using System;
using System.IO;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using TakuAgentMod.Diagnostics;

namespace TakuAgentMod;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private static bool _isInitialized;
    private static Harmony? _harmony;

    public static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        WriteBootstrapMarker("Initialize() entered.");

        try
        {
            _harmony ??= new Harmony("io.retr0.sts2.taku_agent");
            _harmony.PatchAll(typeof(ModEntry).Assembly);

            WriteBootstrapMarker("Harmony patching completed.");
            BattleStateCaptureService.Log("Taku Agent initialized. Battle state capture service is active.");
        }
        catch (Exception ex)
        {
            WriteBootstrapMarker($"Initialize() failed: {ex}");
            throw;
        }
    }

    private static void WriteBootstrapMarker(string message)
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }

        string directory = Path.Combine(root, "STS2TakuAgent");
        Directory.CreateDirectory(directory);

        string line = $"[{DateTimeOffset.Now:O}] {message}{Environment.NewLine}";
        File.AppendAllText(Path.Combine(directory, "bootstrap.log"), line);
    }
}
