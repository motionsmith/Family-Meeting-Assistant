using Newtonsoft.Json.Linq;

public class Room1
{
    private bool isDoorOpen = false;
    private int tries = 3;
    private float dialOrientation;
    public async Task<Message> TurnDoorHandle(ToolCall call, CancellationToken cancelToken)
    {
        var message = new Message {
            Role = Role.Tool,
            ToolCallId = call.Id,
            FollowUp = true
        };
        if (isDoorOpen == false && tries > 0)
        {
            if (dialOrientation > 70 && dialOrientation < 110)
            {
                isDoorOpen = true;
                message.Content = "The handle slips open with ease. You're free!";
            }
            else
            {
                tries--;
                message.Content = $"You hear a loud buzzing sound, and a robotic voice says \"{tries} tries left.\"";
            }
        }
        else if (isDoorOpen)
        {
            message.Content = $"The door is already open, but the latch still slides around nicely.";
        }
        else
        {
            message.Content = $"You hear a loud buzzing sound, and a robotic voice says \"Game over\"";
        }
        return message;
    }

    public async Task<Message> TurnCompassDial(ToolCall call, CancellationToken cancelToken)
    {
        var functionName = call.Function.Name;
        var arguments = call.Function.Arguments;
        var argsJObj = JObject.Parse(arguments);
        dialOrientation = (float)argsJObj["orientation"];
        return new Message {
            Role = Role.Tool,
            ToolCallId = call.Id,
            Content = $"You turned the dial needle so it is now facing {dialOrientation} degrees.",
            FollowUp = true
        };
    }
}
