using System;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

public class ChoreManager
{
    public List<Chore> Chores { get; } = new List<Chore>();
    public object PromptList => string.Join('\n', Chores.Select(chore =>
    {
        var s = chore.Name;
        if (chore.DueDate.HasValue)
        {
            s += $" due {chore.DueDate.Value.ToShortDateString()}";
        }
        return s;
    }));

    public async Task<Message> File(ToolCall toolCall, CancellationToken cancelToken)
    {
        var argsJObj = JObject.Parse(toolCall.Function.Arguments);
        var newChoreName = argsJObj["title"].ToString();
        DateTime? dueDt = null;
        if (argsJObj.TryGetValue("due", out var val))
        {
            dueDt = DateTime.Parse(val.ToString());
        }
        var newChore = new Chore(newChoreName, dueDt);
        var dupes = Chores.Where(c => c.Name == newChore.Name).ToList();
        foreach (var dupe in dupes)
        {
            Chores.Remove(dupe);
        }
        Chores.Add(newChore);
        SaveChores();
        return new Message
        {
            Content = $"Filed a task: {newChore.Name}",
            Role = Role.Tool,
            ToolCallId = toolCall.Id
        };
    }

    public async Task<Message> Complete(ToolCall toolCall, CancellationToken cancelToken)
    {
        var argsJObj = JObject.Parse(toolCall.Function.Arguments);
        var completedChoreName = argsJObj["title"].ToString();
        var completeTaskContent = $"Completed the task to {completedChoreName}";
        var completedChores = Chores.Where(c => c.Name == completedChoreName).ToList();
        foreach (var completedChore in completedChores)
        {
            Chores.Remove(completedChore);
        }
        SaveChores();
        return new Message
        {
            Content = completeTaskContent,
            Role = Role.Tool,
            ToolCallId = toolCall.Id
        };
    }

    public async Task<string> List(CancellationToken cancelToken)
    {
        var choreList = PromptList;
        return $"Family task list:\n{choreList}";
    }

    public async Task<Message> List(ToolCall call, CancellationToken cancelToken)
    {
        var functionName = call.Function.Name;
        var arguments = call.Function.Arguments;
        var argsJObj = JObject.Parse(arguments);
        var listChoresContent = await List(cancelToken);
        return new Message
        {
            Content = listChoresContent,
            Role = Role.Tool,
            ToolCallId = call.Id
        };
    }

    private void SaveChores()
    {
        var choreFilePath = GetFilePath();
        var choresFileContents = string.Join('\n', Chores.Select(chore =>
        {
            var dueStr = chore.DueDate.HasValue ? chore.DueDate.Value.ToShortDateString() : "null";
            var s = $"{chore.Name},{dueStr}";
            return s;
        }));
        System.IO.File.WriteAllText(choreFilePath, choresFileContents);
    }

    public void LoadChores()
    {
        var choresFilePath = GetFilePath();
        if (System.IO.File.Exists(choresFilePath) == false)
        {
            Chores.Add(new Chore("Come up with some chores."));
            SaveChores();
        }
        var choresFileContents = System.IO.File.ReadAllText(choresFilePath);
        Chores.AddRange(choresFileContents.Split('\n').Select(str =>
        {
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

    private string GetFilePath()
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