using CryptoExchange.Net.Authentication;
using GateIo.Net.Clients;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Gate.io Console Connector — старт\n");

        var apiKey = Environment.GetEnvironmentVariable("GATE_API_KEY");
        var apiSecret = Environment.GetEnvironmentVariable("GATE_API_SECRET");
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            Console.WriteLine("[!] Установите переменные окружения GATE_API_KEY / GATE_API_SECRET");
            return;
        }

        using var restClient = new GateIoRestClient(o =>
        {
            o.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
        });

        try
        {
            // Получение позиций
            var positionsResult = await restClient.PerpetualFuturesApi.Trading.GetPositionsAsync("BTC_USDT");
            if (positionsResult.Success && positionsResult.Data.Any())
            {
                foreach (var p in positionsResult.Data)
                {
                    Console.WriteLine($"• {p.Contract} | size: {p.Size} | entry: {p.EntryPrice} | mark: {p.MarkPrice} | PnL: {p.UnrealisedPnl}");
                }
            }
            else
            {
                Console.WriteLine("Открытых позиций нет или ошибка: " + positionsResult.Error?.Message);
            }

            // Получение информации об аккаунте
            var accountResult = await restClient.PerpetualFuturesApi.Account.GetAccountAsync("usdt");
            if (accountResult.Success)
            {
                Console.WriteLine($"Доступный баланс: {accountResult.Data.Available} USDT | Общий: {accountResult.Data.Total} USDT");
            }
            else
            {
                Console.WriteLine("Ошибка получения баланса: " + accountResult.Error?.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка REST: " + ex.Message);
        }

        using var socketClient = new GateIoSocketClient(o =>
        {
            o.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
        });

        try
        {
            // Правильный вызов WebSocket подписки с учетом сигнатуры метода
            var subResult = await socketClient.PerpetualFuturesApi.SubscribeToTradeUpdatesAsync(
                settlementAsset: "usdt",  // Settlement asset (usdt, btc или usd)
                contract: "BTC_USDT",     // Контракт
                onMessage: data =>        // Обработчик сообщений
                {
                    foreach (var t in data.Data)
                        Console.WriteLine($"[WS] Trade {t.Contract} {t.Price} x {t.Quantity} @ {t.CreateTime:HH:mm:ss}");
                },
                ct: default);            // CancellationToken

            if (!subResult.Success)
                Console.WriteLine("Ошибка подписки WS: " + subResult.Error?.Message);
            else
                Console.WriteLine("WS подписка активна. Нажмите Ctrl+C для выхода.");

            await Task.Delay(10000); // Бесконечное ожидание
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка WS: " + ex.Message);
        }
    }
}