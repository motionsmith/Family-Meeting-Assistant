using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class SmithsonianDefaultCircumstance : Circumstance
{
    public override List<Tool> Tools {get; } = new ();
    public override string IntroDesc => introPrompt;
    protected override string ContextDesc => defaultContextPrompt;
    protected override string SaveString {get; set; } = string.Empty;
    public override Message PinnedMessage
    {
        get
        {
            var sb = new StringBuilder();
            sb.AppendLine(playerCoreDesc);
            sb.AppendLine(ContextDesc);
            return new Message
            {
                Role = Role.System,
                Content = sb.ToString()
            };
        }
    }
    
    protected override string SaveFileName => "smithsonian-default-state.csv";

    // State

    // Prompts
    private string playerCoreDesc = ErrorPrompt;
    private string introPrompt = ErrorPrompt;
    private string defaultContextPrompt = ErrorPrompt;

    public SmithsonianDefaultCircumstance(
        WeatherMessageProvider owmClient,
        NewsMessageProvider newsClient,
        SettingsManager settingsManager,
        ClientTaskService taskManager,
        ReminderService reminderService,
        TimeMessageProvider timeMessageProvider)
    {
        Tools.Add(owmClient.GetCurrentLocalWeatherTool);
        Tools.Add(newsClient.GetTopHeadlinesTool);
        Tools.AddRange(settingsManager.SettingsTools);
        Tools.Add(taskManager.ListTasksTool);
        Tools.Add(taskManager.FileTaskTool);
        Tools.Add(taskManager.CompleteTaskTool);
        Tools.Add(timeMessageProvider.GetTimeTool);
        Tools.Add(reminderService.CreateReminderTool);
        Tools.Add(reminderService.ListRemindersTool);
        Tools.Add(reminderService.CancelReminderTool);
    }

    public override async Task LoadStateAsync(CancellationToken cancelToken)
    {
        SaveString = await StringIO.LoadStateAsync(SaveString, SaveFileName, cancelToken);
        playerCoreDesc = await LoadPromptAsync("smithsonian-default-core.md", cancelToken);
        introPrompt = await LoadPromptAsync("smithsonian-default-intro.md", cancelToken);
        defaultContextPrompt = await LoadPromptAsync("smithsonian-default-context.md", cancelToken);
    }
}
