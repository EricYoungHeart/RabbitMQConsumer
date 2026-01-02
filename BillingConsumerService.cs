using System;
using System.ServiceProcess;
using System.IO;
using Consumer.Infrastructure.Messaging;
using Consumer.Services;
using Consumer.Models.Configs;

namespace Consumer
{
    /// <summary>
    /// Основной класс службы Windows для обработки сообщений RabbitMQ.
    /// Наследуется от ServiceBase для работы в качестве службы Windows. Для этого добавлена ссылка на System.ServiceProcess
    /// </summary>
    public class BillingConsumerService : ServiceBase
    {
        private RabbitMqConsumer _rabbitConsumer;
        private BillMessagePersistenceService _persistenceService;

        public BillingConsumerService()
        {
            this.ServiceName = "BillingConsumerService";
        }

        /// <summary>
        /// Метод для ручного запуска в консольном режиме (отладка).
        /// Вызывает защищенный метод OnStart базового класса. По-другому его (и OnStop()) не вызовешь!
        /// </summary>
        /// <param name="args">Аргументы командной строки</param>
        public void TestStart(string[] args) => OnStart(args);
        /// <summary>
        /// Метод для ручной остановки в консольном режиме (отладка).
        /// Вызывает защищенный метод OnStop базового класса.
        /// </summary>        
        public void TestStop() => OnStop();

        // ===== ПЕРЕОПРЕДЕЛЕННЫЕ МЕТОДЫ ServiceBase =====

        /// <summary>
        /// Вызывается при запуске службы (как в режиме службы, так и в консольном).
        /// Здесь происходит инициализация всех компонентов.
        /// </summary>
        protected override void OnStart(string[] args)
        {
            try
            {
                // 1. Загрузка конфигурации. Получаем конфигурацию RabbitMQ из файла настроек.
                var mqConfig = ConfigurationHelper.GetConfig<RabbitMqConfig>("RabbitMQ");

                // 2. Настройка путей. Определяем путь для сохранения входящих сообщений.
                // AppDomain.CurrentDomain.BaseDirectory - путь к папке с исполняемым файлом
                string inputPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "input_messages");

                // 3. Инициализация Persistence (сервис сохранения сообщений).
                _persistenceService = new BillMessagePersistenceService(inputPath);
                // Подписываемся на события логирования от сервиса сохранения
                _persistenceService.OnLog = LogMessage;

                // 4. Инициализация RabbitMQ потребителя
                _rabbitConsumer = new RabbitMqConsumer(mqConfig);
                // Подписываемся на события логирования от сервиса сохранения
                _rabbitConsumer.OnLog = LogMessage;

                // 5. Начинаем прослушивать очередь. Передаем JSON сообщение сервису сохранения (то, что получили от брокера, сохраняем на диске).
                _rabbitConsumer.StartConsuming(async (json) =>
                {
                    return await _persistenceService.SaveMessageAsync(json);
                });
                // Логируем успешный старт службы
                LogMessage("Сервис успешно запущен.");
            }
            catch (Exception ex)
            {
                LogMessage($"Фатальная ошибка при запуске службы: {ex.Message}");
                // надо подумать о записи в EventLog!
                if (!Environment.UserInteractive) throw;
            }
        }
        /// <summary>
        /// Вызывается при остановке службы. Здесь происходит освобождение ресурсов.
        /// </summary>
        protected override void OnStop()
        {
            LogMessage("Останавливаем сервис...");
            _rabbitConsumer?.Dispose();
        }

        /// <summary>
        /// Универсальный метод логирования. В зависимости от режима запуска выводим сообщения либо в консоль, либо пишем в файл.
        /// </summary>
        /// <param name="message"></param>
        private void LogMessage(string message)
        {
            string logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {message}";

            if (Environment.UserInteractive)
            {
                Console.WriteLine(logLine);
            }
            else
            {
                // Пока пишем логи в просто файл.
                try
                {
                    string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "service_log.txt");
                    File.AppendAllText(logPath, logLine + Environment.NewLine);
                }
                // "Тихий" catch -предотвращаем падение службы из - за проблем с логированием
                // надо добавить fallback в EventLog
                catch { /* Чтобы не было краша в случае ошибки записи лога */ }
            }
        }
    }
}