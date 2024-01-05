using PgpCore;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;

namespace RL.Common.Encryption;

public class PgpCrypto(ILogger<PgpCrypto> logger, IConfiguration configuration)
{
    private readonly ILogger<PgpCrypto> _logger = logger ??
        throw new ArgumentNullException(nameof(logger));

    private readonly IConfiguration _configuration = configuration ??
        throw new ArgumentNullException(nameof(configuration));

    [Function(nameof(PgpCrypto))]
    public async Task Run([BlobTrigger("encrypt/{name}", Connection = "AzureWebJobsStorage")] BlobClient blobClient,
        string name
    )
    {
        _logger.LogInformation("C# Blob trigger function processing blob\n Name:{name}", name);
        string publicKey = _configuration["PublicKey"] ?? throw new ArgumentNullException("PublicKey", "Parameter 'PublicKey' is not defined in settings file or it is not valid");
        string connectionString = _configuration["AzureWebJobsStorage"] ?? throw new ArgumentNullException("ConnectionString", "Parameter 'ConnectionString' is not defined in settings file or it is not valid"); ;

        var inputStream = await blobClient.OpenReadAsync(new BlobOpenReadOptions(true));

        using var outputStream = new MemoryStream();
        using var publicKeyStream = new FileStream(publicKey, FileMode.Open);
        var encryptionKeys = new EncryptionKeys(publicKeyStream);

        var pgp = new PGP(encryptionKeys);
        await pgp.EncryptAsync(inputStream, outputStream);

        var blobOutClient = new BlobClient(connectionString, "encrypted", $"{name}.pgp");
        outputStream.Position = 0;
        await blobOutClient.UploadAsync(outputStream);
        inputStream.Dispose();
    }
}
