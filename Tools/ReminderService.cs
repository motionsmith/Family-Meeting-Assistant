using System;
using System.ComponentModel;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ReminderService : IMessageProvider
{
    private static readonly string fileName = "reminders.json";
    public static async Task<ReminderService> CreateAsync(CancellationToken cancelToken)
    {
        var defaultContents = Serialize(null);
        var fileContents = await StringIO.LoadStateAsync(defaultContents, fileName, cancelToken);
        var remindersData = Deserialize(fileContents);
        var instance = new ReminderService(remindersData);
        return instance;
    }

    private static string Serialize(IEnumerable<ClientReminder> reminders)
    {
        var remindersResult = new RemindersResult(reminders);

        try
        {
            return JsonConvert.SerializeObject(remindersResult);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to serialize reminder data. {ex.Message}");
            throw;
        }
    }

    private static IEnumerable<ClientReminder> Deserialize(string reminderFileContents)
    {
        try
        {
            var x = JsonConvert.DeserializeObject<RemindersResult>(reminderFileContents);
            return x != null && x.Reminders != null ? x.Reminders : new List<ClientReminder>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to deserialize reminder data. {ex.Message}");
            throw;
        }
    }

    public Tool CreateReminderTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "create_reminder",
            Description = "Adds a new reminder.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "title", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "A short phrase that helps The Client remember something later."
                                }
                            },
                            {
                                "time", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The date and time that The Client will be alerted. Use the format MM/dd/yyyy h:mm:sstt."
                                }
                            }
                        },
                Required = new List<string> { "title", "time" }
            }
        }
    };
    public Tool CancelReminderTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "cancel_reminder",
            Description = "Removes a reminder. This reminder will no longer alert The Client.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "title", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The title of the reminder to be cancelled."
                                }
                            }
                        },
                Required = new List<string> { "title" }
            }
        }
    };

    public Tool ListRemindersTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "list_reminders",
            Description = "Lists the reminders."
        }
    };

    private readonly List<ClientReminder> clientReminders = new List<ClientReminder>();

    private ReminderService(IEnumerable<ClientReminder> reminders)
    {
        CreateReminderTool.Execute = CreateReminderAsync;
        ListRemindersTool.Execute = ListRemindersAsync;
        CancelReminderTool.Execute = CancelReminderAsync;

        clientReminders = new List<ClientReminder>(reminders);
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var now = DateTime.Now;
        var elapsedReminders = clientReminders.Where(r => r.Time < now).ToList();
        clientReminders.RemoveAll(r => r.Time < now);
        var elapsedMessages = elapsedReminders.Select(r => new Message
        {
            Role = Role.System,
            Content = GetReminderPrompt(r, "Reminder Elapsed"),
            FollowUp = true
        });

        if (elapsedMessages.Count() > 0)
        {
            await SaveAsync(cts.Token);
            return elapsedMessages;
        }
        return elapsedMessages;
    }

    private async Task<Message> CreateReminderAsync(ToolCall toolCall, CancellationToken cancelToken)
    {
        string prompt;
        var argsJObj = JObject.Parse(toolCall.Function.Arguments);
        try
        {
            var newReminder = new ClientReminder(
                argsJObj["title"].ToString(),
                DateTime.Parse((string)argsJObj["time"]));

            var dupes = clientReminders.Where(c => c.Title == newReminder.Title).ToList();
            foreach (var dupe in dupes)
            {
                clientReminders.Remove(dupe);
            }
            clientReminders.Add(newReminder);
            await SaveAsync(cancelToken);
            prompt = $"Reminder created.";

        }
        catch (Exception ex)
        {
            prompt = $"Could not create reminder. {ex.Message}";
        }

        return new Message
        {
            Content = prompt,
            Role = Role.Tool,
            ToolCallId = toolCall.Id,
            FollowUp = true
        };


    }

    private async Task<Message> CancelReminderAsync(ToolCall toolCall, CancellationToken cancelToken)
    {
        var argsJObj = JObject.Parse(toolCall.Function.Arguments);
        string prompt;
        try
        {
            var reminderTitle = argsJObj["title"].ToString();
            var canceledReminders = clientReminders.Where(c => c.Title == reminderTitle).ToList();
            foreach (var canceledReminder in canceledReminders)
            {
                clientReminders.Remove(canceledReminder);
            }
            await SaveAsync(cancelToken);
            if (canceledReminders == null || canceledReminders.Count() == 0)
            {
                prompt = $"The System failed to cancel the reminder. It did not find any reminders with the title {reminderTitle}";
            }
            else
            {
                prompt = "Canceled the reminder.";
            }
        }
        catch (Exception ex)
        {
            prompt = $"The System failed to cancel the reminder. {ex.Message}";
        }
        return new Message
        {
            Content = prompt,
            Role = Role.Tool,
            ToolCallId = toolCall.Id,
            FollowUp = true // Ask assistant to follow up after this tool call.
        };
    }

    private string GetRemindersListPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("| Reminder | Time |");
        sb.AppendLine($"|---|---|");
        foreach (var r in clientReminders)
            sb.AppendLine($"| {r.Title} | {r.Time} |");
        return sb.ToString();
    }

    private string GetReminderPrompt(ClientReminder reminder, string h1)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {h1}");
        sb.AppendLine("| Reminder Title | Time |");
        sb.AppendLine($"|---|---|");
        sb.AppendLine($"| {reminder.Title} | {reminder.Time} |");
        return sb.ToString();
    }

    private Task<Message> ListRemindersAsync(ToolCall call, CancellationToken cancelToken)
    {
        var msg = new Message
        {
            Content = GetRemindersListPrompt(),
            Role = Role.Tool,
            ToolCallId = call.Id,
            FollowUp = true // Ask Assistant to follow up after this tool call.
        };
        return Task.FromResult(msg);
    }

    private async Task SaveAsync(CancellationToken cancelToken)
    {
        var tasksFileContents = Serialize(clientReminders);
        await StringIO.SaveStateAsync(tasksFileContents, fileName, cancelToken);
    }
}

public class ClientReminder
{
    [JsonProperty("title", Required = Required.Always)]
    public string Title { get; set; }
    [JsonProperty("time", Required = Required.Always)]
    public readonly DateTime Time;

    public ClientReminder(string title, DateTime time)
    {
        Title = title;
        Time = time;
    }
}

public class RemindersResult
{
    [JsonProperty("reminders", Required = Required.Always)]
    public List<ClientReminder> Reminders { get; set; } = new List<ClientReminder>();

    public RemindersResult(IEnumerable<ClientReminder>? reminders)
    {
        if (reminders != null) Reminders.AddRange(reminders);
    }
}