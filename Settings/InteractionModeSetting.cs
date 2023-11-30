public class InteractionModeSetting : SettingBase
{
    public override Tool GetSettingTool { get; } = new Tool
    {
        Function = new ToolFunction
        {
            Name = "get_interaction_mode",
            Description = "Gets the current interaction mode for the Assistant: Converse, Listen or Ignore"
        }
    };

    public override Tool SetSettingTool { get; } = new Tool
    {
        Function = new ToolFunction
        {
            Name = "set_interaction_mode",
            Description = "Sets the current interaction mode for the Assistant: Converse, Listen, or Ignore ",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                    {
                        "value", new ToolFunctionParameterProperty
                        {
                            Type = "string",
                            Enum = new List<string>{ "Converse", "Listen", "Ignore" },
                            Description = "The interaction mode for the Assistant—controls how the Assistant listens and responds."
                        }
                    }
                },
                Required = new List<string> { "value" }
            }
        }
    };
    public static SettingConfig SettingConfig { get; } = new SettingConfig("interaction-mode-setting.csv", InteractionMode.Ignore.ToString(), typeof(InteractionModeSetting));

    public override string SerializedValue
    {
        get
        {
            return Value.ToString();
        }
        
        set
        {
            Value = (InteractionMode)Enum.Parse(typeof(InteractionMode), value);
        }
    }

    private InteractionMode TypedValue => (InteractionMode)Value;

    private bool sentIntroMessage;

    public InteractionModeSetting(string startingValue) : base(startingValue)
    {
    }

    public override Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        if (sentIntroMessage) return Task.FromResult(new Message[] { }.AsEnumerable());

        var content = $"The Interaction Mode setting is {TypedValue}";
        var introMsg = new Message
        {
            Role = Role.System,
            Content = content
        };
        sentIntroMessage = true;
        return Task.FromResult(new Message[] { introMsg }.AsEnumerable());
    }
}

public enum InteractionMode
{
    Converse,
    Listen,
    Ignore
}