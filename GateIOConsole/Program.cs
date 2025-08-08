using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        const string settle = "usdt";
        const string contract = "BTC_USDT";

        using var rest = new GateIoRestClient(o =>
        {
            o.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
        });

        try
        {
            // ----- АККАУНТ (перпеты)
            var account = await rest.PerpetualFuturesApi.Account.GetAccountAsync(settle);
            if (account.Success)
            {
                var a = account.Data;
                Console.WriteLine($"Аккаунт {settle.ToUpper()} | Total: {a.Total} | Avail: {a.Available} | UnrealisedPnL: {a.UnrealisedPnl}");
            }
            else
            {
                Console.WriteLine("Ошибка аккаунта: " + account.Error?.Message);
            }

            // ----- ПОЗИЦИИ (все по USDT-perp)
            var positions = await rest.PerpetualFuturesApi.Trading.GetPositionsAsync(settle);
            if (positions.Success)
            {
                var open = positions.Data
                    .Where(p => p.Size != 0L)
                    .ToList();

                if (open.Count == 0)
                {
                    Console.WriteLine("Открытых позиций не найдено.");
                }
                else
                {
                    Console.WriteLine("Открытые позиции:");
                    foreach (var p in open)
                    {
                        // side выводим по знаку размера
                        var side = p.Size > 0 ? "LONG" : "SHORT";

                        // безопасно считаем %PnL по mark/entry
                        decimal? pnlPct = null;
                        if (p.EntryPrice.HasValue && p.EntryPrice.Value != 0 && p.MarkPrice.HasValue)
                        {
                            var raw = (p.MarkPrice.Value - p.EntryPrice.Value) / p.EntryPrice.Value * 100m;
                            // если хочешь учитывать направление позиции знаковым коэффициентом:
                            raw *= p.Size > 0 ? 1 : -1;
                            pnlPct = Math.Round(raw, 4);
                        }

                        Console.WriteLine(
                            $"• {p.Contract} | {side} | size={p.Size} | lev={p.Leverage} | " +
                            $"entry={p.EntryPrice?.ToString() ?? "-"} | mark={p.MarkPrice?.ToString() ?? "-"} | " +
                            $"liq={p.LiquidationPrice?.ToString() ?? "-"} | uPnL={p.UnrealisedPnl?.ToString() ?? "-"} | " +
                            $"uPnL%={(pnlPct?.ToString() ?? "-")}"
                        );
                    }
                }

            }
            else
            {
                Console.WriteLine("Ошибка позиций: " + positions.Error?.Message);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка REST: " + ex.Message);
        }

        // ----- WEBSOCKETЫ
        using var socket = new GateIoSocketClient(o =>
        {
            o.ApiCredentials = new ApiCredentials(apiKey, apiSecret);
        });

        try
        {
            // ТИКЕР по контракту (даёт FundingRate/Indicative, MarkPrice, IndexPrice и т.д.)
            var tickerSub = await socket.PerpetualFuturesApi.SubscribeToTickerUpdatesAsync(
                settle, contract,
                msg =>
                {
                    var t = msg.Data.First();
                    Console.WriteLine($"[TICKER] {t.Contract} last:{t.LastPrice} mark:{t.MarkPrice} idx:{t.IndexPrice} fr:{t.FundingRate} fri:{t.FundingRateIndicative}");
                },
                ct: CancellationToken.None);

            if (!tickerSub.Success)
                Console.WriteLine("Ошибка подписки на тикер: " + tickerSub.Error?.Message);

            // СДЕЛКИ по контракту
            var tradesSub = await socket.PerpetualFuturesApi.SubscribeToTradeUpdatesAsync(
                settle, contract,
                msg =>
                {
                    foreach (var t in msg.Data)
                        Console.WriteLine($"[TRADES] {t.Contract} {t.Price} x {t.Quantity} @ {t.CreateTime:HH:mm:ss}");
                },
                ct: CancellationToken.None);

            if (!tradesSub.Success)
                Console.WriteLine("Ошибка подписки на трейды: " + tradesSub.Error?.Message);

            Console.WriteLine("WS подписки активны. Нажмите Ctrl+C для выхода.");
            await Task.Delay(Timeout.Infinite);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Ошибка WS: " + ex.Message);
        }
    }
}
