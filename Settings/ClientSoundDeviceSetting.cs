using Newtonsoft.Json.Linq;

public class ClientSoundDeviceSetting : SettingBase
{
    public override Tool GetSettingTool { get; } = new Tool
    {
        Function = new ToolFunction
        {
            Name = "get_client_sound_device",
            Description = "Gets the current value of The Client's Sound Device. If this value doesn't match The Client's actual sound device, you can ask them to clarify and then change it."
        }
    };

    public override Tool SetSettingTool { get; } = new Tool
    {
        Function = new ToolFunction
        {
            Name = "set_client_sound_device",
            Description = "Sets the current value of The Client's Sound Device to match The Client's actual sound device.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                    {
                        "value", new ToolFunctionParameterProperty
                        {
                            Type = "string",
                            Enum = new List<string>{ "Unknown", "OpenAirSpeakers", "Headphones" },
                            Description = "The type of device that The Client hears your voice through. This impacts whether you are interruptable by voice."
                        }
                    }
                },
                Required = new List<string> { "value" }
            }
        }
    };
    public static SettingConfig SettingConfig { get; } = new SettingConfig("client-sound-device.csv", "0", typeof(ClientSoundDeviceSetting));

    public override string SerializedValue
    {
        get
        {
            return ((int)Value).ToString();
        }
        
        set
        {
            Value = (SoundDeviceTypes)Enum.Parse(typeof(SoundDeviceTypes), value);
        }
    }

    private SoundDeviceTypes TypedValue => (SoundDeviceTypes)Value;

    private bool sentIntroMessage;

    public ClientSoundDeviceSetting(string defaultValue) : base(defaultValue)
    {
    }

    public override Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        if (sentIntroMessage) return Task.FromResult(new Message[] { }.AsEnumerable());

        var content = $"The Client's Sound Device Setting value is {TypedValue}";
        if (TypedValue == SoundDeviceTypes.Unknown)
        {
            content += $"\nYou inquire whether they are using headphones.";
        }
        if (TypedValue == SoundDeviceTypes.Headphones)
        {
            content += $"\nYou are concerned whether they are still wearing headphones.";
        }
        var introMsg = new Message
        {
            Role = Role.System,
            Content = content
        };
        sentIntroMessage = true;
        return Task.FromResult(new Message[] { introMsg }.AsEnumerable());
    }
}

public enum SoundDeviceTypes
{
    Unknown,
    OpenAirSpeakers,
    Headphones
}