# Family Meeting Assistant

AI-powered, voice-driven .NET console assistant combining Azure Cognitive Services and OpenAI GPT for context-aware conversations and tool integration.

Key Features
- Speech-to-text & text-to-speech via Azure Cognitive Services
- Conversational AI powered by GPT-3.5 & GPT-4 with function calling
- Extensible toolset: weather, headlines, time, reminders, tasks, account management, interactive games, museum guide
- Modular contexts (“Circumstances”) for scenario-driven dialogue
- Wake-word activation, interrupt control, continuous listening

Prerequisites
- .NET 7.0 SDK
- Azure Speech Services: SPEECH_KEY, SPEECH_REGION
- OpenAI API: OPENAI_KEY, (optional) OPENAI_ORG
- OpenWeatherMap API: OWM_KEY
- NewsAPI API: NEWSAPI_API_KEY

Configuration
In the project directory, set user secrets:
```
dotnet user-secrets set SPEECH_KEY <your-key>
dotnet user-secrets set SPEECH_REGION <your-region>
dotnet user-secrets set OPENAI_KEY <your-key>
dotnet user-secrets set OPENAI_ORG <your-org-id>  # optional
dotnet user-secrets set OWM_KEY <your-key>
dotnet user-secrets set NEWSAPI_API_KEY <your-key>
```

Build & Run
```
dotnet restore
dotnet build
dotnet run --project "Family Meeting Assistant.csproj"
```

Usage
- Speak the assistant name (default “Smithsonian”) or use Converse mode to trigger responses
- Issue commands: weather, time, reminders, task management, account status, or scenario actions
- Interrupt speech with Ctrl+C or voice interrupt command

Extensibility
- Add contexts: subclass Circumstance and register in Program.cs
- Add tools: define a ToolFunction and implement its Execute delegate

Project Structure
- Assets: prompts & audio samples
- Chat: message flow, OpenAI integration, observers
- Circumstances: scenario modules
- Tools: service clients (weather, tasks, reminders)
- Settings: runtime configuration tools

Contributing
Pull requests welcome. Follow code style, add tests for new features, update prompts and documentation as needed.
