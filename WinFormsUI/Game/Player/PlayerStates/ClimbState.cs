using OpenTK.Mathematics;
using XEngine.Core.Scenery;
using static XEngine.Core.Utils.MathUtils;
using static WinFormsUI.Game.Box2D.ContactFlags;
using static WinFormsUI.Game.Player.Contol.ActionType;

namespace WinFormsUI.Game.Player.PlayerStates
{
    /// <summary>
    /// Состояние лазания по лестнице.
    /// Отключает гравитацию и позволяет перемещаться вверх/вниз и в стороны.
    /// </summary>
    internal class ClimbState : IPlayerState
    {
        public string DebugName => "Climbing";
        private float savedGravity = 0;

        /// <summary>
        /// Отключает гравитацию и устанавливает анимацию падения/лазания.
        /// </summary>
        public void Enter(GPlayer player, GScene scene)
        {
            savedGravity = player.Body.GravityScale;
            player.Body.GravityScale = 0;
            player.Model.SetFalling();
        }

        /// <summary>
        /// Восстанавливает стандартную гравитацию.
        /// </summary>
        public void Exit(GPlayer player, GScene scene)
        {
            player.Body.GravityScale = savedGravity;
        }

        public void ProcessInput(GPlayer player, GScene scene, float dt)
        {
            var hor = player.Control.HorizotnalInput();
            if (hor != 0) player.IsRightFacing = player.Model.SetFacingDiecration(hor);
            var dir = new Vector2(hor, player.Control.VerticalInput()) * 2;
            var vel = LimitedStep(FromB2Vec2(player.Body.LinearVelocity), dir, 100 * dt);
            player.Body.ApplyImpulse(vel.X, vel.Y);

            if (!player.Contacts.Has(LADDER))
            {
                player.SwitchTo<FallState>(scene);
                return;
            }

            if (player.Contacts.Has(SOLID) && player.Control.Fetch("down", ActionActive))
            {
                player.SwitchTo<GroundedState>(scene);
                return;
            }
        }
    }
}
