using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Consumer.Models.Configs;
using System;
using System.Text;
using System.Threading.Tasks;

namespace Consumer.Infrastructure.Messaging
{
    /// <summary>
    /// Класс для потребления сообщений от брокера. Реализует IDisposable для корректного освобождения ресурсов.
    /// </summary>
    public class RabbitMqConsumer : IDisposable
    {
        // конфигурация подключения к брокеру
        private readonly RabbitMqConfig _config;
        // соединение и канал RabbitMQ
        private IConnection _connection;
        private IModel _channel;
        // флаг для корректной реализации IDisposable
        private bool _disposed;
        // объект для синхронизации доступа к каналу (канал не Thread-Safe!)
        private readonly object _channelLock = new object(); // For thread safety

        /// <summary>
        /// Делегат для логирования. Позволяет внешнему коду получать логи. 
        /// </summary>
        public Action<string> OnLog { get; set; }

        /// <summary>
        /// Конструктор потребителя RabbitMQ.
        /// </summary>
        /// <param name="config">Конфигурация подключения к RabbitMQ</param>
        /// <exception cref="ArgumentNullException">Если config равен null</exception>
        public RabbitMqConsumer(RabbitMqConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            InitializeConnection();
        }
        /// <summary>
        /// Инициализирует подключение к RabbitMQ.
        /// Создает фабрику, соединение и канал с настройками из конфигурации.
        /// </summary>
        private void InitializeConnection()
        {
            // Создание фабрики подключения с параметрами из конфигурации
            var factory = new ConnectionFactory
            {
                HostName = _config.HostName,
                Port = _config.Port,
                UserName = _config.UserName,
                Password = _config.Password,
                VirtualHost = _config.VirtualHost,
                // ВАЖНО: Автоматическое восстановление подключения при разрывах сети
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };
            // Настройка TLS, если требуется. Сделал из-за Windows 7 (там нет TLS, только SSL, возиться не стал...)
            if (_config.UseTls)
            {
                factory.Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = _config.HostName,
                    AcceptablePolicyErrors = System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch
                };
            }

            // // Создание соединения с именем клиента для идентификации в RabbitMQ Management UI - по IP невозможно понять, кто подключился!
            _connection = factory.CreateConnection($"{_config.ConnectionSettings?.ClientProvidedNamePrefix ?? "Billing"}-Consumer");
            // создание канала (канал = виртуальное соединение внутри физического).
            _channel = _connection.CreateModel();
            // Настройка QoS (Quality of Service) - ограничиваем prefetch до 1 сообщения
            // Это означает: обрабатываем не более 1 сообщения параллельно на этом канале
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);
        }
        /// <summary>
        /// Начинает потребление сообщений из очереди.
        /// </summary>
        /// <param name="onMessageReceived">
        /// Асинхронная функция-обработчик, вызываемая для каждого полученного сообщения.
        /// Должна возвращать bool: true - сообщение обработано успешно, false - требуется повторная обработка.
        /// </param>
        public void StartConsuming(Func<string, Task<bool>> onMessageReceived)
        {
            // Проверяем состояние очереди(без изменения её свойств)
            var queueDetails = _channel.QueueDeclarePassive(_config.QueueName);
            // Логируем количество сообщений в очереди на момент старта
            Log($"[*] Queue '{_config.QueueName}' has {queueDetails.MessageCount} messages waiting.");
            Log("[*] Начинаем потребление...");
            
            // Создаем потребителя (consumer)
            var consumer = new EventingBasicConsumer(_channel);
            
            // Подписываемся на событие получения сообщения
            consumer.Received += async (model, ea) =>
            {
                // Получаем тело сообщения и конвертируем в строку
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                try
                {
                    // Вызываем внешний обработчик сообщения
                    bool isProcessed = await onMessageReceived(message);

                    lock (_channelLock) // Ensure channel operations are thread-safe
                    {
                        if (isProcessed)
                        {
                            // Подтверждаем успешную обработку сообщения
                            _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
                        }
                        else
                        {
                            // Отказываемся от сообщения и возвращаем его в очередь для повторной обработки
                            _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Критическая ошибка при обработке сообщения
                    Log($"[!] Критическая ошибка при обработке сообщения: {ex.Message}");
                    lock (_channelLock)
                    {
                        // Возвращаем сообщение в очередь при любой необработанной ошибке
                        _channel.BasicNack(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    }
                }
            };

            // Начинаем потребление сообщений из очереди
            // autoAck: false - подтверждения будут отправляться вручную после обработки
            _channel.BasicConsume(queue: _config.QueueName,
                                 autoAck: false,
                                 consumer: consumer);
        }
        /// <summary>
        /// Вспомогательный метод для логирования.
        /// Использует внешний обработчик если задан, иначе пишет в Console.
        /// </summary>
        private void Log(string message)
        {
            // If the Service or Console provided a logging action, use it. 
            // Otherwise, fallback to Console (useful for Debug mode).
            if (OnLog != null) OnLog(message);
            else Console.WriteLine(message);
        }
        /// <summary>
        /// Освобождает ресурсы RabbitMQ (канал и соединение).
        /// Реализация паттерна Disposable.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_channelLock)
                {
                    _channel?.Close();
                    _connection?.Close();
                }
                _disposed = true;
            }
        }
    }
}