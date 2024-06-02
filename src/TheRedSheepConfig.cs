using BepInEx.Configuration;

namespace LethalCompanyTheRedSheep;

public class TheRedSheepConfig : SyncedInstance<TheRedSheepConfig>
{
    public readonly ConfigEntry<bool> TheRedSheepEnabled;
    public readonly ConfigEntry<string> TheRedSheepSpawnRarity;
    
    public TheRedSheepConfig(ConfigFile cfg)
    {
        InitInstance(this);
        
        TheRedSheepEnabled = cfg.Bind(
            "The Red Sheep Spawn Values",
            "The Red Sheep Enabled",
            true,
            "Whether The Red Sheep is enabled (will spawn in games)."
        );
        
        TheRedSheepSpawnRarity = cfg.Bind(
            "The Red Sheep Spawn Values", 
            "VThe Red Sheep Spawn Rarity",
            "All:30",
            "Spawn weight of The Red Sheep on all moons. You can to add to it any moon, just follow the format (also needs LLL installed for LE moons to work with this config)."
        );
    }
}