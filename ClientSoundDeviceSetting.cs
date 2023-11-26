


using Newtonsoft.Json.Linq;

public class ClientSoundDeviceSetting : IMessageProvider
{
    public enum SoundDeviceTypes
    {
        Unknown,
        OpenAirSpeakers,
        Headphones
    }

    private static readonly string fileName = "client-sound-device.csv";

    public readonly Tool GetSettingTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "get_client_sound_device",
            Description = "Gets the current value of The Client's Sound Device. If this value doesn't match The Client's actual sound device, you can ask them to clarify and then change it."
        }
    };

    public readonly Tool SetSettingTool = new Tool
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
                Required = new List<string> { "title" }
            }
        }
    };

    public SoundDeviceTypes Value {get; private set; }
    
    private bool sentIntroMessage;

    public static async Task<ClientSoundDeviceSetting> CreateAsync(CancellationToken tkn)
    {
        var fileContents = await StringIO.LoadStateAsync("0", fileName, tkn);
        var loadedValue = (SoundDeviceTypes)Enum.Parse(typeof(SoundDeviceTypes), fileContents);
        var instance = new ClientSoundDeviceSetting(loadedValue);
        return await Task.FromResult(instance);
    }

    private ClientSoundDeviceSetting(SoundDeviceTypes initialValue)
    {
        Value = initialValue;
        GetSettingTool.Execute = GetSettingMessageAsync;
        SetSettingTool.Execute = UpdateValueAsync;
    }

    public Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        if (sentIntroMessage) return Task.FromResult(new Message[]{}.AsEnumerable());

        var content = $"The Client's Sound Device Setting value is {Value}";
        if (Value == SoundDeviceTypes.Unknown)
        {
            content += $"\nYou inquire whether they are using headphones.";
        }
        if (Value == SoundDeviceTypes.Headphones)
        {
            content = $"\nYou are concerned whether they are still wearing headphones.";
        }
        var introMsg = new Message
        {
            Role = Role.System,
            Content = content
        };
        sentIntroMessage = true;
        return Task.FromResult(new Message[] { introMsg }.AsEnumerable());
    }

    private Task<Message> GetSettingMessageAsync(ToolCall tc, CancellationToken tkn)
    {
        var msg = new Message
        {
            Content = $"The Client Sound Device Setting value is {Value}",
            Role = Role.Tool,
            ToolCallId = tc.Id
        };
        return Task.FromResult(msg);
    }

    private async Task SaveStateAsync(CancellationToken cancelToken)
    {
        await StringIO.SaveStateAsync(((int)Value).ToString(), fileName, cancelToken);
    }

    private async Task<Message> UpdateValueAsync(ToolCall tc, CancellationToken cancelToken)
    {
        var args = JObject.Parse(tc.Function.Arguments);
        Value = (SoundDeviceTypes)Enum.Parse(typeof(SoundDeviceTypes), (string)args["value"]);
        await SaveStateAsync(cancelToken);
        return new Message
        {
            Content = $"The Client Sound Device Setting has been changed to {Value}. Confirm in a word or two.",
            Role = Role.Tool,
            ToolCallId = tc.Id
        };
    }
}