using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ktl.be.infrastructure.external.Documents;
using Photino.NET;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ktl.be.desktop
{
    internal static class Program
    {
        private const string AppTitle = "Sao Mai - Ký số";

        private const uint PP_CLIENT_HWND = 1;

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptSetProvParam(IntPtr hProv, uint dwParam, ref IntPtr pbData, uint dwFlags);

        [STAThread]
        private static void Main(string[] args)
        {
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            var app = ktl.be.api.Program.BuildApp(args);
            app.Urls.Clear();
            app.Urls.Add("http://127.0.0.1:0");
            app.Start();

            var url = app.Services
                .GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!
                .Addresses.First();

            var window = new PhotinoWindow()
                .SetTitle(AppTitle)
                .SetIconFile(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"))
                .SetUseOsDefaultSize(false)
                .SetSize(1280, 820)
                .Center()
                .SetResizable(true)
                .SetMinSize(1024, 700)
                .SetDevToolsEnabled(true)
                .RegisterWebMessageReceivedHandler(OnWebMessage)
                .RegisterWindowCreatedHandler(OnWindowCreated)
                .Load(new Uri(url));

            window.WaitForClose();

            app.StopAsync().GetAwaiter().GetResult();
        }

        private static void OnWindowCreated(object? sender, EventArgs e)
        {
            try
            {
                var hwnd = ((PhotinoWindow)sender!).WindowHandle;
                var logger = NLog.LogManager.GetCurrentClassLogger();
                if (hwnd == IntPtr.Zero)
                {
                    logger.Warn("OnWindowCreated: HWND = 0, hộp thoại PIN của token sẽ không có cửa sổ chủ.");
                    return;
                }

                SigningUiContext.WindowHandle = hwnd;
                var capiOk = CryptSetProvParam(IntPtr.Zero, PP_CLIENT_HWND, ref hwnd, 0);
                logger.Info($"OnWindowCreated: HWND=0x{hwnd.ToInt64():X}, PP_CLIENT_HWND={capiOk}");
            }
            catch
            {
            }
        }

        private static void OnWebMessage(object? sender, string message)
        {
            var window = (PhotinoWindow)sender!;

            if (message == "pickFolder")
            {
                var paths = window.ShowOpenFolder("Chọn thư mục", null, false);
                var path = paths != null && paths.Length > 0 ? paths[0] : string.Empty;
                window.SendWebMessage("folder:" + path);
            }
            else if (message == "pickImage")
            {
                var filters = new (string Name, string[] Extensions)[] { ("Ảnh (*.png, *.jpg, *.jpeg)", new[] { "png", "jpg", "jpeg" }) };
                var paths = window.ShowOpenFile("Chọn ảnh", null, false, filters);
                var path = paths != null && paths.Length > 0 ? paths[0] : string.Empty;
                window.SendWebMessage("image:" + path);
            }
            else if (message == "pickWord")
            {
                var filters = new (string Name, string[] Extensions)[] { ("Word (*.docx)", new[] { "docx" }) };
                var paths = window.ShowOpenFile("Chọn file template Word", null, false, filters);
                var path = paths != null && paths.Length > 0 ? paths[0] : string.Empty;
                window.SendWebMessage("word:" + path);
            }
            else if (message == "pickPdf")
            {
                var filters = new (string Name, string[] Extensions)[] { ("PDF (*.pdf)", new[] { "pdf" }) };
                var paths = window.ShowOpenFile("Chọn file PDF", null, true, filters);
                window.SendWebMessage("pdf:" + string.Join('|', paths ?? Array.Empty<string>()));
            }
            else if (message == "pickExcel")
            {
                var filters = new (string Name, string[] Extensions)[] { ("Excel (*.xlsx)", new[] { "xlsx" }) };
                var paths = window.ShowOpenFile("Chọn file dữ liệu Excel", null, false, filters);
                var path = paths != null && paths.Length > 0 ? paths[0] : string.Empty;
                window.SendWebMessage("excel:" + path);
            }
            else if (message.StartsWith("openPath:", StringComparison.Ordinal))
            {
                var path = message["openPath:".Length..].Replace('/', '\\');
                if (!string.IsNullOrWhiteSpace(path) && (Directory.Exists(path) || File.Exists(path)))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }
            }
        }
    }
}
