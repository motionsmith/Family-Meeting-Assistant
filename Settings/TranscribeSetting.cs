public class TranscribeSetting : SettingBase
{
    public override Tool GetSettingTool { get; } = new Tool
    {
        Function = new ToolFunction
        {
            Name = "is_transcribe_active",
            Description = "True when The Client's transcription service is transcribing their speech. False when transcription service is not running. When false, The Client is muted."
        }
    };

    public override Tool SetSettingTool { get; } = new Tool
    {
        Function = new ToolFunction
        {
            Name = "set_transcribe_active",
            Description = "Activates or deactivates The Client's transcription service. When not active, The Client is essentially muted.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                    {
                        "value", new ToolFunctionParameterProperty
                        {
                            Type = "boolean",
                            Description = "True when The Client's transcription is active. The user is muted when false."
                        }
                    }
                },
                Required = new List<string> { "value" }
            }
        }
    };
    public static SettingConfig SettingConfig { get; } = new SettingConfig(null, null, typeof(TranscribeSetting));

    public override string SerializedValue
    {
        get
        {
            return ((bool)Value).ToString();
        }
        
        set
        {
            Value = bool.Parse(value);
        }
    }

    private bool TypedValue => (bool)Value;

    private bool sentIntroMessage;

    public TranscribeSetting() : base(true.ToString())
    {
    }

    public override Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        if (sentIntroMessage) return Task.FromResult(new Message[] { }.AsEnumerable());

        var content = $"Transcription is{(Value == false ? " not" : string.Empty)} running.";
        var introMsg = new Message
        {
            Role = Role.System,
            Content = content
        };
        sentIntroMessage = true;
        return Task.FromResult(new Message[] { introMsg }.AsEnumerable());
    }
}