using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DHT.Server.Service.Middlewares;
using DHT.Utils.Resources;
using Microsoft.AspNetCore.Http;

namespace DHT.Server.Endpoints;

[ServerAuthorizationMiddleware.NoAuthorization]
sealed class ViewerEndpoint(ResourceLoader resources) : BaseEndpoint {
	private readonly Dictionary<string, byte[]?> cache = new ();
	private readonly SemaphoreSlim cacheSemaphore = new (1);
	
	protected override async Task Respond(HttpRequest request, HttpResponse response, CancellationToken cancellationToken) {
		string path = (string?) request.RouteValues["path"] ?? "index.html";
		string resourcePath = "Viewer/" + path;
		
		byte[]? resourceBytes = await resources.ReadExternalBytesAsyncIfExists(resourcePath);
		if (resourceBytes != null) {
			response.Headers.CacheControl = "no-store";
			await WriteFileIfFound(response, path, resourceBytes, cancellationToken);
			return;
		}
		
		await cacheSemaphore.WaitAsync(cancellationToken);
		try {
			if (!cache.TryGetValue(resourcePath, out resourceBytes)) {
				cache[resourcePath] = resourceBytes = await resources.ReadBytesAsyncIfExists(resourcePath, allowExternalOverride: false);
			}
		} finally {
			cacheSemaphore.Release();
		}
		
		await WriteFileIfFound(response, path, resourceBytes, cancellationToken);
	}
}
