using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System;
using System.IO;
using System.Text;
using UnifiedStorage.Common.Design;

namespace UnifiedStorage.GoogleDrive;

public class DriveServiceFactory : IStorageEndpointFactory<DriveService>
{
    public DriveService CreateEndpoint(
        string connectionString)
    {
        GoogleCredential credential;
        using var stream = GenerateStreamFromString(connectionString);
        credential = GoogleCredential
            .FromStream(stream)
            .CreateScoped(new[] { DriveService.ScopeConstants.DriveFile });

        var service = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = "Emn.Storage.GoogleDrive"
        });

        return service ?? throw new InvalidOperationException(
            "Unable to initialize google drive service.");
    }

    private MemoryStream GenerateStreamFromString(string value)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
    }
}