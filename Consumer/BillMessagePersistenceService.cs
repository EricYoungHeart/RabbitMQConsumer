using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Consumer.Infrastructure.Messaging.Models; 

namespace Consumer.Services
{
    /// <summary>
    /// Сервис для сохранения сообщений о счетах (Bill) на диск.
    /// Не наследует от ServiceBase, так как это бизнес-логика, а не Windows Service.
    /// </summary>
    public class BillMessagePersistenceService
    {
        // Путь к директории для сохранения файлов
        private readonly string _outputDirectory;
        // Настройки JSON сериализации
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// Делегат для логирования. Позволяет внешнему коду получать сообщения о событиях.
        /// </summary>
        public Action<string> OnLog { get; set; }
        /// <summary>
        /// Конструктор сервиса сохранения сообщений.
        /// </summary>
        /// <param name="outputDirectory">Путь к директории для сохранения файлов</param>
        public BillMessagePersistenceService(string outputDirectory)
        {
            // Сохраняем путь к выходной директории
            _outputDirectory = outputDirectory;
            // Настраиваем параметры JSON сериализации
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // КРИТИЧЕСКИ ВАЖНО: Убеждаемся, что директория существует
            // Для служб Windows важно использовать абсолютные пути
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }
        /// <summary>
        /// Асинхронно сохраняет сообщение на диск.
        /// </summary>
        /// <param name="rawJson">Сырой JSON, полученный из RabbitMQ</param>
        /// <returns>true - если сохранение успешно, false - при ошибке</returns>
        public async Task<bool> SaveMessageAsync(string rawJson)
        {
            try
            {
                // 1. ДЕСЕРИАЛИЗАЦИЯ ДЛЯ ВАЛИДАЦИИ И ПОЛУЧЕНИЯ ДАННЫХ
                // Десериализуем JSON для проверки структуры и извлечения метаданных
                var message = JsonSerializer.Deserialize<BillVersionMessage>(rawJson, _jsonOptions);
                if (message == null) return false;

                // 2. ГЕНЕРАЦИЯ УНИКАЛЬНОГО ИМЕНИ ФАЙЛА
                // Используем данные из сообщения и временную метку для уникальности
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm_ssfff");
                string fileName = $"Bill_{message.BillId}_Per_{message.Period}_V{message.CurrentVersion}_{timestamp}.json";
                string filePath = Path.Combine(_outputDirectory, fileName);

                // 3. СИНХРОННАЯ ЗАПИСЬ С АСИНХРОННЫМИ ОПЕРАЦИЯМИ
                // Используем асинхронную запись для эффективности
                using (var stream = new FileStream(filePath, 
                    FileMode.Create,  // Создать новый файл (перезаписать если существует)
                    FileAccess.Write, // Только запись
                    FileShare.None, // Запретить доступ другим процессам
                    4096,  // Размер буфера
                    useAsync: true)) // Использовать асинхронные операции
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    await writer.WriteAsync(rawJson); // Асинхронная запись JSON
                    await writer.FlushAsync(); // Принудительная запись из буфера
                }

                // Логируем успешное сохранение
                Log($"💾 [Хранилище] Сохранён: {fileName}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"💥 [Хранилище] Ошибка при сохранении файла: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Вспомогательный метод для логирования.
        /// Использует внешний обработчик если задан, иначе пишет в Console.
        /// </summary>
        private void Log(string message)
        {
            if (OnLog != null) OnLog(message);
            else Console.WriteLine(message);
        }
    }
}