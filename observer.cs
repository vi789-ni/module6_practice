using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StockExchangeObserver
{
    public interface IStockObserver
    {
        string Name { get; }
        Task OnPriceChangedAsync(string symbol, decimal price);
        int NotificationsReceived { get; set; }
    }

    public class StockExchange
    {
        private readonly ConcurrentDictionary<string, decimal> _prices = new ConcurrentDictionary<string, decimal>();
        private readonly ConcurrentDictionary<string, List<IStockObserver>> _subscribers = new ConcurrentDictionary<string, List<IStockObserver>>();

        private readonly List<string> _log = new List<string>();

        public void AddStock(string symbol, decimal initialPrice)
        {
            _prices[symbol] = initialPrice;
            _subscribers.TryAdd(symbol, new List<IStockObserver>());
            Log($"Stock added: {symbol} at {initialPrice}");
        }

        public bool HasStock(string symbol) => _prices.ContainsKey(symbol);

        public decimal? GetPrice(string symbol) => _prices.TryGetValue(symbol, out var p) ? p : (decimal?)null;

        public void RegisterObserver(string symbol, IStockObserver observer)
        {
            if (!_prices.ContainsKey(symbol))
            {
                Log($"Register failed: stock {symbol} not found for observer {observer.Name}");
                return;
            }
            var list = _subscribers.GetOrAdd(symbol, _ => new List<IStockObserver>());
            lock (list)
            {
                if (!list.Contains(observer))
                {
                    list.Add(observer);
                    Log($"Observer '{observer.Name}' registered to {symbol}");
                }
            }
        }

        public void RemoveObserver(string symbol, IStockObserver observer)
        {
            if (!_subscribers.ContainsKey(symbol))
            {
                Log($"Remove failed: stock {symbol} not found for observer {observer.Name}");
                return;
            }
            var list = _subscribers[symbol];
            lock (list)
            {
                if (list.Remove(observer))
                    Log($"Observer '{observer.Name}' removed from {symbol}");
                else
                    Log($"Observer '{observer.Name}' not found in subscribers of {symbol}");
            }
        }

        public void UpdatePrice(string symbol, decimal newPrice)
        {
            if (!_prices.ContainsKey(symbol))
            {
                Log($"UpdatePrice failed: stock {symbol} not found.");
                return;
            }

            decimal old = _prices[symbol];
            _prices[symbol] = newPrice;
            Log($"Price for {symbol} changed from {old} to {newPrice}");
            NotifyObserversAsync(symbol, newPrice);
        }

        private void NotifyObserversAsync(string symbol, decimal price)
        {
            if (!_subscribers.TryGetValue(symbol, out var list)) return;
            List<IStockObserver> snapshot;
            lock (list) { snapshot = list.ToList(); }

            foreach (var obs in snapshot)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await obs.OnPriceChangedAsync(symbol, price);
                        obs.NotificationsReceived++;
                        Log($"Notified '{obs.Name}' about {symbol}={price}");
                    }
                    catch (Exception ex)
                    {
                        Log($"Error notifying '{obs.Name}' about {symbol}: {ex.Message}");
                    }
                });
            }
        }

        private void Log(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            lock (_log) _log.Add(entry);
            Console.WriteLine(entry);
        }

        public void PrintSubscribersReport()
        {
            Console.WriteLine("\n=== Отчёт подписчиков по акциям ===");
            foreach (var kv in _subscribers)
            {
                Console.WriteLine($"\nАкция: {kv.Key}");
                if (kv.Value.Count == 0) Console.WriteLine("  (нет подписчиков)");
                else
                {
                    foreach (var obs in kv.Value)
                        Console.WriteLine($"  - {obs.Name} (уведомлений получено: {obs.NotificationsReceived})");
                }
            }
        }

        public void PrintLog()
        {
            Console.WriteLine("\n=== Лог событий ===");
            lock (_log)
            {
                foreach (var l in _log) Console.WriteLine(l);
            }
        }

        public IEnumerable<string> GetAllStocks() => _prices.Keys;
    }

    public class Trader : IStockObserver
    {
        public string Name { get; }
        public int NotificationsReceived { get; set; } = 0;

        public Trader(string name) { Name = name; }

        public Task OnPriceChangedAsync(string symbol, decimal price)
        {
            Console.WriteLine($"{Name}: получил обновление — {symbol} = {price}");
            return Task.CompletedTask;
        }
    }

    public class EmailNotifier : IStockObserver
    {
        public string Name { get; }
        private string _email;
        public int NotificationsReceived { get; set; } = 0;

        public EmailNotifier(string name, string email)
        {
            Name = name; _email = email;
        }

        public Task OnPriceChangedAsync(string symbol, decimal price)
        {
            Console.WriteLine($"{Name}: отправлено письмо на {_email} о {symbol}={price}");
            return Task.CompletedTask;
        }
    }

    public class TradingRobot : IStockObserver
    {
        public string Name { get; }
        public int NotificationsReceived { get; set; } = 0;
        private readonly Dictionary<string, (decimal buyBelow, decimal sellAbove)> _rules;

        public TradingRobot(string name)
        {
            Name = name;
            _rules = new Dictionary<string, (decimal, decimal)>();
        }

        public void SetRule(string symbol, decimal buyBelow, decimal sellAbove)
        {
            _rules[symbol] = (buyBelow, sellAbove);
        }

        public Task OnPriceChangedAsync(string symbol, decimal price)
        {
            if (_rules.TryGetValue(symbol, out var rule))
            {
                if (price <= rule.buyBelow)
                {
                    Console.WriteLine($"{Name}: условие BUY для {symbol} выполнено (price={price} <= {rule.buyBelow}). Совершаю покупку.");
                }
                else if (price >= rule.sellAbove)
                {
                    Console.WriteLine($"{Name}: условие SELL для {symbol} выполнено (price={price} >= {rule.sellAbove}). Совершаю продажу.");
                }
                else
                {
                    Console.WriteLine($"{Name}: наблюдаю {symbol}={price} — условий нет.");
                }
            }
            else
            {
                Console.WriteLine($"{Name}: нет правил для {symbol}. Игнорирую.");
            }
            return Task.CompletedTask;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var exchange = new StockExchange();

            exchange.AddStock("AAPL", 180.50m);
            exchange.AddStock("GOOG", 2800.00m);
            exchange.AddStock("TSLA", 220.00m);

            // Создадим наблюдателей
            var traderAlice = new Trader("Trader Alice");
            var emailBob = new EmailNotifier("Email Bob", "bob@example.com");
            var robot = new TradingRobot("RobotX");
            robot.SetRule("AAPL", buyBelow: 170m, sellAbove: 200m);
            robot.SetRule("TSLA", buyBelow: 200m, sellAbove: 260m);

            exchange.RegisterObserver("AAPL", traderAlice);
            exchange.RegisterObserver("AAPL", robot);
            exchange.RegisterObserver("TSLA", robot);
            exchange.RegisterObserver("GOOG", emailBob);

            Console.WriteLine("\n Система биржевых торгов ");

            while (true)
            {
                Console.WriteLine("\nДоступные действия:");
                Console.WriteLine("1 - Показать акции и цены");
                Console.WriteLine("2 - Обновить цену акции (симуляция)");
                Console.WriteLine("3 - Зарегистрировать наблюдателя на акцию");
                Console.WriteLine("4 - Удалить наблюдателя с акции");
                Console.WriteLine("5 - Показать отчёт подписчиков");
                Console.WriteLine("6 - Показать лог");
                Console.WriteLine("7 - Добавить новую акцию");
                Console.WriteLine("0 - Выход");
                Console.Write("Выбор: ");
                string choice = Console.ReadLine();

                if (choice == "0") break;

                try
                {
                    switch (choice)
                    {
                        case "1":
                            Console.WriteLine("\nАкции:");
                            foreach (var s in exchange.GetAllStocks())
                            {
                                Console.WriteLine($" - {s} : {exchange.GetPrice(s):F2}");
                            }
                            break;

                        case "2":
                            Console.Write("Введите символ акции: ");
                            string sym = Console.ReadLine().ToUpper();
                            if (!exchange.HasStock(sym)) { Console.WriteLine("Акция не найдена."); break; }
                            Console.Write("Новая цена: ");
                            if (!decimal.TryParse(Console.ReadLine(), out decimal newPrice)) { Console.WriteLine("Неверная цена."); break; }
                            exchange.UpdatePrice(sym, newPrice);
                            await Task.Delay(200);
                            break;

                        case "3":
                            Console.Write("Введите тип наблюдателя (1-Trader,2-Email,3-Robot): ");
                            string t = Console.ReadLine();
                            Console.Write("На какую акцию подписать (символ): ");
                            string symReg = Console.ReadLine().ToUpper();
                            if (!exchange.HasStock(symReg)) { Console.WriteLine("Акция не найдена."); break; }
                            if (t == "1")
                            {
                                Console.Write("Имя трейдера: ");
                                var name = Console.ReadLine();
                                var tr = new Trader(name);
                                exchange.RegisterObserver(symReg, tr);
                            }
                            else if (t == "2")
                            {
                                Console.Write("Имя нотификатора: ");
                                var name = Console.ReadLine();
                                Console.Write("Email: ");
                                var email = Console.ReadLine();
                                var en = new EmailNotifier(name, email);
                                exchange.RegisterObserver(symReg, en);
                            }
                            else if (t == "3")
                            {
                                Console.Write("Имя робота: ");
                                var name = Console.ReadLine();
                                var rob = new TradingRobot(name);
                                Console.Write("Хотите задать правило для этой акции? (y/n): ");
                                var yn = Console.ReadLine();
                                if (yn.ToLower() == "y")
                                {
                                    Console.Write("Порог BUY (<=): ");
                                    decimal buy = decimal.Parse(Console.ReadLine());
                                    Console.Write("Порог SELL (>=): ");
                                    decimal sell = decimal.Parse(Console.ReadLine());
                                    rob.SetRule(symReg, buy, sell);
                                }
                                exchange.RegisterObserver(symReg, rob);
                            }
                            else Console.WriteLine("Неверный тип");
                            break;

                        case "4":
                            Console.Write("На какой акции удалить наблюдателя: ");
                            string symRem = Console.ReadLine().ToUpper();
                            if (!exchange.HasStock(symRem)) { Console.WriteLine("Акция не найдена."); break; }
                            Console.Write("Введите имя наблюдателя для удаления: ");
                            string nameRem = Console.ReadLine();
                            // Поиск объекта
                            var listField = typeof(StockExchange).GetField("_subscribers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(exchange) as ConcurrentDictionary<string, List<IStockObserver>>;
                            if (listField != null && listField.TryGetValue(symRem, out var lst))
                            {
                                IStockObserver found = null;
                                lock (lst)
                                {
                                    found = lst.FirstOrDefault(o => o.Name.Equals(nameRem, StringComparison.OrdinalIgnoreCase));
                                }
                                if (found != null)
                                {
                                    exchange.RemoveObserver(symRem, found);
                                }
                                else Console.WriteLine("Наблюдатель не найден на этой акции.");
                            }
                            break;

                        case "5":
                            exchange.PrintSubscribersReport();
                            break;

                        case "6":
                            exchange.PrintLog();
                            break;

                        case "7":
                            Console.Write("Символ новой акции: ");
                            string newSym = Console.ReadLine().ToUpper();
                            Console.Write("Начальная цена: ");
                            if (!decimal.TryParse(Console.ReadLine(), out decimal initPrice)) { Console.WriteLine("Неверная цена."); break; }
                            exchange.AddStock(newSym, initPrice);
                            break;

                        default:
                            Console.WriteLine("Неверный выбор.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
            }

            Console.WriteLine("Завершение. Нажмите любую клавишу...");
            Console.ReadKey();
        }
    }
}
