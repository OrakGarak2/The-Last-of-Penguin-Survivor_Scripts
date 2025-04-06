using System.Collections.Generic;
using UnityEngine;
using Lop.Survivor.inventroy.Item;

using Mirror;
using Lop.Survivor.inventroy.Item.Ground;

public class NetworkObjectManager : NetworkBehaviour
{
    public static NetworkObjectManager Instance;

    public List<Transform> itemList = new List<Transform>();
    public ScriptableItemData[] itemDataLists;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        NetworkServer.RegisterHandler<AuthorityRequestMessage>(OnAuthorityRequest);
        NetworkServer.RegisterHandler<ItemReadymessage>(OnClientReady);
        NetworkServer.RegisterHandler<DestroyNetworkObjectMessage>(ReceiveDestroyNetworkObjectMessage);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        NetworkClient.RegisterHandler<ItemMessage>(ReceiveItemList);   
    }

    public void SendClientReadyMessage()
    {
        NetworkClient.Send(new ItemReadymessage());
    }

    #region 네트워크에 소환되어 있는 오브젝트들 삭제
    public void DestroyNetworkObject(GameObject obj)
    {
        if (isServerOnly)
        {
            NetworkServer.Destroy(obj);
            return;
        }

        NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
        if (identity != null)
        {
            NetworkClient.Send(
                new DestroyNetworkObjectMessage
                {
                    netId = identity.netId
                });
        }
        else
        {
            Debug.LogError("서버에서 삭제하려는 오브젝트의 NetworkIdentity가 존재하지 않습니다.");
        }
    }

    private void ReceiveDestroyNetworkObjectMessage(NetworkConnection conn, DestroyNetworkObjectMessage msg)
    {
        if (NetworkServer.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
        {
            GameObject obj = identity.gameObject;
            // 서버에서 해당 오브젝트 삭제
            NetworkServer.Destroy(obj);
            Debug.Log($"{msg.netId} 오브젝트가 서버에서 삭제 됐습니다.");
        }
        else
        {
            Debug.LogWarning($"{msg.netId} 오브젝트를 서버에서 찾지 못했습니다.");
        }
    }

    public struct DestroyNetworkObjectMessage : NetworkMessage
    {
        public uint netId;
    }
    #endregion

    #region 오브젝트에게 권한 부여
    // 클라이언트에서 권한 요청
    public void RequestAuthority(GameObject obj)
    {
        NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();

        if (identity != null)
        {
            AuthorityRequestMessage message = new AuthorityRequestMessage
            {
                netId = identity.netId
            };
            NetworkClient.Send(message);
        }
    }

    // 서버에서 클라이언트의 요청을 처리하고 권한을 부여
    private void OnAuthorityRequest(NetworkConnectionToClient conn, AuthorityRequestMessage msg)
    {
        // 요청된 오브젝트의 NetworkIdentity를 가져옴
        if (NetworkServer.spawned.TryGetValue(msg.netId, out NetworkIdentity identity))
        {
            // 해당 오브젝트가 다른 클라이언트에게 권한이 있는지 확인
            if (identity.connectionToClient != null && identity.connectionToClient != conn)
            {
                // 기존 클라이언트로부터 권한 제거
                identity.RemoveClientAuthority();
            }

            // 권한을 요청한 클라이언트에 권한 부여
            identity.AssignClientAuthority(conn);
        }
        else
        {
            Debug.LogError($"오브젝트가 서버에 존재하지 않습니다. netId: {msg.netId}");
        }
    }

    public struct AuthorityRequestMessage : NetworkMessage
    {
        public uint netId;
    }
    #endregion

    #region 새 클라이언트가 입장했을 때 아이템 동기화
    /// <summary>
    /// 서버에 있는 아이템 리스트에 추가
    /// </summary>
    public void AddItemList(Transform item)
    {
        itemList.Add(item);
    }

    /// <summary>
    /// 서버에 있는 아이템 리스트에서 제거
    /// </summary>
    public void RemoveItemList(Transform item)
    {
        itemList.Remove(item);
    }

    private void OnClientReady(NetworkConnection conn, ItemReadymessage msg)
    {
        if (itemList.Count > 0)
        {
            SendItemList(conn);
        }
    }

    private void SendItemList(NetworkConnection conn)
    {
        if(itemList.Count == 0) { return; }

        List<byte[]> itemDataToBytes = new List<byte[]>();
        List<Vector3> itemPositions = new List<Vector3>();
        List<float> destroyTimes = new List<float>();

        foreach (Transform item in itemList)
        {
            itemDataToBytes.Add(ChunkSync.SerializeToJson(item.GetComponent<ItemGround>().data));
            itemPositions.Add(item.position);
            destroyTimes.Add(item.GetComponent<DestroyByTime>().destroyTime);
        }

        ItemMessage itemMessage = new ItemMessage
        {
            itemDataToByteList = itemDataToBytes,
            positionList = itemPositions,
            destroyTimeList = destroyTimes
        };

        conn.Send(itemMessage);
    }


    private void ReceiveItemList(ItemMessage message)
    {
        Debug.Log("아이템 메시지 수신");

        for (int i = 0; i < message.itemDataToByteList.Count; i++)
        {
            ItemData itemData = ChunkSync.DeserializeFromJson<ItemData>(message.itemDataToByteList[i]);

            GameObject prefab = FindItemGameObject(itemData.itemName);

            prefab.GetComponent<ItemGround>().data.InitData(itemData);

            prefab.GetComponent<DestroyByTime>().destroyTime = message.destroyTimeList[i];

            Instantiate(prefab, message.positionList[i], Quaternion.identity);
        }
    }

    private GameObject FindItemGameObject(string itemName)
    {
        foreach (var item in itemDataLists)
        {
            if (item.itemName == itemName)
            {
                return item.itemObject;
            }
        }

        return null;
    }

    public struct ItemMessage : NetworkMessage
    {
        public List<byte[]>     itemDataToByteList;
        public List<Vector3>    positionList;
        public List<float>      destroyTimeList;
    }

    public struct ItemReadymessage : NetworkMessage { }
    #endregion
}
