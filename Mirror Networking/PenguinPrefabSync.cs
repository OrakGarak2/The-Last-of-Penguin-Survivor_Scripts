using UnityEngine;
using Mirror;

public class PenguinPrefabSync : NetworkBehaviour
{
    [SerializeField] PenguinStatus penguinStatus;

    [SyncVar(hook = nameof(HookId))] public string id;
    [SyncVar(hook = nameof(HookColorNumber))] public int colorNumber;

    private int syncCount = 0;
    private const int totalSyncCount = 2;

    [SyncVar(hook = nameof(HookIsInBuilding))] public bool isOutBuilding;

    [SerializeField] GameObject projector;

    private void Awake()
    {
        penguinStatus = GetComponent<PenguinStatus>();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        // 로컬 플레이어로 입장할 때 고른 펭귄을 받아옴
        id = PlayerPrefs.GetString("CurrentPenguin");
        colorNumber = PlayerPrefs.GetInt("CurrentColorNumber");

        penguinStatus.LoadPrefab(id, colorNumber);

        if(!isServer)
        {
            CmdLoadPrefab(id, colorNumber);
        }
    }

    [Command] // 서버에서 적용
    private void CmdLoadPrefab(string syncId, int syncColorNumber)
    {
        id = syncId;
        colorNumber = syncColorNumber;
    }

    private void HookId(string oldId, string newId)
    {
        syncCount++;

        if (syncCount == totalSyncCount) LoadPrefab();
    }

    private void HookColorNumber(int oldNum, int newNum)
    {
        syncCount++;

        if (syncCount == totalSyncCount) LoadPrefab();
    }

    private void LoadPrefab()
    {
        if(isLocalPlayer) { return; }

        penguinStatus.LoadPrefab(id, colorNumber);
    }

    public void SetPlayerInBuilding(bool isVisible)
    {
        CmdSetPlayerInBuilding(isVisible);
    }

    [Command]
    private void CmdSetPlayerInBuilding(bool isVisible)
    {
        isOutBuilding = isVisible;
    }

    private void HookIsInBuilding(bool oldBool, bool newBool)
    {
        if (isLocalPlayer) return;

        penguinStatus.GetComponent<Rigidbody>().useGravity = newBool;
        projector.SetActive(newBool);
    }
}
