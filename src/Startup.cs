﻿using System.Text.Json.Serialization;

namespace TelegramMonitor;

public class Startup : AppStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddLoggingSetup();

        services.AddConsoleFormatter();

        services.AddCorsAccessor();

        services.AddControllers()
                  .AddInjectWithUnifyResult()
                  .AddJsonOptions(o =>
                      o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSchedule(options =>
        options.AddJob(App.EffectiveTypes.ScanToBuilders()));

        services.AddHttpRemote(builder => { })
            .ConfigureOptions(options =>
                options.JsonSerializerOptions.Converters.AddDateTimeTypeConverters("yyyy-MM-dd HH:mm:ss"));
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            DefaultFileNames = new List<string> { "index.html" }
        });

        app.UseRouting();

        app.UseCorsAccessor();

        app.UseInject("api");

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}