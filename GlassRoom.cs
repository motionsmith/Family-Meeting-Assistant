﻿using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class GlassRoom
{
    public Tool PressButtonTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "press_button",
            Description = "Presses the big button on the panel in the container, seemingly to orient or initiate something?"
        }
    };
    public Tool TurnDialTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "turn_dial",
            Description = "Controls the direction that the dial with the green arrrow is facing.",
            Parameters = new ToolFunctionParameters {
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
    private bool isSequenceInitiated = false;
    private int tries = 3;
    private float dialOrientation;

    public GlassRoom()
    {
        PressButtonTool.Execute = PressButtonAsync;
        TurnDialTool.Execute = TurnDialAsync;
    }

    public bool IsWinningMessage(Message msg)
    {
        var input = string.Empty;
        input += msg.Content;
        if (msg.ToolCalls != null)
        {
            foreach (var tc in msg.ToolCalls)
            {
                var arguments = tc.Function.Arguments;
                var argsJObj = JObject.Parse(arguments);
                var textToSpeak = argsJObj["text"]?.ToString();
                input += $"\n{textToSpeak}";
            }
        }

        string pattern = @"(?i:(?<![\'""])(potatoes)(?![\'""]))";

        bool messageContainsLiteral = Regex.IsMatch(input, pattern);
        return msg.Role == Role.Assistant && messageContainsLiteral;
    }

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
        await SaveAsync(cancelToken);
        return message;
    }

    public async Task<Message> TurnDialAsync(ToolCall call, CancellationToken cancelToken)
    {
        var functionName = call.Function.Name;
        var arguments = call.Function.Arguments;
        var argsJObj = JObject.Parse(arguments);
        dialOrientation = (float)argsJObj["orientation"];
        dialOrientation = dialOrientation % 360;
        await SaveAsync(cancelToken);
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

    private async Task SaveAsync(CancellationToken cancelToken)
    {
        var filePath = GetFilePath();
        string contents = $"{isSequenceInitiated},{tries},{dialOrientation}";
        await File.WriteAllTextAsync(filePath, contents, cancelToken);
    }

    public async Task LoadAsync(CancellationToken cancelToken)
    {
        var filePath = GetFilePath();
        if (File.Exists(filePath) == false)
        {
            await SaveAsync(cancelToken);
        }
        var fileContents = await File.ReadAllTextAsync(filePath, cancelToken);
        var vals = fileContents.Split(',');
        isSequenceInitiated = bool.Parse(vals[0]);
        tries = int.Parse(vals[1]);
        dialOrientation = float.Parse(vals[2]);
    }

    private string GetFilePath()
    {
        var appDataDirPath = Environment.SpecialFolder.ApplicationData.ToString();
        string appDataFullPath = Path.GetFullPath(appDataDirPath);
        return Path.Combine(appDataFullPath, "room1.txt");
    }
}