using Dac;
using Dac.Interfaces;
using Dac.Models;
using Dac.Models.Config;
using Dac.Models.Plc;

using Microsoft.Extensions.DependencyInjection;

using Serilog;

namespace DacWebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });
            //Cors
            builder.Services.AddCors(o => o.AddPolicy("Cors_Policy", builder =>
            {
                builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
                //.AllowCredentials();
            }));

            #region DI
            //讀取appsetting.json中的PlcConfig設定值
            var plcConfig = builder.Configuration.GetSection("PlcConfig").Get<PlcConfig>();
            builder.Services.Configure<PlcConfig>(builder.Configuration.GetSection("PlcConfig"));

            //依不同的plc廠牌將PlcService注入該廠牌的plc實體
            switch (plcConfig!.Brand)
            {
                case "Mitsubishi":
                    builder.Services.AddSingleton<IPlc, Mitsubishi>();
                    break;
                case "TestPlc":
                    builder.Services.AddSingleton<IPlc, TestPlc>();
                    break;
                default:
                    builder.Services.AddSingleton<IPlc, TestPlc>();
                    break;
            }
            builder.Services.AddSingleton<IPlcService, PlcService>();
            //注入常註背景服務，如果有需要開啟就自動建立連線機制的話
            builder.Services.AddHostedService<HostedService>();
            #endregion

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            app.UseSerilogRequestLogging();
            app.UseHttpsRedirection();
            app.UseRouting();
            //app.UseCors() must put between app.UseRouting(); and app.UseEndpoints
            app.UseCors("Cors_Policy");
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
