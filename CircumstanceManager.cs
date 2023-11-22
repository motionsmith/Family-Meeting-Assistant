using System.Reflection.Metadata;

namespace Family_Meeting_Assistant;

public class CircumstanceManager
{
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
    public readonly GlassRoom glassRoom = new GlassRoom();

    public List<int> State = new List<int> {
        0
    };

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
            for (int i = 0 ; i < vals.Length; i++)
            {
                State[i] = int.Parse(vals[i]);
            }
        }
    }

    public List<Tool> Tools => CurrentCircumstance.Tools;

    public Message PinnedMessage => CurrentCircumstance.PinnedMessage;

    public Message PlayerJoinedMessage => CurrentCircumstance.PlayerJoinedMessage;

    private readonly string fileName = "current-circumstances.csv";

    public CircumstanceManager()
    {
        Circumstances = new List<Circumstance> {
            glassRoom
        };
    }

    public async Task UpdateCurrentCircumstance(Message assistantMessage, CancellationToken cancelToken)
    {
        var exitVal = CurrentCircumstance.GetCircumstanceExitCondition(assistantMessage);
        if (exitVal != 0)
        {
            for (int i = 0; i < State.Count; i++)
            {
                if (State[i] == 0)
                {
                    State[i] = exitVal;
                    await SaveAsync(cancelToken);
                    break; // Break out of the loop after modifying the first 0
                }
            }
        }
    }

    public async Task LoadStateAsync(CancellationToken cancelToken)
    {
        StateString = await StringIO.LoadStateAsync(StateString, fileName, cancelToken);
        foreach (var circustance in Circumstances)
        {
            await circustance.LoadStateAsync(cancelToken);
        }
    }

    public async Task SaveAsync(CancellationToken cancelToken)
    {
        await StringIO.SaveStateAsync(StateString, fileName, cancelToken);
    }
}
