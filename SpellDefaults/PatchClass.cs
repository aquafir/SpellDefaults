using ACE.DatLoader.FileTypes;

namespace SpellDefaults;

[HarmonyPatch]
public class PatchClass
{
    #region Settings
    const int RETRIES = 10;

    public static Settings Settings = new();
    static string settingsPath => Path.Combine(Mod.ModPath, "Settings.json");
    private FileInfo settingsInfo = new(settingsPath);

    private JsonSerializerOptions _serializeOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private void SaveSettings()
    {
        string jsonString = JsonSerializer.Serialize(Settings, _serializeOptions);

        if (!settingsInfo.RetryWrite(jsonString, RETRIES))
        {
            ModManager.Log($"Failed to save settings to {settingsPath}...", ModManager.LogLevel.Warn);
            Mod.State = ModState.Error;
        }
    }

    private void LoadSettings()
    {
        if (!settingsInfo.Exists)
        {
            ModManager.Log($"Creating {settingsInfo}...");
            SaveSettings();
        }
        else
            ModManager.Log($"Loading settings from {settingsPath}...");

        if (!settingsInfo.RetryRead(out string jsonString, RETRIES))
        {
            Mod.State = ModState.Error;
            return;
        }

        try
        {
            Settings = JsonSerializer.Deserialize<Settings>(jsonString, _serializeOptions);
        }
        catch (Exception)
        {
            ModManager.Log($"Failed to deserialize Settings: {settingsPath}", ModManager.LogLevel.Warn);
            Mod.State = ModState.Error;
            return;
        }
    }
    #endregion

    #region Start/Shutdown
    public async void Start()
    {
        //Need to decide on async use
        Mod.State = ModState.Loading;
        LoadSettings();

        if (Mod.State == ModState.Error)
        {
            ModManager.DisableModByPath(Mod.ModPath);
            return;
        }

        await RunOnStartup();

        Mod.State = ModState.Running;
    }

    public void Shutdown()
    {
        //if (Mod.State == ModState.Running)
        // Shut down enabled mod...

        //If the mod is making changes that need to be saved use this and only manually edit settings when the patch is not active.
        //SaveSettings();

        if (Mod.State == ModState.Error)
            ModManager.Log($"Improper shutdown: {Mod.ModPath}", ModManager.LogLevel.Error);
    }
    #endregion

    static async Task RunOnStartup()
    {
        while (true)
        {
            if (WorldManager.WorldStatus == WorldManager.WorldStatusState.Open)
            {
                SetupSpells();
                break;
            }
            await Task.Delay(1000);
        }
    }

    private static void SetupSpells()
    {
        foreach (var spellId in Settings.Spells)
        {
            var spell = new Spell(spellId);
            var spellBase = spell._spellBase;

            spellBase.Duration = Settings.Duration;
        }

        OverrideSpellSets();
    }

    private static void OverrideSpellSets()
    {
        try
        {
            foreach (var setOverride in Settings.Sets)
            {
                var key = (uint)setOverride.Key;

                //Add missing sets
                if (!DatManager.PortalDat.SpellTable.SpellSet.TryGetValue(key, out var datSet))
                {
                    datSet = new();
                    DatManager.PortalDat.SpellTable.SpellSet.Add(key, datSet);
                }

                //Order overriding sets by tier
                var tiers = setOverride.Value.OrderBy(x => x.NumEquipped);
                var start = tiers.FirstOrDefault().NumEquipped;
                var end = tiers.LastOrDefault().NumEquipped;

                //Clear sets
                datSet.SpellSetTiers.Clear();
                datSet.SpellSetTiersNoGaps.Clear();
                datSet.HighestTier = end;

                foreach (var tier in tiers)
                {
                    //Make and add SpellSetTier
                    SpellSetTiers sst = new() { Spells = tier.Spells.Select(x => (uint)x).ToList() };
                    datSet.SpellSetTiers.TryAdd(tier.NumEquipped, sst);
                }

                //Create the no-gaps list using ACE logic.  Could be improved
                SpellSetTiers lastSpellSetTier = null;
                for (uint i = 0; i <= datSet.HighestTier; i++)
                {
                    if (datSet.SpellSetTiers.TryGetValue(i, out var spellSetTiers))
                        lastSpellSetTier = spellSetTiers;

                    if (lastSpellSetTier != null)
                        datSet.SpellSetTiersNoGaps.TryAdd(i, lastSpellSetTier);
                }
            }
        }
        catch (Exception ex)
        {
            ModManager.Log(ex.Message, ModManager.LogLevel.Error);
            return;
        }

        ModManager.Log($"Replaced {Settings.Sets.Count} EquipmentSets with a combined {Settings.Sets.Sum(x => x.Value.Count())} tiers and {Settings.Sets.Values.Select(y => y.Sum(z => z.Spells.Count()))}");
    }

    [CommandHandler("listset", AccessLevel.Player, CommandHandlerFlag.None)]
    public static void HandleSS(Session session, params string[] parameters)
    {
        var p = session.Player;
        var s = GetLastAppraisedObject(session);

        if (s is null || s.EquipmentSetId is null)
            return;

        if (!DatManager.PortalDat.SpellTable.SpellSet.TryGetValue((uint)s.EquipmentSetId, out var set))
            return;

        //ACE.Server.Entity.SpellSet loads from DatManager.PortalDat.SpellTable.SpellSet
        var sb = new StringBuilder($"\nEquipmentSet {s.EquipmentSetId}:");
        foreach (var tier in set.SpellSetTiersNoGaps)
        {
            sb.Append($"\nWearing >= {tier.Key}");
            foreach (var spellId in tier.Value.Spells)
            {
                Spell spell = new Spell(spellId);
                sb.Append($"\n  ({spellId}) {spell.Name}");
            }
        }
        p.SendMessage($"{sb}");
    }

    //[CommandHandler("setup-spells", AccessLevel.Developer, CommandHandlerFlag.None)]
    //public static void HandleSetupSpells(Session session, params string[] parameters)
    //{
    //    SetupSpells();
    //}

    //Lazy helper
    public static WorldObject GetLastAppraisedObject(Session session)
    {
        var targetID = session.Player.RequestedAppraisalTarget;
        if (targetID == null)
        {
            return null;
        }

        var target = session.Player.FindObject(targetID.Value, Player.SearchLocations.Everywhere, out _, out _, out _);
        if (target == null)
        {
            return null;
        }
        return target;
    }
}

