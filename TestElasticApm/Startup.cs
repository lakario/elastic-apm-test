using System;
using System.Collections.Generic;
using System.Linq;
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
                    await context.Response.WriteAsync($"<a href=\"/trans1\">Distributed Trans 1</a>");
                });

                endpoints.MapGet("/trans1", async context =>
                {
                    await context.Response.WriteAsync($"<a href=\"/trans1/1\">Click here to begin</a>");
                });
                
                endpoints.MapGet("/trans1/1", async context =>
                {
                    var trans = Agent.Tracer.StartTransaction("Dist Trans 1", ApiConstants.TypeRequest);

                    await trans.CaptureSpan("step 1 processing", ApiConstants.ActionExec, async () => await Task.Delay(30));
                    
                    var trace =
                        (Agent.Tracer.CurrentSpan?.OutgoingDistributedTracingData
                         ?? Agent.Tracer.CurrentTransaction?.OutgoingDistributedTracingData).SerializeToString();

                    await context.Response.WriteAsync($"<a href=\"/trans1/2?s={trace}\">Continue</a>");
                });
                
                endpoints.MapGet("/trans1/2", async context =>
                {
                    var deser = DistributedTracingData.TryDeserializeFromString(context.Request.Query["s"]);
                    var trans = Agent.Tracer.StartTransaction("Dist Trans 1", ApiConstants.TypeRequest,
                        deser);
                    
                    await trans.CaptureSpan("step 2 processing", ApiConstants.ActionExec, async () => await Task.Delay(15));
                    
                    var trace =
                        (Agent.Tracer.CurrentSpan?.OutgoingDistributedTracingData
                         ?? Agent.Tracer.CurrentTransaction?.OutgoingDistributedTracingData).SerializeToString();
                    
                    await context.Response.WriteAsync($"<a href=\"/trans1/3?s={trace}\">Continue</a>");
                });
                
                endpoints.MapGet("/trans1/3", async context =>
                {
                    var deser = DistributedTracingData.TryDeserializeFromString(context.Request.Query["s"]);
                    var trans = Agent.Tracer.StartTransaction("Dist Trans 1", ApiConstants.TypeRequest,
                        deser);
                    
                    await trans.CaptureSpan("step 3 processing", ApiConstants.ActionExec, async () => await Task.Delay(40));

                    trans.End();
                    
                    await context.Response.WriteAsync($"<a href=\"/trans1\">Start over</a> / <a href=\"/\">Exit</a>");
                });
            });
        }
    }
}