using UnityEngine;
using Lop.Survivor.Island.Buildingbase;

using Mirror;
using UnityEngine.UI;

public class BuildingMeshSync : NetworkBehaviour
{
    #region 빌딩 메쉬 동기화
    [SyncVar(hook = nameof(OnChildRotationChanged))] public float meshRotY;
    [SyncVar(hook = nameof(OnChildPositionChanged))] public float meshPosY;

    private void OnChildRotationChanged(float oldRotY, float newRotY)
    {
        NetworkIdentity identity = GetComponent<NetworkIdentity>();

        if (identity != null)
        {
            // 자식 오브젝트의 Y축 회전 값만 변경
            Transform MeshTransform = transform.GetChild(0).transform;

            MeshTransform.rotation = Quaternion.Euler(
                MeshTransform.rotation.eulerAngles.x,
                newRotY,
                MeshTransform.rotation.eulerAngles.z
            );
        }
    }

    private void OnChildPositionChanged(float oldPosY, float newPosY)
    {
        NetworkIdentity identity = GetComponent<NetworkIdentity>();

        if (identity != null)
        {
            // 자식 오브젝트의 Y축 회전 값만 변경
            Transform MeshTransform = transform.GetChild(0).transform;

            MeshTransform.position = new Vector3(MeshTransform.position.x, newPosY, MeshTransform.position.z);
        }
    }
    #endregion

    #region 빌딩 내구도 동기화
    [SyncVar(hook = nameof(HookCurrentDurability))] public float currentDurability;
    private BuildingFrame interactionButton;
    private Slider durabilitySlider;

    private void Start()
    {
        interactionButton = GetComponent<BuildingFrame>();
        
        if(interactionButton == null) { return; }
        
        durabilitySlider =  interactionButton.durabilitySlider;
        
        if(isServer) currentDurability = interactionButton.currentDurability;
        
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        
        NetworkServer.ReplaceHandler<DurabilityMessage>(ReceiveDurabilityMessage);
    }

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
