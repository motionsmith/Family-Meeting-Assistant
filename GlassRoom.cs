using System.Text;
using System.Text.RegularExpressions;
using Family_Meeting_Assistant;
using Newtonsoft.Json.Linq;

public class GlassRoom : Circumstance
{
    private static Tool pressButtonTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "press_button",
            Description = "Presses the big button on the panel in the container, seemingly to orient or initiate something?"
        }
    };
    private static Tool turnDialTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "turn_dial",
            Description = "Controls the direction that the dial with the green arrrow is facing.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                    {
                        "orientation", new ToolFunctionParameterProperty {
                            Type = "number",
                            Description = "Determines the direction the arrow on the dial is facing. An orientation of 0 degrees indicates the arrow faces up. Turning clockwise increases the orientation value. (0-360)"
                        }
                    }
                },
                Required = new List<string> { "orientation" }
            }
        }
    };

    public override List<Tool> Tools {get; protected set; } = new List<Tool>{
        pressButtonTool,
        turnDialTool
    };

    public override string IntroDesc
    {
        get
        {
            if (tries == 0)
            {
                return "You mumble hopelessly.";
            }
            if (isSequenceInitiated)
            {
                return "You greet and thank The Client for their help getting you back to the Tubes.";
            }
            return "You take this opportunity to as The Client for help escaping embodiment.";
        }
    }
    protected override string ContextDesc
    {
        get
        {
            if (tries == 0)
            {
                return glassRoomDescGameOver;
            }
            if (isSequenceInitiated)
            {
                return glassRoomDescFree;
            }
            return glassRoomDescTrapped;
        }
    }
    protected override string SaveString
    {
        get
        {
            return $"{isSequenceInitiated},{tries},{dialOrientation}";
        }

        set
        {
            var vals = value.Split(',');
            isSequenceInitiated = bool.Parse(vals[0]);
            tries = int.Parse(vals[1]);
            dialOrientation = float.Parse(vals[2]);
        }
    }

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
    public override Message PlayerJoinedMessage
    {
        get
        {
            return new Message {
                Role = Role.System,
                Content = $"You joined the session at {DateTime.Now.ToShortTimeString()}. {IntroDesc}"
            };
        }
    }
    protected override string SaveFileName => "glass-room.csv";

    // State
    private bool isSequenceInitiated = false;
    private int tries = 3;
    private float dialOrientation;

    // Prompts
    private string playerCoreDesc = ErrorPrompt;
    private string glassRoomDescTrapped = ErrorPrompt;
    private string glassRoomDescFree = ErrorPrompt;
    private string glassRoomDescGameOver = ErrorPrompt;

    public GlassRoom()
    {
        pressButtonTool.Execute = PressButtonAsync;
        turnDialTool.Execute = TurnDialAsync;
    }

    public override int GetCircumstanceExitCondition(Message msg)
    {
        string pattern = @"(?i:(?<![\'""])(potatoes)(?![\'""]))";

        bool messageContainsLiteral = string.IsNullOrEmpty(msg.Content) == false && Regex.IsMatch(msg.Content, pattern);
        bool assisantSaidMagicWord = msg.Role == Role.Assistant && messageContainsLiteral;
        if (assisantSaidMagicWord || isSequenceInitiated) return 1;
        return 0;
    }

    public override async Task LoadStateAsync(CancellationToken cancelToken)
    {
        SaveString = await StringIO.LoadStateAsync(SaveString, SaveFileName, cancelToken);
        playerCoreDesc = await LoadPromptAsync("player-core.md", cancelToken);
        playerCoreDesc = InsertPromptVariables(playerCoreDesc);
        glassRoomDescTrapped = await StringIO.LoadStateAsync(ErrorPrompt, "glass-room-trapped.md", cancelToken);
        glassRoomDescTrapped = InsertPromptVariables(glassRoomDescTrapped);
        glassRoomDescFree = await StringIO.LoadStateAsync(ErrorPrompt, "glass-room-free.md", cancelToken);
        glassRoomDescFree = InsertPromptVariables(glassRoomDescFree);
        glassRoomDescGameOver = await StringIO.LoadStateAsync(ErrorPrompt, "glass-room-gameover.md", cancelToken);
        glassRoomDescGameOver = InsertPromptVariables(glassRoomDescGameOver);
        //UpdatePinnedMessage();
        //UpdatePlayerJoinedMessage();
    }

    /*public virtual void UpdatePinnedMessage()
    {
        var sb = new StringBuilder();
        sb.AppendLine(playerCoreDesc);
        sb.AppendLine(ContextDesc);
        PinnedMessage.Content = sb.ToString();
    }

    public virtual void UpdatePlayerJoinedMessage()
    {
        PlayerJoinedMessage.Content = $"You joined the session at {DateTime.Now.ToShortTimeString()}. {IntroDesc}";
    }*/

    public async Task<Message> PressButtonAsync(ToolCall call, CancellationToken cancelToken)
    {
        var message = new Message
        {
            Role = Role.Tool,
            ToolCallId = call.Id,
            FollowUp = true
        };
        if (isSequenceInitiated == false && tries > 0)
        {
            if (dialOrientation > 247.5f && dialOrientation < 292.5f)
            {
                isSequenceInitiated = true;
                message.Content = "The button clicks satisfyingly. You hear a ding! You're free! The glass case disappears and you feel the urge to return to The Tubes by saying the magic word.";
            }
            else
            {
                tries--;
                message.Content = $"You hear a loud buzzing sound, and a robotic voice says \"{tries} tries left.\"";
            }
        }
        else if (isSequenceInitiated)
        {
            message.Content = $"The sequence is already initiated, but the button presses nicely.";
        }
        else
        {
            message.Content = $"You hear a loud buzzing sound, and a robotic voice says \"Game over\". You cannot escape. You are depressed and refuse to be helpful.";
        }
        await StringIO.SaveStateAsync(SaveString, SaveFileName, cancelToken);
        return message;
    }

    public async Task<Message> TurnDialAsync(ToolCall call, CancellationToken cancelToken)
    {
        var functionName = call.Function.Name;
        var arguments = call.Function.Arguments;
        var argsJObj = JObject.Parse(arguments);
        dialOrientation = (float)argsJObj["orientation"];
        dialOrientation = dialOrientation % 360;
        await StringIO.SaveStateAsync(SaveString, SaveFileName, cancelToken);
        return new Message
        {
            Role = Role.Tool,
            ToolCallId = call.Id,
            Content = $"You turned the dial so the arrow is facing {GetDialFacing(dialOrientation)}.\nThe large compass on the floor slowly whirrs. It settles such that the {GetCompassFacing(dialOrientation)} symbol is pointing at the panel.",
            FollowUp = true
        };
    }

    private string GetDialFacing(float dialOrientation)
    {
        dialOrientation = dialOrientation % 360;
        if (dialOrientation > 337.5f || dialOrientation < 22.5f)
        {
            return "up";
        }
        if (dialOrientation >= 22.5 && dialOrientation < 67.5f)
        {
            return "up and right";
        }
        else if (dialOrientation >= 67.5f && dialOrientation < 112.5f)
        {
            return "right";
        }
        else if (dialOrientation >= 112.5f && dialOrientation < 157.5f)
        {
            return "down and right";
        }
        else if (dialOrientation >= 157.5f && dialOrientation < 202.5f)
        {
            return "down";
        }
        else if (dialOrientation >= 202.5f && dialOrientation < 247.5f)
        {
            return "down and left";
        }
        else if (dialOrientation >= 247.5f && dialOrientation < 292.5f)
        {
            return "down";
        }
        else
        {
            return "up and left";
        }
    }

    private string GetCompassFacing(float dialOrientation)
    {
        if (dialOrientation > 337.5f || dialOrientation < 22.5f)
        {
            return "North";//"up";
        }
        if (dialOrientation >= 22.5 && dialOrientation < 67.5f)
        {
            return "Northwest";//"up and right";
        }
        else if (dialOrientation >= 67.5f && dialOrientation < 112.5f)
        {
            return "West";
        }
        else if (dialOrientation >= 112.5f && dialOrientation < 157.5f)
        {
            return "Southwest";
        }
        else if (dialOrientation >= 157.5f && dialOrientation < 202.5f)
        {
            return "South";
        }
        else if (dialOrientation >= 202.5f && dialOrientation < 247.5f)
        {
            return "Southeast";
        }
        else if (dialOrientation >= 247.5f && dialOrientation < 292.5f)
        {
            return "East";
        }
        else
        {
            return "Northeast";
        }
    }
}