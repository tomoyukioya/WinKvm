using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WinKvm
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();

                // logger
                var configuration = new ConfigurationBuilder()
                      .SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", true, true)
                      .Build();

                // MinLevel取得
                var minLevel = configuration.GetSection("Logging").GetSection("File").GetValue<string>("MinLevel");
                minLevel ??= "Debug";

                // LogggerFactory作成
                var loggingSection = configuration.GetSection("Logging");
                var loggerFactory = LoggerFactory.Create(builder => builder
                    .AddDebug()
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddFile(loggingSection, fileLoggerOpts =>
                    {
                        fileLoggerOpts.MinLevel = (LogLevel)Enum.Parse(typeof(LogLevel), minLevel);
                    }));

                // Start Application
                Application.Run(new MainForm(loggerFactory));
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}