using Microsoft.Extensions.DependencyInjection;
using Nebula.Shared.Services;

namespace Nebula.UnitTest.NebulaSharedTests;

[TestFixture]
[TestOf(nameof(PopupMessageService))]
public class PopupMessageServiceTests : BaseSharedTest
{
    public override void BeforeServiceBuild(IServiceCollection services)
    {
    }

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        
        var popupService = _sharedUnit.GetService<PopupMessageService>();
        
        popupService.OnCloseRequired = (popup) => ((IDisposable)popup).Dispose();
    }

    [Test]
    public void DisposeTest()
    {
        var popup = new TestPopup();
        var popupService = _sharedUnit.GetService<PopupMessageService>();
        popupService.ClosePopup(popup);
        Assert.That(popup.Disposed, Is.True);
    }

    private sealed class TestPopup : IDisposable
    {
        public bool Disposed { get; private set; }
        
        public void Dispose()
        {
            Disposed = true;
        }
    }
}