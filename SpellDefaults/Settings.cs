namespace SpellDefaults;

public class Settings
{
    public List<SpellId> Spells { get; set; } = Enum.GetValues<SpellId>().Where(x => x.ToString().Contains("Paragon")).ToList();
    public double Duration { get; set; } = TimeSpan.FromHours(24).TotalSeconds;
}