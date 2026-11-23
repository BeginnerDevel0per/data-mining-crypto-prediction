using CryptoMovementAnalyzer.Api.Services;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient<BinanceService>();
// Swagger Servisleri
builder.Services.AddEndpointsApiExplorer();
// builder.Services.AddSwaggerGen(); satırını silip yerine bunu yapıştır:
builder.Services.AddSwaggerGen(options =>
{
    // Projenin ürettiği XML dosyasının adını ve yolunu dinamik olarak bulur
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);

    // Yorumları Swagger'a dahil eder
    options.IncludeXmlComments(xmlPath);
});
var app = builder.Build();



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
