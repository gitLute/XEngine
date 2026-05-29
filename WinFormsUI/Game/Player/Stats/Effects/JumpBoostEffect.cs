namespace WinFormsUI.Game.Player.Stats.Effects
{
    /// <summary>
    /// Эффект увеличения силы прыжка.
    /// </summary>
    internal class JumpBoostEffect(float intensity) : Effect
    {
        public override float JumpPower => base.JumpPower * intensity;
    }
}
