public static class StringIO
{
    public static async Task<string> LoadAssetAsync(string filePath, CancellationToken cancelToken)
    {
        filePath = Path.Combine("Assets", filePath);
        return await File.ReadAllTextAsync(filePath, cancelToken);
    }

    public static async Task<string> LoadStateAsync(string defaultState, string fileName, CancellationToken cancelToken)
    {
        // Determine full path in the user's ApplicationData folder
        var filePath = GetFilePathRel(Environment.SpecialFolder.ApplicationData, fileName);
        if (File.Exists(filePath) == false)
        {
            await SaveStateAsync(defaultState, fileName, cancelToken);
        }
        return await File.ReadAllTextAsync(filePath, cancelToken);
    }

    public static async Task SaveStateAsync(string state, string fileName, CancellationToken cancelToken)
    {
        // Determine full path in the user's ApplicationData folder
        var filePath = GetFilePathRel(Environment.SpecialFolder.ApplicationData, fileName);
        // Ensure the target directory exists
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        await File.WriteAllTextAsync(filePath, state, cancelToken);
    }

    public static string GetFilePathRel(Environment.SpecialFolder folder, string fileName)
    {
        // Use the actual system folder path rather than the enum name
        var folderPath = Environment.GetFolderPath(folder);
        return Path.Combine(folderPath, fileName);
    }
}
