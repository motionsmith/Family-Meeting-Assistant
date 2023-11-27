public static class StringIO
{
    public static async Task<string> LoadStateAsync(string defaultState, string fileName, CancellationToken cancelToken)
    {
        
        var filePath = GetFilePath(fileName);
        if (File.Exists(filePath) == false)
        {
            await SaveStateAsync(defaultState, fileName, cancelToken);
        }
        return await File.ReadAllTextAsync(filePath, cancelToken);
    }

    public static async Task SaveStateAsync(string state, string fileName, CancellationToken cancelToken)
    {
        var filePath = GetFilePath(fileName);
        await File.WriteAllTextAsync(filePath, state, cancelToken);
    }

    private static string GetFilePath(string fileName)
    {
        var appDataDirPath = Environment.SpecialFolder.ApplicationData.ToString();
        string appDataFullPath = Path.GetFullPath(appDataDirPath);
        return Path.Combine(appDataFullPath, fileName);
    }
}
