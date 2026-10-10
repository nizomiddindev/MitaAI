using System;
using System.Windows.Forms;

namespace MitaAI.Launcher;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
