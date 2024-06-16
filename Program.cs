using Autofac;
using Autofac.Extensions.DependencyInjection;
using divitiae_api.Models;
using divitiae_api.Models.ErrorHandling;
using divitiae_api.Services;
using divitiae_api.Services.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization;
using System.Text;
using Autofac.Core;
using Microsoft.EntityFrameworkCore;
using divitiae_api.SQLData;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using divitiae_api.Models.Mailing;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

builder.Host.ConfigureContainer<ContainerBuilder>(builder =>
{
    builder.RegisterType<UserServices>().As<IUserServices>().InstancePerLifetimeScope();
    builder.RegisterType<ItemServices>().As<IItemServices>().InstancePerLifetimeScope();
    builder.RegisterType<AppServices>().As<IAppServices>().InstancePerLifetimeScope();
    builder.RegisterType<WorkEnvironmentServices>().As<IWorkEnvironmentServices>().InstancePerLifetimeScope();
    builder.RegisterType<WorkspaceServices>().As<IWorkspaceServices>().InstancePerLifetimeScope();
    builder.RegisterType<UserToWorkEnvRoleServices>().As<IUserToWorkEnvRoleServices>().InstancePerLifetimeScope();
    builder.RegisterType<WsAppsRelationServices>().As<IWsAppsRelationServices>().InstancePerLifetimeScope();
    builder.RegisterType<ItemActivityLogServices>().As<IItemActivityLogServices>().InstancePerLifetimeScope();
    builder.RegisterType<EmailServices>().As<IEmailServices>().InstancePerLifetimeScope();
    builder.RegisterType<AppTaskServices>().As<IAppTaskServices>().InstancePerLifetimeScope();
});

// Add services to the container.
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<MongoDBSettings>(builder.Configuration.GetSection("MongoDB"));
builder.Services.AddDbContext<SQLDataContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SQLConnectionURI"));
});
builder.Services.AddHttpContextAccessor();


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
                .GetBytes(builder.Configuration.GetSection("AppSettings:Token").Value)),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

builder.Services.AddCors(p => p.AddPolicy("corsapp", builder =>
{
    builder.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
}));

builder.Services.AddAutoMapper(typeof(Program).Assembly);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.WriteIndented = true;
        options.JsonSerializerOptions.IncludeFields = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "JWTToken_Auth_API",
        Version = "v1"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\"",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {
        {
            new OpenApiSecurityScheme {
                Reference = new OpenApiReference {
                    Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});


builder.Services.Configure<RouteOptions>(options => { options.LowercaseUrls = true; });


var app = builder.Build();

var objectSerializer = new ObjectSerializer(type =>
                ObjectSerializer.DefaultAllowedTypes(type) || type.FullName.StartsWith("divitiae_api.Models.ItemRelation"));
BsonSerializer.RegisterSerializer(objectSerializer);


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStatusCodePages(async statusCodeContext =>
{
    switch (statusCodeContext.HttpContext.Response.StatusCode)
    {
        case 401:
            statusCodeContext.HttpContext.Response.StatusCode = 401;
            await statusCodeContext.HttpContext.Response.WriteAsJsonAsync(new ErrorMessage { httpStatus = 401, Message = "Unauthorized." });
            break;
        //case 403:
        //    statusCodeContext.HttpContext.Response.StatusCode = 403;
        //    await statusCodeContext.HttpContext.Response.WriteAsJsonAsync(new ErrorMessage { httpStatus = 403, Message = "Forbidden" });
        //    break;
    }
});

app.UseCors("corsapp");

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
