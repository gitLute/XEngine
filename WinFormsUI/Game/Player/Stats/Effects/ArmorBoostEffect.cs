namespace WinFormsUI.Game.Player.Stats.Effects
{
    /// <summary>
    /// Эффект увеличения показателя брони.
    /// </summary>
    internal class ArmorBoostEffect(float intensity) : Effect
    {
        public override float Armor => base.Armor * intensity;
    }
}
