
public abstract class Circumstance
{
    public abstract List<Tool> Tools {get;}
    protected abstract string SaveFileName {get; }
    protected static string ErrorPrompt = $"You are an assistant who always says \"Error loading a prompt. Ensure your prompt files exist.\"";

    // Circumstance serialization
    protected abstract string SaveString {get; set; }

    protected abstract string ContextDesc {get; }

    public abstract string IntroDesc {get;}

    public abstract Message PinnedMessage {get;}

    public abstract Task LoadStateAsync(CancellationToken cancelToken);

    protected async Task<string> LoadPromptAsync(string fileName, CancellationToken cancelToken)
    {
        var prompt = await StringIO.LoadStateAsync(ErrorPrompt, fileName, cancelToken);
        prompt = InsertPromptVariables(prompt);
        return prompt;
    }

    protected virtual string InsertPromptVariables(string prompt)
    {
        return prompt
                .Replace("\"", "\\\"")
                .Replace(Environment.NewLine, "\\n")
                .Replace("[[ASSISTANT_NAME]]", JustStrings.ASSISTANT_NAME)
                .Replace("[[NOW]]", DateTime.Now.ToString());
    }

    public virtual void OnNewMessages(IEnumerable<Message> messages, Action<int> exitCallback)
    {
        
    }

    public virtual Message PlayerJoinedMessage
    {
        get
        {
            return new Message {
                Role = Role.System,
                Content = $"You joined the session at {DateTime.Now.ToString()}. {IntroDesc}"
            };
        }
    }
}
