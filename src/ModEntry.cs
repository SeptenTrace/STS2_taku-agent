using MegaCrit.Sts2.Core.Modding;

namespace TakuAgentMod;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    private static bool _isInitialized;

    public static void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        // Add cards, relics, patches, and event registration here.
    }
}
