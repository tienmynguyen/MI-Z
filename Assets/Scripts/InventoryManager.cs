using System;
using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    [SerializeField] private int startGold = 100;
    [SerializeField] private bool persistAcrossScenes;

    [SerializeField] private List<string> ownedEquipments = new();

    public int Gold { get; private set; }
    public IReadOnlyList<string> OwnedEquipments => ownedEquipments;

    public event Action<int> OnGoldChanged;
    public event Action<string> OnEquipmentAdded;
 
    private void Awake()
    {
        Gold = Mathf.Max(0, startGold);

        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void Start()
    {
        OnGoldChanged?.Invoke(Gold);
    }

    public void AddGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Gold += amount;
        OnGoldChanged?.Invoke(Gold);
    }

    public bool CanAfford(int amount)
    {
        return amount > 0 && Gold >= amount;
    }

    public bool TrySpendGold(int amount)
    {
        if (!CanAfford(amount))
        {
            return false;
        }

        Gold -= amount;
        OnGoldChanged?.Invoke(Gold);
        return true;
    }

    public bool HasEquipment(string equipmentId)
    {
        if (string.IsNullOrWhiteSpace(equipmentId))
        {
            return false;
        }

        return ownedEquipments.Contains(equipmentId);
    }

    public bool AddEquipment(string equipmentId)
    {
        if (string.IsNullOrWhiteSpace(equipmentId) || ownedEquipments.Contains(equipmentId))
        {
            return false;
        }

        ownedEquipments.Add(equipmentId);
        OnEquipmentAdded?.Invoke(equipmentId);
        return true;
    }
}
