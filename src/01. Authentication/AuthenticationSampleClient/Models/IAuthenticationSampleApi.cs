using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthenticationSampleClient.Models;

public interface IAuthenticationSampleApi
{
    [Get("/api/Plants/getplants")]
    Task<IEnumerable<Plant>> GetPlants();

    //[Get("/api/values/{id}")]
    //Task<string> GetWithParameter([AliasAs("id")] int id);

    //[Post("/api/values")]
    //Task<string> PostWithTestObject([Body] ModelForTest modelObject);

    //[Put("/api/values/{id}")]
    //Task<string> PutWithParameters([AliasAs("id")] int id, [Body] ModelForTest modelObject);

    //[Delete("/api/values/{id}")]
    //Task<string> DeleteWithParameters([AliasAs("id")] int id);
}
