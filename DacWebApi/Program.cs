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
            //Ū��appsetting.json����PlcConfig�]�w��
            var plcConfig = builder.Configuration.GetSection("PlcConfig").Get<PlcConfig>();
            builder.Services.Configure<PlcConfig>(builder.Configuration.GetSection("PlcConfig"));

            //�̤��P��plc�t�P�NPlcService�`�J�Ӽt�P��plc����
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
            //�`�J�`���I���A�ȡA�p�G���ݭn�}�ҴN�۰ʫإ߳s�u�����
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
