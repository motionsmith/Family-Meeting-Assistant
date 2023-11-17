using System;
public static class ChoreManager
{
    public static List<Chore> Chores { get; } = new List<Chore>();
    public static object PromptList => string.Join('\n', Chores.Select(chore => {
        var s = chore.Name;
        if (chore.DueDate.HasValue)
        {
            s += $" due {chore.DueDate.Value.ToShortDateString()}";
        }
        return s;
    }));

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
        SaveChores();
    }

    private static void SaveChores()
    {
        var choreFilePath = GetFilePath();
        var choresFileContents = string.Join('\n', Chores.Select(chore => {
            var dueStr = chore.DueDate.HasValue ? chore.DueDate.Value.ToShortDateString() : "null";
            var s = $"{chore.Name}, {dueStr}";
            return s;
    }));
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
        Chores.AddRange(choresFileContents.Split('\n').Select(str => {
            var s = str.Split(',');
            var choreName = s[0];
            DateTime? dueDt = null;
            if (DateTime.TryParse(s[1], out var dt))
            {
                dueDt = dt;
            }
            return new Chore(choreName, dueDt);
        }));
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
    public readonly DateTime? DueDate;

    public Chore(string name, DateTime? due = null)
    {
        Name = name;
        DueDate = due;
    }
}