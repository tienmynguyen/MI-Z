using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class GoldCoinPickup : NetworkBehaviour
{
    [SerializeField] private int coinValue = 1;
    [SerializeField] private float rotateSpeed = 120f;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up * (rotateSpeed * Time.deltaTime), Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetComponentInParent(other, out PlayerNetwork collector))
        {
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!IsServer)
            {
                return;
            }

            CollectOnServer(collector);
        }
        else
        {
            GrantGold(collector);
            Debug.Log($"Collected {coinValue} gold coin(s) by {collector.name}");
            Destroy(gameObject);
        }
    }

    private void CollectOnServer(PlayerNetwork collector)
    {
        GrantGold(collector);
        Debug.Log($"Collected {coinValue} gold coin(s) by {collector.name}");

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void GrantGold(PlayerNetwork collector)
    {
        InventoryManager inventory = collector.GetComponent<InventoryManager>();
        if (inventory == null)
        {
            return;
        }

        inventory.AddGold(coinValue);
    }

    private bool TryGetComponentInParent<T>(Collider source, out T component) where T : Component
    {
        component = source.GetComponentInParent<T>();
        return component != null;
    }
}
