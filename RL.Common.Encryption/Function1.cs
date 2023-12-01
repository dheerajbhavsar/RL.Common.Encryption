using PgpCore;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Azure.Storage.Blobs;

namespace RL.Common.Encryption;

public class Function1(ILogger<Function1> logger, IConfiguration configuration)
{
    private readonly ILogger<Function1> _logger = logger ??
        throw new ArgumentNullException(nameof(logger));

    private readonly IConfiguration _configuration = configuration ??
        throw new ArgumentNullException(nameof(configuration));

    [Function(nameof(Function1))]
    public async Task Run([BlobTrigger("encrypt/{name}", Connection = "AzureWebJobsStorage")] BlobClient blobClient,
        string name
    )
    {
        _logger.LogInformation("C# Blob trigger function processed blob\n Name:{name}", name);
        string publicKey = _configuration["PublicKey"] ?? throw new ArgumentNullException("PublicKey", "Parameter 'PublicKey' is not defined in settings file or it is not valid'");
        string connectionString = _configuration["AzureWebJobsStorage"] ?? throw new ArgumentNullException("ConnectionString", "Parameter 'ConnectionString' is not defined in settings file or it is not valid'"); ;

        var inputStream = await blobClient.OpenReadAsync(new Azure.Storage.Blobs.Models.BlobOpenReadOptions(true));

        using MemoryStream outputStream = new();
        EncryptionKeys encryptionKeys;
        using Stream publicKeyStream = new FileStream(publicKey, FileMode.Open);
        encryptionKeys = new EncryptionKeys(publicKeyStream);

        var pgp = new PGP(encryptionKeys);
        await pgp.EncryptAsync(inputStream, outputStream);

        var blobOutClient = new BlobClient(connectionString, "encrypted", $"{name}.pgp");
        outputStream.Position = 0;
        await blobOutClient.UploadAsync(outputStream);
        inputStream.Dispose();
    }
}
