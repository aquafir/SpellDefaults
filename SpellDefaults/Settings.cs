using ACE.DatLoader.Entity;
using ACE.DatLoader.FileTypes;

namespace SpellDefaults;

public class Settings
{
    public List<SpellId> Spells { get; set; } = new();//Enum.GetValues<SpellId>().Where(x => x.ToString().Contains("Paragon")).ToList();
    public double Duration { get; set; } = TimeSpan.FromHours(24).TotalSeconds;
    //public SetSettings Sets {get;set;} = new();

    public Dictionary<EquipmentSet, List<SetTier>> Sets { get; set; } = new()
    {
        [EquipmentSet.Adepts] = new()
        {
            new(1, new()
            {
                SpellId.HarmSelf1,
                SpellId.AcidProtectionSelf1,
            }),
            new(3, new()
            {
                SpellId.HarmSelf3,
                SpellId.AcidProtectionSelf3,
            }),
            new(4, new()
            {
                SpellId.HarmSelf5,
                SpellId.AcidProtectionSelf5,
            }),
        },
        [(EquipmentSet)999] = new()
        {
            new(1, new()
            {
                SpellId.HarmSelf6,
                SpellId.FireProtectionSelf6,
            }),
            new(2, new()
            {
                SpellId.HarmSelf7,
                SpellId.FireProtectionSelf7,
            }),
        },
    };
}

public record struct SetTier(uint NumEquipped, List<SpellId> Spells);
