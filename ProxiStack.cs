using System.Reflection;
using HarmonyLib;

namespace ProxiStack
{
    public class ProxiStackMod : IModApi
    {
        public const string HarmonyId = "com.elternvszombies.proxistack";

        private static ProxiStackMod _instance;
        public static ProxiStackMod Instance => _instance;

        public ProxiStackConfig Config { get; private set; }
        public string ModPath { get; private set; }

        public void InitMod(Mod _modInstance)
        {
            _instance = this;
            ModPath = _modInstance.Path;

            Log.Out("[ProxiStack] Initializing ProxiStack v1.1.9");

            Config = new ProxiStackConfig();
            Config.Load(System.IO.Path.Combine(ModPath, "ProxiStackConfig.xml"));

            Harmony harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            Log.Out("[ProxiStack] Harmony patches registered");

            // Registriere das NetPackage in bekannten Paket-Typen von NetPackageManager
            try
            {
                System.Type npm = typeof(NetPackageManager);
                var field = npm.GetField("knownPackageTypes", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (field == null)
                {
                    Log.Warning("[ProxiStack] NetPackageManager.knownPackageTypes field not found!");
                }
                else
                {
                    System.Collections.IDictionary dict = field.GetValue(null) as System.Collections.IDictionary;
                    if (dict == null)
                    {
                        Log.Warning("[ProxiStack] NetPackageManager.knownPackageTypes value is null!");
                    }
                    else
                    {
                        if (!dict.Contains("NetPackageProxiStack"))
                        {
                            dict.Add("NetPackageProxiStack", typeof(NetPackageProxiStack));
                            Log.Out("[ProxiStack] NetPackageProxiStack registered in knownPackageTypes successfully.");
                        }
                        else
                        {
                            Log.Out("[ProxiStack] NetPackageProxiStack was already present in knownPackageTypes.");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[ProxiStack] NetPackage registration failed with exception: {ex.Message}");
            }

            Log.Out("[ProxiStack] ProxiStack initialization complete");
        }
    }
}