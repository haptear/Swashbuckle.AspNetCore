using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

namespace Swashbuckle.AspNetCore.Swagger
{
    public class SwaggerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ISwaggerProvider _swaggerProvider;
        private readonly JsonSerializer _swaggerSerializer;
        private readonly SwaggerOptions _options;
        private readonly TemplateMatcher _requestMatcher;

        public SwaggerMiddleware(
            RequestDelegate next,
            ISwaggerProvider swaggerProvider,
            IOptions<MvcJsonOptions> mvcJsonOptions,
            SwaggerOptions options)
        {
            _next = next;
            _swaggerProvider = swaggerProvider;
            _swaggerSerializer = SwaggerSerializerFactory.Create(mvcJsonOptions);
            _options = options;
            _requestMatcher = new TemplateMatcher(TemplateParser.Parse(options.RouteTemplate), new RouteValueDictionary());
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (!RequestingSwaggerDocument(httpContext.Request, out string documentName))
            {
                await _next(httpContext);
                return;
            }

            var basePath = string.IsNullOrEmpty(httpContext.Request.PathBase)
                ? null
                : httpContext.Request.PathBase.ToString();

            try
            {
                var swagger = _swaggerProvider.GetSwagger(documentName, null, basePath);

                // One last opportunity to modify the Swagger Document - this time with request context
                foreach (var filter in _options.PreSerializeFilters)
                {
                    filter(swagger, httpContext.Request);
                }

               await RespondWithSwaggerJson(httpContext.Response, swagger);
            }
            catch (Exception ex)
            {
                Console.WriteLine(GetException(ex));
                throw ex;
            }

        }

        /// <summary>
        /// 构造异常信息
        /// </summary>
        /// <param name="ex">The ex.</param>
        /// <returns>String.</returns>
        public static String GetException(Exception ex)
        {
            try
            {
                if (ex == null)
                {
                    return String.Empty;
                }
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("<exception logTime='{0}'>\r\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendFormat("    <type><![CDATA[{0}]]></type>\r\n", ex.GetType().FullName);
                sb.AppendFormat("    <message><![CDATA[\r\n{0}\r\n]]></message>\r\n", ex.Message);
                sb.AppendFormat("    <stackTrace><![CDATA[\r\n{0}\r\n]]></stackTrace>\r\n", ex.StackTrace);
                if (ex.InnerException != null)
                {
                    AppendInnerExceptionInfo(sb, ex.InnerException, "    ");
                }
                sb.Append("</exception>\r\n");
                return sb.ToString();
            }
            catch
            {
                return "Exception when analyse service exception.";
            }
        }

        //构造异常信息
        /// <summary>
        /// Appends the inner exception information.
        /// </summary>
        /// <param name="sb">The sb.</param>
        /// <param name="ex">The ex.</param>
        /// <param name="prefix">The prefix.</param>
        private static void AppendInnerExceptionInfo(StringBuilder sb, Exception ex, String prefix)
        {
            sb.AppendFormat("{0}<innerException>\r\n", prefix);
            sb.AppendFormat("{0}    <type><![CDATA[{1}]]></type>\r\n", prefix, ex.GetType().FullName);
            sb.AppendFormat("{0}    <message><![CDATA[\r\n{1}\r\n]]></message>\r\n", prefix, ex.Message);
            sb.AppendFormat("{0}    <stackTrace><![CDATA[\r\n{1}\r\n]]></stackTrace>\r\n", prefix, ex.StackTrace);
            if (ex.InnerException != null)
            {
                AppendInnerExceptionInfo(sb, ex.InnerException, prefix + "    ");
            }
            sb.AppendFormat("{0}</innerException>\r\n", prefix);
        }

        private bool RequestingSwaggerDocument(HttpRequest request, out string documentName)
        {
            documentName = null;
            if (request.Method != "GET") return false;

			var routeValues = new RouteValueDictionary();
            if (!_requestMatcher.TryMatch(request.Path, routeValues) || !routeValues.ContainsKey("documentName")) return false;

            documentName = routeValues["documentName"].ToString();
            return true;
        }

        private async Task RespondWithSwaggerJson(HttpResponse response, SwaggerDocument swagger)
        {
            response.StatusCode = 200;
            response.ContentType = "application/json;charset=utf-8";

            var jsonBuilder = new StringBuilder();
            using (var writer = new StringWriter(jsonBuilder))
            {
                _swaggerSerializer.Serialize(writer, swagger);
                await response.WriteAsync(jsonBuilder.ToString(), new UTF8Encoding(false));
            }
        }
    }
}
