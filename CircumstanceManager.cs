public class CircumstanceManager : IChatObserver
{
    public static async Task<CircumstanceManager> CreateAsync(IEnumerable<Circumstance> circumstances, Action<Message> setPinnedMessageDel, CancellationToken cancelToken)
    {
        var fileContents = await StringIO.LoadStateAsync("0,0", fileName, cancelToken);
        foreach (var circustance in circumstances)
        {
            await circustance.LoadStateAsync(cancelToken);
        }
        return new CircumstanceManager(circumstances, fileContents, setPinnedMessageDel);
    }

    private static readonly string fileName = "current-circumstances.csv";

    public Circumstance CurrentCircumstance
    {
        get
        {
            for (int i = 0; i < State.Count; i++)
            {
                if (State[i] == 0)
                    return Circumstances[i];
            }
            return Circumstances.Last();
            //throw new NotImplementedException("Need to implement this condition. Some sort of End of game scenario.");
        }
    }

    public List<int> State = new List<int> { 0, 0 };

    public readonly List<Circumstance> Circumstances;

    private string StateString
    {
        get
        {
            return string.Join(',', State);
        }
        set
        {
            var vals = value.Split(',');
            for (int i = 0; i < vals.Length; i++)
            {
                State[i] = int.Parse(vals[i]);
            }
        }
    }

    public List<Tool> Tools => CurrentCircumstance.Tools;

    public Message PinnedMessage => CurrentCircumstance.PinnedMessage;

    public Message PlayerJoinedMessage => CurrentCircumstance.PlayerJoinedMessage;
    
    private Action<Message> setPinnedMessageDel;

    private CircumstanceManager(IEnumerable<Circumstance> circumstances, string initialValue, Action<Message> setPinnedMessageDel)
    {
        Circumstances = circumstances.ToList();
        StateString = initialValue;
        this.setPinnedMessageDel = setPinnedMessageDel;
    }

    public void SaveAsync()
    {
        var _ = StringIO.SaveStateAsync(StateString, fileName, new CancellationTokenSource().Token);
    }

    private void ChangeCircumstance(int exitVal)
    {
        if (exitVal != 0)
        {
            for (int i = 0; i < State.Count; i++)
            {
                if (State[i] == 0)
                {
                    State[i] = exitVal;
                    SaveAsync();
                    break; // Break out of the loop after modifying the first 0
                }
            }
        }
    }

    public void OnNewMessages(IEnumerable<Message> messages)
    {
        CurrentCircumstance.OnNewMessages(messages, ChangeCircumstance);
        setPinnedMessageDel.Invoke(CurrentCircumstance.PinnedMessage);
    }
}
