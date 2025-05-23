using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Lop.Survivor.Island;
using System.Security.Principal;

public class BuildingSync : NetworkBehaviour
{
    [SyncVar(hook = nameof(HookCurrentDurability))] public float currentDurability;
    private BuildingFrame interactionButton;
    private Slider durabilitySlider;

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<BuildingSpawnMessage>(ReceiveBuildingSpawnMessage);
        NetworkServer.ReplaceHandler<DurabilityMessage>(ReceiveDurabilityMessage);

        interactionButton = GetComponent<BuildingFrame>();
        if (interactionButton == null) { return; }

        durabilitySlider = interactionButton.durabilitySlider;
        if (isServer) currentDurability = interactionButton.currentDurability;
    }

    public void SpawnBuilding(GameObject building)
    {
        NetworkServer.Spawn(building);  // 서버에서 직접 스폰
    }

    public void SendBuildingSpawnMessage(string buildingType, int posX, int posY, int posZ, float buildingRotY)
    {
        BuildingSpawnMessage buildingSpawnMessage = new BuildingSpawnMessage
        {
            builingTypeName = buildingType,
            buildingPosX = posX,
            buildingPosY = posY,
            buildingPosZ = posZ,
            buildingRotY = buildingRotY
        };

        NetworkClient.Send(buildingSpawnMessage);
    }

    private void ReceiveBuildingSpawnMessage(NetworkConnection conn, BuildingSpawnMessage msg)
    {
        BuildingManager.Instance.PlaceBuildingOnMapSync(msg.builingTypeName, new Vector3Int(msg.buildingPosX, msg.buildingPosY, msg.buildingPosZ), msg.buildingRotY);
    }

    public void DestroyBuilding(GameObject building)
    {
        NetworkIdentity identity = building.GetComponent<NetworkIdentity>();

        if (isServer)
        {
            NetworkServer.Destroy(building);
        }
        else
        {
            NetworkObjectManager.Instance.DestroyNetworkObject(building);
        }
    }

    public struct BuildingSpawnMessage : NetworkMessage
    {
        public string builingTypeName;
        public int buildingPosX;
        public int buildingPosY;
        public int buildingPosZ;
        public float buildingRotY;
    }

    #region 빌딩 내구도 동기화

    private void OnDisable()
    {
        if (interactionButton == null) { return; }

        if (isServer)
            NetworkServer.UnregisterHandler<DurabilityMessage>();
    }

    private void HookCurrentDurability(float oldDurability, float newDurability)
    {
        if (interactionButton == null) { return; }

        if (durabilitySlider == null) return;
        durabilitySlider.value = newDurability;
    }

    public void SendDurabilityMessage(float variation)
    {
        if (interactionButton == null) { return; }

        DurabilityMessage durabilityMessage = new DurabilityMessage
        {
            variation = variation
        };

        NetworkClient.Send(durabilityMessage);
    }

    private void ReceiveDurabilityMessage(NetworkConnection conn, DurabilityMessage message)
    {
        if (interactionButton == null) { return; }

        interactionButton.UpdateDurability(message.variation);
    }

    public struct DurabilityMessage : NetworkMessage
    {
        public float variation;
    }
    #endregion
}