using Microsoft.UI.Xaml;

namespace CodexSessionSync.WinUI;

public class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.Start((p) => new App());
    }
}
