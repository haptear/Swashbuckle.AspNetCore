using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Swashbuckle.AspNetCore.Swagger;

namespace Swashbuckle.AspNetCore.SwaggerGen
{
    public class SwaggerResponseAttributeFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            if (context.MethodInfo == null) return;
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            var swaggerResponseAttributes = context.MethodInfo.GetCustomAttributes(true)
                .Union(context.MethodInfo.DeclaringType.GetTypeInfo().GetCustomAttributes(true))
                .OfType<SwaggerResponseAttribute>();

            var apiDesc = context.ApiDescription;
            var attributes = GetActionAttributes(apiDesc);

            if (!attributes.Any())
                return;

            if (operation.Responses == null)
            {
                operation.Responses = new Dictionary<string, Response>();
            }

            foreach (var attribute in attributes)
            {
                ApplyAttribute(operation, context, attribute);
            }
        }

        private static void ApplyAttribute(Operation operation, OperationFilterContext context, SwaggerResponseAttribute attribute)
        {
            var key = attribute.StatusCode.ToString();
            if (!operation.Responses.TryGetValue(key, out Response response))
            {
                response = new Response();
            }

            if (attribute.Description != null)
                response.Description = attribute.Description;

            if (attribute.Type != null && attribute.Type != typeof(void))
                response.Schema = context.SchemaRegistry.GetOrRegister(attribute.Type);

            operation.Responses[key] = response;
        }

        private static IEnumerable<SwaggerResponseAttribute> GetActionAttributes(ApiDescription apiDesc)
        {
            var controllerAttributes = apiDesc.ControllerAttributes().OfType<SwaggerResponseAttribute>();
            var actionAttributes = apiDesc.ActionAttributes().OfType<SwaggerResponseAttribute>();
            return controllerAttributes.Union(actionAttributes);
        }
    }
}
