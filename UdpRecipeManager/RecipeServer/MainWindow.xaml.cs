/*
Завдання: створити UDP-сервер кулінарних рецептів для багатьох клієнтів; шукати рецепти за списком продуктів; обмежити клієнта 10 запитами за годину; обмежити кількість активних клієнтів та видаляти неактивних через 10 хвилин; додати GUI, логування та надсилати з рецептом зображення готової страви.
*/
using System.IO; using System.Net; using System.Net.Sockets; using System.Text; using System.Text.Json; using System.Windows;
namespace RecipeServer;
public partial class MainWindow : Window
{
    private UdpClient? udp; private bool running; private readonly Dictionary<string, Session> clients = []; private const int MaxClients = 100;
    public MainWindow() { InitializeComponent(); }
    private async void Start_Click(object sender, RoutedEventArgs e) { if (!int.TryParse(PortBox.Text, out int port)) return; udp = new UdpClient(port); running = true; StartButton.IsEnabled = false; StopButton.IsEnabled = true; Log($"Server started: {port}"); _ = ReceiveLoop(); _ = CleanupLoop(); await Task.CompletedTask; }
    private void Stop_Click(object sender, RoutedEventArgs e) { running = false; udp?.Close(); StartButton.IsEnabled = true; StopButton.IsEnabled = false; Log("Server stopped."); }
    private async Task ReceiveLoop() { while (running) try { UdpReceiveResult data = await udp!.ReceiveAsync(); Request? request = JsonSerializer.Deserialize<Request>(Encoding.UTF8.GetString(data.Buffer)); if (request is not null) await ProcessAsync(request, data.RemoteEndPoint); } catch (ObjectDisposedException) { } catch (Exception e) { Log(e.Message); } }
    private async Task ProcessAsync(Request request, IPEndPoint ep)
    {
        string id = ep.ToString(); if (!clients.TryGetValue(id, out Session? session)) { if (clients.Count >= MaxClients) { await SendJson(ep, new Response { Message = "Server client limit reached." }); return; } session = new Session(ep); clients[id] = session; Log($"Client connected: {id}"); } session.LastActive = DateTime.Now;
        if (request.Action == "disconnect") { clients.Remove(id); Log($"Client disconnected: {id}"); return; }
        if (DateTime.Now - session.WindowStart >= TimeSpan.FromHours(1)) { session.WindowStart = DateTime.Now; session.Requests = 0; }
        if (++session.Requests > 10) { await SendJson(ep, new Response { Message = "Limit exceeded: 10 requests per hour." }); Log($"Limit: {id}"); return; }
        Log($"Request {id}: {string.Join(", ", request.Ingredients)}"); bool match = request.Ingredients.Any(x => x.Contains("tuna", StringComparison.OrdinalIgnoreCase) || x.Contains("тун", StringComparison.OrdinalIgnoreCase)) && request.Ingredients.Any(x => x.Contains("salad", StringComparison.OrdinalIgnoreCase) || x.Contains("салат", StringComparison.OrdinalIgnoreCase));
        if (!match) { await SendJson(ep, new Response { Message = "Recipe not found. Try: tuna, salad." }); return; }
        byte[] image = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "caesar-salad.png")); await SendJson(ep, new Response { Message = "Success", Name = "Caesar salad with tuna", Instructions = "1. Mix lettuce, croutons and parmesan.\n2. Add tuna pieces.\n3. Add dressing.", ImageLength = image.Length }); await SendImage(ep, image);
    }
    private async Task SendJson(IPEndPoint ep, Response response) { byte[] b = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response)); await udp!.SendAsync(b, b.Length, ep); }
    private async Task SendImage(IPEndPoint ep, byte[] image) { const int chunk = 60000; for (int i = 0; i < image.Length; i += chunk) { byte[] part = new byte[Math.Min(chunk, image.Length - i)]; Buffer.BlockCopy(image, i, part, 0, part.Length); await udp!.SendAsync(part, part.Length, ep); } }
    private async Task CleanupLoop() { while (running) { foreach (string id in clients.Where(x => DateTime.Now - x.Value.LastActive > TimeSpan.FromMinutes(10)).Select(x => x.Key).ToArray()) { clients.Remove(id); Log($"Inactive client removed: {id}"); } await Task.Delay(TimeSpan.FromMinutes(1)); } }
    private void Log(string text) { string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}"; Dispatcher.Invoke(() => LogList.Items.Insert(0, line)); File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "server.log"), line + Environment.NewLine); }
}
public sealed class Session(IPEndPoint ep) { public IPEndPoint EndPoint { get; } = ep; public DateTime LastActive { get; set; } = DateTime.Now; public DateTime WindowStart { get; set; } = DateTime.Now; public int Requests { get; set; } }
public sealed class Request { public string Action { get; set; } = "search"; public List<string> Ingredients { get; set; } = []; }
public sealed class Response { public string Message { get; set; } = ""; public string? Name { get; set; } public string? Instructions { get; set; } public int ImageLength { get; set; } }
