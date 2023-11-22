public abstract class Circumstance
{
    public abstract List<Tool> Tools {get; protected set;}
    protected abstract string SaveFileName {get; }
    protected static string ErrorPrompt = $"You are an assistant who always says \"Error loading a prompt. Ensure your prompt files exist.\"";

    // Circumstance serialization
    protected abstract string SaveString {get; set; }

    protected abstract string ContextDesc {get; }

    public abstract string IntroDesc {get;}

    public abstract Message PinnedMessage {get;}

    public abstract Message PlayerJoinedMessage {get; }

    public abstract Task LoadStateAsync(CancellationToken cancelToken);

    public abstract int GetCircumstanceExitCondition(Message mesage);

    protected async Task<string> LoadPromptAsync(string fileName, CancellationToken cancelToken)
    {
        return await StringIO.LoadStateAsync(ErrorPrompt, fileName, cancelToken);
    }

    protected virtual string InsertPromptVariables(string prompt)
    {
        return prompt
                .Replace("\"", "\\\"")
                .Replace(Environment.NewLine, "\\n")
                .Replace("[[ASSISTANT_NAME]]", JustStrings.ASSISTANT_NAME)
                .Replace("[[NOW]]", DateTime.Now.ToString());
    }
}
