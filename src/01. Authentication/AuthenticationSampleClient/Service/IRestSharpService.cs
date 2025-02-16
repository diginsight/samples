using RestSharp;

namespace AuthenticationSampleClient;

public interface IRestSharpService
{
    Task<RestResponse> Get(string baseUrl, Dictionary<string, string> parameters, CancellationToken? cancellationToken = default);

    Task<RestResponse> Post(string baseUrl, Dictionary<string, string> parameters, string body, CancellationToken? cancellationToken = default);

    Task<RestResponse> Put(string baseUrl, Dictionary<string, string> parameters, string body, CancellationToken? cancellationToken = default);
}
