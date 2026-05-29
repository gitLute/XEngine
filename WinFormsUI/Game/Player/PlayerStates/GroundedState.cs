using XEngine.Core.Scenery;
using Box2D.NET;
using XEngine.Core.Box2DCompat;
using WinFormsUI.Game.Drop;
using static WinFormsUI.Game.Player.Contol.ActionType;
using static WinFormsUI.Game.Box2D.ContactFlags;

namespace WinFormsUI.Game.Player.PlayerStates
{
    /// <summary>
    /// Базовое состояние игрока на земле.
    /// Управляет ходьбой, прыжками, взаимодействием с предметами и переходом в другие состояния.
    /// </summary>
    internal class GroundedState : IPlayerState
    {
        public string DebugName => "grounded";

        /// <summary>
        /// Устанавливает начальную анимацию (покой или движение).
        /// </summary>
        public void Enter(GPlayer player, GScene scene)
        {
            if (player.Control.HorizotnalInput() == 0) player.Model.SetIdling();
            else player.Model.SetMoving();
        }

        public void Exit(GPlayer player, GScene scene) { }

        public void ProcessInput(GPlayer player, GScene scene, float dt)
        {
            if (player.Control.Fetch("up", ActionActive) && player.Contacts.Has(LADDER))
            {
                player.SwitchTo<ClimbState>(scene);
                return;
            }

            if (player.Control.Fetch("up", ActionStart))
            {
                player.JumpTimer.Start();
                player.Body.ApplyImpulse(0, player.Stats.JumpPower);
            }

            if (player.Control.Fetch("aux", ActionStart))
            {
                player.Weaponry.Swap();
                player.Model.UpdatePockets(player.Weaponry);
            }

            if (player.Control.Fetch("act", ActionStart))
            {
                var shapeid = player.Contacts.Get(ITEM_HITBOX).FirstOrDefault();
                if (player.Weaponry.HeldWeapon == null &&
                    B2Worlds.b2Shape_IsValid(shapeid) &&
                    B2Helpers.TryFetchEntity(shapeid, out var dropEntity) &&
                    dropEntity.TryGet<GDrop>(out var drop) &&
                    drop.TryTake(out var weapon))
                {
                    player.Weaponry.Equip(weapon);
                    player.Model.UpdatePockets(player.Weaponry);
                }
            }

            float hor = player.Control.HorizotnalInput();
            if (hor == 0) player.Model.SetIdling();
            else
            {
                player.Model.SetMoving();
                player.IsRightFacing = player.Model.SetFacingDiecration(hor);
                player.Move(hor, dt);
            }

            if (!player.Contacts.Has(SOLID)) player.SwitchTo<FallState>(scene);
            else if (hor == 0 && player.Control.Fetch("aim", ActionActive)) player.SwitchTo<AimState>(scene);
        }
    }
}
