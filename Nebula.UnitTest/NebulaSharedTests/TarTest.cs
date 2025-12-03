using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared;
using Nebula.Shared.Services;
using Nebula.Shared.Services.Logging;
using Nebula.Shared.Utils;

namespace Nebula.UnitTest.NebulaSharedTests;

[TestFixture]
[TestOf(typeof(DotnetResolverService))]
public class TarTest : BaseSharedTest
{
    private FileService _fileService = default!;
    private ConfigurationService _configurationService = default!;
    private readonly HttpClient _httpClient = new();
    
    public override void BeforeServiceBuild(IServiceCollection services)
    {
        TestServiceHelper.InitFileServiceTest();
    }
    
    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _fileService = _sharedUnit.GetService<FileService>();
        _configurationService = _sharedUnit.GetService<ConfigurationService>();
    }
    
    [Test]
    public async Task DownloadTarAndUnzipTest()
    {
        DotnetUrlHelper.RidOverrideTest = "linux-x64";
        Console.WriteLine($"Downloading dotnet {DotnetUrlHelper.GetRuntimeIdentifier()}...");

        var url = DotnetUrlHelper.GetCurrentPlatformDotnetUrl(
            _configurationService.GetConfigValue(CurrentConVar.DotnetUrl)!
        );

        using var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();

        Directory.CreateDirectory(FileService.RootPath);

        if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zipArchive = new ZipArchive(stream);
            zipArchive.ExtractToDirectory(FileService.RootPath, true);
        }
        else if (url.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                 || url.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            TarUtils.ExtractTarGz(stream, FileService.RootPath);
        }
        else
        {
            throw new NotSupportedException("Unsupported archive format.");
        }

        Console.WriteLine("Downloading dotnet complete.");
    }
}