using Azure.Core;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleClient;

public class RestSharpService
{
    TokenCredential credential;

    public RestSharpService(TokenCredential credential)
    {
        this.credential = credential;
    }

    public async Task<RestResponse> Get(string baseUrl, Dictionary<string, string> parameters, CancellationToken? cancellationToken = default)
    {
        RestClient client = new RestClient();
        RestRequest request = new RestRequest(baseUrl, Method.Get);

        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                request.AddParameter(p.Key, p.Value, ParameterType.QueryString);
            }
        }

        var token = await credential.GetTokenAsync(new TokenRequestContext(Constants.Scopes), cancellationToken ?? CancellationToken.None);
        request.AddHeader("Authorization", $"Bearer {token.Token}");
        request.AddHeader("Content-Type", "application/json");
        //request.AddHeader("ZUMO-API-VERSION", "3.0.0");


        var response = await client.ExecuteAsync(request);
        return response;
    }

    public async Task<RestResponse> Post(string baseUrl, Dictionary<string, string> parameters, string body, CancellationToken? cancellationToken = default)
    {
        RestClient client = new RestClient();
        RestRequest request = new RestRequest(baseUrl, Method.Post);

        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                request.AddParameter(p.Key, p.Value, ParameterType.QueryString);
            }
        }

        var token = await credential.GetTokenAsync(new TokenRequestContext() { }, cancellationToken ?? CancellationToken.None);
        request.AddStringBody(body, DataFormat.Json);
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", token.Token);
        var response = await client.ExecuteAsync(request);
        return response;
    }

    public async Task<RestResponse> Put(string baseUrl, Dictionary<string, string> parameters, string body, CancellationToken? cancellationToken = default)
    {
        RestClient client = new RestClient();
        RestRequest request = new RestRequest(baseUrl, Method.Put);

        if (parameters != null)
        {
            foreach (var p in parameters)
            {
                request.AddParameter(p.Key, p.Value, ParameterType.QueryString);
            }

        }
        var token = await credential.GetTokenAsync(new TokenRequestContext() { }, cancellationToken ?? CancellationToken.None);
        request.AddStringBody(body, DataFormat.Json);
        request.AddHeader("Content-Type", "application/json");
        request.AddHeader("Authorization", token.Token);
        var response = await client.ExecuteAsync(request);
        return response;
    }
}
