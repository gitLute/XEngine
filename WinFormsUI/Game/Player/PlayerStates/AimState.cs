using WinFormsUI.Game.Combat.Weapons;
using XEngine.Core.Scenery;
using static WinFormsUI.Game.Player.Contol.ActionType;

namespace WinFormsUI.Game.Player.PlayerStates
{
    /// <summary>
    /// Состояние прицеливания игрока.
    /// Управляет оружием, анимацией и логикой стрельбы или сброса оружия.
    /// </summary>
    internal class AimState : IPlayerState
    {
        private const float Lim = 0.05f;
        private const float LowerLimit = -MathF.PI / 2 + Lim;
        private const float UpperLimit = MathF.PI / 2 - Lim;
        private float _angle = 0;

        private WeaponItem? currentWeapon = null!;
        public string DebugName => "Aiming";

        /// <summary>
        /// Инициализирует оружие и визуальную модель.
        /// </summary>
        public void Enter(GPlayer player, GScene scene)
        {
            if (!player.Weaponry.TryTake(out currentWeapon))
            {
                player.SwitchTo<GroundedState>(scene);
                return;
            }

            player.Model.SetWeaponTexture(currentWeapon);
            player.Model.UpdatePockets(player.Weaponry);
            player.Model.SetAiming();
        }

        /// <summary>
        /// Возвращает оружие в инвентарь и сбрасывает текстуры.
        /// </summary>
        public void Exit(GPlayer player, GScene scene)
        {
            player.Model.SetFacingDiecration(player.IsRightFacing ? 1 : -1);
            player.Model.ResetWeaponTexture();
            player.Weaponry.Equip(currentWeapon);
            player.Model.UpdatePockets(player.Weaponry);
        }

        public void ProcessInput(GPlayer player, GScene scene, float dt)
        {
            if (player.Control.Fetch("aux", ActionStart) && currentWeapon != null)
            {
                player.Drop(currentWeapon, canPickUp: true);
                currentWeapon = null;
            }

            if (player.Control.Fetch("act", ActionActive) && currentWeapon != null)
            {
                if (currentWeapon.CurrentAmmo == 0)
                {
                    player.Drop(currentWeapon, expirationTime: 3);
                    currentWeapon = null;
                }
                else player.Shoot(currentWeapon);
            }

            if (player.Control.Fetch("aim", ActionInactive) || currentWeapon == null)
            {
                player.SwitchTo<GroundedState>(scene);
                return;
            }

            var hor = player.Control.HorizotnalInput();
            var playerRight = player.IsRightFacing;

            if (hor == 0)
            {
                _angle = Math.Clamp(_angle + player.Control.VerticalInput() * dt, LowerLimit, UpperLimit);

                var (sin, cos) = MathF.SinCos(_angle);
                player.Model.SetFacingDiecration((playerRight ? 1 : -1) * cos, sin);
                return;
            }

            player.IsRightFacing = player.Model.SetFacingDiecration(hor > 0 ? 1 : -1);
            _angle = 0;
        }
    }
}
