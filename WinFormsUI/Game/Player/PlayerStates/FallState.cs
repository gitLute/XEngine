using XEngine.Core.Scenery;
using static WinFormsUI.Game.Box2D.ContactFlags;
using static WinFormsUI.Game.Player.Contol.ActionType;

namespace WinFormsUI.Game.Player.PlayerStates
{
    /// <summary>
    /// Состояние свободного падения или полета.
    /// Управляет горизонтальным движением в воздухе и возможностью начала подъема по лестнице.
    /// </summary>
    internal class FallState : IPlayerState
    {
        public string DebugName => "Falling";

        /// <summary>
        /// Устанавливает анимацию прыжка/падения.
        /// </summary>
        public void Enter(GPlayer player, GScene scene) { player.Model.SetFalling(); }

        public void Exit(GPlayer player, GScene scene) { }

        /// <summary>
        /// Обрабатывает ввод в воздухе: движение, попытку прыжка (если разрешено таймером), смену оружия.
        /// Проверяет контакт с землей или лестницей для смены состояния.
        /// </summary>
        public void ProcessInput(GPlayer player, GScene scene, float dt)
        {
            if (player.Control.Fetch("up", ActionActive) && player.Contacts.Has(LADDER))
            {
                player.SwitchTo<ClimbState>(scene);
                return;
            }

            if (player.Contacts.Has(SOLID))
            {
                player.JumpTimer.Reset();
                player.SwitchTo<GroundedState>(scene);
                return;
            }

            if (player.Control.Fetch("aux", ActionStart))
            {
                player.Weaponry.Swap();
                player.Model.UpdatePockets(player.Weaponry);
            }

            float hor = player.Control.HorizotnalInput();
            if (hor != 0) player.IsRightFacing = player.Model.SetFacingDiecration(hor);
            player.Move(hor, dt, 0.5f);

            var jump = player.Control.Fetch("up", ActionActive);
            if (!jump) player.JumpTimer.Reset();
            if (jump && player.JumpTimer.IsRunning) player.Body.ApplyImpulse(0, player.Stats.JumpPower * dt);

            if (player.Control.Fetch("up", ActionEnd))
                player.Body.ApplyImpulse(0, -player.Body.LinearVelocity.Y * 0.5f);
        }
    }
}
