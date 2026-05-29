using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinFormsUI.Game.Combat.Weapons;
using XEngine.Core.Base;
using XEngine.Core.Utils;

namespace WinFormsUI.Game.Drop
{
    /// <summary>
    /// Компонент, представляющий выпавшее оружие на сцене.
    /// Управляет возможностью подбора и жизненным циклом предмета.
    /// </summary>
    internal class GDrop : GameComponent, IDisposable
    {
        private WeaponItem? heldWeapon = null;

        /// <summary>
        /// Флаг, разрешающий подбор предмета игроком.
        /// </summary>
        public bool CanPickup { get; private set; }

        /// <summary>
        /// Инициализирует компонент заданным экземпляром оружия.
        /// </summary>
        /// <param name="weapon">Экземпляр оружия.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GDrop Init(WeaponItem weapon)
        {
            heldWeapon = weapon;
            return this;
        }

        /// <summary>
        /// Устанавливает флаг возможности подбора.
        /// </summary>
        /// <param name="value">Значение флага.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GDrop SetPickupFlag(bool value)
        {
            CanPickup = value;
            return this;
        }

        /// <summary>
        /// Пытается передать оружие вызывающему коду и помечает сущность на удаление.
        /// </summary>
        /// <param name="weapon">Выходной параметр с переданным оружием.</param>
        /// <returns>True, если оружие успешно передано; иначе false.</returns>
        public bool TryTake(out WeaponItem weapon)
        {
            Owner.MarkDelete();
            if (CanPickup && heldWeapon != null)
            {
                CanPickup = false;
                weapon = heldWeapon;
                heldWeapon = null;
                return true;
            }

            weapon = null!;
            return false;
        }

        /// <summary>
        /// Освобождает ресурсы, связанные с таймером оружия, при удалении сущности.
        /// </summary>
        public void Dispose()
        {
            if (heldWeapon != null) Owner.Scene.UnregisterTimer(heldWeapon.FireTimer);
        }
    }
}
