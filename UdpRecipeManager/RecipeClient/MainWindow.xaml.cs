/*
Завдання: створити UDP-додаток для пошуку кулінарних рецептів за продуктами. Клієнт надсилає запит, отримує рецепт та зображення готової страви, може виконати новий запит або відключитися. Сервер має обмеження запитів, активних клієнтів та логування.
*/
using System.IO; using System.Net; using System.Net.Sockets; using System.Text; using System.Text.Json; using System.Windows; using System.Windows.Media.Imaging;
namespace RecipeClient;
public partial class MainWindow : Window
{
    private UdpClient? udp; private IPEndPoint? server;
    public MainWindow() { InitializeComponent(); Closing += async (_, _) => await Disconnect(); }
    private void Connect_Click(object sender, RoutedEventArgs e) { if (!int.TryParse(PortBox.Text, out int port)) { MessageText.Text = "Invalid port."; return; } udp?.Close(); udp = new UdpClient(); server = new IPEndPoint(IPAddress.Parse(IpBox.Text), port); MessageText.Text = "Connected."; }
    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        if (udp is null || server is null) { MessageText.Text = "Connect to the server first."; return; } List<string> ingredients = IngredientsBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList(); if (ingredients.Count == 0) return;
        try { SearchButton.IsEnabled = false; await Send(new Request { Ingredients = ingredients }); UdpReceiveResult result = await udp.ReceiveAsync(); Response? response = JsonSerializer.Deserialize<Response>(Encoding.UTF8.GetString(result.Buffer)); if (response is null) return; MessageText.Text = response.Message; NameText.Text = response.Name ?? ""; InstructionsText.Text = response.Instructions ?? ""; DishImage.Source = null; if (response.ImageLength > 0) { byte[] image = await ReceiveImage(response.ImageLength); DishImage.Source = ToBitmap(image); } }
        catch (Exception error) { MessageText.Text = "Network error: " + error.Message; } finally { SearchButton.IsEnabled = true; }
    }
    private async Task Send(Request request) { byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request)); await udp!.SendAsync(data, data.Length, server!); }
    private async Task<byte[]> ReceiveImage(int length) { using MemoryStream output = new(); while (output.Length < length) { UdpReceiveResult part = await udp!.ReceiveAsync(); await output.WriteAsync(part.Buffer); } return output.ToArray(); }
    private static BitmapImage ToBitmap(byte[] image) { using MemoryStream stream = new(image); BitmapImage bitmap = new(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap; }
    private async Task Disconnect() { try { if (udp is not null && server is not null) await Send(new Request { Action = "disconnect" }); } catch { } udp?.Close(); }
}
public sealed class Request { public string Action { get; set; } = "search"; public List<string> Ingredients { get; set; } = []; }
public sealed class Response { public string Message { get; set; } = ""; public string? Name { get; set; } public string? Instructions { get; set; } public int ImageLength { get; set; } }
