using System.Reflection;
using System.Text;
using DHT.Utils.Resources;

const string Prefix = "ResourceLoaderTestOverrides/";
string testDirectory = Path.Combine(AppContext.BaseDirectory, "ResourceLoaderTestOverrides");
string originalWorkingDirectory = Environment.CurrentDirectory;
int checks = 0;

void Equal<T>(T expected, T actual, string message) {
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) {
        throw new Exception($"{message}: expected '{expected}', got '{actual}'");
    }
    checks++;
}

// Never remove a pre-existing directory. All test files stay in this owned directory.
if (Directory.Exists(testDirectory) || File.Exists(testDirectory)) {
    throw new Exception("Test directory already exists: " + testDirectory);
}
Directory.CreateDirectory(testDirectory);

try {
    var loader = new ResourceLoader(Assembly.GetExecutingAssembly());
    string alpha = Prefix + "alpha.txt";
    string beta = Prefix + "beta.txt";
    string alphaPath = Path.Combine(testDirectory, "alpha.txt");
    string betaPath = Path.Combine(testDirectory, "beta.txt");

    string embeddedAlpha = await loader.ReadTextAsync(alpha);
    string embeddedBeta = await loader.ReadTextAsync(beta);
    Equal("embedded-alpha", embeddedAlpha.Trim(), "Embedded text fallback");
    Equal("embedded-beta", embeddedBeta.Trim(), "Forward-slash manifest name");
    Equal(true, (await loader.ReadBytesAsyncIfExists(alpha))!.SequenceEqual(Encoding.UTF8.GetBytes(embeddedAlpha)), "Embedded byte fallback");
    Equal<byte[]?>(null, await loader.ReadExternalBytesAsyncIfExists(alpha), "Missing external file does not return embedded bytes");

    await File.WriteAllTextAsync(alphaPath, "external-alpha");
    Equal("external-alpha", await loader.ReadTextAsync(alpha), "External text overrides embedded text");
    Equal("external-alpha", await loader.ReadTextAsync(alpha.Replace('/', '\\')), "Backslash request path");
    Equal("external-alpha", Encoding.UTF8.GetString((await loader.ReadExternalBytesAsyncIfExists(alpha))!), "External-only reads return the override");
    Equal(embeddedAlpha, Encoding.UTF8.GetString((await loader.ReadBytesAsyncIfExists(alpha, allowExternalOverride: false))!), "Cache population always reads embedded bytes even if an override appears");

    Environment.CurrentDirectory = testDirectory;
    Equal("external-alpha", await loader.ReadTextAsync(alpha), "Resolve relative to the executable, not the working directory");

    await File.WriteAllTextAsync(alphaPath, "edited-alpha");
    Equal("edited-alpha", await loader.ReadTextAsync(alpha), "Reread an edited override");
    Equal("edited-alpha", Encoding.UTF8.GetString((await loader.ReadExternalBytesAsyncIfExists(alpha))!), "External byte reads do not cache edits");
    await File.WriteAllTextAsync(alphaPath, "");
    Equal("", await loader.ReadTextAsync(alpha), "An empty override does not fall back");
    Equal(0, (await loader.ReadExternalBytesAsyncIfExists(alpha))!.Length, "Empty external bytes remain an override");
    File.Delete(alphaPath);
    Equal(embeddedAlpha, await loader.ReadTextAsync(alpha), "Removing an override restores embedded text");
    Equal<byte[]?>(null, await loader.ReadExternalBytesAsyncIfExists(alpha), "Removing an override returns control to the embedded cache");

    byte[] externalBytes = [0, 255, 128, 42];
    await File.WriteAllBytesAsync(betaPath, externalBytes);
    Equal(true, (await loader.ReadBytesAsyncIfExists(beta))!.SequenceEqual(externalBytes), "External binary override");
    File.Delete(betaPath);

    await File.WriteAllTextAsync(alphaPath, "joined-alpha");
    await File.WriteAllTextAsync(Path.Combine(testDirectory, "extra.js"), "must not be included");
    Equal(embeddedBeta + "|joined-alpha", await loader.ReadJoinedAsync(Prefix, '|', ["/beta.txt"]), "Joined scripts preserve order and mix overrides with embedded fallback");
    Equal(embeddedBeta + "|joined-alpha", await loader.ReadJoinedAsync(Prefix.Replace('/', '\\'), '|', ["/beta.txt"]), "Backslash joined prefix");
    await File.WriteAllTextAsync(alphaPath, "joined-edited");
    Equal(embeddedBeta + "|joined-edited", await loader.ReadJoinedAsync(Prefix, '|', ["/beta.txt"]), "Joined scripts reread edits");
    File.Delete(alphaPath);
    Equal(embeddedBeta + "|" + embeddedAlpha, await loader.ReadJoinedAsync(Prefix, '|', ["/beta.txt"]), "Joined scripts fall back after removal");
    Equal("", await loader.ReadJoinedAsync("Missing/", '|', []), "Empty resource group");

    Equal<byte[]?>(null, await loader.ReadBytesAsyncIfExists(Prefix + "extra.js"), "Do not expose external-only files");
    Equal<byte[]?>(null, await loader.ReadExternalBytesAsyncIfExists(Prefix + "extra.js"), "External-only reads still require an embedded resource");
    Equal<byte[]?>(null, await loader.ReadBytesAsyncIfExists(Path.Combine(testDirectory, "extra.js")), "Do not expose absolute paths");
    Equal<byte[]?>(null, await loader.ReadBytesAsyncIfExists(Prefix + "../ResourceLoaderTestOverrides/extra.js"), "Do not expose traversal paths");

    bool missingTextRejected = false;
    try {
        await loader.ReadTextAsync(Prefix + "extra.js");
    } catch (ArgumentException) {
        missingTextRejected = true;
    }
    Equal(true, missingTextRejected, "Missing text resources remain errors");

    Console.WriteLine($"Passed {checks} resource loader checks.");
} finally {
    Environment.CurrentDirectory = originalWorkingDirectory;
    Directory.Delete(testDirectory, recursive: true);
}
