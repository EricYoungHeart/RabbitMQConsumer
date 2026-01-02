using Consumer.Services;
using System;
using System.Collections.Generic;

namespace Consumer.Infrastructure.Messaging.Models
{
    /// <summary>
    /// Сообщение об обнаруженной разнице версий счёта МО.
    /// Отправляется в RabbitMQ для уведомления других систем.
    /// </summary>
    public class BillVersionMessage
    {
        public string BillId { get; set; }
        public string Period { get; set; }
        public string MoId { get; set; }
        /// <summary>
        /// Выражение вида 17246554, Char(8)
        /// </summary>
        public string PreviousVersion { get; set; }  // Локальная версия (из DBF)
        /// <summary>
        /// Выражение вида 17246554, Char(8)
        /// </summary>
        public string CurrentVersion { get; set; }   // Актуальная версия (из SOAP)
        public DateTime DifferenceDetectedAt { get; set; }
        public VersionChangeType ChangeType { get; set; }

        // Дополнительные данные для контекста
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Тип события для маршрутизации в RabbitMQ.
        /// </summary>
        public string EventType => "billing.version.difference";
    }
    /// <summary>
    /// Тип изменения версии счёта.
    /// </summary>
    public enum VersionChangeType
    {
        /// <summary>
        /// Нет изменений.
        /// </summary>
        NoChange,

        /// <summary>
        /// Версия обновлена (изменена).
        /// </summary>
        VersionUpdated,

        /// <summary>
        /// Появилась новая версия (локально не было версии).
        /// </summary>
        NewVersion,

        /// <summary>
        /// Версия удалена (в актуальных данных отсутствует).
        /// </summary>
        VersionRemoved
    }
}