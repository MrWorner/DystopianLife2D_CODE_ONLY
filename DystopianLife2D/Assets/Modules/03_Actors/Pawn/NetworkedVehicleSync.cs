// НАЗНАЧЕНИЕ: Синхронизация видимости и коллизий персонажа при посадке в транспорт
// ЗАВИСИМОСТИ: NetworkObject, SpriteRenderer, Collider2D
// ПРИМЕЧАНИЕ: Методы Hide/Show вызываются только на сервере

using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;
using UniRx;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class NetworkedVehicleSync : NetworkBehaviour
{
    #region Поля: Required
    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private SpriteRenderer[] _pawnRenderers;

    [PropertyOrder(-1), BoxGroup("Required"), Required(InfoMessageType.Error), SerializeField]
    private Collider2D[] _pawnColliders;
    #endregion

    #region Поля: Debug
    [BoxGroup("DEBUG"), SerializeField] protected bool _ColoredDebug = true;
    #endregion

    #region Unity Методы
    private void Awake()
    {
        // Проверка обязательных ссылок
        if (_pawnRenderers == null || _pawnRenderers.Length == 0)
            DebugUtils.LogMissingReference(this, nameof(_pawnRenderers));

        if (_pawnColliders == null || _pawnColliders.Length == 0)
            DebugUtils.LogMissingReference(this, nameof(_pawnColliders));
    }
    #endregion

    #region Публичные методы
    /// <summary>
    /// Спрятать персонажа (вызывается ТОЛЬКО на сервере)
    /// </summary>
    public void HidePawn()
    {
        if (!IsServer) return;
        HidePawnClientRpc();
    }

    /// <summary>
    /// Показать персонажа (вызывается ТОЛЬКО на сервере)
    /// </summary>
    public void ShowPawn()
    {
        if (!IsServer) return;
        ShowPawnClientRpc();
    }
    #endregion

    #region Client RPC
    [ClientRpc]
    private void HidePawnClientRpc()
    {
        SetPawnVisibility(false);
    }

    [ClientRpc]
    private void ShowPawnClientRpc()
    {
        SetPawnVisibility(true);
    }

    public void SetPawnVisibility(bool visible)
    {
        if (_pawnRenderers != null)
        {
            foreach (var renderer in _pawnRenderers)
            {
                if (renderer != null) renderer.enabled = visible;
            }
        }

        if (_pawnColliders != null)
        {
            foreach (var collider in _pawnColliders)
            {
                if (collider != null) collider.enabled = visible;
            }
        }

        ColoredDebug.CLog(gameObject, "<color=orange>[SYSTEM]</color> Видимость персонажа установлена: {0}", _ColoredDebug, visible);
    }
    #endregion

    #region Личные методы

    #endregion
}