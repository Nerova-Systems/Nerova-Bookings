using SharedKernel.Integrations.BlobStorage;
using Yarp.ReverseProxy.Transforms;

namespace AppGateway.Transformations;

public class SharedAccessSignatureRequestTransform(
    [FromKeyedServices("account-storage")] IBlobStorageClient accountBlobStorageClient,
    [FromKeyedServices("main-storage")] IBlobStorageClient mainBlobStorageClient
)
    : RequestTransform
{
    public override ValueTask ApplyAsync(RequestTransformContext context)
    {
        string containerName;
        IBlobStorageClient blobStorageClient;
        if (context.Path.StartsWithSegments("/avatars"))
        {
            containerName = "avatars";
            blobStorageClient = accountBlobStorageClient;
        }
        else if (context.Path.StartsWithSegments("/logos"))
        {
            containerName = "logos";
            blobStorageClient = accountBlobStorageClient;
        }
        else if (context.Path.StartsWithSegments("/service-images"))
        {
            containerName = "service-images";
            blobStorageClient = mainBlobStorageClient;
        }
        else
        {
            return ValueTask.CompletedTask;
        }

        var sharedAccessSignature = blobStorageClient.GetSharedAccessSignature(containerName, TimeSpan.FromMinutes(10));
        context.HttpContext.Request.QueryString = new QueryString(sharedAccessSignature);

        return ValueTask.CompletedTask;
    }
}
