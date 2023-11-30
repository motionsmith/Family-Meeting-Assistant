using System;
using System.ComponentModel;
using Newtonsoft.Json.Linq;

public class ClientTaskService : IMessageProvider
{
    private static readonly string fileName = "tasks.csv";
    public static async Task<ClientTaskService> CreateAsync(CancellationToken cancelToken)
    {
        var fileContents = await StringIO.LoadStateAsync("Win the game!,null", fileName, cancelToken);
        var instance = new ClientTaskService(fileContents);
        return instance;
    }
    
    public Tool FileTaskTool = new Tool
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
        }
    };
    public Tool CompleteTaskTool = new Tool
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
        }
    };

    public Tool ListTasksTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "list_tasks",
            Description = "Lists the tasks in the Client's task list."
        }
    };

    public object PromptList => string.Join('\n', clientTasks.Select(clientTask =>
    {
        var s = clientTask.Name;
        if (clientTask.DueDate.HasValue)
        {
            s += $" (due {clientTask.DueDate.Value.ToShortDateString()})";
        }
        return s;
    }));

    private readonly List<ClientTask> clientTasks = new List<ClientTask>();
    private bool sentIntroMessage = false;

    private ClientTaskService(string fileContents)
    {
        FileTaskTool.Execute = File;
        ListTasksTool.Execute = List;
        CompleteTaskTool.Execute = Complete;

        clientTasks.AddRange(fileContents.Split('\n').Select(str =>
            {
                var s = str.Split(',');
                var taskName = s[0];
                DateTime? dueDt = null;
                if (DateTime.TryParse(s[1], out var dt))
                {
                    dueDt = dt;
                }
                return new ClientTask(taskName, dueDt);
            }));
    }


    public Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        if (sentIntroMessage) return Task.FromResult(new Message[] { }.AsEnumerable());
        var introMsg = new Message
        {
            Role = Role.System,
            Content = GetTaskListPrompt()
        };
        sentIntroMessage = true;
        return Task.FromResult(new Message[] { introMsg }.AsEnumerable());
    }

    private async Task<Message> File(ToolCall toolCall, CancellationToken cancelToken)
    {
        var argsJObj = JObject.Parse(toolCall.Function.Arguments);
        var newTaskName = argsJObj["title"].ToString();
        DateTime? dueDt = null;
        if (argsJObj.TryGetValue("due", out var val))
        {
            dueDt = DateTime.Parse(val.ToString());
        }
        var newTask = new ClientTask(newTaskName, dueDt);
        var dupes = clientTasks.Where(c => c.Name == newTask.Name).ToList();
        foreach (var dupe in dupes)
        {
            clientTasks.Remove(dupe);
        }
        clientTasks.Add(newTask);
        await SaveTasksAsync(cancelToken);
        return new Message
        {
            Content = $"Filed a task: {newTask.Name}\nBriefly confirm.",
            Role = Role.Tool,
            ToolCallId = toolCall.Id,
            FollowUp = true
        };
    }

    private async Task<Message> Complete(ToolCall toolCall, CancellationToken cancelToken)
    {
        var argsJObj = JObject.Parse(toolCall.Function.Arguments);
        var completedTaskName = argsJObj["title"].ToString();
        var completeTaskContent = $"Completed the task to {completedTaskName}";
        var completedTasks = clientTasks.Where(c => c.Name == completedTaskName).ToList();
        foreach (var completedTask in completedTasks)
        {
            clientTasks.Remove(completedTask);
        }
        await SaveTasksAsync(cancelToken);
        return new Message
        {
            Content = completeTaskContent,
            Role = Role.Tool,
            ToolCallId = toolCall.Id,
            FollowUp = true // Ask assistant to follow up after this tool call.
        };
    }

    private string GetTaskListPrompt()
    {
        var taskList = PromptList;
        return $"Client task list:\n{taskList}";
    }

    private Task<Message> List(ToolCall call, CancellationToken cancelToken)
    {
        var msg = new Message
        {
            Content = GetTaskListPrompt(),
            Role = Role.Tool,
            ToolCallId = call.Id,
            FollowUp = true // Ask Assistant to follow up after this tool call.
        };
        return Task.FromResult(msg);
    }

    private async Task SaveTasksAsync(CancellationToken cancelToken)
    {
        var tasksFileContents = string.Join('\n', clientTasks.Select(clientTask =>
        {
            var dueStr = clientTask.DueDate.HasValue ? clientTask.DueDate.Value.ToShortDateString() : "null";
            var s = $"{clientTask.Name},{dueStr}";
            return s;
        }));
        await StringIO.SaveStateAsync(tasksFileContents, fileName, cancelToken);
    }
}

public class ClientTask
{
    public readonly string Name = string.Empty;
    public readonly DateTime? DueDate;

    public ClientTask(string name, DateTime? due = null)
    {
        Name = name;
        DueDate = due;
    }
}