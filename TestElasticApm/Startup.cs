using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elastic.Apm;
using Elastic.Apm.Api;
using Elastic.Apm.NetCoreAll;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TestElasticApm
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseAllElasticApm(_configuration);

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    var output = new StringBuilder();
                    output.Append("<ul>");
                    output.Append("<li><a href=\"/trans1/1\">Distributed Transaction 1</a></li>");
                    output.Append("<li><a href=\"/trans2\">Distributed Transaction 2</a></li>");
                    output.Append("</ul>");

                    context.Response.ContentType = "text/html";
                    await context.Response.WriteAsync(output.ToString());
                });

                endpoints.MapGet("/trans1/1", async context =>
                {
                    var trans = Agent.Tracer.StartTransaction("Dist Trans 1", ApiConstants.TypeRequest);
                    await trans.CaptureSpan("step 1 processing", ApiConstants.ActionExec, async () => await Task.Delay(30));
                    trans.End();
                    
                    var trace = trans.OutgoingDistributedTracingData.SerializeToString();

                    await context.Response.WriteAsync($"<a href=\"/trans1/2?s={trace}\">Continue</a>");
                });
                
                endpoints.MapGet("/trans1/2", async context =>
                {
                    var deser = DistributedTracingData.TryDeserializeFromString(context.Request.Query["s"]);
                    
                    var trans = Agent.Tracer.StartTransaction("Dist Trans 1", ApiConstants.TypeRequest,
                        deser);
                    await trans.CaptureSpan("step 2 processing", ApiConstants.ActionExec, async () => await Task.Delay(15));
                    trans.End();
                    
                    var trace = trans.OutgoingDistributedTracingData.SerializeToString();
                    
                    await context.Response.WriteAsync($"<a href=\"/trans1/3?s={trace}\">Continue</a>");
                });
                
                endpoints.MapGet("/trans1/3", async context =>
                {
                    var deser = DistributedTracingData.TryDeserializeFromString(context.Request.Query["s"]);
                    
                    var trans = Agent.Tracer.StartTransaction("Dist Trans 1", ApiConstants.TypeRequest,
                        deser);
                    await trans.CaptureSpan("step 3 processing", ApiConstants.ActionExec, async () => await Task.Delay(40));
                    trans.End();
                    
                    await context.Response.WriteAsync($"<a href=\"/trans1/1\">Restart</a> / <a href=\"/\">Exit</a>");
                });
                
                endpoints.MapGet("/trans2", async context =>
                {
                    // transaction 1
                    var trans1 = Agent.Tracer.StartTransaction("Dist Trans 2", ApiConstants.TypeRequest);
                    await trans1.CaptureSpan("step 1 processing", ApiConstants.ActionExec, async () => await Task.Delay(30));
                    trans1.End();       
                    
                    // transaction 2
                    var trans2 = Agent.Tracer.StartTransaction("Dist Trans 2", ApiConstants.TypeRequest,
                        DistributedTracingData.TryDeserializeFromString(trans1.OutgoingDistributedTracingData.SerializeToString()));
                    await trans2.CaptureSpan("step 2 processing", ApiConstants.ActionExec, async () => await Task.Delay(30));
                    trans2.End();
                    
                    // transaction 3
                    var trans3 = Agent.Tracer.StartTransaction("Dist Trans 2", ApiConstants.TypeRequest,
                        DistributedTracingData.TryDeserializeFromString(trans2.OutgoingDistributedTracingData.SerializeToString()));
                    await trans3.CaptureSpan("step 3 processing", ApiConstants.ActionExec, async () => await Task.Delay(30));
                    trans3.End();
                    
                    await context.Response.WriteAsync($"<a href=\"/trans2\">Restart</a> / <a href=\"/\">Exit</a>");
                });
            });
        }
    }
}