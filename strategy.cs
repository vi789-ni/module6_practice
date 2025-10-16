using System;
using System.Collections.Generic;

namespace TravelBookingStrategy
{
    public interface ICostCalculationStrategy
    {
        decimal CalculateCost(TripRequest request);
        string Name { get; }
    }

    public class TripRequest
    {
        public decimal DistanceKm { get; set; }          
        public int Passengers { get; set; }             
        public string TravelClass { get; set; }          
        public int Children { get; set; }               
        public int Seniors { get; set; }                
        public int Bags { get; set; }                    
        public bool Insurance { get; set; }             
        public int Transfers { get; set; }            
        public decimal BaseFareFactor { get; set; } = 1m;
    }

    public class PlaneCostStrategy : ICostCalculationStrategy
    {
        public string Name => "Самолёт";

        public decimal CalculateCost(TripRequest r)
        {
            decimal basePerKm = 0.12m; 
            decimal cost = r.DistanceKm * basePerKm;

            if (r.TravelClass.ToLower() == "business")
                cost *= 2.5m; 
            else
                cost *= 1.0m;

            if (r.Bags > r.Passengers)
                cost += (r.Bags - r.Passengers) * 20m;

            cost *= (1 + 0.1m * r.Transfers);

            if (r.Insurance) cost += 5m * r.Passengers;

            cost = ApplyDiscounts(cost, r);

            cost *= r.BaseFareFactor;

            return Math.Max(0, cost) * r.Passengers;
        }

        private decimal ApplyDiscounts(decimal cost, TripRequest r)
        {
            decimal perPassenger = cost;
            decimal childrenDiscount = perPassenger * 0.5m * r.Children;
            decimal seniorDiscount = perPassenger * 0.2m * r.Seniors;
            decimal totalDiscount = childrenDiscount + seniorDiscount;
            return perPassenger * r.Passengers - totalDiscount;
        }
    }

    public class TrainCostStrategy : ICostCalculationStrategy
    {
        public string Name => "Поезд";

        public decimal CalculateCost(TripRequest r)
        {
            decimal basePerKm = 0.06m;
            decimal cost = r.DistanceKm * basePerKm;

            if (r.TravelClass.ToLower() == "business")
                cost *= 1.6m;

            cost += r.Bags * 2m;

            cost *= (1 + 0.03m * r.Transfers);

            if (r.Insurance) cost += 2m * r.Passengers;

            cost = ApplyDiscounts(cost, r);
            cost *= r.BaseFareFactor;

            return Math.Max(0, cost) * r.Passengers;
        }

        private decimal ApplyDiscounts(decimal cost, TripRequest r)
        {
            decimal per = cost;
            decimal childrenDiscount = per * 0.5m * r.Children;
            decimal seniorDiscount = per * 0.3m * r.Seniors;
            return per * r.Passengers - (childrenDiscount + seniorDiscount);
        }
    }

    public class BusCostStrategy : ICostCalculationStrategy
    {
        public string Name => "Автобус";

        public decimal CalculateCost(TripRequest r)
        {
            decimal basePerKm = 0.03m;
            decimal cost = r.DistanceKm * basePerKm;

            if (r.TravelClass.ToLower() == "business")
                cost *= 1.2m; 

            cost += r.Bags * 1m;

            cost *= (1 + 0.02m * r.Transfers);

            if (r.Insurance) cost += 1m * r.Passengers;

            cost = ApplyDiscounts(cost, r);
            cost *= r.BaseFareFactor;

            return Math.Max(0, cost) * r.Passengers;
        }

        private decimal ApplyDiscounts(decimal cost, TripRequest r)
        {
            decimal per = cost;
            decimal childrenDiscount = per * 0.7m * r.Children; 
            decimal seniorDiscount = per * 0.25m * r.Seniors;
            return per * r.Passengers - (childrenDiscount + seniorDiscount);
        }
    }

    public class TravelBookingContext
    {
        private ICostCalculationStrategy _strategy;
        public void SetStrategy(ICostCalculationStrategy strategy) => _strategy = strategy;

        public decimal Calculate(TripRequest request)
        {
            if (_strategy == null) throw new InvalidOperationException("Стратегия расчёта не установлена.");
            return _strategy.CalculateCost(request);
        }

        public string CurrentStrategyName => _strategy?.Name ?? "(не установлена)";
    }

    class Program
    {
        static void Main(string[] args)
        {
            TravelBookingContext ctx = new TravelBookingContext();
            Console.WriteLine(" Система бронирования ");

            var strategies = new Dictionary<string, ICostCalculationStrategy>
            {
                {"1", new PlaneCostStrategy()},
                {"2", new TrainCostStrategy()},
                {"3", new BusCostStrategy()}
            };

            while (true)
            {
                Console.WriteLine($"\nТекущая стратегия: {ctx.CurrentStrategyName}");
                Console.WriteLine("1 - Установить стратегию (1:Plane, 2:Train, 3:Bus)");
                Console.WriteLine("2 - Ввести параметры поездки и рассчитать стоимость");
                Console.WriteLine("3 - Добавить/показать стратегии");
                Console.WriteLine("0 - Выход");
                Console.Write("Выбор: ");
                string main = Console.ReadLine();

                if (main == "0") break;

                if (main == "1")
                {
                    Console.Write("Введите номер стратегии (1/2/3): ");
                    string s = Console.ReadLine();
                    if (strategies.ContainsKey(s))
                    {
                        ctx.SetStrategy(strategies[s]);
                        Console.WriteLine($"Стратегия '{strategies[s].Name}' установлена.");
                    }
                    else
                    {
                        Console.WriteLine("Неверный номер стратегии.");
                    }
                }
                else if (main == "2")
                {
                    try
                    {
                        var request = ReadTripRequest();
                        decimal cost = ctx.Calculate(request);
                        Console.WriteLine($"\nИтоговая стоимость для {request.Passengers} пассажира(ов): {cost:F2}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка: {ex.Message}");
                    }
                }
                else if (main == "3")
                {
                    Console.WriteLine("\nДоступные стратегии:");
                    foreach (var kv in strategies)
                        Console.WriteLine($"{kv.Key} - {kv.Value.Name}");
                    Console.WriteLine("");
                }
                else
                {
                    Console.WriteLine("Неверный выбор.");
                }
            }

            Console.WriteLine("Завершение. Нажмите любую клавишу...");
            Console.ReadKey();
        }

        static TripRequest ReadTripRequest()
        {
            TripRequest r = new TripRequest();

            Console.Write("Расстояние (км): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal dist) || dist < 0) throw new ArgumentException("Неправильное расстояние.");
            r.DistanceKm = dist;

            Console.Write("Количество пассажиров: ");
            if (!int.TryParse(Console.ReadLine(), out int passengers) || passengers <= 0) throw new ArgumentException("Неправильное количество пассажиров.");
            r.Passengers = passengers;

            Console.Write("Класс обслуживания (economy/business): ");
            string cls = Console.ReadLine().Trim();
            if (cls != "economy" && cls != "business") cls = "economy";
            r.TravelClass = cls;

            Console.Write("Дети (кол-во): ");
            if (!int.TryParse(Console.ReadLine(), out int kids) || kids < 0) throw new ArgumentException("Неправильное число детей.");
            r.Children = kids;

            Console.Write("Пенсионеры (кол-во): ");
            if (!int.TryParse(Console.ReadLine(), out int seniors) || seniors < 0) throw new ArgumentException("Неправильное число пенсионеров.");
            r.Seniors = seniors;

            if (r.Children + r.Seniors > r.Passengers) throw new ArgumentException("Сумма детей и пенсионеров не может превышать пассажиров.");

            Console.Write("Багаж (кол-во мест): ");
            if (!int.TryParse(Console.ReadLine(), out int bags) || bags < 0) throw new ArgumentException("Неправильное число багажа.");
            r.Bags = bags;

            Console.Write("Страховка (yes/no): ");
            string ins = Console.ReadLine().Trim().ToLower();
            r.Insurance = (ins == "yes" || ins == "y");

            Console.Write("Пересадки (кол-во): ");
            if (!int.TryParse(Console.ReadLine(), out int transfers) || transfers < 0) throw new ArgumentException("Неправильное число пересадок.");
            r.Transfers = transfers;

            Console.Write("Региональный коэффициент (например, 1.0): ");
            if (!decimal.TryParse(Console.ReadLine(), out decimal factor) || factor <= 0) factor = 1m;
            r.BaseFareFactor = factor;

            return r;
        }
    }
}
