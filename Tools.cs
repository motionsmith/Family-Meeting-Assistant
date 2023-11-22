public static class Tools
{
    public static Tool FileTaskTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "file_task",
            Description = "Adds a task to the Client task list. Replaces tasks with the same title.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "title", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "A short description that helps the Client remember what needs to be done to complete this task."
                                }
                            },
                            {
                                "due", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The date that the task is due, in the format MM/DD/YYYY."
                                }
                            }
                        },
                Required = new List<string> { "title" }
            }
        }
    };
    public static Tool ListTasksTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "list_tasks",
            Description = "Lists the tasks in the Client's task list."
        }
    };
    public static Tool CompleteTaskTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "complete_task",
            Description = "Removes a task from the Client's task list.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "title", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The title of the task to be removed."
                                }
                            }
                        },
                Required = new List<string> { "title" }
            }
        }
    };
    public static Tool SpeakTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "speak",
            Description = "Causes the LLM to speak using text-to-speech though the user's speakers.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "text", new ToolFunctionParameterProperty
                                {
                                    Type = "string",
                                    Description = "The text to be spoken."
                                }
                            }
                        },
                Required = new List<string> { "text" }
            }
        }
    };
    public static Tool GetCurrentLocalWeatherTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "get_current_local_weather",
            Description = "Returns current local weather data from Open Weather Map API."
        }
    };
    public static Tool PressButtonTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "press_button",
            Description = "Presses the big button on the panel in the container, seemingly to orient or initiate something?"
        }
    };
    public static Tool TurnDialTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "turn_dial",
            Description = "Controls the direction that the dial with the green arrrow is facing.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                            {
                                "orientation", new ToolFunctionParameterProperty
                                {
                                    Type = "number",
                                    Description = "Determines the direction the arrow on the dial is facing. An orientation of 0 degrees indicates the arrow faces up. Turning clockwise increases the orientation value. (0-360)"
                                }
                            }
                        },
                Required = new List<string> { "orientation" }
            }
        }
    };
    public static readonly List<Tool> List = new List<Tool>()
    {
        FileTaskTool,
        ListTasksTool,
        CompleteTaskTool,
        SpeakTool,
        GetCurrentLocalWeatherTool,
        PressButtonTool,
        TurnDialTool
    };
}
