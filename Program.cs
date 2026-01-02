using System;
using System.ServiceProcess;

namespace Consumer
{
    // Статический класс Program - точка входа в приложение
    static class Program
    {
        /// <summary>
        /// Главная точка входа в приложение
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Используем using для гарантированного освобождения ресурсов
            using (var service = new BillingConsumerService())
            {
                // Проверяем, запущено ли приложение в интерактивном режиме (консоль) или же как служба Windows
                // Это - реализация Dual Mode. Сделано для простоты отладки. Отладка Windows Services - fucking hell, 
                // поэтому консоль используем для отладки.
                if (Environment.UserInteractive)
                {
                    // Устанавливаем заголовок консоли. Почему in English? Потому что я так хочу...
                    Console.Title = "Billing Consumer - Debug Mode";
                    Console.WriteLine("=== Running in CONSOLE mode ===");

                    // Установка обработчика Ctrl+C для graceful shutdown
                    Console.CancelKeyPress += (sender, e) =>
                    {
                        e.Cancel = true; // Предотвращаем немедленное завершение
                        Console.WriteLine("\nCtrl+C detected. Stopping gracefully...");
                        service.TestStop();
                        Console.WriteLine("Service stopped. Exiting...");
                        Environment.Exit(0);
                    };

                    // Запускаем сервис в тестовом режиме (он полнофункциональный!). Метод реализован.
                    service.TestStart(args);

                    Console.WriteLine("\nPress [Enter] to stop the consumer...");
                    Console.ReadLine();

                    service.TestStop();
                    Console.WriteLine("Stopped. Goodbye!");
                }
                else
                {
                    // ServiceBase.Run - стандартный способ запуска Windows Service
                    // Он автоматически управляет жизненным циклом службы:
                    // - Вызывает OnStart() при запуске службы
                    // - Вызывает OnStop() при остановке службы
                    // - Обрабатывает команды паузы, продолжения и т.д.
                    // Регистрация сервиса из cmd (в PowerShell так не работает!): 
                    // sc create BillingConsumerService binPath= "C:\Deploy\BillingConsumer\Consumer.exe" start= auto
                    // и п
                    // sc failure BillingConsumerService reset= 86400 actions= restart/60000/restart/60000/restart/60000

                    ServiceBase.Run(service);
                }
            }
        }
    }
}