public static class StringIO
{
    public static async Task<string> LoadAssetAsync(string filePath, CancellationToken cancelToken)
    {
        filePath = Path.Combine("Assets", filePath);
        return await File.ReadAllTextAsync(filePath, cancelToken);
    }

    public static async Task<string> LoadStateAsync(string defaultState, string fileName, CancellationToken cancelToken)
    {
        var filePath = GetFilePathRel(Environment.SpecialFolder.ApplicationData, fileName);
        if (File.Exists(filePath) == false)
        {
            await SaveStateAsync(defaultState, fileName, cancelToken);
        }
        return await File.ReadAllTextAsync(filePath, cancelToken);
    }

    public static async Task SaveStateAsync(string state, string fileName, CancellationToken cancelToken)
    {
        var filePath = GetFilePathRel(Environment.SpecialFolder.ApplicationData, fileName);
        await File.WriteAllTextAsync(filePath, state, cancelToken);
    }

    public static string GetFilePathRel(Environment.SpecialFolder folder, string fileName)
    {
        return Path.Combine(folder.ToString(), fileName);
    }
}
