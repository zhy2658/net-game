using UnityEngine;
using Unity.Netcode;

public class NetworkPlayerSpawner : NetworkBehaviour
{
    public Vector3 spawnPosition = new Vector3(82f, 10.86234f, -50f);

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            var playerObject = client.PlayerObject;
            if (playerObject != null)
            {
                var cc = playerObject.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                
                playerObject.transform.position = spawnPosition;
                
                if (cc != null) cc.enabled = true;
            }
        }
    }
    
    public override void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
             NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
        base.OnDestroy();
    }
}
