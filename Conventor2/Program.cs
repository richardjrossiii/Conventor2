// See https://aka.ms/new-console-template for more information
using Conventor2;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


IServiceCollection services = new ServiceCollection();
services.AddLogging();

IServiceProvider serviceProvider = services.BuildServiceProvider();
ILoggerFactory loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

ConventionExpander.ParseConventions("example.yaml");


await using var htmlRenderer = new HtmlRenderer(serviceProvider, loggerFactory);
var html = await htmlRenderer.Dispatcher.InvokeAsync(async () => {
    var dictionary = new Dictionary<string, object?> {
    };

    var parameters = ParameterView.FromDictionary(dictionary);
    var output = await htmlRenderer.RenderComponentAsync<ConventorRenderer>(parameters);

    return output.ToHtmlString();
});

File.WriteAllText("test.html", html);