using System.IO.Compression;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nebula.Shared.Models;
using Nebula.Shared.Services;
using Robust.LoaderApi;

namespace Nebula.UnitTest.NebulaSharedTests;

[TestFixture]
[TestOf(typeof(FileService))]
public class FileServiceTests : BaseSharedTest
{
    private FileService _fileService = default!;

    public override void BeforeServiceBuild(IServiceCollection services)
    {
        TestServiceHelper.InitFileServiceTest();
    }

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _fileService = _sharedUnit.GetService<FileService>();
    }

    [Test]
    public void CreateFileApi_CreatesCorrectPath()
    {
        var subPath = "test-folder";
        var fileApi = _fileService.CreateFileApi(subPath);

        using (var stream = new MemoryStream("test"u8.ToArray()))
        {
            fileApi.Save("test.txt", stream);
        }
        
        var expectedPath = Path.Combine(FileService.RootPath, subPath);

        Assert.That(Directory.Exists(expectedPath), Is.True, $"Expected path to be created: {expectedPath}");
    }

    [Test]
    public void EnsureTempDir_CreatesDirectoryAndReturnsApi()
    {
        var api = _fileService.EnsureTempDir(out var path);

        Assert.That(Directory.Exists(path), Is.True);
        Assert.That(api, Is.Not.Null);
    }

    [Test]
    public void OpenZip_ReturnsZipFileApi_WhenValid()
    {
        var testZipPath = Path.Combine(FileService.RootPath, "test.zip");
        using (var archive = ZipFile.Open(testZipPath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("test.txt");
            using var streamWriter = new StreamWriter(entry.Open());
            streamWriter.Write(testZipPath);
            streamWriter.Flush();
        }
        
        IDisposable? streamDisposable = null;

        var mockFileApi = new Mock<IFileApi>();
        mockFileApi
            .Setup(x => x.TryOpen(testZipPath, out It.Ref<Stream>.IsAny))
            .Returns((string _, out Stream stream) =>
            {
                stream = File.OpenRead(testZipPath);
                streamDisposable = stream;
                return true;
            });

        var zipApi = _fileService.OpenZip(testZipPath, mockFileApi.Object);
        Assert.That(zipApi, Is.Not.Null);
        
        Assert.That(zipApi.TryOpen("test.txt", out var textStream), Is.True);

        using (var reader = new StreamReader(textStream!))
        {
            Assert.That(reader.ReadToEnd(), Is.EqualTo(testZipPath));
        }
        
        textStream!.Dispose();
        streamDisposable?.Dispose();

        File.Delete(testZipPath);
    }

    [Test]
    public void RemoveAllFiles_DeletesAllFilesAndDirectories()
    {
        var testDir = Path.Combine(FileService.RootPath, "cleanup-test");
        Directory.CreateDirectory(testDir);

        File.WriteAllText(Path.Combine(testDir, "test1.txt"), "data");
        Directory.CreateDirectory(Path.Combine(testDir, "subdir"));

        var mockHandler = new Mock<ILoadingHandler>();
        mockHandler.Setup(x => x.AppendJob(It.IsAny<int>())).Verifiable();
        mockHandler.Setup(x => x.AppendResolvedJob(It.IsAny<int>())).Verifiable();

        _fileService.RemoveAllFiles("cleanup-test", mockHandler.Object, CancellationToken.None);

        Assert.That(Directory.Exists(testDir), Is.True);
        Assert.That(Directory.GetFiles(testDir).Length, Is.EqualTo(0));
        Assert.That(Directory.GetDirectories(testDir).Length, Is.EqualTo(0));
    }

    [Test]
    public void OpenZip_ThrowsException_WhenFileApiFails()
    {
        var mockFileApi = new Mock<IFileApi>();
        mockFileApi.Setup(x => x.TryOpen(It.IsAny<string>(), out It.Ref<Stream>.IsAny))
                   .Returns(false);

        var result = _fileService.OpenZip("invalid.zip", mockFileApi.Object);
        Assert.That(result, Is.Null);
    }
}
