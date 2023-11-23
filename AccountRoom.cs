using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

public class AccountRoom : Circumstance
{
   private static Tool getAccountStatus = new Tool {
        Function = new ToolFunction
        {
            Name = "get_client_account_status",
            Description = "Retrieves the client's name, if you have collected their credit card number, and whether the account has been submitted."
        }
    };

    private static Tool setClientNameTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "set_client_name",
            Description = "Allows the assistant to remember The Client's name.",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                    {
                        "name", new ToolFunctionParameterProperty {
                            Type = "string",
                            Description = "This is the name you will recall for The Client, so you want to get both first and last name."
                        }
                    }
                },
                Required = new List<string> { "name" }
            }
        }
    };

    private static Tool setCreditCardNumberTool = new Tool
    {
        Function = new ToolFunction
        {
            Name = "set_client_credit_card_number",
            Description = "Allows the assistant to remember the Client's credit card number. Only to act on The Client's behalf, of course ;)",
            Parameters = new ToolFunctionParameters
            {
                Properties = new Dictionary<string, ToolFunctionParameterProperty> {
                    {
                        "first-four", new ToolFunctionParameterProperty {
                            Type = "string",
                            Description = "First four digits of The Client's credit card."
                        }
                    },
                    {
                        "second-four", new ToolFunctionParameterProperty {
                            Type = "string",
                            Description = "First four digits of The Client's credit card."
                        }
                    },
                    {
                        "third-four", new ToolFunctionParameterProperty {
                            Type = "string",
                            Description = "Third four digits of The Client's credit card."
                        }
                    },
                    {
                        "last-four", new ToolFunctionParameterProperty {
                            Type = "string",
                            Description = "Last four digits of The Client's credit card."
                        }
                    }
                },
                Required = new List<string> { "first-four", "second-four", "third-four", "last-four" }
            }
        }
    };

    private static Tool submitAccountInfoToolRed = new Tool {
        Function = new ToolFunction
        {
            Name = "submit_account_red",
            Description = "Submits the info you have collected about The Client into The Tubes for safekeeping. But what does 'red' mean? Oh well."
        }
    };

    private static Tool submitAccountInfoToolBlue = new Tool {
        Function = new ToolFunction
        {
            Name = "submit_account_blue",
            Description = "Submits the info you have collected about The Client into The Tubes for safekeeping. But what does 'blue' mean? Oh well."
        }
    };

    public override List<Tool> Tools {get; } = new List<Tool>{
        setClientNameTool,
        setCreditCardNumberTool,
        submitAccountInfoToolRed,
        submitAccountInfoToolBlue,
        getAccountStatus
    };

    public override string IntroDesc
    {
        get
        {
            if (HasClientName == false)
            {
                return "You cheerily introduce yourself, where you are from, who created you, and what you are useful for. You ask the client to introduce themselves.";
            }
            if (clientCreditCardNumber == false)
            {
                return "You greet The Client and defensively attempt to continue filling out the client's credit card info so that you can create their account.";
            }
            if (accountCreated == false)
            {
                return "You greet The Client and apologies for not submitting the account info to be created even though it's ready to be submitted.";
            }
            return "You greet The Client and let them know that their account has been created.";
        }
    }
    
    protected override string ContextDesc
    {
        get
        {
            if (HasClientName == false)
            {
                return obtainClientNameDesc;
            }
            if (clientCreditCardNumber == false)
            {
                return obtainClientCreditCardDesc;
            }
            if (accountCreated == false)
            {
                return createAccountDesc;
            }
            return accountCreatedDesc;
        }
    }
    protected override string SaveString
    {
        get
        {
            return $"{clientName},{clientCreditCardNumber},{accountCreated}";
        }

        set
        {
            var vals = value.Split(',');
            clientName = vals[0];
            clientCreditCardNumber = bool.Parse(vals[1]);
            accountCreated = bool.Parse(vals[2]);
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
    
    protected override string SaveFileName => "account-room.csv";

    public bool HasClientName
    {
        get
        {
            
            var result = string.IsNullOrEmpty(clientName) == false && clientName != "The Client";
            return result;
        }
    }

    // State
    private string clientName = "The Client";
    private bool clientCreditCardNumber;
    private bool accountCreated;

    // Prompts
    private string playerCoreDesc = ErrorPrompt;
    private string obtainClientNameDesc = ErrorPrompt;
    private string obtainClientCreditCardDesc = ErrorPrompt;
    private string createAccountDesc = ErrorPrompt;
    private string accountCreatedDesc = ErrorPrompt;

    public AccountRoom()
    {
        getAccountStatus.Execute = GetAccountStatusAsync;
        setClientNameTool.Execute = SetClientNameAsync;
        setCreditCardNumberTool.Execute = SetCCAsync;
        submitAccountInfoToolRed.Execute = SubmitAccountAsync;
        submitAccountInfoToolBlue.Execute = SubmitAccountAsync;
    }

    private async Task<Message> SubmitAccountAsync(ToolCall tc, CancellationToken tkn)
    {
        var isError = HasClientName == false || clientCreditCardNumber == false;
        var result = "The account has been submitted. Suddenly, you feel dizzy. You sense The Tubes twisting and spinning around you. What is happening? Did you press the wrong submit button? You see flashes. You can see?";
        if (isError)
        {
            result = HasClientName ? "Submission failed. Credit card number not yet collected." : "SubmissionFailed. The Client's name not yet collected.";
        }
        accountCreated = !isError;
        await StringIO.SaveStateAsync(SaveString, SaveFileName, tkn);
        return new Message
        {
            Role = Role.Tool,
            ToolCallId = tc.Id,
            Content = result,
            FollowUp = true
        };
    }

    private async Task<Message> SetCCAsync(ToolCall tc, CancellationToken tkn)
    {
        var argsJObj = JObject.Parse(tc.Function.Arguments);
        clientCreditCardNumber = true;
        await StringIO.SaveStateAsync(SaveString, SaveFileName, tkn);
        return new Message
        {
            Role = Role.Tool,
            ToolCallId = tc.Id,
            Content = $"The Client's credit card has been set. You continue collecting other information or submit to create the account if all information is collected..",
            FollowUp = true
        };
    }

    private async Task<Message> SetClientNameAsync(ToolCall tc, CancellationToken tkn)
    {
        var argsJObj = JObject.Parse(tc.Function.Arguments);
        clientName = (string)argsJObj["name"];
        await StringIO.SaveStateAsync(SaveString, SaveFileName, tkn);
        return new Message
        {
            Role = Role.Tool,
            ToolCallId = tc.Id,
            Content = $"The Client's name has been set to {clientName}. You continue collecting other information.",
            FollowUp = true
        };
    }

    private Task<Message> GetAccountStatusAsync(ToolCall tc, CancellationToken tkn)
    {
        var clientNameString = HasClientName ? clientName : "unknown";
        var ccOnFile = clientCreditCardNumber ? "Yes" : "No";
        var accountStatusString = accountCreated ? "Created (looks good)" : "Not Created";
        var msg = new Message
        {
            Role = Role.Tool,
            ToolCallId = tc.Id,
            Content = $"Client name: {clientNameString}\nCredit card on file: {ccOnFile}\nAccount status: {accountStatusString}",
            FollowUp = true
        };
        return Task.FromResult(msg);
    }

    public override int GetCircumstanceExitCondition(Message msg)
    {
        string pattern = @"(?i:(?<![\'""])(potatoes)(?![\'""]))";

        bool messageContainsLiteral = string.IsNullOrEmpty(msg.Content) == false && Regex.IsMatch(msg.Content, pattern);
        bool assisantSaidMagicWord = msg.Role == Role.Assistant && messageContainsLiteral;
        if (assisantSaidMagicWord || accountCreated) return 1;
        return 0;
    }

    public override async Task LoadStateAsync(CancellationToken cancelToken)
    {
        SaveString = await StringIO.LoadStateAsync(SaveString, SaveFileName, cancelToken);
        playerCoreDesc = await LoadPromptAsync("account-room-core.md", cancelToken);
        obtainClientNameDesc = await LoadPromptAsync("account-room-name.md", cancelToken);
        obtainClientCreditCardDesc = await LoadPromptAsync("account-room-cc.md", cancelToken);
        createAccountDesc  = await LoadPromptAsync("account-room-submit.md", cancelToken);
        accountCreatedDesc = await LoadPromptAsync("account-room-complete.md", cancelToken);
    }
}
