using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using DotLiquid;
using System.Text;
using System;
using Microsoft.Extensions.Logging;

namespace LiquidTransform.functionapp
{
    public static class LiquidTransformer
    {
       
        [FunctionName("LiquidTransformer")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "liquidtransformer/{liquidtransformfilename}")] HttpRequestMessage req,
            [Blob("liquid-transforms/{liquidtransformfilename}", FileAccess.Read)] Stream inputBlob,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            if (inputBlob == null)
            {
                log.LogError("inputBlog null");
                return req.CreateErrorResponse(HttpStatusCode.NotFound, "Liquid transform not found");
            }            

            // Load the Liquid transform in a string
            var sr = new StreamReader(inputBlob);
            var liquidTransform = sr.ReadToEnd();

            var contentReader = new JsonContentReader();
            var contentWriter = new JsonContentWriter("application/json");

            Hash inputHash;

            try
            {
                inputHash = await contentReader.ParseRequestAsync(req.Content);

            }
            catch (Exception ex)
            {
                log.LogError(ex.Message, ex);
                return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Error parsing request body", ex);
            }

            // Register the Liquid custom filter extensions
           // Template.RegisterFilter(typeof(CustomFilters));

            // Execute the Liquid transform
            Template template;

            try
            {
                template = Template.Parse(liquidTransform);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message, ex);
                return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Error parsing Liquid template", ex);
            }

            string output = string.Empty;

            try
            {
                output = template.Render(inputHash);
            }
            catch (Exception ex)
            {
                log.LogError(ex.Message, ex);
                return req.CreateErrorResponse(HttpStatusCode.InternalServerError, "Error rendering Liquid template", ex);
            }

            if (template.Errors != null && template.Errors.Count > 0)
            {
                if (template.Errors[0].InnerException != null)
                {
                    return req.CreateErrorResponse(HttpStatusCode.InternalServerError, $"Error rendering Liquid template: {template.Errors[0].Message}", template.Errors[0].InnerException);
                }
                else
                {
                    return req.CreateErrorResponse(HttpStatusCode.InternalServerError, $"Error rendering Liquid template: {template.Errors[0].Message}");
                }
            }

            try
            {
                var content = contentWriter.CreateResponse(output);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = content
                };
            }
            catch (Exception ex)
            {
                // Just log the error, and return the Liquid output without parsing
                log.LogError(ex.Message, ex);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(output, Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
