using System.Net;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using Microsoft.Extensions.Options;
using WriteFluency.Users.WebApi.Options;

namespace WriteFluency.Users.WebApi.Authentication;

internal sealed record LoginGeoLocationData(
    string? CountryIsoCode,
    string? CountryName,
    string? City);

internal interface ILoginGeoLocationDataSource
{
    LoginGeoLocationData? Lookup(IPAddress ipAddress);
}

internal sealed class MaxMindGeoLocationDataSource : ILoginGeoLocationDataSource, IDisposable
{
    private readonly LoginLocationOptions _options;
    private readonly TokenCredential _tokenCredential;
    private readonly object _sync = new();

    private DatabaseReader? _databaseReader;
    private MemoryStream? _databaseStream;
    private BlobClient? _blobClient;
    private ETag? _currentBlobEtag;
    private DateTimeOffset _nextBlobMetadataCheckUtc = DateTimeOffset.MinValue;

    public MaxMindGeoLocationDataSource(
        IOptions<LoginLocationOptions> options,
        TokenCredential tokenCredential)
    {
        _options = options.Value;
        _tokenCredential = tokenCredential;
    }

    public LoginGeoLocationData? Lookup(IPAddress ipAddress)
    {
        var reader = EnsureReader();

        try
        {
            var city = reader.City(ipAddress);
            var countryIsoCode = Normalize(city.Country.IsoCode);
            var countryName = Normalize(city.Country.Name);
            var cityName = Normalize(city.City.Name);

            if (countryIsoCode is null && countryName is null && cityName is null)
            {
                return null;
            }

            return new LoginGeoLocationData(countryIsoCode, countryName, cityName);
        }
        catch (AddressNotFoundException)
        {
            return null;
        }
    }

    private DatabaseReader EnsureReader()
    {
        return ShouldUseBlobSource()
            ? EnsureBlobReader()
            : EnsureLocalReader();
    }

    private DatabaseReader EnsureLocalReader()
    {
        lock (_sync)
        {
            if (_databaseReader is not null && _databaseStream is null)
            {
                return _databaseReader;
            }

            var path = _options.GeoLite2CityDbPath?.Trim();
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("LoginLocation:GeoLite2CityDbPath is not configured.");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"GeoLite2 database file was not found at '{path}'.", path);
            }

            ReplaceReader(new DatabaseReader(path), newStream: null);
            _currentBlobEtag = null;
            _nextBlobMetadataCheckUtc = DateTimeOffset.MinValue;

            return _databaseReader!;
        }
    }

    private DatabaseReader EnsureBlobReader()
    {
        var nowUtc = DateTimeOffset.UtcNow;
        if (_databaseReader is not null
            && _databaseStream is not null
            && nowUtc < _nextBlobMetadataCheckUtc)
        {
            return _databaseReader;
        }

        lock (_sync)
        {
            nowUtc = DateTimeOffset.UtcNow;
            if (_databaseReader is not null
                && _databaseStream is not null
                && nowUtc < _nextBlobMetadataCheckUtc)
            {
                return _databaseReader;
            }

            _blobClient ??= CreateBlobClient();
            var properties = GetBlobProperties(_blobClient);
            _nextBlobMetadataCheckUtc = nowUtc.AddMinutes(GetBlobMetadataRefreshMinutes());

            if (_databaseReader is not null
                && _databaseStream is not null
                && _currentBlobEtag.HasValue
                && properties.ETag == _currentBlobEtag.Value)
            {
                return _databaseReader;
            }

            var stream = new MemoryStream();
            DownloadBlobToStream(_blobClient, stream);
            stream.Position = 0;

            ReplaceReader(new DatabaseReader(stream), stream);
            _currentBlobEtag = properties.ETag;

            return _databaseReader!;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _databaseReader?.Dispose();
            _databaseStream?.Dispose();
            _databaseReader = null;
            _databaseStream = null;
        }
    }

    private bool ShouldUseBlobSource()
    {
        return !string.IsNullOrWhiteSpace(_options.GeoLite2CityBlobUri);
    }

    private BlobClient CreateBlobClient()
    {
        var blobUriRaw = _options.GeoLite2CityBlobUri?.Trim();
        if (string.IsNullOrWhiteSpace(blobUriRaw))
        {
            throw new InvalidOperationException("LoginLocation:GeoLite2CityBlobUri is not configured.");
        }

        if (!Uri.TryCreate(blobUriRaw, UriKind.Absolute, out var blobUri))
        {
            throw new InvalidOperationException("LoginLocation:GeoLite2CityBlobUri must be an absolute URI.");
        }

        return new BlobClient(blobUri, _tokenCredential);
    }

    private BlobProperties GetBlobProperties(BlobClient blobClient)
    {
        try
        {
            return blobClient.GetProperties().Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"GeoLite2 blob was not found at '{blobClient.Uri}'.", blobClient.Uri.AbsoluteUri, ex);
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException($"Failed to read GeoLite2 blob metadata from '{blobClient.Uri}'.", ex);
        }
    }

    private static void DownloadBlobToStream(BlobClient blobClient, Stream destination)
    {
        try
        {
            blobClient.DownloadTo(destination);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"GeoLite2 blob was not found at '{blobClient.Uri}'.", blobClient.Uri.AbsoluteUri, ex);
        }
        catch (RequestFailedException ex)
        {
            throw new InvalidOperationException($"Failed to download GeoLite2 blob from '{blobClient.Uri}'.", ex);
        }
    }

    private double GetBlobMetadataRefreshMinutes()
    {
        return Math.Max(1, _options.BlobMetadataRefreshMinutes);
    }

    private void ReplaceReader(DatabaseReader newReader, MemoryStream? newStream)
    {
        var oldReader = _databaseReader;
        var oldStream = _databaseStream;

        _databaseReader = newReader;
        _databaseStream = newStream;

        oldReader?.Dispose();
        oldStream?.Dispose();
    }

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
