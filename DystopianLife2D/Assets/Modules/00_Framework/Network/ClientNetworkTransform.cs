using Unity.Netcode.Components;

public class ClientNetworkTransform : NetworkTransform
{
    // Переопределяем проверку авторитарности: если есть владелец, он главный
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}