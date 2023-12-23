// See https://aka.ms/new-console-template for more information
using Conventor2;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;


IServiceCollection services = new ServiceCollection();
services.AddLogging();

IServiceProvider serviceProvider = services.BuildServiceProvider();
ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

if (args.Length != 2) {
    Console.Error.WriteLine($"Input and output directories must be specified {args.Length}");
    Environment.Exit(1);
}

string inputDirectory = args[0];
string outputDirectory = args[1];

ConventionParser.ParseSections(Path.Join(inputDirectory, "sections.yaml"));

await using var htmlRenderer = new HtmlRenderer(serviceProvider, loggerFactory);
var html = await htmlRenderer.Dispatcher.InvokeAsync(async () => {
    var dictionary = new Dictionary<string, object?> {
    };

    var parameters = ParameterView.FromDictionary(dictionary);
    var output = await htmlRenderer.RenderComponentAsync<ConventorRenderer>(parameters);

    return output.ToHtmlString();
});

File.WriteAllText(Path.Join(outputDirectory, "index.html"), html);