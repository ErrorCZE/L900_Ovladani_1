using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Smdn.TPSmartHomeDevices.Tapo;
using Smdn.TPSmartHomeDevices.Tapo.Protocol;


class Program
{

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    const int SW_HIDE = 0;

    class Options
    {
        [Option('a', "akce", Required = true, HelpText = "Akce, která se má provést (např. turnoff, setbrightness, setcolor).")]
        public string Action { get; set; }

        [Option('b', "jas", HelpText = "Úroveň jasu (0-100).")]
        public int Brightness { get; set; }

        [Option('c', "barva", HelpText = "Parametry pro barvu ve formátu 'hue:saturation'.")]
        public string Color { get; set; }

        [Option('n', "noconsole", Default = false, HelpText = "Skryj konzoli.")]
        public bool NoConsole { get; set; }
    }

    static async Task Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddTapoProtocolSelector(
  static device => TapoSessionProtocol.Klap
);

        var parser = new Parser(with => with.EnableDashDash = true);
        var result = parser.ParseArguments<Options>(args);

        var options = result.MapResult(
            o => o,
            _ => new Options()
        );

        if (options.NoConsole)
        {
            var hWnd = GetConsoleWindow();
            ShowWindow(hWnd, SW_HIDE);
        }

        var exeName = Path.GetFileNameWithoutExtension(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);
        var configFile = $"{exeName}.txt";

        if (!File.Exists(configFile))
        {
            Console.WriteLine($"Konfigurační soubor '{configFile}' nenalezen.");
            return;
        }

        var configLines = File.ReadAllLines(configFile);

        if (configLines.Length < 3)
        {
            Console.WriteLine("Konfigurační soubor musí mít tyto řádky: IP adresa zařízení, email, heslo");
            return;
        }

        var ipAddress = configLines[0].Trim();
        var email = configLines[1].Trim();
        var password = configLines[2].Trim();

        await Parser.Default.ParseArguments<Options>(args)
            .WithParsedAsync(async options =>
            {
                using var device = new L900(ipAddress, email, password, services.BuildServiceProvider());

                switch (options.Action.ToLower())
                {
                    case "turnoff":
                        await device.TurnOffAsync();
                        Console.WriteLine("Zařízení vypnuto.");
                        break;
                    case "turnon":
                        await device.TurnOnAsync();
                        Console.WriteLine("Zařízení zapnuto.");
                        break;
                    case "setbrightness":
                        if (options.Brightness >= 0 && options.Brightness <= 100)
                        {
                            await device.SetBrightnessAsync(options.Brightness);
                            Console.WriteLine($"Jas nastaven na {options.Brightness}%.");
                        }
                        else
                        {
                            Console.WriteLine("Jas musí být mezi 0 až 100");
                        }
                        break;
                    case "setcolor":
                        if (!string.IsNullOrEmpty(options.Color))
                        {
                            var colorParams = options.Color.Split(':');
                            if (colorParams.Length == 2 && int.TryParse(colorParams[0], out int hue) && int.TryParse(colorParams[1], out int saturation))
                            {
                                await device.SetColorAsync(hue, saturation);
                                Console.WriteLine($"Barva nastavena na Hue: {hue}, Saturation: {saturation}.");
                            }
                            else
                            {
                                Console.WriteLine("Neplatný formát! Použij 'hue:saturation'.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Je potřeba parametr pro barvu pro využití akce 'setcolor'");
                        }
                        break;
                    default:
                        Console.WriteLine($"Neplatná akce: {options.Action}");
                        break;
                }
            });
    }
}
