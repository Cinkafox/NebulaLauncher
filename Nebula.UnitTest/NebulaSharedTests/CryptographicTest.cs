using Nebula.Shared.Utils;

namespace Nebula.UnitTest.NebulaSharedTests;

[TestFixture]
[TestOf(typeof(CryptographicStore))]
public class CryptographicTest
{
    [Test]
    public async Task EncryptDecrypt()
    {
        var key = CryptographicStore.GetComputerKey();
        Console.WriteLine($"Key: {key}");
        var entry = new TestEncryptEntry("Hello", "World");
        Console.WriteLine($"Raw data: {entry}");
        var encrypt = CryptographicStore.Encrypt(entry, key);
        Console.WriteLine($"Encrypted data: {encrypt}");
        var decrypt = await CryptographicStore.Decrypt<TestEncryptEntry>(encrypt, key);
        Console.WriteLine($"Decrypted data: {decrypt}");
        Assert.That(decrypt, Is.EqualTo(entry));
    }
}

public record struct TestEncryptEntry(string Key, string Value);