using Sirenix.OdinInspector;
using UnityEngine;

public class WeaponItem : PickableItem
{
    [BoxGroup("Weapon"), SerializeField]
    private WeaponType _weaponType = WeaponType.Pistol;

    [BoxGroup("Weapon"), SerializeField]
    private int _maxAmmo = 15;

    [BoxGroup("Weapon"), SerializeField]
    private int _currentAmmo = 15;

    [BoxGroup("Weapon"), SerializeField]
    private float _damage = 10f;

    protected override void OnItemPickedUp(Pawn picker)
    {
        base.OnItemPickedUp(picker);

        /*
        // Добавить оружие в инвентарь игрока
        var inventory = picker.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            inventory.AddWeapon(_weaponType, _currentAmmo, _damage);
            //ColoredDebug.CLog(gameObject,$"<color=orange>Оружие {_weaponType} добавлено в инвентарь!</color>");
        }
        */
    }

    // Пример дополнительной функциональности
    public void Reload(int ammoToAdd)
    {
        _currentAmmo = Mathf.Min(_currentAmmo + ammoToAdd, _maxAmmo);
    }
}

public enum WeaponType
{
    Pistol,
    Rifle,
    Shotgun,
    Knife
}
