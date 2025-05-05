using Nebula.Shared.FileApis;
using Nebula.Shared.Models;

namespace Nebula.Shared.Services;

public partial class ContentService
{
    public bool CheckMigration(ILoadingHandler loadingHandler)
    {
        _logger.Log("Checking migration...");

        var migrationList = ContentFileApi.AllFiles.Where(f => !f.Contains("\\")).ToList();
        if(migrationList.Count == 0) return false;
        
        _logger.Log($"Found {migrationList.Count} migration files. Starting migration...");
        Task.Run(() => DoMigration(loadingHandler, migrationList));
        return true;
    }

    private void DoMigration(ILoadingHandler loadingHandler, List<string> migrationList)
    {
        loadingHandler.SetJobsCount(migrationList.Count);
        
        Parallel.ForEach(migrationList, (f,_)=>MigrateFile(f,loadingHandler));
        
        if (loadingHandler is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private void MigrateFile(string file, ILoadingHandler loadingHandler)
    {
        if(!ContentFileApi.TryOpen(file, out var stream))
        {
            loadingHandler.AppendResolvedJob();
            return;
        }
            
        ContentFileApi.Save(HashApi.GetManifestPath(file), stream);
        stream.Dispose();
        ContentFileApi.Remove(file);
        loadingHandler.AppendResolvedJob();
    }
}