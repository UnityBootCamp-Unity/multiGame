using Game.Persistence.Jwt;
using Game.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Persistence.Repositories;
namespace Game.Persistence
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddSingleton<IUserSessionRepository, InMemoryUserSessionRepository>();

            builder.Services.AddDbContext<GameDbContext>(
                options => options
                    .UseMySql(
                        DBConnectionSettings.CONNECTION, 
                        ServerVersion.AutoDetect(DBConnectionSettings.CONNECTION) // ��Ÿ�ӿ����� DB ���� �ڵ�����
                        )
                    .EnableSensitiveDataLogging() // ���� �ܰ迡���� �����
                    .EnableDetailedErrors()); // ���� �ܰ迡���� �����
            
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddControllers();

            // JWT ����
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = JwtUtils.Issuer,
                        ValidateAudience = true,
                        ValidAudience = JwtUtils.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = JwtUtils.SymKey,
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(5)
                    };
                });


            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapGet("/", () => Results.Ok(new { status = "Auth service is running." }));
            app.Run();
        }
    }
}
