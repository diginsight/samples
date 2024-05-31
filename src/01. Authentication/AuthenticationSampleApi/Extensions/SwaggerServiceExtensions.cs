namespace SampleWebApi
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.OpenApi.Models;
    using Swashbuckle.AspNetCore.SwaggerGen;

    //namespace ABB.EL.Common.Api.Controllers.DataExport.Swagger;

    public class FixNestedSwaggerParameterAttribute : Attribute, IFilterMetadata
    {
    }

    public class FixNestedSwaggerParameterOperationFilter : IOperationFilter
    {
        private readonly string? documentName;

        public FixNestedSwaggerParameterOperationFilter(string? documentName = null)
        {
            this.documentName = documentName;
        }

        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            // if documentName is null, just process all documents
            if (this.documentName is not null && context.DocumentName != this.documentName)
            {
                return;
            }

            // The first implementation just searches for FixNestedSwaggerParameterAttribute and in case it founds it 
            // just filters out any name that contains a dot

            var fixAttributes = context.ApiDescription.ActionDescriptor.FilterDescriptors
                .Select(f => f.Filter)
                .OfType<FixNestedSwaggerParameterAttribute>();

            // var fixAttributes = context.MethodInfo.DeclaringType.GetCustomAttributes(true)
            //     .Union(context.MethodInfo.GetCustomAttributes(true))
            //     .OfType<FixNestedSwaggerParameterAttribute>();

            if (!fixAttributes.Any())
            {
                return;
            }

            foreach (var param in operation.Parameters)
            {
                if (param.Name.Contains('.'))
                {
                    param.Name = param.Name.Split('.').Last();
                }
            }
        }
    }
    public static class SwaggerServiceExtensions
    {
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("common", new OpenApiInfo { Title = "Common Services", Version = "common" });
                c.SwaggerDoc("sample-webapi", new OpenApiInfo { Title = "Sample Web Api", Version = "v1" });
                c.EnableAnnotations();

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = @"JWT Authorization header using the Bearer scheme. \r\n\r\n
                      Enter 'Bearer' [space] and then your token in the text input below.
                      \r\n\r\nExample: 'Bearer 12345abcdef'",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement()
                {
                    {
                      new OpenApiSecurityScheme
                      {
                        Reference = new OpenApiReference
                          {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                          },
                        Scheme = "oauth2",
                        Name = "Bearer",
                        In = ParameterLocation.Header,
                      },
                      new List<string>()
                    },
                });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);

                c.OperationFilter<FixNestedSwaggerParameterOperationFilter>("common");
                c.OperationFilter<FixNestedSwaggerParameterOperationFilter>("sample-webapi");
            });

            return services;
        }

        public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
        {
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/common/swagger.json", EndpointCategories.COMMONSERVICES);
                c.SwaggerEndpoint("/swagger/sample-webapi/swagger.json", EndpointCategories.DATAEXPORT);
            });

            return app;
        }
    }
    public static class EndpointCategories
    {
        public const string COMMONSERVICES = "Common Services";
        public const string DATAEXPORT = "Sample Web Api";
    }
}
