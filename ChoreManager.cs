public static class ChoreManager
{
    public static List<Chore> Chores { get; } = new List<Chore>();
    public static object PromptList => string.Join('\n', Chores.Select(chore => chore.Name));

    public static void Add(Chore chore)
    {
        Chores.Add(chore);
        SaveChores();
    }

    public static void Complete(string choreName)
    {
        var chore = Chores.FirstOrDefault(chore => chore.Name == choreName);
        if (chore != null)
        {
            Chores.Remove(chore);
        }
    }

    private static void SaveChores()
    {
        var choreFilePath = GetFilePath();
        var choresFileContents = string.Join('\n', Chores.Select(chore => $"{chore.Name}"));
        File.WriteAllText(choreFilePath, choresFileContents);
    }

    public static void LoadChores()
    {
        var choresFilePath = GetFilePath();
        if (File.Exists(choresFilePath) == false)
        {
            Add(new Chore("Come up with some chores."));
        }
        var choresFileContents = File.ReadAllText(choresFilePath);
        Chores.AddRange(choresFileContents.Split('\n').Select(str => new Chore(str)));
    }

    private static string GetFilePath()
    {
        var appDataDirPath = Environment.SpecialFolder.ApplicationData.ToString();
        string appDataFullPath = Path.GetFullPath(appDataDirPath);
        return Path.Combine(appDataFullPath, "chores.txt");
    }
}

public class Chore
{
    public readonly string Name = string.Empty;

    public Chore(string name)
    {
        Name = name;
    }
}