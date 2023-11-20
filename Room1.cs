using Newtonsoft.Json.Linq;

public class Room1
{
    private bool isSequenceInitiated = false;
    private int tries = 3;
    private float dialOrientation;
    public async Task<Message> PressButton(ToolCall call, CancellationToken cancelToken)
    {
        var message = new Message {
            Role = Role.Tool,
            ToolCallId = call.Id,
            FollowUp = true
        };
        if (isSequenceInitiated == false && tries > 0)
        {
            if (dialOrientation > 70 && dialOrientation < 110)
            {
                isSequenceInitiated = true;
                message.Content = "The button clicks satisfyingly. You hear a ding.!";
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
        return message;
    }

    public async Task<Message> TurnDial(ToolCall call, CancellationToken cancelToken)
    {
        var functionName = call.Function.Name;
        var arguments = call.Function.Arguments;
        var argsJObj = JObject.Parse(arguments);
        dialOrientation = (float)argsJObj["orientation"];
        dialOrientation = dialOrientation % 360;
        return new Message {
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
            return "East";//"up";
        }
        if (dialOrientation >= 22.5 && dialOrientation < 67.5f)
        {
            return "Northeast";//"up and right";
        }
        else if (dialOrientation >= 67.5f && dialOrientation < 112.5f)
        {
            return "North";
        }
        else if (dialOrientation >= 112.5f && dialOrientation < 157.5f)
        {
            return "Northwest";
        }
        else if (dialOrientation >= 157.5f && dialOrientation < 202.5f)
        {
            return "West";
        }
        else if (dialOrientation >= 202.5f && dialOrientation < 247.5f)
        {
            return "Southwest";
        }
        else if (dialOrientation >= 247.5f && dialOrientation < 292.5f)
        {
            return "South";
        }
        else
        {
            return "Southeast";
        }
    }
}
