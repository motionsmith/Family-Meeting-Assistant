using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class SmithsonianDefaultCircumstance : Circumstance
{
    public override List<Tool> Tools {get; } = new() {
        ChoreManager.ListTasksTool,
        ChoreManager.FileTaskTool,
        ChoreManager.CompleteTaskTool
    };
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

    public SmithsonianDefaultCircumstance(OpenWeatherMapClient owmClient)
    {
        Tools.Add(owmClient.GetCurrentLocalWeatherTool);
    }

    public override int GetCircumstanceExitCondition(Message msg)
    {
        return 0;
    }

    public override async Task LoadStateAsync(CancellationToken cancelToken)
    {
        SaveString = await StringIO.LoadStateAsync(SaveString, SaveFileName, cancelToken);
        playerCoreDesc = await LoadPromptAsync("smithsonian-default-core.md", cancelToken);
        introPrompt = await LoadPromptAsync("smithsonian-default-intro.md", cancelToken);
    }
}
