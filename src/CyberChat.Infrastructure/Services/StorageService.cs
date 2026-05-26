using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Minio;
using Minio.DataModel.Args;
using CyberChat.Application.Common.Interfaces;

namespace CyberChat.Infrastructure.Services;

public class StorageService : IStorageService
{
    private readonly IMinioClient? _minioClient;
    private readonly string _localStoragePath;
    private readonly string _localBaseUrl;
    private readonly bool _useMinio;

    public StorageService(IConfiguration configuration)
    {
        var endpoint = configuration["Minio:Endpoint"];
        var accessKey = configuration["Minio:AccessKey"];
        var secretKey = configuration["Minio:SecretKey"];
        var useSSL = bool.TryParse(configuration["Minio:Secure"], out var secure) && secure;

        _useMinio = !string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(accessKey);

        if (_useMinio)
        {
            try
            {
                _minioClient = new MinioClient()
                    .WithEndpoint(endpoint)
                    .WithCredentials(accessKey, secretKey)
                    .WithSSL(useSSL)
                    .Build();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Minio client initialization failed. Falling back to local storage. Error: {ex.Message}");
                _useMinio = false;
            }
        }

        // Setup local storage fallback path
        _localStoragePath = configuration["Storage:LocalPath"] ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "uploads");
        _localBaseUrl = configuration["Storage:LocalBaseUrl"] ?? "/uploads";

        if (!_useMinio && !Directory.Exists(_localStoragePath))
        {
            Directory.CreateDirectory(_localStoragePath);
        }
    }

    public async Task<string> UploadFileAsync(string bucketName, string fileName, Stream stream, string contentType, CancellationToken cancellationToken = default)
    {
        if (_useMinio && _minioClient != null)
        {
            try
            {
                // Ensure bucket exists
                var bucketExists = await _minioClient.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucketName), cancellationToken);
                if (!bucketExists)
                {
                    await _minioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucketName), cancellationToken);
                }

                // Upload file
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName)
                    .WithStreamData(stream)
                    .WithObjectSize(stream.Length)
                    .WithContentType(contentType);

                await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

                // Return URL
                // In production, return the object URL, for this example we return a reconstructed URL
                return $"/{bucketName}/{fileName}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Minio upload failed: {ex.Message}. Falling back to local storage.");
            }
        }

        // Local storage fallback
        var localFilePath = Path.Combine(_localStoragePath, fileName);
        var directory = Path.GetDirectoryName(localFilePath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var fileStream = File.Create(localFilePath))
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        // Return local relative URL
        return $"{_localBaseUrl}/{fileName.Replace('\\', '/')}";
    }

    public async Task DeleteFileAsync(string bucketName, string fileName, CancellationToken cancellationToken = default)
    {
        if (_useMinio && _minioClient != null)
        {
            try
            {
                var removeObjectArgs = new RemoveObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName);

                await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Minio file deletion failed: {ex.Message}. Falling back to local storage.");
            }
        }

        // Local file delete
        var localFilePath = Path.Combine(_localStoragePath, fileName);
        if (File.Exists(localFilePath))
        {
            File.Delete(localFilePath);
        }
        await Task.CompletedTask;
    }
}
