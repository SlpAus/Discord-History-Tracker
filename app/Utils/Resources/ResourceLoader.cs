using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DHT.Utils.Resources;

public sealed class ResourceLoader(Assembly assembly) {
	private Stream? TryGetExternalStream(string embeddedName) {
		// Only call this for a known manifest resource. Request paths must not
		// turn the resource loader into a general-purpose file server.
		string normalizedName = embeddedName.Replace(oldChar: '\\', newChar: '/');
		string externalPath = Path.Combine(AppContext.BaseDirectory, normalizedName.Replace('/', Path.DirectorySeparatorChar));
		try {
			return File.OpenRead(externalPath);
		} catch (FileNotFoundException) {
			return null;
		} catch (DirectoryNotFoundException) {
			return null;
		}
	}

	private string? FindEmbeddedName(string filename) {
		filename = filename.Replace(oldChar: '\\', newChar: '/');
		return assembly.GetManifestResourceNames().FirstOrDefault(name => name.Replace(oldChar: '\\', newChar: '/') == filename);
	}

	private Stream? TryGetResourceStream(string filename, bool allowExternalOverride = true) {
		string? embeddedName = FindEmbeddedName(filename);
		if (embeddedName == null) {
			return null;
		}

		return (allowExternalOverride ? TryGetExternalStream(embeddedName) : null) ?? assembly.GetManifestResourceStream(embeddedName);
	}
	
	private Stream GetResourceStream(string filename) {
		return TryGetResourceStream(filename) ?? throw new ArgumentException("Missing embedded resource: " + filename);
	}
	
	private async Task<string> ReadTextAsync(Stream stream) {
		using var reader = new StreamReader(stream, Encoding.UTF8);
		return await reader.ReadToEndAsync();
	}
	
	private async Task<byte[]> ReadBytesAsync(Stream stream) {
		using (stream) {
			using var memoryStream = new MemoryStream();
			await stream.CopyToAsync(memoryStream);
			return memoryStream.ToArray();
		}
	}
	
	public async Task<string> ReadTextAsync(string filename) {
		return await ReadTextAsync(GetResourceStream(filename));
	}
	
	public async Task<byte[]?> ReadBytesAsyncIfExists(string filename, bool allowExternalOverride = true) {
		return TryGetResourceStream(filename, allowExternalOverride) is {} stream ? await ReadBytesAsync(stream) : null;
	}

	public async Task<byte[]?> ReadExternalBytesAsyncIfExists(string filename) {
		string? embeddedName = FindEmbeddedName(filename);
		return embeddedName != null && TryGetExternalStream(embeddedName) is {} stream ? await ReadBytesAsync(stream) : null;
	}

	public async Task<string> ReadJoinedAsync(string path, char separator, string[] order) {
		path = path.Replace(oldChar: '\\', newChar: '/');
		List<(string NormalizedName, string EmbeddedName)> resourceNames = [];
		
		foreach (string embeddedName in assembly.GetManifestResourceNames()) {
			string embeddedNameNormalized = embeddedName.Replace(oldChar: '\\', newChar: '/');
			if (embeddedNameNormalized.StartsWith(path)) {
				resourceNames.Add((embeddedNameNormalized, embeddedName));
			}
		}
		
		StringBuilder joined = new ();
		
		int GetOrderKey(string name) {
			int key = Array.FindIndex(order, name.EndsWith);
			return key == -1 ? order.Length : key;
		}
		
		foreach ((_, string embeddedName) in resourceNames.OrderBy(item => GetOrderKey(item.NormalizedName))) {
			using Stream stream = TryGetExternalStream(embeddedName) ?? assembly.GetManifestResourceStream(embeddedName)!;
			joined.Append(await ReadTextAsync(stream)).Append(separator);
		}
		
		return joined.ToString(startIndex: 0, Math.Max(val1: 0, joined.Length - 1));
	}
}
