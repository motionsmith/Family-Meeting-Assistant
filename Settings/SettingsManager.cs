
public class SettingsManager : IMessageProvider
{
    private readonly List<ISetting> settings;

    public IEnumerable<Tool> SettingsTools => settings.SelectMany(s => new Tool[] { s.GetSettingTool, s.SetSettingTool});

    public static async Task<SettingsManager> CreateInstance(IEnumerable<SettingConfig> settingConfigs, CancellationToken cancelToken)
    {
        var createSettingsTask = settingConfigs.Select(cfg => CreateSettingObject(cfg, cancelToken));
        var settings = await Task.WhenAll(createSettingsTask);
        var instance = new SettingsManager(settingConfigs, settings.AsEnumerable());
        return instance;
    }

    private SettingsManager(IEnumerable<SettingConfig> settingConfigs, IEnumerable<ISetting> settings)
    {
        var settingsConfigsByType = settingConfigs.Select(cfg => new KeyValuePair<Type, SettingConfig>(cfg.settingType, cfg));
        this.settings = new List<ISetting>(settings);
    }

    private static async Task<ISetting> CreateSettingObject(SettingConfig config, CancellationToken cancelToken)
    {
        ISetting? instance;
        if (string.IsNullOrEmpty(config.fileName))
        {
            //if (config.settingType == typeof(bool))
            instance = (ISetting)Activator.CreateInstance(config.settingType, string.Empty);
        }
        else
        {
            // Load file contents using fileName
            var fileContents = await StringIO.LoadStateAsync(config.defaultValue, config.fileName, cancelToken);
            // Create an instance of a Setting with the loaded setting value
            instance = (ISetting)Activator.CreateInstance(config.settingType, fileContents);
        }

        // Wrap the setting tools' execute methods
        instance.GetSettingTool.Execute = async (a, b) => 
        {
            var msg = await instance.CreateSettingStatusMessage(a, b);
            return msg;
        };
        instance.SetSettingTool.Execute = async (a, b) =>
        {
            var msg = await instance.UpdateValueAsync(a, b);
            await SaveStateAsync(config, instance, cancelToken);
            return msg;
        };
        return instance;
    }

    private static async Task SaveStateAsync(SettingConfig config, ISetting setting, CancellationToken cancelToken)
    {
        if (string.IsNullOrEmpty(config.fileName)) return; // no need to save this setting.
        await StringIO.SaveStateAsync(setting.SerializedValue, config.fileName, cancelToken);
    }

    public async Task<IEnumerable<Message>> GetNewMessagesAsync(CancellationTokenSource cts)
    {
        var tasks = settings.Select(setting => setting.GetNewMessagesAsync(cts));
        var results = await Task.WhenAll(tasks);
        return results.SelectMany(messages => messages);
    }

    public Func<TValue> GetterFor<T, TValue>() where T : ISetting
    {
        var x = settings.OfType<T>().First();
        return () => (TValue)x.Value;
    }

    internal Action<TValue> SetterFor<T, TValue>() where T : ISetting
    {
        var x = settings.OfType<T>().First();
        return (value) => x.Value = value;
    }
}
public class SettingConfig
{
    public readonly string fileName;
    public readonly Type settingType;
    public readonly string defaultValue;

    public SettingConfig(string fileNm, string defVal, Type type)
    {
        fileName = fileNm;
        defaultValue = defVal;
        settingType = type;
    }
}
