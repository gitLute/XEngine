using XEngine.Core.Base;

namespace WinFormsUI.Game.Combat.Weapons
{
    /// <summary>
    /// Компонент, управляющий экипированным и запасным оружием сущности.
    /// </summary>
    public class GWeaponry : GameComponent
    {
        public WeaponItem? HeldWeapon { get; private set; } = null;
        public WeaponItem? AuxWeapon { get; private set; } = null;

        /// <summary>
        /// Инициализирует компонент, создавая и экипируя стартовое оружие по идентификатору.
        /// </summary>
        /// <param name="StartWeapon">Идентификатор начального оружия.</param>
        /// <returns>Текущий экземпляр компонента.</returns>
        public GWeaponry Init(string StartWeapon)
        {
            if (WeaponUtils.Instance.TryCreateWeapon(StartWeapon, out var w))
            {
                w.Init(Owner.Scene);
                Equip(w);
            }
            return this;
        }

        /// <summary>
        /// Пытается снять текущее экипированное оружие.
        /// </summary>
        /// <param name="weapon">Выходной параметр со снятым оружием.</param>
        /// <returns>True, если оружие было успешно снято; иначе false.</returns>
        public bool TryTake(out WeaponItem weapon)
        {
            bool res = HeldWeapon != null;
            weapon = HeldWeapon!;
            HeldWeapon = null;
            return res;
        }

        /// <summary>
        /// Экипирует указанное оружие, если слот свободен.
        /// </summary>
        /// <param name="weapon">Оружие для экипировки.</param>
        /// <returns>True, если оружие успешно экипировано; иначе false.</returns>
        public bool Equip(WeaponItem? weapon)
        {
            if (HeldWeapon == null)
            {
                HeldWeapon = weapon;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Меняет местами экипированное и запасное оружие.
        /// </summary>
        public void Swap() => (HeldWeapon, AuxWeapon) = (AuxWeapon, HeldWeapon);
    }
}
