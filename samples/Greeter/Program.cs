using Cobbler;

var app = new CommandApp("greeter", "Says hello in a few ways").Version("1.0.0");

app.Root.Flag("shout", "Use capitals", 's', global: true);

app.Add(new Command("hello", "Greet someone")
    .Argument("name", "Who to greet")
    .Option("times", "How many greetings", 'n', "N", defaultValue: "1")
    .Option("style", "Tone of voice", choices: new[] { "plain", "fancy" }, defaultValue: "plain")
    .Example("greeter hello Josh -n 2 --style fancy")
    .Handle(r =>
    {
        var name = r.Argument("name")!;
        var line = r.Get<string>("style") == "fancy" ? $"Good day to you, {name}." : $"Hello, {name}!";
        if (r.Flag("shout"))
        {
            line = line.ToUpperInvariant();
        }

        for (var i = 0; i < r.Get<int>("times"); i++)
        {
            Console.WriteLine(line);
        }
    }));

app.Add(new Command("farewell", "Say goodbye")
    .Alias("bye")
    .Argument("names", "People to wave at", required: false, variadic: true)
    .Handle(r =>
    {
        var names = r.ArgumentValues("names");
        Console.WriteLine(names.Count == 0 ? "Bye, everyone." : "Bye, " + string.Join(" and ", names) + ".");
    }));

return app.Run(args);
