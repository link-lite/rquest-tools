using System.Text;
using System.Text.Json;
using Flurl;
using Microsoft.Extensions.Options;
using RquestBridge.Config;
using RquestBridge.Models;

namespace RquestBridge.Services;

public class HutchApiClient
{
  private readonly HttpClient _client;
  private readonly HutchAgentOptions _hutchAgentOptions;
  private readonly ILogger<HutchApiClient> _logger;
  private readonly MinioOptions _minioOptions;

  public HutchApiClient(
    HttpClient client,
    IOptions<HutchAgentOptions> hutchAgentOptions,
    ILogger<HutchApiClient> logger, IOptions<MinioOptions> minioOptions)
  {
    _client = client;
    _logger = logger;
    _minioOptions = minioOptions.Value;
    _hutchAgentOptions = hutchAgentOptions.Value;
  }

  private StringContent AsHttpJsonString<T>(T value)
    => new StringContent(
      JsonSerializer.Serialize(value),
      Encoding.UTF8,
      "application/json");

  public async Task HutchEndpointPost(string jobId)
  {
    var requestUri = Url.Combine(_hutchAgentOptions.Host,
      _hutchAgentOptions.EndpointBase,
      _hutchAgentOptions.SubmitJobEndpoint
    );
    var payload = new HutchJob()
    {
      SubId = jobId,
      CrateSource = new FileStorageDetails()
      {
        AccessKey = _minioOptions.AccessKey,
        Bucket = _minioOptions.Bucket,
        Path = jobId + ".zip",
        SecretKey = _minioOptions.SecretKey,
        Host = _minioOptions.Host,
        Secure = _minioOptions.Secure
      }
    };
    // POST to HutchAgent
    var response = await _client.PostAsync(requestUri, AsHttpJsonString(payload));
    _logger.LogInformation(response.StatusCode.ToString());
  }
}
