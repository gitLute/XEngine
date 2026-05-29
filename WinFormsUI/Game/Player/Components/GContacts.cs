using Box2D.NET;
using WinFormsUI.Game.Box2D;
using XEngine.Core.Base;
using XEngine.Core.Box2DCompat;
using static XEngine.Core.Box2DCompat.B2Helpers;

namespace WinFormsUI.Game.Player.Components
{
    /// <summary>
    /// Компонент для отслеживания контактов физического тела с другими объектами.
    /// Позволяет регистрировать типы контактов и получать информацию о них.
    /// </summary>
    public class GContacts : GameComponent
    {
        private readonly Dictionary<ContactFlags, int> _contacts = [];
        private readonly Dictionary<ContactFlags, HashSet<B2ShapeId>> _contactsSet = [];

        /// <summary>
        /// Регистрирует тип контакта для отслеживания.
        /// </summary>
        /// <param name="flags">Флаг типа контакта.</param>
        /// <param name="isTracked">Если true, сохраняет идентификаторы фигур в множестве.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GContacts Register(ContactFlags flags, bool isTracked = false)
        {
            _contacts[flags] = 0;
            if (isTracked) _contactsSet[flags] = [];
            return this;
        }

        /// <summary>
        /// Проверяет наличие активных контактов указанного типа.
        /// </summary>
        /// <param name="type">Тип контакта.</param>
        /// <returns>True, если есть хотя бы один активный контакт данного типа.</returns>
        public bool Has(ContactFlags type) => _contacts.TryGetValue(type, out var contact) && contact > 0;

        /// <summary>
        /// Возвращает множество идентификаторов фигур, участвующих в контактах указанного типа.
        /// </summary>
        /// <param name="type">Тип контакта.</param>
        /// <returns>Множество идентификаторов фигур или пустое множество.</returns>
        public HashSet<B2ShapeId> Get(ContactFlags type) => _contactsSet.TryGetValue(type, out var contact) ? contact : [];

        /// <summary>
        /// Добавляет фигуру в список активных контактов.
        /// Вызывается при начале контакта.
        /// </summary>
        /// <param name="shapeid">Идентификатор фигуры.</param>
        public void AddContacts(B2ShapeId shapeid)
        {
            foreach (var contactId in _contacts.Keys.Where(cid => CheckFlag(shapeid, (ulong)cid)))
            {
                _contacts[contactId]++;
                if (_contactsSet.TryGetValue(contactId, out var contactSet))
                    contactSet.Add(shapeid);
            }
        }

        /// <summary>
        /// Удаляет фигуру из списка активных контактов.
        /// Вызывается при окончании контакта.
        /// </summary>
        /// <param name="shapeid">Идентификатор фигуры.</param>
        public void RemoveContacts(B2ShapeId shapeid)
        {
            foreach (var contactId in _contacts.Keys.Where(cid => CheckFlag(shapeid, (ulong)cid)))
            {
                _contacts[contactId]--;
                if (_contactsSet.TryGetValue(contactId, out var contactSet))
                    contactSet.Remove(shapeid);
            }
        }

        /// <summary>
        /// Статический обработчик начала контакта.
        /// Обновляет счетчики контактов для обеих участвующих сущностей.
        /// </summary>
        /// <param name="ev">Данные о событии контакта.</param>
        public static void ContactParseAdd(ContactWrapper ev)
        {
            ev.EntityA?.Get<GContacts>()?.AddContacts(ev.ShapeIdB);
            ev.EntityB?.Get<GContacts>()?.AddContacts(ev.ShapeIdA);
        }


        /// <summary>
        /// Статический обработчик окончания контакта.
        /// Уменьшает счетчики контактов для обеих участвующих сущностей.
        /// </summary>
        /// <param name="ev">Данные о событии контакта.</param>
        public static void ContactParseRemove(ContactWrapper ev)
        {
            ev.EntityA?.Get<GContacts>()?.RemoveContacts(ev.ShapeIdB);
            ev.EntityB?.Get<GContacts>()?.RemoveContacts(ev.ShapeIdA);
        }
    }
}
