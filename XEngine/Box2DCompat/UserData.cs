using XEngine.Core.Box2DCompat.Components;

namespace XEngine.Core.Box2DCompat
{
    /// <summary>
    /// Пользовательские данные, прикрепляемые к телу Box2D. Хранит ссылку на компонент GBox2DBody для связи с логикой игры.
    /// </summary>
    public class UserData(GBox2DBody hostBoty)
    {
        public GBox2DBody HostBody = hostBoty;
    }
}
