using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinFormsUI.Game.Config
{
    /// <summary>
    /// Интерфейс для объектов, имеющих уникальный строковый идентификатор.
    /// Используется для индексации в системах загрузки данных.
    /// </summary>
    public interface IIdentifilable
    {
        /// <summary>
        /// Уникальный идентификатор объекта.
        /// </summary>
        string Id { get; }
    }
}
