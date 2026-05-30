using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace HMDN_QuanLyVatTu
{
    public static class WebApiConfig
    {
        //public static void Register(HttpConfiguration config)
        //{
        //    // Web API configuration and services

        //    // Web API routes
        //    config.MapHttpAttributeRoutes();

        //    config.Routes.MapHttpRoute(
        //        name: "DefaultApi",
        //        routeTemplate: "api/{controller}/{id}",
        //        defaults: new { id = RouteParameter.Optional }
        //    );
        //}
        public static void Register(HttpConfiguration config)
        {
            config.MapHttpAttributeRoutes(); // BẮT BUỘC phải có

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{action}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // Configure JSON Formatter settings
            var json = config.Formatters.JsonFormatter;
            
            // Fix Circular Reference Loop globally
            json.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
            
            // Ensure UTF-8 is the primary encoding
            json.SupportedEncodings.Clear();
            json.SupportedEncodings.Add(new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            
            // Add custom handler to automatically append charset=utf-8 to json response Content-Type headers
            config.MessageHandlers.Add(new JsonCharsetHandler());
        }
    }

    public class JsonCharsetHandler : System.Net.Http.DelegatingHandler
    {
        protected override async System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.Content != null && response.Content.Headers.ContentType != null && 
                response.Content.Headers.ContentType.MediaType == "application/json")
            {
                response.Content.Headers.ContentType.CharSet = "utf-8";
            }
            return response;
        }
    }
}
