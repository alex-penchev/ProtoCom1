using ProtoCom1Emb;

// See https://aka.ms/new-console-template for more information
Console.BackgroundColor = ConsoleColor.Black;
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("-Console starts-");

// Demo runner
ProtoCom1? proto1 = null;

void ConIO(string message, bool bRequiresUI, ConsoleColor color)
{
    string? res = "";
    if (bRequiresUI)
    {
        // simulates user input
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = ConsoleColor.Yellow;

        Console.WriteLine(message); 

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("> ");
        res = Console.ReadLine();
        proto1?.UserInput(res ?? "");
    }
    else
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
    }

}

proto1 = new ProtoCom1("", ConIO);
proto1.LoadFromFile("sample.p1s");

Console.BackgroundColor = ConsoleColor.Black;
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("-Console ends-");