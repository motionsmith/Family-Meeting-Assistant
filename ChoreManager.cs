using System;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

public static class ChoreManager
{
    public static List<Chore> Chores { get; } = new List<Chore>();
    public static readonly Tool FileTaskTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "file_task",
            Description = "Adds a task to the Client task list. Replaces tasks with the same title.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "title", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "A short description that helps the Client remember what needs to be done to complete this task."
                                }
                            },
                            {
                                "due", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The date that the task is due, in the format MM/DD/YYYY."
                                }
                            }
                        },
                Required = new List<string> { "title" }
            }
        },
        Execute = File
    };
    public static readonly Tool CompleteTaskTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "complete_task",
            Description = "Removes a task from the Client's task list.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "title", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The title of the task to be removed."
                                }
                            }
                        },
                Required = new List<string> { "title" }
            }
        },
        Execute = Complete
    };
    public static readonly Tool ListTasksTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "list_tasks",
            Description = "Lists the tasks in the Client's task list."
        },
        Execute = List
    };
    
    public static object PromptList => string.Join('\n', Chores.Select(chore =>
    {
        var s = chore.Name;
        if (chore.DueDate.HasValue)
        {
            s += $" due {chore.DueDate.Value.ToShortDateString()}";
        }
        return s;
    }));

    public static async Task<Message> File(ToolCall toolCall, CancellationToken cancelToken)
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
        await SaveChoresAsync(cancelToken);
        return new Message
        {
            Content = $"Filed a task: {newChore.Name}",
            Role = Role.Tool,
            ToolCallId = toolCall.Id
        };
    }

    public static async Task<Message> Complete(ToolCall toolCall, CancellationToken cancelToken)
    {
        var argsJObj = JObject.Parse(toolCall.Function.Arguments);
        var completedChoreName = argsJObj["title"].ToString();
        var completeTaskContent = $"Completed the task to {completedChoreName}";
        var completedChores = Chores.Where(c => c.Name == completedChoreName).ToList();
        foreach (var completedChore in completedChores)
        {
            Chores.Remove(completedChore);
        }
        await SaveChoresAsync(cancelToken);
        return new Message
        {
            Content = completeTaskContent,
            Role = Role.Tool,
            ToolCallId = toolCall.Id,
            FollowUp = true // Ask assistant to follow up after this tool call.
        };
    }

    public static async Task<string> List(CancellationToken cancelToken)
    {
        var choreList = PromptList;
        return $"Client task list:\n{choreList}";
    }

    public static async Task<Message> List(ToolCall call, CancellationToken cancelToken)
    {
        var listChoresContent = await List(cancelToken);
        return new Message
        {
            Content = listChoresContent,
            Role = Role.Tool,
            ToolCallId = call.Id,
            FollowUp = true // Ask Assistant to follow up after this tool call.
        };
    }

    private static async Task SaveChoresAsync(CancellationToken cancelToken)
    {
        var choreFilePath = GetFilePath();
        var choresFileContents = string.Join('\n', Chores.Select(chore =>
        {
            var dueStr = chore.DueDate.HasValue ? chore.DueDate.Value.ToShortDateString() : "null";
            var s = $"{chore.Name},{dueStr}";
            return s;
        }));
        await System.IO.File.WriteAllTextAsync(choreFilePath, choresFileContents, cancelToken);
    }

    public static async Task LoadAsync(CancellationToken cancelToken)
    {
        var choresFilePath = GetFilePath();
        if (System.IO.File.Exists(choresFilePath) == false)
        {
            Chores.Add(new Chore("Come up with some chores."));
            await SaveChoresAsync(cancelToken);
        }
        var choresFileContents = await System.IO.File.ReadAllTextAsync(choresFilePath, cancelToken);
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