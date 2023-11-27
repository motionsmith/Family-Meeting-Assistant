using Newtonsoft.Json.Linq;

public class GptModelSetting : SettingBase
{
    public override Tool GetSettingTool { get; } = new Tool
    {
        Function = new ToolFunction
        {
            Name = "get_current_gpt_model",
            Description = "Gets the current GPT model that is being used for Chat Completion."
        }
    };

    public override Tool SetSettingTool { get; } = new Tool
    {
        Function = new ToolFunction
        {
            Name = "set_current_gpt_model",
            Description = "Gets the current GPT model that is being used for Chat Completion.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                    {
                        "value", new ToolFunctionParameterProperty
                        {
                            Type = "string",
                            Enum = new List<string>{ "GPT-3.5", "GPT-4" },
                            Description = "The model to be used for Chat Completion. Some models are cheapter, some are more intelligent."
                        }
                    }
                },
                Required = new List<string> { "value" }
            }
        }
    };
    public static SettingConfig SettingConfig { get; } = new SettingConfig("gpt-model-setting.csv", "GPT-4", typeof(GptModelSetting));

    public override string SerializedValue
    {
        get
        {
            return (GptModel)Value == GptModel.Gpt35 ? "GPT-3.5" : "GPT-4";
        }
        
        set
        {
            Value = value == "GPT-3.5" ? GptModel.Gpt35 : GptModel.Gpt4;
        }
    }

    private GptModel TypedValue => (GptModel)Value;

    private bool sentIntroMessage;

    public GptModelSetting(string startingValue) : base(startingValue)
    {
    }

    public override Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        if (sentIntroMessage) return Task.FromResult(new Message[] { }.AsEnumerable());

        var content = $"The Gpt Model Setting is {TypedValue}";
        var introMsg = new Message
        {
            Role = Role.System,
            Content = content
        };
        sentIntroMessage = true;
        return Task.FromResult(new Message[] { introMsg }.AsEnumerable());
    }
}

public enum GptModel
{
    Gpt35,
    Gpt4
}